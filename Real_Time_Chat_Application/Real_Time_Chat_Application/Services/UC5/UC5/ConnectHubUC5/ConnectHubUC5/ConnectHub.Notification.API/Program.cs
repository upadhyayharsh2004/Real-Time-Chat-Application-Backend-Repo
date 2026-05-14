using System.Text;
using ConnectHub.Notification.Data;
using ConnectHub.Notification.Hubs;
using ConnectHub.Notification.Middleware;
using ConnectHub.Notification.Repositories.Implementations;
using ConnectHub.Notification.Repositories.Interfaces;
using ConnectHub.Notification.Services.Implementations;
using ConnectHub.Notification.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// ── Serilog ───────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Seq(Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://localhost:5341")
    .Enrich.FromLogContext()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ── Database — SQL Server + EF Core 8 ─────────────────────────────
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("NotificationDb"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

// ── JWT Bearer — same secret as UC1/UC2/UC3/UC4 ───────────────────
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwt = builder.Configuration.GetSection("Jwt");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = jwt["Issuer"],
        ValidAudience            = jwt["Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwt["Secret"]!))
    };

    // SignalR: JWT from query string ?access_token= (WebSocket cannot carry headers)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var token = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) &&
                context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
                context.Token = token;
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// ── Repositories — AddScoped ────────────────────────────────────────
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

// ── Services — AddScoped ────────────────────────────────────────────
// NotificationService uses IHubContext<NotificationHub> (injected by SignalR DI)
// to push real-time badge count updates to recipient's active connections.
// Uses MailKit/MimeKit for email notifications to offline users.
builder.Services.AddScoped<INotificationService, NotificationService>();

// ── RabbitMQ Consumer — BackgroundService ──────────────────────────
// Consumes events from UC1/UC2/UC3/UC4:
//   connecthub.message.sent          → UC2 → MESSAGE notification
//   connecthub.room.message.sent     → UC2 → MENTION notification
//   connecthub.message.read          → UC2 → mark notification read
//   connecthub.message.deleted       → UC2 → delete notification
//   connecthub.room.member.joined    → UC3 → ROOM_INVITE notification
//   connecthub.room.created          → UC3 → log
//   connecthub.user.deactivated      → UC1 → clear notifications
//   connecthub.user.role.changed     → UC1 → ROLE_CHANGE notification
//   connecthub.user.registered       → UC1 → welcome PLATFORM notification
//   connecthub.presence.online       → UC4 → log
//   connecthub.presence.offline      → UC4 → trigger queued emails
builder.Services.AddHostedService<NotificationConsumer>();

// ── SignalR ─────────────────────────────────────────────────────────
// NotificationHub pushes "NotificationCount" badge updates.
// IHubContext<NotificationHub> injected into NotificationService.
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors      = builder.Environment.IsDevelopment();
    options.KeepAliveInterval         = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval     = TimeSpan.FromSeconds(60);
    options.MaximumReceiveMessageSize = 32 * 1024;
});

// ── Controllers + Swagger ────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "ConnectHub — Notification API",
        Version = "v1",
        Description =
            "Notification microservice for ConnectHub. " +
            "Dispatches in-app and email notifications. " +
            "After Send(), calls IHubContext<NotificationHub>.Clients.User(recipientId)" +
            ".SendAsync('NotificationCount', unreadCount) for real-time badge update. " +
            "MailKit/MimeKit sends email to offline users. " +
            "Consumes events from UC1 (user.*), UC2 (message.*), UC3 (room.*), UC4 (presence.*).",
        Contact = new OpenApiContact
        {
            Name  = "ConnectHub Platform",
            Email = "support@connecthub.io"
        }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "Bearer",
        BearerFormat = "JWT",
        In          = ParameterLocation.Header,
        Description = "Enter JWT from ConnectHub Auth Service (UC1)."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
});

// ── CORS ──────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("ConnectHubPolicy", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:3000", "http://localhost:5000" };
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ── Health Checks ─────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<NotificationDbContext>("notification-db");

// ── Build ─────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Auto-migrate on startup ────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    db.Database.Migrate();
}

// ── Middleware pipeline ────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ConnectHub Notification API v1");
    c.RoutePrefix = "swagger";
    c.DisplayRequestDuration();
    c.EnableDeepLinking();
});

app.UseSerilogRequestLogging();
// app.UseHttpsRedirection();
app.UseCors("ConnectHubPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── SignalR Hub ────────────────────────────────────────────────────────
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapHealthChecks("/health");

app.Run();
