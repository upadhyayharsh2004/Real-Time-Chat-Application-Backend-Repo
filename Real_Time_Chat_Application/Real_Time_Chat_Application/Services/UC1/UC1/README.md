# ConnectHub.Auth.API

**Authentication Microservice** for ConnectHub Real-Time Chat Platform  
`.NET 8 / ASP.NET Core / EF Core 8 / SQL Server`

---

## What this service does

Handles all identity and authentication for ConnectHub:
- User registration with username, display name, email, password
- Login with JWT Bearer tokens (access + refresh token pair)
- Google OAuth2 and GitHub OAuth2 login
- Refresh token rotation (secure logout support)
- Profile update (display name, bio, avatar URL)
- Password change with bcrypt (PasswordHasher<User>)
- Full-text user search by username or display name
- Account deactivation (IsActive = false — preserves message history)
- SetOnlineStatus (called by ChatHub on connect/disconnect)

---

## Project structure

```
ConnectHub.Auth.API/
├── Controllers/
│   └── UserController.cs          ← All API endpoints
├── Data/
│   └── AuthDbContext.cs           ← EF Core DbContext + index config
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs
├── Models/
│   ├── Entities/
│   │   ├── User.cs                ← EF Core entity (exact doc fields)
│   │   └── RefreshToken.cs        ← Refresh token entity
│   └── DTOs/
│       └── AuthDtos.cs            ← All request/response DTOs
├── Repositories/
│   ├── Interfaces/
│   │   ├── IUserRepository.cs
│   │   └── IRefreshTokenRepository.cs
│   └── Implementations/
│       ├── UserRepository.cs
│       └── RefreshTokenRepository.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IUserService.cs        ← Exact methods from class diagram
│   │   └── IJwtService.cs
│   └── Implementations/
│       ├── UserService.cs
│       └── JwtService.cs
├── Tests/
│   └── UserServiceTests.cs        ← xUnit + Moq unit tests
├── Program.cs                     ← DI, JWT, OAuth, Swagger, CORS
├── appsettings.json
├── Dockerfile                     ← Multi-stage build
└── docker-compose.yml             ← SQL Server + Auth API
```

---

## API Endpoints

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/users/register` | None | Register new account |
| POST | `/api/users/login` | None | Login → JWT tokens |
| POST | `/api/users/logout` | JWT | Logout + revoke tokens |
| POST | `/api/users/refresh` | None | Rotate refresh token |
| GET | `/api/users/validate?token=` | None | Validate JWT |
| GET | `/api/users/{id}` | JWT | Get user profile |
| PUT | `/api/users/{id}` | JWT | Update profile |
| POST | `/api/users/{id}/change-password` | JWT | Change password |
| GET | `/api/users/search?q=` | JWT | Search users |
| GET | `/api/users/active` | Admin | All active users |
| DELETE | `/api/users/{id}/deactivate` | Admin | Deactivate account |
| GET | `/api/users/oauth2/google` | None | Google OAuth2 |
| GET | `/api/users/oauth2/github` | None | GitHub OAuth2 |
| GET | `/api/users/oauth2/callback` | None | OAuth callback |
| GET | `/health` | None | Health check |
| GET | `/swagger` | None | Swagger UI |

---

## How to run

### Option 1 — Docker Compose (recommended)
```bash
cd ConnectHub.Auth.API
docker-compose up --build
```
- Auth API: http://localhost:5001
- Swagger UI: http://localhost:5001/swagger

### Option 2 — Local with SQL Server
1. Update connection string in `appsettings.Development.json`
2. Run migrations:
```bash
dotnet ef database update
```
3. Run the API:
```bash
dotnet run
```

---

## Configuration (appsettings.json)

| Key | Description |
|-----|-------------|
| `ConnectionStrings:AuthDb` | SQL Server connection string |
| `Jwt:Secret` | JWT signing key (min 32 chars) |
| `Jwt:Issuer` | Token issuer (`ConnectHub`) |
| `Jwt:Audience` | Token audience (`ConnectHubUsers`) |
| `Jwt:ExpiryMinutes` | Access token lifetime (default: 60) |
| `Jwt:RefreshExpiryDays` | Refresh token lifetime (default: 7) |
| `OAuth:Google:ClientId` | Google OAuth app client ID |
| `OAuth:GitHub:ClientId` | GitHub OAuth app client ID |

---

## Important — JWT for SignalR

The `OnMessageReceived` event in `Program.cs` reads the token from the
`?access_token=` query string. This is **required** because WebSocket
connections cannot send `Authorization` headers. The ChatHub reads
`Context.UserIdentifier` which maps to the `NameIdentifier` claim
(UserId) set in `JwtService.GenerateAccessToken()`.

---

## DI Registration Summary

| Service | Lifetime | Reason |
|---------|----------|--------|
| `AuthDbContext` | Scoped | EF Core standard |
| `IUserRepository` | Scoped | Per-request DB operations |
| `IRefreshTokenRepository` | Scoped | Per-request DB operations |
| `IUserService` | Scoped | Depends on scoped repos |
| `IJwtService` | Scoped | Stateless, safe as scoped |
| `IPasswordHasher<User>` | Scoped | ASP.NET Identity standard |

---

## Running tests
```bash
dotnet test
```
