// using System.Text;
// using System.Text.Json;
// using ConnectHub.Message.Models.Events;
// using ConnectHub.Message.Repositories.Interfaces;
// using RabbitMQ.Client;
// using RabbitMQ.Client.Events;

// namespace ConnectHub.Message.Services.Implementations;

// /// <summary>
// /// MessageConsumer — BackgroundService that consumes INBOUND events from
// /// other microservices via RabbitMQ.
// ///
// /// Consumes:
// ///   connecthub.user.deactivated  → from Auth-Service (UC1)
// ///     Action: soft-delete all messages sent by the deactivated user so they
// ///             no longer appear in other users' inboxes.
// ///
// ///   connecthub.room.deleted      → from ChatRoom-Service (UC3)
// ///     Action: soft-delete all messages that belong to the deleted room.
// ///
// /// Uses IServiceScopeFactory for scoped DB access in async callbacks.
// /// Reads host/credentials from IConfiguration — never hardcoded.
// /// BasicNack+requeue on any exception so no message is silently lost.
// /// </summary>
// public class MessageConsumer : BackgroundService
// {
//     private readonly IServiceScopeFactory _scopeFactory;
//     private readonly IConfiguration _config;
//     private readonly ILogger<MessageConsumer> _logger;

//     private const string QueueUserDeactivated = "connecthub.user.deactivated";

//     private const string QueueUserOnline = "connecthub.message.user.online";

//     private const string QueueUserOffline = "connecthub.message.user.offline";

//     private const string QueueUserReactivated = "connecthub.message.user.reactivated";
//     private const string QueueRoomDeleted = "connecthub.room.deleted";

//     public MessageConsumer(
//         IServiceScopeFactory scopeFactory,
//         IConfiguration config,
//         ILogger<MessageConsumer> logger)
//     {
//         _scopeFactory = scopeFactory;
//         _config = config;
//         _logger = logger;
//     }

//     protected override Task ExecuteAsync(CancellationToken stoppingToken)
//     {
//         // Run on a background thread so startup is not blocked
//         Task.Run(() => StartConsuming(stoppingToken), stoppingToken);
//         return Task.CompletedTask;
//     }

//     private void StartConsuming(CancellationToken stoppingToken)
//     {
//         try
//         {
//             var factory = new ConnectionFactory
//             {
//                 HostName = _config["RabbitMQ:Host"] ?? "localhost",
//                 UserName = _config["RabbitMQ:Username"] ?? "guest",
//                 Password = _config["RabbitMQ:Password"] ?? "guest",
//                 Port = int.TryParse(_config["RabbitMQ:Port"], out var port) ? port : 5672,
//                 VirtualHost = _config["RabbitMQ:VHost"] ?? "/"
//             };

//             var connection = factory.CreateConnection();
//             var channel = connection.CreateModel();

//             // Declare both queues as durable (must match UC1/UC3 publisher declarations)
//             channel.QueueDeclare(QueueUserDeactivated, durable: true, exclusive: false, autoDelete: false);
//             channel.QueueDeclare(QueueUserReactivated, durable: true, exclusive: false, autoDelete: false);
//             channel.QueueDeclare(QueueUserOnline, durable: true, exclusive: false, autoDelete: false);
//             channel.QueueDeclare(QueueUserOnline, durable: true, exclusive: false, autoDelete: false);

//             channel.QueueDeclare(QueueUserOffline, durable: true, exclusive: false, autoDelete: false);

//             channel.QueueDeclare(QueueUserOffline, durable: true, exclusive: false, autoDelete: false);
//             channel.QueueDeclare(QueueRoomDeleted, durable: true, exclusive: false, autoDelete: false);

//             // Process one message at a time
//             channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

//             _logger.LogInformation(
//                 "[RabbitMQ] Message consumer listening on [{Q1}] and [{Q2}]",
//                 QueueUserDeactivated, QueueRoomDeleted);

//             // ── Consumer: UserDeactivated from Auth-Service (UC1) ─────────────
//             var userDeactivatedConsumer = new EventingBasicConsumer(channel);
//             userDeactivatedConsumer.Received += async (_, ea) =>
//             {
//                 try
//                 {
//                     var json = Encoding.UTF8.GetString(ea.Body.ToArray());
//                     var @event = JsonSerializer.Deserialize<UserDeactivatedEvent>(json,
//                         new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

//                     if (@event != null && @event.UserId > 0)
//                     {
//                         _logger.LogInformation(
//                             "[RabbitMQ] UserDeactivated received — UserId={UserId}. " +
//                             "Soft-deleting all messages sent by this user.",
//                             @event.UserId);

