using System.Text;
using System.Text.Json;
using ConnectHub.Media.Models.Events;
using ConnectHub.Media.Repositories.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ConnectHub.Media.Services.Implementations;

/// <summary>
/// MediaConsumer — BackgroundService consuming INBOUND events from UC1 and UC3.
/// Same pattern as UC2-UC5: reads config, IServiceScopeFactory, BasicNack+requeue.
///
/// Queues consumed (EXACT names matching UC1/UC3 publishers):
///   connecthub.user.deactivated → UC1 → flag user files as expired
///   connecthub.room.deleted     → UC3 → mark room files as expired for cleanup
/// </summary>
public class MediaConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<MediaConsumer> _logger;

    private const string QueueUserDeactivated = "connecthub.user.deactivated";
    private const string QueueRoomDeleted     = "connecthub.room.deleted";

    public MediaConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<MediaConsumer> logger)
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
                HostName    = _config["RabbitMQ:Host"]     ?? "localhost",
                UserName    = _config["RabbitMQ:Username"] ?? "guest",
                Password    = _config["RabbitMQ:Password"] ?? "guest",
                Port        = int.TryParse(_config["RabbitMQ:Port"], out var port) ? port : 5672,
                VirtualHost = _config["RabbitMQ:VHost"]    ?? "/"
            };

            var connection = factory.CreateConnection();
            var channel    = connection.CreateModel();

            channel.QueueDeclare(QueueUserDeactivated, durable: true, exclusive: false, autoDelete: false);
            channel.QueueDeclare(QueueRoomDeleted,     durable: true, exclusive: false, autoDelete: false);
            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            _logger.LogInformation(
                "[RabbitMQ] Media consumer listening on [{Q1}] and [{Q2}]",
                QueueUserDeactivated, QueueRoomDeleted);

            // ── UserDeactivated — from UC1 ─────────────────────────
            var deactivatedConsumer = new EventingBasicConsumer(channel);
            deactivatedConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var json   = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var @event = JsonSerializer.Deserialize<UserDeactivatedEvent>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (@event != null)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] UserDeactivated UserId={UserId} — marking files as expired.",
                            @event.UserId);

                        // Mark all user's files as expired (picked up by daily cleanup)
                        using var scope = _scopeFactory.CreateScope();
                        var repo = scope.ServiceProvider.GetRequiredService<IMediaRepository>();
                        var files = await repo.FindByUploadedBy(@event.UserId);
                        foreach (var file in files)
                        {
                            file.ExpiresAt = DateTime.UtcNow.AddDays(7); // grace period
                            await repo.Update(file);
                        }
                    }

                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Error processing UserDeactivatedEvent");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            };

            // ── RoomDeleted — from UC3 ─────────────────────────────
            var roomDeletedConsumer = new EventingBasicConsumer(channel);
            roomDeletedConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var json   = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var @event = JsonSerializer.Deserialize<RoomDeletedEvent>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (@event != null)
                    {
                        _logger.LogInformation(
                            "[RabbitMQ] RoomDeleted RoomId={RoomId} — marking room files as expired.",
                            @event.RoomId);

                        using var scope = _scopeFactory.CreateScope();
                        var repo = scope.ServiceProvider.GetRequiredService<IMediaRepository>();
                        await repo.MarkRoomFilesExpired(@event.RoomId, DateTime.UtcNow.AddDays(1));
                    }

                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RabbitMQ] Error processing RoomDeletedEvent");
                    channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                }
            };

            channel.BasicConsume(QueueUserDeactivated, autoAck: false, deactivatedConsumer);
            channel.BasicConsume(QueueRoomDeleted,     autoAck: false, roomDeletedConsumer);

            stoppingToken.WaitHandle.WaitOne();
            channel.Close();
            connection.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[RabbitMQ] Media consumer failed to connect. " +
                "Service continues — messages will queue when broker is available.");
        }
    }
}
