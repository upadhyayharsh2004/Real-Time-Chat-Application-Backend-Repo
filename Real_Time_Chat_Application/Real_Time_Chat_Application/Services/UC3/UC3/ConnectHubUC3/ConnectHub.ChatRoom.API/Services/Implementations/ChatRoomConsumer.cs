using System.Text;
using System.Text.Json;
using ConnectHub.ChatRoom.Models.Events;
using ConnectHub.ChatRoom.Repositories.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ConnectHub.ChatRoom.Services.Implementations;

/// <summary>
/// ChatRoomConsumer — BackgroundService that consumes INBOUND events from other microservices.
///
/// Consumes:
///   connecthub.room.message.sent  → from UC2 Message-Service
///     Action: update ChatRoom.LastMessageContent + LastMessageAt for sidebar display
///
///   connecthub.user.deactivated   → from UC1 Auth-Service
///     Action: remove deactivated user from all room memberships
///
/// Same pattern as UC2 MessageConsumer:
///   - Reads host/user/password from IConfiguration (not hardcoded)
///   - Uses IServiceScopeFactory for scoped DB access
///   - BasicNack with requeue on failure
///   - Does NOT consume own outbound queues (no circular loop)
/// </summary>
public class ChatRoomConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ChatRoomConsumer> _logger;

    private const string QueueRoomMessageSent = "connecthub.room.message.sent";

    private const string QueueUserOnline = "connecthub.chatroom.user.online";

    private const string QueueUserOffline = "connecthub.chatroom.user.offline";

    private const string QueueUserDeactivated = "connecthub.chatroom.user.deactivated";

    private const string QueueUserReactivated = "connecthub.chatroom.user.reactivated";

    public ChatRoomConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<ChatRoomConsumer> logger)
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

            channel.QueueDeclare(QueueRoomMessageSent, durable: true, exclusive: false, autoDelete: false);
            channel.QueueDeclare(QueueUserDeactivated, durable: true, exclusive: false, autoDelete: false);
            channel.QueueDeclare(QueueUserReactivated, durable: true, exclusive: false, autoDelete: false);
            channel.QueueDeclare(QueueUserOnline, durable: true, exclusive: false, autoDelete: false);

            channel.QueueDeclare(QueueUserOffline, durable: true, exclusive: false, autoDelete: false);
            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            _logger.LogInformation("[RabbitMQ] ChatRoom consumer listening on [{Q1}] and [{Q2}]",
                QueueRoomMessageSent, QueueUserDeactivated);

            // ── RoomMessageSent — from UC2 ─────────────────────────
            var roomMsgConsumer = new EventingBasicConsumer(channel);
            roomMsgConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var @event = JsonSerializer.Deserialize<RoomMessageSentEvent>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (@event != null && @event.RoomId > 0)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] RoomMessageSent: RoomId={RoomId} MessageId={MessageId}",
                            @event.RoomId, @event.MessageId);

                        using var scope = _scopeFactory.CreateScope();
                        var repo = scope.ServiceProvider.GetRequiredService<IChatRoomRepository>();

                        // Update room's last message preview for sidebar display
                        await repo.UpdateLastMessage(@event.RoomId, @event.Content, @event.SentAt);
                    }

                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Error processing RoomMessageSentEvent");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            };
            var userOnlineConsumer = new EventingBasicConsumer(channel);

            userOnlineConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());

                    var @event = JsonSerializer.Deserialize<UserOnlineEvent>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    if (@event != null && @event.UserId > 0)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] UserOnline received: UserId={UserId}",
                            @event.UserId);
                    }

                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[RabbitMQ] Error processing UserOnlineEvent");

                    channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };
            var userOfflineConsumer = new EventingBasicConsumer(channel);

            userOfflineConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());

                    var @event = JsonSerializer.Deserialize<UserOfflineEvent>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    if (@event != null && @event.UserId > 0)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] UserOffline received: UserId={UserId}",
                            @event.UserId);
                    }

                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[RabbitMQ] Error processing UserOfflineEvent");

                    channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            // ── UserDeactivated — from UC1 ─────────────────────────
            var userDeactivatedConsumer = new EventingBasicConsumer(channel);
            userDeactivatedConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var @event = JsonSerializer.Deserialize<UserDeactivatedEvent>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (@event != null && @event.UserId > 0)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] UserDeactivated: UserId={UserId}", @event.UserId);

                        using var scope = _scopeFactory.CreateScope();
                        var repo = scope.ServiceProvider.GetRequiredService<IChatRoomRepository>();

                        // Remove deactivated user from all active room memberships
                        await repo.RemoveUserFromAllRooms(@event.UserId);
                    }

                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Error processing UserDeactivatedEvent");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            };
            var userReactivatedConsumer = new EventingBasicConsumer(channel);

            userReactivatedConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());

                    var @event = JsonSerializer.Deserialize<UserReactivatedEvent>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    if (@event != null && @event.UserId > 0)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] UserReactivated: UserId={UserId}",
                            @event.UserId);
                    }

                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[RabbitMQ] Error processing UserReactivatedEvent");

                    channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            channel.BasicConsume(QueueRoomMessageSent, autoAck: false, roomMsgConsumer);
            channel.BasicConsume(QueueUserDeactivated, autoAck: false, userDeactivatedConsumer);
            channel.BasicConsume(QueueUserReactivated, autoAck: false, userReactivatedConsumer);
            channel.BasicConsume(QueueUserOnline,autoAck: false,userOnlineConsumer);
            channel.BasicConsume(QueueUserOffline,autoAck: false,userOfflineConsumer);

            stoppingToken.WaitHandle.WaitOne();
            channel.Close();
            connection.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[RabbitMQ] ChatRoom consumer failed to connect. " +
                "Service continues — messages will queue when broker is available.");
        }
    }
}
