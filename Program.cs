using Microsoft.EntityFrameworkCore;
using TaskTestApp;
using TaskTestApp.Contexts;
using TaskTestApp.Interfaces;
using TaskTestApp.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSqlConnection")));

builder.Services.AddScoped<ITaskEventPublisher, RabbitMqTaskEventPublisher>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    await dbContext.Database.MigrateAsync();
}

app.MapPost("/tasks", async (TaskAddModel addModel, ApplicationDbContext db) =>
{
    if (string.IsNullOrEmpty(addModel.Title))
    {
        return Results.BadRequest("Title is required");
    }

    if (addModel.Title.Length > 200)
    {
        return Results.BadRequest("Title is too long");
    }
    
    var task = new TaskItem
    {
        Title = addModel.Title,
        IsCompleted = addModel.IsCompleted,
        CreatedAt = addModel.CreatedAt ?? DateTimeOffset.UtcNow,
        CompletedAt = addModel.CompletedAt,
        Priority = addModel.Priority
    };

    db.TaskItems.Add(task);
    await db.SaveChangesAsync();

    return Results.Ok(task);
});

app.MapGet("/tasks", async (ApplicationDbContext db) =>
{
    var tasks = await db.TaskItems.ToListAsync();
    
    return Results.Ok(tasks);
});

app.MapPut("/tasks/{id:guid}/complete", async (Guid id, ApplicationDbContext db, ITaskEventPublisher publisher, ILogger<Program> logger) =>
{
    var task = await db.TaskItems.FindAsync(id);

    if (task is null)
    {
        return Results.NotFound(new
        {
            Error = "Task not found"
        });
    }

    if (task.IsCompleted)
    {
        return Results.BadRequest(new
        {
            Error = "Task is already completed"
        });
    }

    task.IsCompleted = true;
    task.CompletedAt = DateTimeOffset.UtcNow;

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        logger.LogInformation("Concurrent completion detected for task {TaskId}", id);

        return Results.Conflict(new
        {
            Error = "Task was modified by another request"
        });
    }

    var message = new TaskCompletedEvent(
        TaskId: task.Id,
        Title: task.Title,
        CompletedAt: task.CompletedAt.Value,
        Priority: task.Priority.ToString());

    await publisher.PublishCompletedAsync(message);

    return Results.Ok(task);
});

app.MapDelete("/tasks/{id:guid}", async (Guid id, ApplicationDbContext db) =>
{
    var task = await db.TaskItems.FindAsync(id);

    if (task is null)
    {
        return Results.NotFound(new
        {
            Error = "Task not found"
        });
    }

    db.TaskItems.Remove(task);
    await db.SaveChangesAsync();

    return Results.Ok(task);
});

app.Run();