using System.Threading.Channels;
using TaskTestApp.Models;

namespace TaskTestApp.Services;

public sealed class TaskEventQueue
{
    private readonly Channel<TaskCompletedEvent> _channel =
        Channel.CreateUnbounded<TaskCompletedEvent>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

    public bool TryEnqueue(TaskCompletedEvent message)
    {
        return _channel.Writer.TryWrite(message);
    }

    public IAsyncEnumerable<TaskCompletedEvent> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public void Complete()
    {
        _channel.Writer.TryComplete();
    }
}