using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Smart_Roots_Server.Services
{
    public interface ISupabaseAuthService
    {
        Task<(bool ok, int status, string? message)> RegisterAsync(string email, string password, string? role, CancellationToken ct);
        Task<(bool ok, int status, string? accessToken, string? refreshToken, string? role, string? message)>
            LoginAsync(string email, string password, CancellationToken ct);
        Task<(bool ok, string? role, int status, string? message)> ValidateAsync(string bearerToken, CancellationToken ct);

        // NEW: for cookie-based flows
        Task<(bool ok, int status, string? accessToken, string? refreshToken, string? role, string? message)>
            RefreshAsync(string refreshToken, CancellationToken ct);
    }

    public sealed class SupabaseAuthService : ISupabaseAuthService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly string _url;
        private readonly string _anonKey;
        private readonly string _serviceRoleKey;

        public SupabaseAuthService(IHttpClientFactory httpFactory, IConfiguration cfg)
        {
            _httpFactory = httpFactory;
            _url = cfg["SUPABASE:URL"] ?? throw new InvalidOperationException("SUPABASE:URL missing");
            _anonKey = cfg["SUPABASE:KEY"] ?? throw new InvalidOperationException("SUPABASE:KEY missing");
            _serviceRoleKey = cfg["SUPABASE:SERVICE_ROLE_KEY"] ?? ""; // optional; enables admin endpoints
        }

        /* --------------------- Register --------------------- */
        public async Task<(bool ok, int status, string? message)> RegisterAsync(string email, string password, string? role, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(role) && role.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase))
                return (false, StatusCodes.Status400BadRequest, "Cannot register as admin.");

            email = email.Trim();

            // Pre-check via Admin API (if SRK present)
            if (!string.IsNullOrWhiteSpace(_serviceRoleKey))
            {
                var existingId = await FindUserIdByEmailAsync(email, ct);
                if (!string.IsNullOrWhiteSpace(existingId))
                    return (false, StatusCodes.Status409Conflict, "User already exists.");
            }

            // Prefer Admin Create (confirmed + app_metadata.role)
            if (!string.IsNullOrWhiteSpace(_serviceRoleKey))
            {
                var body = new
                {
                    email,
                    password,
                    email_confirm = true,
                    app_metadata = role is null ? null : new { role },
                    user_metadata = role is null ? null : new { role }
                };

                var c = CreateAuthClient(_serviceRoleKey);
                var res = await c.PostAsync($"{_url}/auth/v1/admin/users",
                    new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

                var txt = await res.Content.ReadAsStringAsync(ct);
                if (res.IsSuccessStatusCode) return (true, StatusCodes.Status201Created, null);

                var status = (int)res.StatusCode;
                if (!string.IsNullOrWhiteSpace(txt) && txt.Contains("already registered", StringComparison.OrdinalIgnoreCase))
                    status = StatusCodes.Status409Conflict;

                return (false, status, string.IsNullOrWhiteSpace(txt) ? res.StatusCode.ToString() : txt);
            }

            // Fallback: public signup
            var payload = new { email, password, data = role is null ? null : new Dictionary<string, object?> { ["role"] = role } };
            var c2 = CreateAuthClient(_anonKey);
            var res2 = await c2.PostAsync($"{_url}/auth/v1/signup",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);

            var txt2 = await res2.Content.ReadAsStringAsync(ct);
            if (!res2.IsSuccessStatusCode)
            {
                var status = (int)res2.StatusCode;
                if (!string.IsNullOrWhiteSpace(txt2) && txt2.Contains("already registered", StringComparison.OrdinalIgnoreCase))
                    status = StatusCodes.Status409Conflict;
                return (false, status, string.IsNullOrWhiteSpace(txt2) ? res2.StatusCode.ToString() : txt2);
            }

            // Best-effort: set role post-signup if SRK available
            try
            {
                if (!string.IsNullOrWhiteSpace(role) && !string.IsNullOrWhiteSpace(_serviceRoleKey))
                {
                    using var doc = JsonDocument.Parse(txt2);
                    var userId = doc.RootElement.GetProperty("user").GetProperty("id").GetString();
                    if (!string.IsNullOrWhiteSpace(userId))
                        await SetAppMetadataRoleAsync(userId!, role!, ct);
                }
            }
            catch { /* ignore */ }

            return (true, StatusCodes.Status201Created, null);
        }

        /* ---------------------- Login ---------------------- */
        public async Task<(bool ok, int status, string? accessToken, string? refreshToken, string? role, string? message)>
            LoginAsync(string email, string password, CancellationToken ct)
        {
            var payload = new { email, password };
            var c = CreateAuthClient(_anonKey);

            var res = await c.PostAsync($"{_url}/auth/v1/token?grant_type=password",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);

            var txt = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                string? msg = null;
                string? errCode = null;
                try
                {
                    using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(txt) ? "{}" : txt);
                    if (doc.RootElement.TryGetProperty("msg", out var m) && m.ValueKind == JsonValueKind.String)
                        msg = m.GetString();
                    if (doc.RootElement.TryGetProperty("error_description", out var ed) && ed.ValueKind == JsonValueKind.String)
                        msg = ed.GetString() ?? msg;
                    if (doc.RootElement.TryGetProperty("error_code", out var ec) && ec.ValueKind == JsonValueKind.String)
                        errCode = ec.GetString();
                    if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                        errCode ??= e.GetString();
                }
                catch { /* ignore */ }

                var lower = (msg ?? txt ?? string.Empty).ToLowerInvariant();
                var ecode = (errCode ?? string.Empty).ToLowerInvariant();

                if (ecode.Contains("invalid_credentials") || ecode.Contains("invalid_grant") || lower.Contains("invalid login credentials"))
                    return (false, StatusCodes.Status401Unauthorized, null, null, null, msg ?? "Invalid credentials.");

                if (lower.Contains("confirm") || lower.Contains("not confirmed"))
                    return (false, StatusCodes.Status403Forbidden, null, null, null, msg ?? "Email not confirmed.");

                return (false, (int)res.StatusCode, null, null, null, string.IsNullOrWhiteSpace(msg) ? txt : msg);
            }

            using var okDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(txt) ? "{}" : txt);
            string? at = okDoc.RootElement.TryGetProperty("access_token", out var atEl) ? atEl.GetString() : null;
            string? rt = okDoc.RootElement.TryGetProperty("refresh_token", out var rtEl) ? rtEl.GetString() : null;

            // Pull role for downstream authZ
            string? role = null;
            if (!string.IsNullOrWhiteSpace(at))
            {
                var (vOk, vRole, _, _) = await ValidateAsync(at, ct);
                if (vOk) role = vRole;
            }

            return (true, 200, at, rt, role, null);
        }

        /* --------------------- Validate --------------------- */
        public async Task<(bool ok, string? role, int status, string? message)> ValidateAsync(string bearerToken, CancellationToken ct)
        {
            var c = CreateAuthClient(_anonKey);
            var req = new HttpRequestMessage(HttpMethod.Get, $"{_url}/auth/v1/user");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            var res = await c.SendAsync(req, ct);
            var txt = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode) return (false, null, (int)res.StatusCode, txt);

            using var doc = JsonDocument.Parse(txt);
            string? role = null;

            if (doc.RootElement.TryGetProperty("app_metadata", out var appMeta) &&
                appMeta.TryGetProperty("role", out var roleEl) && roleEl.ValueKind == JsonValueKind.String)
                role = roleEl.GetString();

            if (role is null &&
                doc.RootElement.TryGetProperty("user_metadata", out var userMeta) &&
                userMeta.TryGetProperty("role", out var roleEl2) && roleEl2.ValueKind == JsonValueKind.String)
                role = roleEl2.GetString();

            role ??= "user";
            return (true, role, 200, null);
        }

        /* ---------------------- Refresh --------------------- */
        public async Task<(bool ok, int status, string? accessToken, string? refreshToken, string? role, string? message)>
            RefreshAsync(string refreshToken, CancellationToken ct)
        {
            var c = CreateAuthClient(_anonKey);
            var body = new { refresh_token = refreshToken };

            var res = await c.PostAsync($"{_url}/auth/v1/token?grant_type=refresh_token",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

            var txt = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return (false, (int)res.StatusCode, null, null, null, string.IsNullOrWhiteSpace(txt) ? "Refresh failed" : txt);

            using var okDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(txt) ? "{}" : txt);
            string? at = okDoc.RootElement.TryGetProperty("access_token", out var atEl) ? atEl.GetString() : null;
            string? rt = okDoc.RootElement.TryGetProperty("refresh_token", out var rtEl) ? rtEl.GetString() : null;

            string? role = null;
            if (!string.IsNullOrWhiteSpace(at))
            {
                var (vOk, vRole, _, _) = await ValidateAsync(at, ct);
                if (vOk) role = vRole;
            }

            return (true, 200, at, rt, role, null);
        }

        /* -------------------- Helpers ---------------------- */
        private HttpClient CreateAuthClient(string bearer)
        {
            var c = _httpFactory.CreateClient("supabase-auth");
            c.DefaultRequestHeaders.Add("apikey", _anonKey);
            if (!string.IsNullOrWhiteSpace(bearer))
                c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            return c;
        }

        private async Task<string?> FindUserIdByEmailAsync(string email, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_serviceRoleKey)) return null;

            var c = CreateAuthClient(_serviceRoleKey);

            // Try email filter
            try
            {
                var res = await c.GetAsync($"{_url}/auth/v1/admin/users?email={Uri.EscapeDataString(email)}", ct);
                var txt = await res.Content.ReadAsStringAsync(ct);
                if (res.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(txt))
                {
                    using var doc = JsonDocument.Parse(txt);

                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("email", out var e) &&
                            e.ValueKind == JsonValueKind.String &&
                            string.Equals(e.GetString(), email, StringComparison.OrdinalIgnoreCase) &&
                            doc.RootElement.TryGetProperty("id", out var idObj) &&
                            idObj.ValueKind == JsonValueKind.String)
                            return idObj.GetString();
                    }

                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            if (el.TryGetProperty("email", out var e) &&
                                e.ValueKind == JsonValueKind.String &&
                                string.Equals(e.GetString(), email, StringComparison.OrdinalIgnoreCase) &&
                                el.TryGetProperty("id", out var idEl) &&
                                idEl.ValueKind == JsonValueKind.String)
                                return idEl.GetString();
                        }
                    }

                    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                        doc.RootElement.TryGetProperty("users", out var usersEl) &&
                        usersEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in usersEl.EnumerateArray())
                        {
                            if (el.TryGetProperty("email", out var e) &&
                                e.ValueKind == JsonValueKind.String &&
                                string.Equals(e.GetString(), email, StringComparison.OrdinalIgnoreCase) &&
                                el.TryGetProperty("id", out var idEl) &&
                                idEl.ValueKind == JsonValueKind.String)
                                return idEl.GetString();
                        }
                    }
                }
            }
            catch { /* fall through */ }

            // Paginated scan fallback
            const int PageSize = 200;
            for (int page = 1; page <= 5; page++)
            {
                var res = await c.GetAsync($"{_url}/auth/v1/admin/users?per_page={PageSize}&page={page}", ct);
                var txt = await res.Content.ReadAsStringAsync(ct);
                if (!res.IsSuccessStatusCode || string.IsNullOrWhiteSpace(txt)) break;

                try
                {
                    using var doc = JsonDocument.Parse(txt);

                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            if (el.TryGetProperty("email", out var e) &&
                                e.ValueKind == JsonValueKind.String &&
                                string.Equals(e.GetString(), email, StringComparison.OrdinalIgnoreCase) &&
                                el.TryGetProperty("id", out var idEl) &&
                                idEl.ValueKind == JsonValueKind.String)
                                return idEl.GetString();
                        }
                        if (doc.RootElement.GetArrayLength() < PageSize) break;
                    }

                    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                        doc.RootElement.TryGetProperty("users", out var usersEl) &&
                        usersEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in usersEl.EnumerateArray())
                        {
                            if (el.TryGetProperty("email", out var e) &&
                                e.ValueKind == JsonValueKind.String &&
                                string.Equals(e.GetString(), email, StringComparison.OrdinalIgnoreCase) &&
                                el.TryGetProperty("id", out var idEl) &&
                                idEl.ValueKind == JsonValueKind.String)
                                return idEl.GetString();
                        }
                        if (usersEl.GetArrayLength() < PageSize) break;
                    }
                }
                catch { break; }
            }

            return null;
        }

        private async Task SetAppMetadataRoleAsync(string userId, string role, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_serviceRoleKey)) return;

            var c = CreateAuthClient(_serviceRoleKey);
            var body = new { app_metadata = new { role } };
            var req = new HttpRequestMessage(HttpMethod.Put, $"{_url}/auth/v1/admin/users/{userId}")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            await c.SendAsync(req, ct); // best-effort
        }
    }
}
