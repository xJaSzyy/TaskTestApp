using TaskTestApp.Models;

namespace TaskTestApp.Interfaces;

public interface ITaskEventPublisher
{
    Task PublishCompletedAsync(TaskCompletedEvent message, CancellationToken cancellationToken = default);
}