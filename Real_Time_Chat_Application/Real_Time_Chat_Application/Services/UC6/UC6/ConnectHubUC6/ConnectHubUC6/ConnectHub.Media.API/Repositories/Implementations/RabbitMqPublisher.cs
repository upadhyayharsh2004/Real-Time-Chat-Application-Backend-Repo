using System.Text;
using System.Text.Json;
using ConnectHub.Media.Models.Events;
using ConnectHub.Media.Repositories.Interfaces;
using RabbitMQ.Client;

namespace ConnectHub.Media.Repositories.Implementations;

/// <summary>
/// RabbitMqPublisher — publishes Media domain events to RabbitMQ.
/// Same pattern as UC1-UC5: reads config, durable, persistent, AddSingleton.
///
/// Queues:
///   connecthub.media.uploaded → Notification-Service, Message-Service
///   connecthub.media.deleted  → Notification-Service
/// </summary>
public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private bool _disposed;

    public const string QueueMediaUploaded = "connecthub.media.uploaded";
    public const string QueueMediaDeleted  = "connecthub.media.deleted";

    public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;
        var factory = new ConnectionFactory
        {
            HostName    = config["RabbitMQ:Host"]     ?? "localhost",
            UserName    = config["RabbitMQ:Username"] ?? "guest",
            Password    = config["RabbitMQ:Password"] ?? "guest",
            Port        = int.TryParse(config["RabbitMQ:Port"], out var port) ? port : 5672,
            VirtualHost = config["RabbitMQ:VHost"]    ?? "/"
        };

        _connection = factory.CreateConnection();
        _channel    = _connection.CreateModel();

        foreach (var q in new[] { QueueMediaUploaded, QueueMediaDeleted })
            _channel.QueueDeclare(q, durable: true, exclusive: false, autoDelete: false);

        _logger.LogInformation("[RabbitMQ] Media publisher connected to {Host}", factory.HostName);
    }

    public Task PublishMediaUploadedAsync(MediaUploadedEvent @event) =>
        Publish(QueueMediaUploaded, @event);

    public Task PublishMediaDeletedAsync(MediaDeletedEvent @event) =>
        Publish(QueueMediaDeleted, @event);

    private Task Publish(string queue, object @event)
    {
        try
        {
            var body  = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
            var props = _channel.CreateBasicProperties();
            props.Persistent  = true;
            props.ContentType = "application/json";

            _channel.BasicPublish(
                exchange: "", routingKey: queue,
                basicProperties: props, body: body);

            _logger.LogInformation("[RabbitMQ] Published {EventType} to {Queue}",
                @event.GetType().Name, queue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RabbitMQ] Failed to publish {EventType}", @event.GetType().Name);
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        try { _channel?.Close(); }    catch { }
        try { _connection?.Close(); } catch { }
        _disposed = true;
    }
}
