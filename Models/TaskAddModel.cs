using TaskTestApp.Enums;

namespace TaskTestApp.Models;

public class TaskAddModel
{
    public string Title { get; set; } = null!;
    public bool IsCompleted { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
}