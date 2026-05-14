using System.Text;
using ConnectHub.Presence.Data;
using ConnectHub.Presence.Hubs;
using ConnectHub.Presence.Middleware;
using ConnectHub.Presence.Repositories.Implementations;
using ConnectHub.Presence.Repositories.Interfaces;
using ConnectHub.Presence.Services.Implementations;
using ConnectHub.Presence.Services.Interfaces;
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
builder.Services.AddDbContext<PresenceDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PresenceDb"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

// ── JWT Bearer — same secret as UC1 / UC2 / UC3 ───────────────────
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
                context.HttpContext.Request.Path.StartsWithSegments("/hubs/presence"))
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
builder.Services.AddScoped<IPresenceRepository, PresenceRepository>();

// ── RabbitMQ Publisher — AddSingleton ──────────────────────────────
// Same pattern as UC1/UC2/UC3: reads config, typed queues, persistent messages.
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

// ── PresenceService — AddSINGLETON (CRITICAL — as per class diagram 4.4) ──────
// The SAME ConcurrentDictionary<int, HashSet<string>> instance MUST be
// shared across ALL Hub connections and API controllers.
// Uses IServiceScopeFactory internally for scoped DB access.
builder.Services.AddSingleton<IPresenceService, PresenceService>();

// ── RabbitMQ Consumer — BackgroundService ──────────────────────────
// IPresenceService is Singleton — injected directly (no IServiceScopeFactory needed
// for sync methods like ClearUserConnections, IsUserOnline).
// Consumes exact queue names from UC1 + UC2:
//   connecthub.user.deactivated      → UC1
//   connecthub.user.online           → UC1 login
//   connecthub.user.offline          → UC1 logout
//   connecthub.user.reactivated      → UC1
//   connecthub.user.profile.updated  → UC1
//   connecthub.message.sent          → UC2
builder.Services.AddHostedService<PresenceConsumer>();

// ── SignalR ─────────────────────────────────────────────────────────
// PresenceHub.OnConnectedAsync → IPresenceService.UserConnected(userId, connectionId)
// PresenceHub.OnDisconnectedAsync → IPresenceService.UserDisconnected(userId, connectionId)
// Broadcasts UserOnline / UserOffline to all other clients
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors    = builder.Environment.IsDevelopment();
    options.KeepAliveInterval       = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval   = TimeSpan.FromSeconds(60);
    options.MaximumReceiveMessageSize = 32 * 1024;
});

// ── Controllers + Swagger ────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "ConnectHub — Presence API",
        Version = "v1",
        Description =
            "Presence microservice for ConnectHub. " +
            "Tracks online/offline using ConcurrentDictionary<int,HashSet<string>> (in-memory Singleton). " +
            "No DB round-trip for real-time status queries. " +
            "Real-time via SignalR PresenceHub at /hubs/presence. " +
            "Publishes connecthub.presence.online / connecthub.presence.offline to RabbitMQ. " +
            "Consumes from UC1 (user.deactivated, user.online, user.offline, user.reactivated, user.profile.updated) " +
            "and UC2 (message.sent).",
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
              .AllowCredentials(); // Required for SignalR
    });
});

// ── Health Checks ─────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PresenceDbContext>("presence-db");

// ── Build ─────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Auto-migrate on startup ────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PresenceDbContext>();
    db.Database.Migrate();
}

// ── Middleware pipeline ────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ConnectHub Presence API v1");
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
app.MapHub<PresenceHub>("/hubs/presence");

app.MapHealthChecks("/health");

app.Run();
