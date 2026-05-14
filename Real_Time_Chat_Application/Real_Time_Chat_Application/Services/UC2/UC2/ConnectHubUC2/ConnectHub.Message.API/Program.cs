using System.Text;
using ConnectHub.Message.Data;
using ConnectHub.Message.Hubs;
using ConnectHub.Message.Middleware;
using ConnectHub.Message.Repositories.Implementations;
using ConnectHub.Message.Repositories.Interfaces;
using ConnectHub.Message.Services.Implementations;
using ConnectHub.Message.Services.Interfaces;
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
builder.Services.AddDbContext<MessageDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("MessageDb"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

// ── Authentication — JWT Bearer ────────────────────────────────────
// Same JWT secret/issuer/audience as Auth-Service (shared token contract).
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
    // JWT is passed as query string ?access_token=<token>
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs/chat"))
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
builder.Services.AddScoped<IMessageRepository, MessageRepository>();

// ── RabbitMQ Publisher — AddSingleton ──────────────────────────────
// Singleton: connection/channel reused across requests (efficient).
// Reads host/user/password from IConfiguration (not hardcoded).
// Publishes AFTER DB save so MessageId is always valid.
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

// ── Services — AddScoped ────────────────────────────────────────────
// MessageService receives IRabbitMqPublisher via DI and publishes
// typed events after every DB operation.
builder.Services.AddScoped<IMessageService, MessageService>();

// ── RabbitMQ Consumer — BackgroundService ──────────────────────────
// Listens for INBOUND events from other microservices:
//   connecthub.user.deactivated → from Auth-Service
//   connecthub.room.deleted     → from ChatRoom-Service
// Uses IServiceScopeFactory for scoped DB access in callbacks.
builder.Services.AddHostedService<MessageConsumer>();

// ── SignalR ─────────────────────────────────────────────────────────
// ChatHub.SendDirectMessage → DB save → RabbitMQ publish → SignalR push
// ChatHub.SendRoomMessage   → DB save → RabbitMQ publish → Group broadcast
// ChatHub.TypingIndicator   → direct push to recipient's connection
// ChatHub.MarkMessageRead   → DB update → RabbitMQ publish → read receipt push
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
}).AddJsonProtocol(options => {
    options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// ── Controllers ─────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── Swagger / Swashbuckle ────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ConnectHub — Message API",
        Version = "v1",
        Description = "Message microservice for ConnectHub. " +
                      "Real-time via SignalR (ChatHub at /hubs/chat). " +
                      "Domain events published to RabbitMQ after every DB operation. " +
                      "REST endpoints for history, search, edit, delete, reactions, pins.",
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
        Description = "Enter JWT Bearer token from ConnectHub Auth Service."
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
    .AddDbContextCheck<MessageDbContext>("message-db");

// ── Build ─────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Auto-migrate on startup ────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MessageDbContext>();
    db.Database.Migrate();
}

// ── Middleware pipeline ────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ConnectHub Message API v1");
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

// ── SignalR Hub ────────────────────────────────────────────────────────
app.MapHub<ChatHub>("/hubs/chat");

app.MapHealthChecks("/health");

app.Run();
