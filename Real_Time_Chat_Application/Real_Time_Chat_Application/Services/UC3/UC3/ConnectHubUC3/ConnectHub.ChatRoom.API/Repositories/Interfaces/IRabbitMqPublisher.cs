using ConnectHub.ChatRoom.Models.Events;

namespace ConnectHub.ChatRoom.Repositories.Interfaces;

/// <summary>
/// IRabbitMqPublisher — typed event publishing for ChatRoom-Service.
/// Same pattern as UC2: placed in Repositories/Interfaces.
/// Publishes AFTER DB save so RoomId is always valid.
/// Fire-and-forget: publish failures are logged but never block DB operations.
/// </summary>
public interface IRabbitMqPublisher
{
    Task PublishRoomCreatedAsync(RoomCreatedEvent @event);
    
    Task PublishRoomInviteSentAsync(RoomInviteSentEvent @event);
    Task PublishRoomDeletedAsync(RoomDeletedEvent @event);
    Task PublishRoomMemberJoinedAsync(RoomMemberJoinedEvent @event);
    Task PublishRoomMemberLeftAsync(RoomMemberLeftEvent @event);
    Task PublishRoomUpdatedAsync(RoomUpdatedEvent @event);
}
