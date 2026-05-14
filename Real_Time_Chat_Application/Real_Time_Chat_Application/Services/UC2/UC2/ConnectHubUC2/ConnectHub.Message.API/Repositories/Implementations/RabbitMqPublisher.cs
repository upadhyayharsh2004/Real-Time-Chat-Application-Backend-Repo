// using System.Text;
// using System.Text.Json;
// using ConnectHub.Message.Models.Events;
// using ConnectHub.Message.Repositories.Interfaces;
// using RabbitMQ.Client;

// namespace ConnectHub.Message.Repositories.Implementations;

// /// <summary>
// /// RabbitMqPublisher — FIXED implementation of IRabbitMqPublisher.
// ///
// /// BUGS FIXED from original UC2:
// /// 1. HostName was hardcoded to "localhost" — now reads from IConfiguration.
// /// 2. Single generic queue "message-queue" — now typed queues per event type.
// /// 3. Generic PublishAsync(object) — now typed methods per event.
// /// 4. Was called BEFORE DB save in ChatHub — now called from MessageService AFTER save.
// /// 5. No persistent message flag — now sets Persistent = true on BasicProperties.
// ///
// /// Registered as AddSingleton — connection/channel reused across requests.
// /// Fire-and-forget: publish failures are logged but never block DB operation.
// ///
// /// Queues (all durable):
// ///   connecthub.message.sent        → Notification-Service, Presence-Service
// ///   connecthub.room.message.sent   → Notification-Service (mentions), ChatRoom-Service
// ///   connecthub.message.read        → Notification-Service (badge clear)
// ///   connecthub.message.deleted     → Notification-Service, ChatRoom-Service
// ///   connecthub.message.edited      → Notification-Service
// /// </summary>
// public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
// {
//     private readonly IConnection _connection;
//     private readonly IModel _channel;
//     private readonly ILogger<RabbitMqPublisher> _logger;

//     // Typed queue names — one per event type
//     public const string QueueMessageSent = "connecthub.message.sent";
//     public const string QueueRoomMessageSent = "connecthub.room.message.sent";
//     public const string QueueMessageRead = "connecthub.message.read";
//     public const string QueueMessageDeleted = "connecthub.message.deleted";
//     public const string QueueMessageEdited = "connecthub.message.edited";

//     public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
//     {
//         _logger = logger;

//         var factory = new ConnectionFactory
//         {
//             //FIXED: Read from IConfiguration — not hardcoded "localhost"
//             HostName = config["RabbitMQ:Host"] ?? "localhost",
//             UserName = config["RabbitMQ:Username"] ?? "guest",
//             Password = config["RabbitMQ:Password"] ?? "guest",
//             Port = int.TryParse(config["RabbitMQ:Port"], out var port) ? port : 5672,
//             VirtualHost = config["RabbitMQ:VHost"] ?? "/"
//         };

//         _connection = factory.CreateConnection();
//         _channel = _connection.CreateModel();

//         // Declare all queues as durable (survive broker restart)
//         foreach (var queue in new[]
//         {
//             QueueMessageSent, QueueRoomMessageSent,
//             QueueMessageRead, QueueMessageDeleted, QueueMessageEdited
//         })
//         {
//             _channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
//         }

//         _logger.LogInformation("[RabbitMQ] Publisher connected to {Host}", factory.HostName);
//     }

//     public Task PublishMessageSentAsync(MessageSentEvent @event) =>
//         Publish(QueueMessageSent, @event);

//     public Task PublishRoomMessageSentAsync(RoomMessageSentEvent @event) =>
//         Publish(QueueRoomMessageSent, @event);

//     public Task PublishMessageReadAsync(MessageReadEvent @event) =>
//         Publish(QueueMessageRead, @event);

//     public Task PublishMessageDeletedAsync(MessageDeletedEvent @event) =>
//         Publish(QueueMessageDeleted, @event);

//     public Task PublishMessageEditedAsync(MessageEditedEvent @event) =>
//         Publish(QueueMessageEdited, @event);

//     private Task Publish(string queue, object @event)
//     {
//         try
//         {
//             var json = JsonSerializer.Serialize(@event);
//             var body = Encoding.UTF8.GetBytes(json);

//             var props = _channel.CreateBasicProperties();
//             props.Persistent = true;          // survive broker restart
//             props.ContentType = "application/json";

