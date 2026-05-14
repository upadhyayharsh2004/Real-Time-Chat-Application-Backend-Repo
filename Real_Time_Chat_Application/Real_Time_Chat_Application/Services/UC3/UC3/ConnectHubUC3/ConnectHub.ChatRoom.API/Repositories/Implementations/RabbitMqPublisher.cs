using System.Text;
using System.Text.Json;
using ConnectHub.ChatRoom.Models.Events;
using ConnectHub.ChatRoom.Repositories.Interfaces;
using RabbitMQ.Client;

namespace ConnectHub.ChatRoom.Repositories.Implementations;

/// <summary>
/// RabbitMqPublisher — publishes ChatRoom domain events to RabbitMQ.
/// Same pattern as UC2: reads config, typed queues, publishes AFTER DB save.
/// Registered as AddSingleton — connection/channel reused across requests.
///
/// Queues (all durable):
///   connecthub.room.created         → Notification-Service
///   connecthub.room.deleted         → Message-Service (UC2), Notification-Service
///   connecthub.room.member.joined   → Notification-Service
///   connecthub.room.member.left     → Notification-Service
///   connecthub.room.updated         → Notification-Service
/// </summary>
public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private bool _disposed;

    public const string QueueRoomCreated = "connecthub.room.created";
    public const string QueueRoomDeleted = "connecthub.room.deleted";
    public const string QueueRoomMemberJoined = "connecthub.room.member.joined";

    public const string QueueRoomInviteSent = "connecthub.room.invite.sent";
    public const string QueueRoomMemberLeft = "connecthub.room.member.left";
    public const string QueueRoomUpdated = "connecthub.room.updated";

    public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMQ:Host"] ?? "localhost",
            UserName = config["RabbitMQ:Username"] ?? "guest",
            Password = config["RabbitMQ:Password"] ?? "guest",
            Port = int.TryParse(config["RabbitMQ:Port"], out var port) ? port : 5672,
            VirtualHost = config["RabbitMQ:VHost"] ?? "/"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare all queues as durable (survive broker restart)
        foreach (var queue in new[]
        {
            QueueRoomCreated, QueueRoomDeleted,
            QueueRoomMemberJoined, QueueRoomMemberLeft,
            QueueRoomUpdated, QueueRoomInviteSent
        })
        {
            _channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
        }

        _logger.LogInformation("[RabbitMQ] ChatRoom publisher connected to {Host}", factory.HostName);
    }

    public Task PublishRoomCreatedAsync(RoomCreatedEvent @event) =>
        Publish(QueueRoomCreated, @event);

    public Task PublishRoomInviteSentAsync(RoomInviteSentEvent @event) =>
        Publish(QueueRoomInviteSent, @event);

    public Task PublishRoomDeletedAsync(RoomDeletedEvent @event) =>
        Publish(QueueRoomDeleted, @event);

    public Task PublishRoomMemberJoinedAsync(RoomMemberJoinedEvent @event) =>
        Publish(QueueRoomMemberJoined, @event);

    public Task PublishRoomMemberLeftAsync(RoomMemberLeftEvent @event) =>
        Publish(QueueRoomMemberLeft, @event);

    public Task PublishRoomUpdatedAsync(RoomUpdatedEvent @event) =>
        Publish(QueueRoomUpdated, @event);

    private Task Publish(string queue, object @event)
    {
        try
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));

            var props = _channel.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";

            _channel.BasicPublish(
                exchange: "",
                routingKey: queue,
                basicProperties: props,
                body: body);

            _logger.LogInformation("[RabbitMQ] Published {EventType} to {Queue}",
                @event.GetType().Name, queue);
        }
        catch (Exception ex)
        {
            // Non-blocking — DB save already succeeded
            _logger.LogError(ex, "[RabbitMQ] Failed to publish {EventType} to {Queue}",
                @event.GetType().Name, queue);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        try { _channel?.Close(); } catch { }
        try { _connection?.Close(); } catch { }
        _disposed = true;
    }
}
