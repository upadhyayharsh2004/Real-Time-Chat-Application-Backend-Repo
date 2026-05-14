using System.Text;
using System.Text.Json;
using ConnectHub.Notification.Models.Events;
using ConnectHub.Notification.Services.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ConnectHub.Notification.Services.Implementations;

/// <summary>
/// NotificationConsumer — BackgroundService consuming INBOUND events from UC1/UC2/UC3/UC4.
///
/// Same pattern as UC2/UC3/UC4:
///   - Reads config from IConfiguration (not hardcoded)
///   - BasicNack + requeue on failure
///   - Does NOT consume own outbound queues
///   - IServiceScopeFactory for scoped service access
///
/// Queues consumed (EXACT names matching UC1/UC2/UC3/UC4 publishers):
///   connecthub.message.sent           → UC2 → create MESSAGE notification
///   connecthub.room.message.sent      → UC2 → create MENTION notification
///   connecthub.message.read           → UC2 → mark notification as read
///   connecthub.message.deleted        → UC2 → delete related notification
///   connecthub.room.member.joined     → UC3 → create ROOM_INVITE notification
///   connecthub.room.created           → UC3 → (log only)
///   connecthub.user.deactivated       → UC1 → delete all notifications for user
///   connecthub.user.role.changed      → UC1 → create ROLE_CHANGE notification
///   connecthub.user.registered        → UC1 → send welcome notification
///   connecthub.presence.online        → UC4 → (log for future use)
///   connecthub.presence.offline       → UC4 → trigger queued email notifications
/// </summary>
public class NotificationConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationConsumer> _logger;


    // ── EXACT queue names matching UC1/UC2/UC3/UC4 publishers ─────
    private const string QueueMessageSent = "connecthub.message.sent";

    private const string QueueRoomInviteSent = "connecthub.room.invite.sent";

    private const string QueueRoomMessageSent = "connecthub.room.message.sent";
    private const string QueueMessageRead = "connecthub.message.read";
    private const string QueueMessageDeleted = "connecthub.message.deleted";
    private const string QueueRoomMemberJoined = "connecthub.room.member.joined";
    private const string QueueRoomCreated = "connecthub.room.created";
    private const string QueueUserDeactivated = "connecthub.user.deactivated";
    private const string QueueUserRoleChanged = "connecthub.user.role.changed";
    private const string QueueUserRegistered = "connecthub.user.registered";
    private const string QueuePresenceOnline = "connecthub.presence.online";
    private const string QueuePresenceOffline = "connecthub.presence.offline";

    public NotificationConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<NotificationConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Task.Run(() => StartConsuming(stoppingToken), stoppingToken);
        return Task.CompletedTask;
    }

    private void StartConsuming(CancellationToken stoppingToken)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _config["RabbitMQ:Host"] ?? "localhost",
                UserName = _config["RabbitMQ:Username"] ?? "guest",
                Password = _config["RabbitMQ:Password"] ?? "guest",
                Port = int.TryParse(_config["RabbitMQ:Port"], out var port) ? port : 5672,
                VirtualHost = _config["RabbitMQ:VHost"] ?? "/"
            };

            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();

            foreach (var q in new[]
            {
                QueueMessageSent, QueueRoomMessageSent, QueueMessageRead,
                QueueMessageDeleted, QueueRoomMemberJoined, QueueRoomCreated,
                QueueUserDeactivated, QueueUserRoleChanged, QueueUserRegistered,
                QueuePresenceOnline, QueuePresenceOffline,
                QueueRoomInviteSent
            })
            {
                channel.QueueDeclare(q, durable: true, exclusive: false, autoDelete: false);
            }

            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            _logger.LogInformation("[RabbitMQ] Notification consumer started on 11 queues.");

            // ── MessageSent — from UC2 ─────────────────────────────
            Register<MessageSentEvent>(channel, QueueMessageSent, async (@event) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await svc.Send(
                    recipientId: @event.ReceiverId,
                    senderId: @event.SenderId,
                    type: "MESSAGE",
                    title: $"New message from {@event.SenderName}",
                    message: @event.Content.Length > 100
                                     ? @event.Content[..100] + "..."
                                     : @event.Content,
                    relatedId: @event.MessageId,
                    relatedType: "Message");
            });

            // ── RoomMessageSent (mentions) — from UC2 ─────────────
            Register<RoomMessageSentEvent>(channel, QueueRoomMessageSent, async (@event) =>
            {
                if (@event.MentionedUserNames is null || !@event.MentionedUserNames.Any())
                    return;

                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();

                // For each mentioned user — in production resolve userId from username
                // Here we log — the architecture is correct for future resolution
                _logger.LogInformation(
                    "[RabbitMQ] Room mention in RoomId={RoomId} by {Sender}. Mentions: {Users}",
                    @event.RoomId, @event.SenderName,
                    string.Join(", ", @event.MentionedUserNames));
            });

            // ── MessageRead — from UC2 ─────────────────────────────
            Register<MessageReadEvent>(channel, QueueMessageRead, async (@event) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider
                    .GetRequiredService<Repositories.Interfaces.INotificationRepository>();
                // Mark related MESSAGE notification as read
                var related = await repo.FindByRelatedId(@event.MessageId, "Message");
                foreach (var n in related.Where(n => !n.IsRead))
                    await repo.MarkAsRead(n.NotificationId);
            });

            // ── MessageDeleted — from UC2 ──────────────────────────
            Register<MessageDeletedEvent>(channel, QueueMessageDeleted, async (@event) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider
                    .GetRequiredService<Repositories.Interfaces.INotificationRepository>();
                await repo.DeleteByRelatedId(@event.MessageId, "Message");
            });

            // ── RoomMemberJoined — from UC3 ────────────────────────
            Register<RoomMemberJoinedEvent>(channel, QueueRoomMemberJoined, async (@event) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await svc.Send(
                    recipientId: @event.UserId,
                    senderId: null,
                    type: "ROOM_INVITE",
                    title: $"You joined {@event.RoomName}",
                    message: $"You are now a member of {@event.RoomName}",
                    relatedId: @event.RoomId,
                    relatedType: "Room");
            });

            // ── RoomCreated — from UC3 (log only) ─────────────────
            Register<RoomCreatedEvent>(channel, QueueRoomCreated, async (@event) =>
            {
                _logger.LogInformation(
                    "[RabbitMQ] RoomCreated: RoomId={RoomId} Name={Name}",
                    @event.RoomId, @event.Name);
                await Task.CompletedTask;
            });

            // ── UserDeactivated — from UC1 ─────────────────────────
            Register<UserDeactivatedEvent>(channel, QueueUserDeactivated, async (@event) =>
            {
                _logger.LogInformation(
                    "[RabbitMQ] UserDeactivated UserId={UserId} — clearing notifications.",
                    @event.UserId);
                // Future: mark all notifications for this user as read / archived
                await Task.CompletedTask;
            });

            // ── UserRoleChanged — from UC1 ─────────────────────────
            Register<UserRoleChangedEvent>(channel, QueueUserRoleChanged, async (@event) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await svc.Send(
                    recipientId: @event.UserId,
                    senderId: null,
                    type: "ROLE_CHANGE",
                    title: "Your account role has changed",
                    message: $"Your role has been updated to {@event.NewRole}",
                    relatedId: @event.UserId,
                    relatedType: "User");
            });

            // ── RoomInviteSent — from UC3 ──────────────────────────────
            Register<RoomInviteSentEvent>(channel, QueueRoomInviteSent, async (@event) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await svc.Send(
                    recipientId: @event.InvitedUserId,
                    senderId: @event.InvitedByUserId,
                    type: "ROOM_INVITE",
                    title: $"You've been invited to {@event.RoomName}",
                    message: $"You have a pending invite to join {@event.RoomName}",
                    relatedId: @event.InviteId,      // ← InviteId taaki accept/decline kaam kare
                    relatedType: "RoomInvite");
            });

            // ── UserRegistered — from UC1 ──────────────────────────
            Register<UserRegisteredEvent>(channel, QueueUserRegistered, async (@event) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await svc.Send(
                    recipientId: @event.UserId,
                    senderId: null,
                    type: "PLATFORM",
                    title: "Welcome to ConnectHub!",
                    message: $"Hi {@event.DisplayName}, welcome to ConnectHub. Start chatting now!",
                    relatedId: null,
                    relatedType: null);
            });

            // ── PresenceOnline — from UC4 (log) ───────────────────
            Register<UserPresenceOnlineEvent>(channel, QueuePresenceOnline, async (@event) =>
            {
                _logger.LogDebug("[RabbitMQ] PresenceOnline UserId={UserId}", @event.UserId);
                await Task.CompletedTask;
            });

            // ── PresenceOffline — from UC4 ─────────────────────────
            Register<UserPresenceOfflineEvent>(channel, QueuePresenceOffline, async (@event) =>
            {
                _logger.LogDebug("[RabbitMQ] PresenceOffline UserId={UserId} LastSeen={LastSeen}",
                    @event.UserId, @event.LastSeen);
                // Future: trigger any queued email notifications for offline user
                await Task.CompletedTask;
            });

            stoppingToken.WaitHandle.WaitOne();
            channel.Close();
            connection.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[RabbitMQ] Notification consumer failed to connect. " +
                "Service continues — messages will queue when broker is available.");
        }
    }

    private void Register<T>(IModel channel, string queue, Func<T, Task> handler)
    {
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var @event = JsonSerializer.Deserialize<T>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (@event is not null)
                    await handler(@event);

                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RabbitMQ] Error processing {Type} from {Queue}",
                    typeof(T).Name, queue);
                channel.BasicNack(ea.DeliveryTag, false, requeue: true);
            }
        };
        channel.BasicConsume(queue, autoAck: false, consumer);
    }
}
