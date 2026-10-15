using System.Text.Json;
using RabbitMQ.Client;
using TaskTestApp.Models;

namespace TaskTestApp.Services;

public sealed class RabbitMqPublisherWorker : BackgroundService
{
    private readonly TaskEventQueue _queue;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqPublisherWorker> _logger;

    public RabbitMqPublisherWorker(TaskEventQueue queue, IConfiguration configuration, ILogger<RabbitMqPublisherWorker> logger)
    {
        _queue = queue;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RabbitMQ publisher worker started");

        try
        {
            await foreach (var message in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await PublishToRabbitMqAsync(message, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to publish completion event for task {TaskId}",
                        message.TaskId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        _logger.LogInformation("RabbitMQ publisher worker stopped");
    }

    private async Task PublishToRabbitMqAsync(
        TaskCompletedEvent message,
        CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            UserName = _configuration["RabbitMQ:Username"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        await using var connection =
            await factory.CreateConnectionAsync(cancellationToken);

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: "task.events",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };

        await channel.BasicPublishAsync(
            exchange: "task.events",
            routingKey: "task.completed",
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Published completion event for task {TaskId}", message.TaskId);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping RabbitMQ publisher; draining queued events");

        _queue.Complete();

        var executeTask = ExecuteTask;

        if (executeTask is not null)
        {
            try
            {
                await executeTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("RabbitMQ publisher did not drain the queue within 5 seconds");
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("RabbitMQ publisher shutdown was interrupted");
            }
        }

        await base.StopAsync(cancellationToken);

        _logger.LogInformation("RabbitMQ publisher shutdown completed");
    }
}