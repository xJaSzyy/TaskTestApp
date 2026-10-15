using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using TaskTestApp.Interfaces;
using TaskTestApp.Models;

namespace TaskTestApp;

public sealed class RabbitMqTaskEventPublisher : ITaskEventPublisher
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqTaskEventPublisher> _logger;

    public RabbitMqTaskEventPublisher(IConfiguration configuration, ILogger<RabbitMqTaskEventPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task PublishCompletedAsync(TaskCompletedEvent message, CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
                UserName = _configuration["RabbitMQ:Username"] ?? "guest",
                Password = _configuration["RabbitMQ:Password"] ?? "guest"
            };

            await using var connection = await factory.CreateConnectionAsync(cancellationToken);

            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await channel.ExchangeDeclareAsync(
                exchange: "task.events",
                type: ExchangeType.Topic,
                durable: true,
                cancellationToken: cancellationToken);

            var body = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(message));

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

            _logger.LogInformation(
                "Published TaskCompleted event for task {TaskId}",
                message.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish TaskCompleted event for task {TaskId}. " +
                "The task completion itself will not be rolled back.",
                message.TaskId);
        }
    }
}