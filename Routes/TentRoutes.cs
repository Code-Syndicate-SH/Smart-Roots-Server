using Microsoft.AspNetCore.Mvc;
using Smart_Roots_Server.Data;
using Smart_Roots_Server.Infrastructure.Dtos;
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
                var item = await repo.GetByMacAsync(macAddress, ct);
                return item is null ? Results.NotFound() : Results.Ok(item);
            });

            // Protected (auth + role)
            group.MapPost("/", async ([FromBody] TentUpsertDto dto, ITentRepository repo, HttpContext ctx, ISupabaseAuthService auth, CancellationToken ct) =>
            {
                var (ok, _, role, status, msg, token) = await RequireRoleAndToken(ctx, auth, AllowedWriteRoles, ct);
                if (!ok)
                    return Results.Json(new { error = msg ?? "Unauthorized" },
                        statusCode: status == 0 ? StatusCodes.Status401Unauthorized : status);

                if (!IsValidType(dto.TentType))
                    return Results.BadRequest(new { error = "tentType must be 'veg' or 'fodder'." });

                var okIns = await repo.InsertAsync(dto, token, ct);
                return okIns
                    ? Results.Created($"/api/tents/{dto.MacAddress}", dto)
                    : Results.Conflict(new { error = "Tent with this MacAddress already exists." });
            });

            group.MapPut("/{macAddress}", async (string macAddress, [FromBody] TentUpsertDto dto, ITentRepository repo, HttpContext ctx, ISupabaseAuthService auth, CancellationToken ct) =>
            {
                var (ok, _, role, status, msg, token) = await RequireRoleAndToken(ctx, auth, AllowedWriteRoles, ct);
                if (!ok)
                    return Results.Json(new { error = msg ?? "Unauthorized" },
                        statusCode: status == 0 ? StatusCodes.Status401Unauthorized : status);

                if (dto.TentType != null && !IsValidType(dto.TentType))
                    return Results.BadRequest(new { error = "tentType must be 'veg' or 'fodder'." });

                var updated = await repo.UpdateAsync(macAddress, dto, token, ct);
                return updated
                    ? Results.Ok(new { message = "Updated." })
                    : Results.NotFound(new { error = "Tent not found." });
            });

            group.MapDelete("/{macAddress}", async (string macAddress, ITentRepository repo, HttpContext ctx, ISupabaseAuthService auth, CancellationToken ct) =>
            {
                var (ok, _, role, status, msg, token) = await RequireRoleAndToken(ctx, auth, AllowedWriteRoles, ct);
                if (!ok)
                    return Results.Json(new { error = msg ?? "Unauthorized" },
                        statusCode: status == 0 ? StatusCodes.Status401Unauthorized : status);

                var deleted = await repo.DeleteAsync(macAddress, token, ct);
                return deleted
                    ? Results.NoContent()
                    : Results.NotFound(new { error = "Tent not found." });
            });

            return group;
        }

        private static bool IsValidType(string? t) =>
            t is null || t.Equals("veg", StringComparison.OrdinalIgnoreCase) || t.Equals("fodder", StringComparison.OrdinalIgnoreCase);

        private static async Task<(bool ok, object? user, string? role, int status, string? message, string? token)>
            RequireRoleAndToken(HttpContext ctx, ISupabaseAuthService auth, HashSet<string> allowed, CancellationToken ct)
        {
            var authz = ctx.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(authz) || !authz.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return (false, null, null, 401, "Missing bearer token", null);

            var token = authz["Bearer ".Length..].Trim();
            var (ok, user, role, status, msg) = await auth.ValidateAsync(token, ct);
            if (!ok) return (false, null, null, status, msg, null);
            if (role is null || !allowed.Contains(role)) return (false, user, role, 403, "Forbidden: insufficient role", token);
            return (true, user, role, 200, null, token);
        }
    }
}