//             _channel.BasicPublish(
//                 exchange: "",
//                 routingKey: queue,
//                 basicProperties: props,
//                 body: body);

//             _logger.LogInformation("[RabbitMQ] Published {EventType} to queue {Queue}",
//                 @event.GetType().Name, queue);
//         }
//         catch (Exception ex)
//         {
//             // Non-blocking — DB save already succeeded; log and continue
//             _logger.LogError(ex, "[RabbitMQ] Failed to publish {EventType} to queue {Queue}",
//                 @event.GetType().Name, queue);
//         }

//         return Task.CompletedTask;
//     }

//     public void Dispose()
//     {
//         try { _channel?.Close(); } catch { /* ignore */ }
//         try { _connection?.Close(); } catch { /* ignore */ }
//     }
// }



using System.Text;
using System.Text.Json;
using ConnectHub.Message.Models.Events;
using ConnectHub.Message.Repositories.Interfaces;
using RabbitMQ.Client;

namespace ConnectHub.Message.Repositories.Implementations;

public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private IConnection? _connection;
    private IModel? _channel;
    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly object _lock = new();

    public const string QueueMessageSent = "connecthub.message.sent";
    public const string QueueRoomMessageSent = "connecthub.room.message.sent";
    public const string QueueMessageRead = "connecthub.message.read";
    public const string QueueMessageDeleted = "connecthub.message.deleted";
    public const string QueueMessageEdited = "connecthub.message.edited";

    private static readonly string[] AllQueues =
    {
        QueueMessageSent, QueueRoomMessageSent,
        QueueMessageRead, QueueMessageDeleted, QueueMessageEdited
    };

    public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;

        _factory = new ConnectionFactory
        {
            HostName = config["RabbitMQ:Host"] ?? "localhost",
            UserName = config["RabbitMQ:Username"] ?? "guest",
            Password = config["RabbitMQ:Password"] ?? "guest",
            Port = int.TryParse(config["RabbitMQ:Port"], out var port) ? port : 5672,
            VirtualHost = config["RabbitMQ:VHost"] ?? "/"
        };

        TryConnect();
    }

    private bool TryConnect()
    {
        lock (_lock)
        {
            if (_channel?.IsOpen == true) return true;

            try
            {
                _channel?.Dispose();
                _connection?.Dispose();

                _connection = _factory.CreateConnection();
                _channel = _connection.CreateModel();

                foreach (var queue in AllQueues)
                    _channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);

                _logger.LogInformation("[RabbitMQ] Publisher connected to {Host}", _factory.HostName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[RabbitMQ] Could not connect to {Host}: {Message}. Messages will be skipped until reconnect.",
                    _factory.HostName, ex.Message);
                _channel = null;
                _connection = null;
                return false;
            }
        }
    }

    public Task PublishMessageSentAsync(MessageSentEvent @event) =>
        Publish(QueueMessageSent, @event);

    public Task PublishRoomMessageSentAsync(RoomMessageSentEvent @event) =>
        Publish(QueueRoomMessageSent, @event);

    public Task PublishMessageReadAsync(MessageReadEvent @event) =>
        Publish(QueueMessageRead, @event);

    public Task PublishMessageDeletedAsync(MessageDeletedEvent @event) =>
        Publish(QueueMessageDeleted, @event);

    public Task PublishMessageEditedAsync(MessageEditedEvent @event) =>
        Publish(QueueMessageEdited, @event);

    private Task Publish(string queue, object @event)
    {
        if (!TryConnect())
        {
            _logger.LogWarning("[RabbitMQ] Skipping publish of {EventType} — no connection available.", @event.GetType().Name);
            return Task.CompletedTask;
        }

        try
        {
            var json = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(json);

            var props = _channel!.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";

            _channel.BasicPublish(
                exchange: "",
                routingKey: queue,
                basicProperties: props,
                body: body);

            _logger.LogInformation("[RabbitMQ] Published {EventType} to queue {Queue}",
                @event.GetType().Name, queue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RabbitMQ] Failed to publish {EventType} to queue {Queue}",
                @event.GetType().Name, queue);
            lock (_lock) { _channel = null; }
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try { _channel?.Close(); } catch { /* ignore */ }
        try { _connection?.Close(); } catch { /* ignore */ }
    }
}