//                         using var scope = _scopeFactory.CreateScope();
//                         var repo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

//                         // Soft-delete every message sent by the deactivated user so
//                         // their content no longer shows in other users' conversation threads.
//                         await repo.SoftDeleteAllByUserId(@event.UserId);

//                         _logger.LogInformation(
//                             "[RabbitMQ] Soft-deleted messages for deactivated UserId={UserId}",
//                             @event.UserId);
//                     }

//                     channel.BasicAck(ea.DeliveryTag, false);
//                 }
//                 catch (Exception ex)
//                 {
//                     _logger.LogError(ex, "[RabbitMQ] Error processing UserDeactivatedEvent");
//                     channel.BasicNack(ea.DeliveryTag, false, requeue: true);
//                 }
//             };

//             // ── Consumer: RoomDeleted from ChatRoom-Service (UC3) ─────────────
//             var roomDeletedConsumer = new EventingBasicConsumer(channel);
//             roomDeletedConsumer.Received += async (_, ea) =>
//             {
//                 try
//                 {
//                     var json = Encoding.UTF8.GetString(ea.Body.ToArray());
//                     var @event = JsonSerializer.Deserialize<RoomDeletedEvent>(json,
//                         new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

//                     if (@event != null && @event.RoomId > 0)
//                     {
//                         _logger.LogInformation(
//                             "[RabbitMQ] RoomDeleted received — RoomId={RoomId}. " +
//                             "Soft-deleting all room messages.",
//                             @event.RoomId);

//                         using var scope = _scopeFactory.CreateScope();
//                         var repo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

//                         await repo.SoftDeleteAllByRoomId(@event.RoomId);

//                         _logger.LogInformation(
//                             "[RabbitMQ] Soft-deleted all messages for RoomId={RoomId}",
//                             @event.RoomId);
//                     }

//                     channel.BasicAck(ea.DeliveryTag, false);
//                 }
//                 catch (Exception ex)
//                 {
//                     _logger.LogError(ex, "[RabbitMQ] Error processing RoomDeletedEvent");
//                     channel.BasicNack(ea.DeliveryTag, false, requeue: true);
//                 }
//             };

//             var userReactivatedConsumer = new EventingBasicConsumer(channel);

//             userReactivatedConsumer.Received += async (_, ea) =>
//             {
//                 try
//                 {
//                     var json = Encoding.UTF8.GetString(ea.Body.ToArray());

//                     var @event = JsonSerializer.Deserialize<UserReactivatedEvent>(
//                         json,
//                         new JsonSerializerOptions
//                         {
//                             PropertyNameCaseInsensitive = true
//                         });

//                     if (@event != null && @event.UserId > 0)
//                     {
//                         _logger.LogInformation(
//                             "[RabbitMQ] UserReactivated received: UserId={UserId}",
//                             @event.UserId);
//                     }

//                     channel.BasicAck(ea.DeliveryTag, false);
//                 }
//                 catch (Exception ex)
//                 {
//                     _logger.LogError(ex,
//                         "[RabbitMQ] Error processing UserReactivatedEvent");

//                     channel.BasicNack(ea.DeliveryTag, false, true);
//                 }
//             };

//             var userOnlineConsumer = new EventingBasicConsumer(channel);

//             userOnlineConsumer.Received += async (_, ea) =>
//             {
//                 try
//                 {
//                     var json = Encoding.UTF8.GetString(ea.Body.ToArray());

//                     var @event = JsonSerializer.Deserialize<UserOnlineEvent>(
//                         json,
//                         new JsonSerializerOptions
//                         {
//                             PropertyNameCaseInsensitive = true
//                         });

//                     if (@event != null && @event.UserId > 0)
//                     {
//                         _logger.LogInformation(
//                             "[RabbitMQ] UserOnline received: UserId={UserId}",
//                             @event.UserId);
//                     }

//                     channel.BasicAck(ea.DeliveryTag, false);
//                 }
//                 catch (Exception ex)
//                 {
//                     _logger.LogError(ex,
//                         "[RabbitMQ] Error processing UserOnlineEvent");

//                     channel.BasicNack(ea.DeliveryTag, false, true);
//                 }
//             };
//             var userOfflineConsumer = new EventingBasicConsumer(channel);

//             userOfflineConsumer.Received += async (_, ea) =>
//             {
//                 try
//                 {
//                     var json = Encoding.UTF8.GetString(ea.Body.ToArray());

