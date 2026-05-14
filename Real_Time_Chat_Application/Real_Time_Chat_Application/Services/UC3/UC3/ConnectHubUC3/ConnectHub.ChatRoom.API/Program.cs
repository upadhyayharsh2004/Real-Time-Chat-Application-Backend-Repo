using System.Text;
using ConnectHub.ChatRoom.Data;
using ConnectHub.ChatRoom.Hubs;
using ConnectHub.ChatRoom.Middleware;
using ConnectHub.ChatRoom.Repositories.Implementations;
using ConnectHub.ChatRoom.Repositories.Interfaces;
using ConnectHub.ChatRoom.Services.Implementations;
using ConnectHub.ChatRoom.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// ── Serilog setup ─────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Seq(
        serverUrl: Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://localhost:5341")
    .Enrich.FromLogContext()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// ── Database — SQL Server + EF Core 8 ─────────────────────────────
builder.Services.AddDbContext<ChatRoomDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("ChatRoomDb"),
        npgsqlOptions  => npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

// ── Authentication — JWT Bearer ────────────────────────────────────
// Same JWT secret/issuer/audience as UC1 Auth-Service + UC2 Message-Service.
// All [Authorize] attributes and RoomHub JWT extraction rely on this.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwt = builder.Configuration.GetSection("Jwt");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt["Issuer"],
        ValidAudience = jwt["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwt["Secret"]!))
    };

    // SignalR WebSocket transport cannot carry custom HTTP headers —
    // JWT passed as query string ?access_token=<token>
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs/rooms"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
});

// ── Repositories — AddScoped ────────────────────────────────────────
builder.Services.AddScoped<IChatRoomRepository, ChatRoomRepository>();

// ── RabbitMQ Publisher — AddSingleton ──────────────────────────────
// Singleton: connection/channel reused across requests.
// Reads host/user/password from IConfiguration.
// Publishes AFTER DB save so RoomId/MemberId are always valid.
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

// ── Services — AddScoped ────────────────────────────────────────────
// ChatRoomService receives IRabbitMqPublisher via DI and publishes
// typed events after every DB operation.
builder.Services.AddScoped<IChatRoomService, ChatRoomService>();

// ── RabbitMQ Consumer — BackgroundService ──────────────────────────
// Listens for INBOUND events from other microservices:
//   connecthub.room.message.sent → from UC2 Message-Service (updates last message)
//   connecthub.user.deactivated  → from UC1 Auth-Service (removes from rooms)
builder.Services.AddHostedService<ChatRoomConsumer>();

// ── SignalR ─────────────────────────────────────────────────────────
// RoomHub: JoinRoom → DB save → RabbitMQ → Groups.AddToGroupAsync → broadcast
// RoomHub: LeaveRoom → DB save → RabbitMQ → Groups.RemoveFromGroupAsync → broadcast
// RoomHub: TypingInRoom → OthersInGroup broadcast
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
});

// ── Controllers ─────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── Swagger / Swashbuckle ────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ConnectHub — ChatRoom API",
        Version = "v1",
        Description = "ChatRoom microservice for ConnectHub. " +
                      "Handles room creation, membership, roles, invites, and presence. " +
                      "Real-time via SignalR (RoomHub at /hubs/rooms). " +
                      "Domain events published to RabbitMQ after every DB operation. " +
                      "Consumes RoomMessageSent from UC2 to update last-message preview. " +
                      "Consumes UserDeactivated from UC1 to remove users from rooms.",
        Contact = new OpenApiContact
        {
            Name = "ConnectHub Platform",
            Email = "support@connecthub.io"
        }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token from ConnectHub Auth Service. Example: Bearer eyJhbGci..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// ── CORS ──────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("ConnectHubPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? new[] { "http://localhost:3000", "http://localhost:5000" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

// ── Health Checks ─────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ChatRoomDbContext>("chatroom-db");

// ── Build ─────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Auto-migrate on startup ────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatRoomDbContext>();
    db.Database.Migrate();
}

// ── Middleware pipeline ────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ConnectHub ChatRoom API v1");
    c.RoutePrefix = "swagger";
    c.DisplayRequestDuration();
    c.EnableDeepLinking();
});

app.UseSerilogRequestLogging();
//app.UseHttpsRedirection();
app.UseCors("ConnectHubPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── SignalR Hub endpoint ─────────────────────────────────────────────
// JWT passed as ?access_token= query param for WebSocket transport
app.MapHub<RoomHub>("/hubs/rooms");

app.MapHealthChecks("/health");

app.Run();
