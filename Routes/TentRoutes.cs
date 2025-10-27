using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Smart_Roots_Server.Data;
using Smart_Roots_Server.Infrastructure.Dtos;
using Smart_Roots_Server.Infrastructure.Validation;
using Smart_Roots_Server.Infrastructure.Security; // PasswordHasher
using Smart_Roots_Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Smart_Roots_Server.Routes
{
    public static class TentRoutes
    {
        private static readonly HashSet<string> AllowedWriteRoles = new(StringComparer.OrdinalIgnoreCase)
        { "admin", "researcher" };

        public static RouteGroupBuilder MapTentApis(this RouteGroupBuilder group)
        {
            // PUBLIC GETS (no password)
            group.MapGet("/", async (ITentRepository repo, CancellationToken ct) =>
            {
                var list = await repo.GetAllPublicAsync(ct);
                return Results.Ok(list);
            });

            group.MapGet("/{macAddress}", async (string macAddress, ITentRepository repo, CancellationToken ct) =>
            {
                var norm = TentCreateDtoValidator.NormalizeMac(macAddress);
                var item = await repo.GetPublicByMacAsync(norm, ct);
                return item is null ? Results.NotFound() : Results.Ok(item);
            });

            // PROTECTED CREATE (requires password; hash before save)
            group.MapPost("/", async (
                [FromBody] TentCreateDto dto,
                IValidator<TentCreateDto> validator,
                ITentRepository repo,
                HttpContext ctx,
                ISupabaseAuthService auth,
                CancellationToken ct) =>
            {
                var v = await validator.ValidateAsync(dto, ct);
                if (!v.IsValid)
                    return Results.Json(new { errors = v.Errors.Select(e => e.ErrorMessage) }, statusCode: StatusCodes.Status400BadRequest);

                var authz = await RequireRoleAndToken(ctx, auth, AllowedWriteRoles, ct);
                if (!authz.Ok)
                    return Results.Json(new { error = authz.Error ?? "Unauthorized" }, statusCode: authz.Status);

                dto.MacAddress = TentCreateDtoValidator.NormalizeMac(dto.MacAddress);

                var passwordHash = PasswordHasher.Hash(dto.Password);
                var okIns = await repo.InsertAsync(dto, passwordHash, authz.Token, ct);
                if (!okIns)
                    return Results.Conflict(new { error = "Tent with this MacAddress already exists." });

                return Results.Created($"/api/tents/{dto.MacAddress}", new TentPublicDto
                {
                    MacAddress = dto.MacAddress,
                    Name = dto.Name,
                    Location = dto.Location
                });
            });

            // PROTECTED UPDATE
            group.MapPut("/{macAddress}", async (
                string macAddress,
                [FromBody] TentUpsertDto dto,
                IValidator<TentUpsertDto> validator,
                ITentRepository repo,
                HttpContext ctx,
                ISupabaseAuthService auth,
                CancellationToken ct) =>
            {
                if (!string.IsNullOrWhiteSpace(dto.MacAddress))
                {
                    var v2 = await validator.ValidateAsync(dto, opts => opts.IncludeProperties(x => x.MacAddress, x => x.TentType), ct);
                    if (!v2.IsValid)
                        return Results.Json(new { errors = v2.Errors.Select(e => e.ErrorMessage) }, statusCode: StatusCodes.Status400BadRequest);
                }
                if (dto.TentType is { Length: > 0 } &&
                    !(dto.TentType.Equals("veg", StringComparison.OrdinalIgnoreCase) || dto.TentType.Equals("fodder", StringComparison.OrdinalIgnoreCase)))
                    return Results.BadRequest(new { error = "tentType must be 'veg' or 'fodder'." });

                var authz = await RequireRoleAndToken(ctx, auth, AllowedWriteRoles, ct);
                if (!authz.Ok)
                    return Results.Json(new { error = authz.Error ?? "Unauthorized" }, statusCode: authz.Status);

                var norm = TentCreateDtoValidator.NormalizeMac(macAddress);
                var updated = await repo.UpdateAsync(norm, dto, authz.Token, ct);
                return updated
                    ? Results.Ok(new { message = "Updated." })
                    : Results.NotFound(new { error = "Tent not found." });
            });

            // PROTECTED DELETE
            group.MapDelete("/{macAddress}", async (
                string macAddress,
                ITentRepository repo,
                HttpContext ctx,
                ISupabaseAuthService auth,
                CancellationToken ct) =>
            {
                var authz = await RequireRoleAndToken(ctx, auth, AllowedWriteRoles, ct);
                if (!authz.Ok)
                    return Results.Json(new { error = authz.Error ?? "Unauthorized" }, statusCode: authz.Status);

                var norm = TentCreateDtoValidator.NormalizeMac(macAddress);
                var deleted = await repo.DeleteAsync(norm, authz.Token, ct);
                return deleted ? Results.NoContent() : Results.NotFound(new { error = "Tent not found." });
            });

            // PUBLIC VERIFY PASSWORD
            group.MapPost("/verify-password", async (
                [FromBody] TentVerifyDto body,
                ITentRepository repo,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(body.MacAddress) || string.IsNullOrWhiteSpace(body.Password))
                    return Results.BadRequest(new { error = "macAddress and password are required." });

                var norm = TentCreateDtoValidator.NormalizeMac(body.MacAddress);

                var hash = await repo.GetPasswordHashByMacAsync(norm, userToken: null, ct);
                if (string.IsNullOrEmpty(hash))
                    return Results.Ok(new { valid = false });

                var ok = PasswordHasher.Verify(body.Password, hash);
                return Results.Ok(new { valid = ok });
            });

            return group;
        }

        private static async Task<AuthzResult> RequireRoleAndToken(
            HttpContext ctx,
            ISupabaseAuthService auth,
            HashSet<string> allowed,
            CancellationToken ct)
        {
            // 1) Authorization header (Bearer)
            string? token = null;
            var authzHeader = ctx.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authzHeader) &&
                authzHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authzHeader["Bearer ".Length..].Trim();
            }

            // 2) Fallback to HttpOnly cookie
            if (string.IsNullOrWhiteSpace(token))
            {
                var cfg = ctx.RequestServices.GetRequiredService<IConfiguration>();
                var cookieName = cfg["AUTH:AccessCookieName"] ?? "sb_access_token"; // must be colon-free
                ctx.Request.Cookies.TryGetValue(cookieName, out token);
            }

            if (string.IsNullOrWhiteSpace(token))
                return new AuthzResult { Ok = false, Status = StatusCodes.Status401Unauthorized, Error = "Missing token" };

            var (ok, role, status, msg) = await auth.ValidateAsync(token!, ct);
            if (!ok) return new AuthzResult { Ok = false, Status = status, Error = msg };
            if (role is null || !allowed.Contains(role))
                return new AuthzResult { Ok = false, Status = StatusCodes.Status403Forbidden, Role = role, Token = token, Error = "Forbidden: insufficient role" };

            return new AuthzResult { Ok = true, Status = 200, Role = role, Token = token };
        }

        private sealed class AuthzResult
        {
            public bool Ok { get; init; }
            public int Status { get; init; }
            public string? Role { get; init; }
            public string? Token { get; init; }
            public string? Error { get; init; }
        }
    }
}