//                     var @event = JsonSerializer.Deserialize<UserOfflineEvent>(
//                         json,
//                         new JsonSerializerOptions
//                         {
//                             PropertyNameCaseInsensitive = true
//                         });

//                     if (@event != null && @event.UserId > 0)
//                     {
//                         _logger.LogInformation(
//                             "[RabbitMQ] UserOffline received: UserId={UserId}",
//                             @event.UserId);
//                     }

//                     channel.BasicAck(ea.DeliveryTag, false);
//                 }
//                 catch (Exception ex)
//                 {
//                     _logger.LogError(ex,
//                         "[RabbitMQ] Error processing UserOfflineEvent");

//                     channel.BasicNack(ea.DeliveryTag, false, true);
//                 }
//             };

//             channel.BasicConsume(QueueUserDeactivated, autoAck: false, userDeactivatedConsumer);
//             channel.BasicConsume(QueueRoomDeleted, autoAck: false, roomDeletedConsumer);
//             channel.BasicConsume(QueueUserReactivated, autoAck: false, userReactivatedConsumer);
//             channel.BasicConsume(QueueUserOnline,autoAck: false,userOnlineConsumer);

//             channel.BasicConsume(QueueUserOffline,autoAck: false,userOfflineConsumer);

//             // Block until app shutdown
//             stoppingToken.WaitHandle.WaitOne();
//             channel.Close();
//             connection.Close();
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex,
//                 "[RabbitMQ] Consumer failed to connect — RabbitMQ may not be running yet. " +
//                 "The API continues; queued messages will be processed once the broker " +
//                 "becomes available and the service restarts.");
//         }
//     }
// }


using System.Text;
using System.Text.Json;
using ConnectHub.Message.Models.Events;
using ConnectHub.Message.Repositories.Interfaces;
using ConnectHub.Message.Hubs;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ConnectHub.Message.Services.Implementations;

