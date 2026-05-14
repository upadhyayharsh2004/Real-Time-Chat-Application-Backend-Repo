using System.Text;
using Azure.Storage.Blobs;
using ConnectHub.Media.Data;
using ConnectHub.Media.Middleware;
using ConnectHub.Media.Repositories.Implementations;
using ConnectHub.Media.Repositories.Interfaces;
using ConnectHub.Media.Services;
using ConnectHub.Media.Services.Implementations;
using ConnectHub.Media.Services.Interfaces;
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
builder.Services.AddDbContext<MediaDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("MediaDb"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));


builder.Services.Configure<AzureBlobOptions>(
    builder.Configuration.GetSection("AzureBlob"));

var azureBlobConnectionString = builder.Configuration["AzureBlob:ConnectionString"]
    ?? "UseDevelopmentStorage=true"; // Azurite for local dev

builder.Services.AddSingleton(new BlobServiceClient(azureBlobConnectionString));

// ── JWT Bearer — same secret as UC1-UC5 ───────────────────────────
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
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// ── Repositories — AddScoped ────────────────────────────────────────
builder.Services.AddScoped<IMediaRepository, MediaRepository>();

// ── RabbitMQ Publisher — AddSingleton ──────────────────────────────
// Same pattern as UC1-UC5: reads config, durable queues, persistent messages.
// Publishes AFTER DB save so FileId is always valid.
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

// ── Services — AddScoped ────────────────────────────────────────────
// MediaService uses BlobServiceClient + IOptions<AzureBlobOptions> + IRabbitMqPublisher.
builder.Services.AddScoped<IMediaService, MediaService>();

// ── RabbitMQ Consumer — BackgroundService ──────────────────────────
// Consumes:
//   connecthub.user.deactivated → UC1 → mark user files as expired
//   connecthub.room.deleted     → UC3 → mark room files as expired
builder.Services.AddHostedService<MediaConsumer>();

// ── MediaCleanupService — IHostedService (daily cleanup) ───────────
// Calls IMediaService.CleanupExpiredFiles() every 24 hours.
// Deletes expired blobs from Azure + removes DB records.
builder.Services.AddHostedService<MediaCleanupService>();

// ── Controllers + Swagger ────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "ConnectHub — Media API",
        Version = "v1",
        Description =
            "File/Media microservice for ConnectHub. " +
            "Handles file uploads via IFormFile (ASP.NET Core multipart form). " +
            "Files uploaded to Azure Blob Storage via BlobServiceClient.GetContainerClient().UploadBlobAsync(). " +
            "GenerateSasUrl() uses BlobSasBuilder with ExpiresOn = UtcNow.AddHours(1) " +
            "for secure download without a public Blob container. " +
            "IHostedService cleans up expired files daily. " +
            "Publishes MediaUploaded/MediaDeleted events to RabbitMQ. " +
            "Consumes user.deactivated (UC1) and room.deleted (UC3).",
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
    .AddDbContextCheck<MediaDbContext>("media-db");

// ── Build ─────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Auto-migrate on startup ────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
    db.Database.Migrate();
}

// ── Middleware pipeline ────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ConnectHub Media API v1");
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
app.MapHealthChecks("/health");

app.Run();
