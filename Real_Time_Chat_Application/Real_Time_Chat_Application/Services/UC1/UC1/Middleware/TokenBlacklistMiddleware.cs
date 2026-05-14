using System.IdentityModel.Tokens.Jwt;
using ConnectHub.Auth.Services.Interfaces;
using System.Security.Claims;

public class TokenBlacklistMiddleware
{
    private readonly RequestDelegate _next;

    public TokenBlacklistMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITokenBlacklistService blacklist)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip completely for register only
        if (path.Contains("/api/users/register", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        if (authHeader != null &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();

            try
            {
                var handler = new JwtSecurityTokenHandler();

                if (handler.CanReadToken(token))
                {
                    var jwt = handler.ReadJwtToken(token);

                    var jti = jwt.Claims
                        .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

                    var userId = jwt.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

                    var isLoginPath = path.Contains("/api/users/login",
                        StringComparison.OrdinalIgnoreCase);

                    // STEP 1: JTI blacklist
                    // For login path — skip this check, let controller handle it
                    if (!string.IsNullOrEmpty(jti) && blacklist.IsBlacklisted(jti))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync("{\"success\":false,\"message\":\"Token invalidated. Please remove old token from Swagger and login fresh.\",\"data\":null,\"errors\":[]}");
                        return;
                    }

                    if (int.TryParse(userId, out int id))
                    {
                        var iatClaim = jwt.Claims
                            .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat ||
                                                 c.Type == "iat")?.Value;

                        if (long.TryParse(iatClaim, out long iatUnix))
                        {
                            var tokenIssuedAt = DateTimeOffset
                                .FromUnixTimeSeconds(iatUnix).UtcDateTime;

                            // Cache check first (fast)
                            if (blacklist.IsUserReactivated(id, tokenIssuedAt))
                            {
                                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                                context.Response.ContentType = "application/json";
                                await context.Response.WriteAsync(
                                    "{\"success\":false,\"message\":\"Account reactivated. Please logout from Swagger and login again.\",\"data\":null,\"errors\":[]}");
                                return;
                            }

                            if (blacklist.IsUserBlacklisted(id, tokenIssuedAt))
                            {
                                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                                context.Response.ContentType = "application/json";
                                await context.Response.WriteAsync(
                                    "{\"success\":false,\"message\":\"Account deactivated Please Contact Admin for reactivation.\",\"data\":null,\"errors\":[]}");
                                return;
                            }

                            // DB fallback — works even after server restart
                            var userService = context.RequestServices
                                .GetRequiredService<IUserService>();
                            var user = await userService.GetUserById(id);

                            if (user != null)
                            {
                                // Reactivated but old token
                                if (user.WasReactivated &&
                                    user.DeactivatedAt.HasValue &&
                                    tokenIssuedAt <= user.DeactivatedAt.Value)
                                {
                                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                                    context.Response.ContentType = "application/json";
                                    await context.Response.WriteAsync(
                                        "{\"success\":false,\"message\":\"Account reactivated. Please logout from Swagger and login again.\",\"data\":null,\"errors\":[]}");
                                    return;
                                }

                                // Deactivated
                                if (!user.IsActive ||
                                    (user.DeactivatedAt.HasValue &&
                                     tokenIssuedAt <= user.DeactivatedAt.Value))
                                {
                                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                                    context.Response.ContentType = "application/json";
                                    await context.Response.WriteAsync(
                                        "{\"success\":false,\"message\":\"Account deactivated.\",\"data\":null,\"errors\":[]}");
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        await _next(context);
    }
}