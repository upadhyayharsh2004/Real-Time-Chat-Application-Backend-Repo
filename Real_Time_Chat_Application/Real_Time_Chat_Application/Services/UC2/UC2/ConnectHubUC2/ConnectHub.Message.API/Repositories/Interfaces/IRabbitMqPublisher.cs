using ConnectHub.Message.Models.Events;

namespace ConnectHub.Message.Repositories.Interfaces;

/// <summary>
/// IRabbitMqPublisher — typed event publishing contract for RabbitMQ.
/// Placed in Repositories/Interfaces to mirror UC1 folder pattern.
///
/// FIXED from original UC2 which had one generic PublishAsync(object) in Interface/ folder.
/// Each method publishes to a specific named queue with a typed event contract,
/// so consuming services know exactly what shape to deserialize.
///
/// Publishing always happens AFTER DB save — never before — so MessageId is valid.
/// </summary>
public interface IRabbitMqPublisher
{
    Task PublishMessageSentAsync(MessageSentEvent @event);
    Task PublishRoomMessageSentAsync(RoomMessageSentEvent @event);
    Task PublishMessageReadAsync(MessageReadEvent @event);
    Task PublishMessageDeletedAsync(MessageDeletedEvent @event);
    Task PublishMessageEditedAsync(MessageEditedEvent @event);
}
