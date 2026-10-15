using TaskTestApp.Interfaces;
using TaskTestApp.Models;

namespace TaskTestApp.Services;

public sealed class QueuedTaskEventPublisher : ITaskEventPublisher
{
    private readonly TaskEventQueue _queue;
    private readonly ILogger<QueuedTaskEventPublisher> _logger;

    public QueuedTaskEventPublisher(TaskEventQueue queue, ILogger<QueuedTaskEventPublisher> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public Task PublishCompletedAsync(TaskCompletedEvent message, CancellationToken cancellationToken = default)
    {
        if (!_queue.TryEnqueue(message))
        {
            _logger.LogError("Could not enqueue completion event for task {TaskId}", message.TaskId);
        }

        return Task.CompletedTask;
    }
}