/// <summary>
/// MessageConsumer — BackgroundService that consumes INBOUND events from
/// other microservices via RabbitMQ.
///
/// Consumes:
///   connecthub.user.deactivated      → from Auth-Service (UC1)
///     Action: soft-delete all messages sent by the deactivated user so they
///             no longer appear in other users' inboxes.
///
///   connecthub.room.deleted          → from ChatRoom-Service (UC3)
///     Action: soft-delete all messages that belong to the deleted room.
///
///   connecthub.user.profile.updated  → from Auth-Service (UC1)
///     Action: broadcast UserProfileUpdated via SignalR so all connected
///             clients refresh avatar/name without a page reload.
///
/// Uses IServiceScopeFactory for scoped DB access in async callbacks.
/// Reads host/credentials from IConfiguration — never hardcoded.
/// BasicNack+requeue on any exception so no message is silently lost.
/// </summary>
public class MessageConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<MessageConsumer> _logger;
    private readonly IHubContext<ChatHub> _hubContext;

    private const string QueueUserDeactivated   = "connecthub.user.deactivated";
    private const string QueueUserOnline        = "connecthub.message.user.online";
    private const string QueueUserOffline       = "connecthub.message.user.offline";
    private const string QueueUserReactivated   = "connecthub.message.user.reactivated";
    private const string QueueRoomDeleted       = "connecthub.room.deleted";
    private const string QueueUserProfileUpdated = "connecthub.user.profile.updated";

    public MessageConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<MessageConsumer> logger,
        IHubContext<ChatHub> hubContext)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run on a background thread so startup is not blocked
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

            // Declare all queues as durable (must match UC1/UC3 publisher declarations)
            channel.QueueDeclare(QueueUserDeactivated,    durable: true, exclusive: false, autoDelete: false);
            channel.QueueDeclare(QueueUserReactivated,    durable: true, exclusive: false, autoDelete: false);
            channel.QueueDeclare(QueueUserOnline,         durable: true, exclusive: false, autoDelete: false);
            channel.QueueDeclare(QueueUserOffline,        durable: true, exclusive: false, autoDelete: false);
            channel.QueueDeclare(QueueRoomDeleted,        durable: true, exclusive: false, autoDelete: false);
            channel.QueueDeclare(QueueUserProfileUpdated, durable: true, exclusive: false, autoDelete: false);

            // Process one message at a time
            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            _logger.LogInformation(
                "[RabbitMQ] Message consumer listening on [{Q1}], [{Q2}], [{Q3}]",
                QueueUserDeactivated, QueueRoomDeleted, QueueUserProfileUpdated);

            // ── Consumer: UserDeactivated from Auth-Service (UC1) ─────────────
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
                            "[RabbitMQ] UserDeactivated received — UserId={UserId}. " +
                            "Soft-deleting all messages sent by this user.",
                            @event.UserId);

                        using var scope = _scopeFactory.CreateScope();
                        var repo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

                        await repo.SoftDeleteAllByUserId(@event.UserId);

                        _logger.LogInformation(
                            "[RabbitMQ] Soft-deleted messages for deactivated UserId={UserId}",
                            @event.UserId);
                    }

                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Error processing UserDeactivatedEvent");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            };

            // ── Consumer: RoomDeleted from ChatRoom-Service (UC3) ─────────────
            var roomDeletedConsumer = new EventingBasicConsumer(channel);
            roomDeletedConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var @event = JsonSerializer.Deserialize<RoomDeletedEvent>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (@event != null && @event.RoomId > 0)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] RoomDeleted received — RoomId={RoomId}. " +
                            "Soft-deleting all room messages.",
                            @event.RoomId);

                        using var scope = _scopeFactory.CreateScope();
                        var repo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

                        await repo.SoftDeleteAllByRoomId(@event.RoomId);

                        _logger.LogInformation(
                            "[RabbitMQ] Soft-deleted all messages for RoomId={RoomId}",
                            @event.RoomId);
                    }

                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Error processing RoomDeletedEvent");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            };

            // ── Consumer: UserReactivated from Auth-Service (UC1) ─────────────
            var userReactivatedConsumer = new EventingBasicConsumer(channel);
            userReactivatedConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var @event = JsonSerializer.Deserialize<UserReactivatedEvent>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (@event != null && @event.UserId > 0)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] UserReactivated received: UserId={UserId}",
                            @event.UserId);
                    }

                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Error processing UserReactivatedEvent");
                    channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            // ── Consumer: UserOnline ───────────────────────────────────────────
            var userOnlineConsumer = new EventingBasicConsumer(channel);
            userOnlineConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var @event = JsonSerializer.Deserialize<UserOnlineEvent>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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
                    _logger.LogError(ex, "[RabbitMQ] Error processing UserOnlineEvent");
                    channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            // ── Consumer: UserOffline ──────────────────────────────────────────
            var userOfflineConsumer = new EventingBasicConsumer(channel);
            userOfflineConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var @event = JsonSerializer.Deserialize<UserOfflineEvent>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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
                    _logger.LogError(ex, "[RabbitMQ] Error processing UserOfflineEvent");
                    channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            // ── Consumer: UserProfileUpdated from Auth-Service (UC1) ──────────
            // When a user changes their avatar or display name, UC1 publishes this
            // event. We broadcast it via SignalR so every connected client can
            // update the avatar in real-time without a page reload.
            var userProfileUpdatedConsumer = new EventingBasicConsumer(channel);
            userProfileUpdatedConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var @event = JsonSerializer.Deserialize<UserProfileUpdatedEvent>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (@event != null && @event.UserId > 0)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] UserProfileUpdated received — UserId={UserId}, AvatarUrl={AvatarUrl}",
                            @event.UserId, @event.AvatarUrl);

                        // Broadcast to ALL connected SignalR clients so every open
                        // browser tab updates the avatar immediately.
                        await _hubContext.Clients.All.SendAsync("UserProfileUpdated", new
                        {
                            userId      = @event.UserId,
                            displayName = @event.DisplayName,
                            avatarUrl   = @event.AvatarUrl,
                        });
                    }

                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Error processing UserProfileUpdatedEvent");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            };

            channel.BasicConsume(QueueUserDeactivated,    autoAck: false, userDeactivatedConsumer);
            channel.BasicConsume(QueueRoomDeleted,        autoAck: false, roomDeletedConsumer);
            channel.BasicConsume(QueueUserReactivated,    autoAck: false, userReactivatedConsumer);
            channel.BasicConsume(QueueUserOnline,         autoAck: false, userOnlineConsumer);
            channel.BasicConsume(QueueUserOffline,        autoAck: false, userOfflineConsumer);
            channel.BasicConsume(QueueUserProfileUpdated, autoAck: false, userProfileUpdatedConsumer);

            // Block until app shutdown
            stoppingToken.WaitHandle.WaitOne();
            channel.Close();
            connection.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[RabbitMQ] Consumer failed to connect — RabbitMQ may not be running yet. " +
                "The API continues; queued messages will be processed once the broker " +
                "becomes available and the service restarts.");
        }
    }
}
