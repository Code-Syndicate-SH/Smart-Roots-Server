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
        Task<(bool ok, string? role, int status, string? message)>
            ValidateAsync(string bearerToken, CancellationToken ct);
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
            _serviceRoleKey = cfg["SUPABASE:SERVICE_ROLE_KEY"] ?? ""; // optional, used to set app_metadata.role
        }

        public async Task<(bool ok, int status, string? message)> RegisterAsync(string email, string password, string? role, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(role) && role.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase))
                return (false, 400, "Cannot register as admin.");

            var payload = new
            {
                email,
                password,
                // goes to user_metadata by default
                data = role is null ? null : new Dictionary<string, object?> { ["role"] = role }
            };

            var c = CreateAuthClient(_anonKey);
            var res = await c.PostAsync($"{_url}/auth/v1/signup",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);

            var txt = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                // Map "already registered" to 409 Conflict if present
                var status = (int)res.StatusCode;
                if (!string.IsNullOrWhiteSpace(txt) && txt.Contains("already registered", StringComparison.OrdinalIgnoreCase))
                    status = StatusCodes.Status409Conflict;
                return (false, status, string.IsNullOrWhiteSpace(txt) ? res.StatusCode.ToString() : txt);
            }

            // Try to set app_metadata.role (so JWT contains role on next login)
            try
            {
                if (!string.IsNullOrWhiteSpace(role) && !_serviceRoleKey.Equals(""))
                {
                    using var doc = JsonDocument.Parse(txt);
                    var userId = doc.RootElement.GetProperty("user").GetProperty("id").GetString();
                    if (!string.IsNullOrWhiteSpace(userId))
                        await SetAppMetadataRoleAsync(userId!, role!, ct);
                }
            }
            catch { /* best-effort; don’t block registration */ }

            // We will return 201 Created from the endpoint
            return (true, StatusCodes.Status201Created, null);
        }

        public async Task<(bool ok, int status, string? accessToken, string? refreshToken, string? role, string? message)>
            LoginAsync(string email, string password, CancellationToken ct)
        {
            var payload = new { email, password };
            var c = CreateAuthClient(_anonKey);

            var res = await c.PostAsync($"{_url}/auth/v1/token?grant_type=password",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);

            var txt = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) return (false, (int)res.StatusCode, null, null, null, txt);

            using var doc = JsonDocument.Parse(txt);
            string? at = doc.RootElement.TryGetProperty("access_token", out var atEl) ? atEl.GetString() : null;
            string? rt = doc.RootElement.TryGetProperty("refresh_token", out var rtEl) ? rtEl.GetString() : null;

            // derive role from /auth/v1/user using the new access token
            string? role = null;
            if (!string.IsNullOrWhiteSpace(at))
            {
                var (ok, r, _, _) = await ValidateAsync(at, ct);
                if (ok) role = r;
            }

            return (true, 200, at, rt, role, null);
        }

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

        private async Task SetAppMetadataRoleAsync(string userId, string role, CancellationToken ct)
        {
            var c = CreateAuthClient(_serviceRoleKey); // admin endpoint requires service role
            var body = new
            {
                app_metadata = new { role }
            };
            var req = new HttpRequestMessage(HttpMethod.Put, $"{_url}/auth/v1/admin/users/{userId}")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            await c.SendAsync(req, ct); // if it fails, we ignore; role still present in user_metadata
        }

        private HttpClient CreateAuthClient(string bearer)
        {
            var c = _httpFactory.CreateClient("supabase-auth");
            c.DefaultRequestHeaders.Add("apikey", _anonKey);
            if (!string.IsNullOrWhiteSpace(bearer))
                c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            return c;
        }
    }
}
