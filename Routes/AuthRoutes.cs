using System.Text;
using System.Text.Json;
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
            // ------------------- REGISTER -------------------
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

                // 201 Created on success (per manager request)
                return Results.StatusCode(StatusCodes.Status201Created);
            });

            // ------------------- LOGIN (HttpOnly cookies) -------------------
            group.MapPost("/login", async (
                [FromBody] LoginRequest req,
                IValidator<LoginRequest> validator,
                ISupabaseAuthService auth,
                HttpContext ctx,
                IConfiguration cfg,
                CancellationToken ct) =>
            {
                var v = await validator.ValidateAsync(req, ct);
                if (!v.IsValid)
                    return Results.Json(new { errors = v.Errors.Select(e => e.ErrorMessage) }, statusCode: StatusCodes.Status400BadRequest);

                var (ok, status, at, rt, role, msg) = await auth.LoginAsync(req.Email.Trim(), req.Password, ct);
                if (!ok)
                    return Results.Json(new { error = msg ?? "Unauthorized" }, statusCode: status);

                SetAuthCookies(ctx, cfg, at!, rt!);

                // Return ONLY role (no tokens in body)
                return Results.Ok(new { role });
            });

            // ------------------- WHO AM I (reads header OR cookie) -------------------
            group.MapGet("/me", async (HttpContext ctx, ISupabaseAuthService auth, IConfiguration cfg, CancellationToken ct) =>
            {
                var token = ReadAccessToken(ctx, cfg);
                if (string.IsNullOrWhiteSpace(token))
                    return Results.Json(new { error = "Missing token" }, statusCode: StatusCodes.Status401Unauthorized);

                var (ok, role, status, msg) = await auth.ValidateAsync(token!, ct);
                return ok
                    ? Results.Ok(new { role })
                    : Results.Json(new { error = msg ?? "Unauthorized" }, statusCode: status);
            });

            // ------------------- LOGOUT (clear cookies) -------------------
            group.MapPost("/logout", (HttpContext ctx, IConfiguration cfg) =>
            {
                ClearAuthCookies(ctx, cfg);
                return Results.NoContent();
            });

            return group;
        }

        // ===================== Helpers =====================

        private static void SetAuthCookies(HttpContext ctx, IConfiguration cfg, string accessToken, string refreshToken)
        {
            var accessCookieName = cfg["AUTH:AccessCookieName"] ?? "sb:access_token";
            var refreshCookieName = cfg["AUTH:RefreshCookieName"] ?? "sb:refresh_token";

            var secure = cfg.GetValue("AUTH:SecureCookies", true);
            var sameSite = ParseSameSite(cfg["AUTH:SameSite"] ?? "None");
            var domain = cfg["AUTH:CookieDomain"];

            var accessExp = ExpFromJwt(accessToken) ?? DateTimeOffset.UtcNow.AddMinutes(15);
            var refreshExp = DateTimeOffset.UtcNow.AddDays(7);

            var accessOpts = new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = sameSite,
                Path = "/",
                Expires = accessExp
            };
            var refreshOpts = new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = sameSite,
                Path = "/",
                Expires = refreshExp
            };

            if (!string.IsNullOrWhiteSpace(domain))
            {
                accessOpts.Domain = domain;
                refreshOpts.Domain = domain;
            }

            ctx.Response.Cookies.Append(accessCookieName, accessToken, accessOpts);
            ctx.Response.Cookies.Append(refreshCookieName, refreshToken, refreshOpts);
        }

        private static void ClearAuthCookies(HttpContext ctx, IConfiguration cfg)
        {
            var accessCookieName = cfg["AUTH:AccessCookieName"] ?? "sb:access_token";
            var refreshCookieName = cfg["AUTH:RefreshCookieName"] ?? "sb:refresh_token";

            var secure = cfg.GetValue("AUTH:SecureCookies", true);
            var sameSite = ParseSameSite(cfg["AUTH:SameSite"] ?? "None");
            var domain = cfg["AUTH:CookieDomain"];

            var expiredOpts = new CookieOptions
            {
                Expires = DateTimeOffset.UnixEpoch,
                HttpOnly = true,
                Secure = secure,
                SameSite = sameSite,
                Path = "/"
            };
            if (!string.IsNullOrWhiteSpace(domain))
                expiredOpts.Domain = domain;

            ctx.Response.Cookies.Append(accessCookieName, "", expiredOpts);
            ctx.Response.Cookies.Append(refreshCookieName, "", expiredOpts);
        }


        private static string? ReadAccessToken(HttpContext ctx, IConfiguration cfg)
        {
            // Prefer Authorization header; fallback to cookie
            var authz = ctx.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authz) && authz.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return authz["Bearer ".Length..].Trim();

            var cookieName = cfg["AUTH:AccessCookieName"] ?? "sb:access_token";
            return ctx.Request.Cookies.TryGetValue(cookieName, out var token) ? token : null;
        }

        private static SameSiteMode ParseSameSite(string v) =>
            v.Equals("none", StringComparison.OrdinalIgnoreCase) ? SameSiteMode.None :
            v.Equals("lax", StringComparison.OrdinalIgnoreCase) ? SameSiteMode.Lax :
            v.Equals("strict", StringComparison.OrdinalIgnoreCase) ? SameSiteMode.Strict : SameSiteMode.None;

        private static DateTimeOffset? ExpFromJwt(string? jwt)
        {
            if (string.IsNullOrWhiteSpace(jwt)) return null;
            try
            {
                var parts = jwt.Split('.');
                if (parts.Length < 2) return null;
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4) { case 2: payload += "=="; break; case 3: payload += "="; break; }
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var unix))
                    return DateTimeOffset.FromUnixTimeSeconds(unix);
            }
            catch { /* ignore */ }
            return null;
        }
    }
}
