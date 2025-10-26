using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Smart_Roots_Server.Infrastructure.Dtos;
using Smart_Roots_Server.Services;

namespace Smart_Roots_Server.Routes
{
    public static class AuthRoutes
    {
        public static RouteGroupBuilder MapAuthApis(this RouteGroupBuilder group)
        {
            group.MapPost("/register", async (
                [FromBody] RegisterRequest req,
                IValidator<RegisterRequest> validator,
                ISupabaseAuthService auth,
                CancellationToken ct) =>
            {
                var v = await validator.ValidateAsync(req, ct);
                if (!v.IsValid)
                    return Results.Json(new { errors = v.Errors.Select(e => e.ErrorMessage) }, statusCode: StatusCodes.Status400BadRequest);

                var (ok, status, msg) = await auth.RegisterAsync(req.Email.Trim(), req.Password, req.Role?.Trim(), ct);
                if (!ok)
                    return Results.Json(new { error = msg ?? "Registration failed." }, statusCode: status);

                // Manager request: 201 on success
                return Results.StatusCode(StatusCodes.Status201Created);
            });

            group.MapPost("/login", async (
                [FromBody] LoginRequest req,
                IValidator<LoginRequest> validator,
                ISupabaseAuthService auth,
                CancellationToken ct) =>
            {
                var v = await validator.ValidateAsync(req, ct);
                if (!v.IsValid)
                    return Results.Json(new { errors = v.Errors.Select(e => e.ErrorMessage) }, statusCode: StatusCodes.Status400BadRequest);

                var (ok, status, at, rt, role, msg) = await auth.LoginAsync(req.Email.Trim(), req.Password, ct);
                return ok
                    ? Results.Ok(new { access_token = at, refresh_token = rt, role })
                    : Results.Json(new { error = msg ?? "Unauthorized" }, statusCode: status);
            });

            return group;
        }
    }
}
