using ConnectHub.Media.Models.Events;

namespace ConnectHub.Media.Repositories.Interfaces;

/// <summary>
/// IRabbitMqPublisher — typed event publishing for Media-Service.
/// Same pattern as UC1-UC5: reads config, durable queues, persistent messages.
/// Registered as AddSingleton.
/// Publishes AFTER DB save so FileId is always valid.
/// </summary>
public interface IRabbitMqPublisher
{
    Task PublishMediaUploadedAsync(MediaUploadedEvent @event);
    Task PublishMediaDeletedAsync(MediaDeletedEvent @event);
}
