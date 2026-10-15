using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using TaskTestApp.Contexts;
using TaskTestApp.Interfaces;
using TaskTestApp.Models;

namespace TaskTestApp.Tests;

public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ITaskEventPublisher> _publisherMock;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _publisherMock = new Mock<ITaskEventPublisher>();

        _publisherMock
            .Setup(p => p.PublishCompletedAsync(
                It.IsAny<TaskCompletedEvent>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ITaskEventPublisher>();

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TasksIntegrationTests");
                });

                services.AddSingleton(_publisherMock.Object);
            });
        });
    }

    [Fact]
    public async Task CreateAndCompleteTask_ShouldPublishEvent()
    {
        using var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/tasks", new
        {
            title = "Integration test task",
            isCompleted = false,
            priority = 1
        });

        var createdJson = await createResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        using var document = JsonDocument.Parse(createdJson);

        var taskId = document.RootElement
            .GetProperty("id")
            .GetGuid();

        var completeResponse = await client.PutAsync(
            $"/tasks/{taskId}/complete",
            content: null);

        Assert.Equal(
            HttpStatusCode.OK,
            completeResponse.StatusCode);

        _publisherMock.Verify(
            p => p.PublishCompletedAsync(
                It.Is<TaskCompletedEvent>(e =>
                    e.TaskId == taskId &&
                    e.Title == "Integration test task"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}