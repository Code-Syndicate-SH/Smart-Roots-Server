using Microsoft.AspNetCore.Mvc;
using Smart_Roots_Server.Infrastructure.Dtos;
using Smart_Roots_Server.Services;

namespace Smart_Roots_Server.Routes
{
    public static class AuthRoutes
    {
        public static RouteGroupBuilder MapAuthApis(this RouteGroupBuilder group)
        {
            group.MapPost("/register", async ([FromBody] RegisterRequest req, ISupabaseAuthService auth, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                    return Results.BadRequest(new { error = "Email and Password are required." });

                var (ok, msg) = await auth.RegisterAsync(req.Email, req.Password, req.Role, ct);
                return ok
                    ? Results.Ok(new { message = "Registered. Check email if confirmation is enabled." })
                    : Results.BadRequest(new { error = msg });
            });

            group.MapPost("/login", async ([FromBody] LoginRequest req, ISupabaseAuthService auth, CancellationToken ct) =>
            {
                var (ok, at, rt, user, msg) = await auth.LoginAsync(req.Email, req.Password, ct);
                return ok
                    ? Results.Ok(new { access_token = at, refresh_token = rt, user })
                    : Results.Json(new { error = msg ?? "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            });

            return group;
        }
    }
}
