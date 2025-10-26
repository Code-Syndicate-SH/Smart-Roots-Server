using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Smart_Roots_Server.Data;
using Smart_Roots_Server.Infrastructure.Dtos;
using Smart_Roots_Server.Infrastructure.Validation;
using Smart_Roots_Server.Services;

namespace Smart_Roots_Server.Routes
{
    public static class TentRoutes
    {
        private static readonly HashSet<string> AllowedWriteRoles = new(StringComparer.OrdinalIgnoreCase)
        { "admin", "researcher" };

        public static RouteGroupBuilder MapTentApis(this RouteGroupBuilder group)
        {
            // Public
            group.MapGet("/", async (ITentRepository repo, CancellationToken ct) =>
            {
                var list = await repo.GetAllAsync(ct);
                return Results.Ok(list);
            });

            group.MapGet("/{macAddress}", async (string macAddress, ITentRepository repo, CancellationToken ct) =>
            {
                var norm = TentUpsertDtoValidator.NormalizeMac(macAddress);
                var item = await repo.GetByMacAsync(norm, ct);
                return item is null ? Results.NotFound() : Results.Ok(item);
            });

            // Protected (auth + role)
            group.MapPost("/", async (
                [FromBody] TentUpsertDto dto,
                IValidator<TentUpsertDto> validator,
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

                dto.MacAddress = TentUpsertDtoValidator.NormalizeMac(dto.MacAddress);
                var okIns = await repo.InsertAsync(dto, authz.Token, ct);
                return okIns
                    ? Results.Created($"/api/tents/{dto.MacAddress}", dto)
                    : Results.Conflict(new { error = "Tent with this MacAddress already exists." });
            });

            group.MapPut("/{macAddress}", async (
                string macAddress,
                [FromBody] TentUpsertDto dto,
                IValidator<TentUpsertDto> validator,
                ITentRepository repo,
                HttpContext ctx,
                ISupabaseAuthService auth,
                CancellationToken ct) =>
            {
                // Allow partial validation for fields that might be provided
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

                var norm = TentUpsertDtoValidator.NormalizeMac(macAddress);
                var updated = await repo.UpdateAsync(norm, dto, authz.Token, ct);
                return updated
                    ? Results.Ok(new { message = "Updated." })
                    : Results.NotFound(new { error = "Tent not found." });
            });

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

                var norm = TentUpsertDtoValidator.NormalizeMac(macAddress);
                var deleted = await repo.DeleteAsync(norm, authz.Token, ct);
                return deleted
                    ? Results.NoContent()
                    : Results.NotFound(new { error = "Tent not found." });
            });

            return group;
        }

        private static async Task<AuthzResult> RequireRoleAndToken(
            HttpContext ctx,
            ISupabaseAuthService auth,
            HashSet<string> allowed,
            CancellationToken ct)
        {
            var authz = ctx.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(authz) || !authz.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return new AuthzResult { Ok = false, Status = StatusCodes.Status401Unauthorized, Error = "Missing bearer token" };

            var token = authz["Bearer ".Length..].Trim();
            var (ok, role, status, msg) = await auth.ValidateAsync(token, ct);
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
