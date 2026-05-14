using System.Text;
using ConnectHub.Auth.Data;
using ConnectHub.Auth.Middleware;
using ConnectHub.Auth.Repositories.Implementations;
using ConnectHub.Auth.Repositories.Interfaces;
using System.Security.Claims;
using ConnectHub.Auth.Services.Implementations;
using ConnectHub.Auth.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using ConnectHub.Auth.Models.Entities;

// ── Serilog setup ────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Seq(
        serverUrl: Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://localhost:5341")
    .Enrich.FromLogContext()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ── Database ─────────────────────────────────────────────────────
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("AuthDb"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

// ── Authentication ────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT Secret not configured.");

builder.Services.AddAuthentication(options =>
{
    // JWT for API endpoints (default)
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;

    // Cookie scheme handles the OAuth sign-in handshake
    options.DefaultSignInScheme       = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name       = "ConnectHub.Auth";
    options.Cookie.SameSite   = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;   // HTTP localhost ke liye
    options.Cookie.HttpOnly   = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Path       = "/";
    options.ExpireTimeSpan    = TimeSpan.FromMinutes(10);    // sirf OAuth handshake ke liye, short TTL
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
            Encoding.UTF8.GetBytes(jwt["Secret"]!)),
        ClockSkew                = TimeSpan.Zero
    };
})
.AddGoogle(options =>
{
    options.ClientId     = builder.Configuration["OAuth:Google:ClientId"]!;
    options.ClientSecret = builder.Configuration["OAuth:Google:ClientSecret"]!;
    options.CallbackPath = "/signin-google";

    // ✅ KEY FIX: explicitly Cookie scheme use karo OAuth handshake ke liye
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

    options.Scope.Add("email");
    options.Scope.Add("profile");
    options.SaveTokens = true;

    // ✅ Correlation cookie settings — HTTP localhost ke liye
    options.CorrelationCookie.Name         = ".AspNetCore.Correlation.Google";
    options.CorrelationCookie.SameSite     = SameSiteMode.Lax;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
    options.CorrelationCookie.HttpOnly     = true;
    options.CorrelationCookie.IsEssential  = true;
    options.CorrelationCookie.Path         = "/";
    options.CorrelationCookie.MaxAge       = TimeSpan.FromMinutes(10);
});

builder.Services.AddAuthorization();

// ── Identity ──────────────────────────────────────────────────────
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// ── Repositories ──────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

// ── Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IJwtService, JwtService>();

// ── Token Blacklist ───────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();

// ── RabbitMQ ──────────────────────────────────────────────────────
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

// ── Controllers ───────────────────────────────────────────────────
builder.Services.AddControllers();

// ── Swagger ───────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "ConnectHub — Auth API",
        Version     = "v1",
        Description = "User authentication and account management microservice for ConnectHub.",
        Contact     = new OpenApiContact { Name = "ConnectHub Platform", Email = "support@connecthub.io" }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "Bearer",
        BearerFormat = "JWT",
        In          = ParameterLocation.Header,
        Description = "Enter your JWT Bearer token. Example: Bearer eyJhbGci..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
});

// ── CORS ──────────────────────────────────────────────────────────
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
              .AllowCredentials();
    });
});

// ── Health Checks ─────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AuthDbContext>("auth-db");

// ── Build ─────────────────────────────────────────────────────────
var app = builder.Build();

// ── Auto-migrate ──────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    db.Database.Migrate();
}

// ── Middleware Pipeline ───────────────────────────────────────────
// ✅ ExceptionHandling sabse pehle — taaki saari errors catch ho sakein
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ConnectHub Auth API v1");
    c.RoutePrefix = "swagger";
    c.DisplayRequestDuration();
    c.EnableDeepLinking();
});

app.UseSerilogRequestLogging();

// ✅ CORS Authentication se pehle
app.UseCors("ConnectHubPolicy");

// ✅ Authentication → TokenBlacklist → Authorization — is order mein hi rehna chahiye
app.UseAuthentication();
app.UseMiddleware<TokenBlacklistMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();