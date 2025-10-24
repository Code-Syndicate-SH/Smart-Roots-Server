using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Smart_Roots_Server.Services
{
    public interface ISupabaseAuthService
    {
        Task<(bool ok, string? message)> RegisterAsync(string email, string password, string? role, CancellationToken ct);
        Task<(bool ok, string? accessToken, string? refreshToken, object? user, string? message)>
            LoginAsync(string email, string password, CancellationToken ct);
        Task<(bool ok, object? user, string? role, int status, string? message)>
            ValidateAsync(string bearerToken, CancellationToken ct);
    }

    public sealed class SupabaseAuthService : ISupabaseAuthService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly string _url;
        private readonly string _anonKey;

        public SupabaseAuthService(IHttpClientFactory httpFactory, IConfiguration cfg)
        {
            _httpFactory = httpFactory;
            _url = cfg["SUPABASE:URL"] ?? throw new InvalidOperationException("SUPABASE:URL missing");
            _anonKey = cfg["SUPABASE:KEY"] ?? throw new InvalidOperationException("SUPABASE:KEY missing");
        }

        public async Task<(bool ok, string? message)> RegisterAsync(string email, string password, string? role, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(role) && role.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase))
                return (false, "Cannot register as admin.");

            var payload = new
            {
                email,
                password,
                data = role is null ? null : new Dictionary<string, object?> { ["role"] = role }
            };

            var c = CreateAuthClient();
            var res = await c.PostAsync($"{_url}/auth/v1/signup",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);

            if (res.IsSuccessStatusCode) return (true, null);
            var text = await res.Content.ReadAsStringAsync(ct);
            return (false, string.IsNullOrWhiteSpace(text) ? res.StatusCode.ToString() : text);
        }

        public async Task<(bool ok, string? accessToken, string? refreshToken, object? user, string? message)>
            LoginAsync(string email, string password, CancellationToken ct)
        {
            var payload = new { email, password };
            var c = CreateAuthClient();

            var res = await c.PostAsync($"{_url}/auth/v1/token?grant_type=password",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);

            var txt = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) return (false, null, null, null, txt);

            using var doc = JsonDocument.Parse(txt);
            string? at = doc.RootElement.TryGetProperty("access_token", out var atEl) ? atEl.GetString() : null;
            string? rt = doc.RootElement.TryGetProperty("refresh_token", out var rtEl) ? rtEl.GetString() : null;
            object? user = doc.RootElement.TryGetProperty("user", out var uEl) ? JsonSerializer.Deserialize<object>(uEl.GetRawText()) : null;

            return (true, at, rt, user, null);
        }

        public async Task<(bool ok, object? user, string? role, int status, string? message)>
            ValidateAsync(string bearerToken, CancellationToken ct)
        {
            var c = CreateAuthClient();
            var req = new HttpRequestMessage(HttpMethod.Get, $"{_url}/auth/v1/user");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            var res = await c.SendAsync(req, ct);
            var txt = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode) return (false, null, null, (int)res.StatusCode, txt);

            using var doc = JsonDocument.Parse(txt);
            var userObj = JsonSerializer.Deserialize<object>(doc.RootElement.GetRawText());
            string? role = null;

            if (doc.RootElement.TryGetProperty("app_metadata", out var appMeta) &&
                appMeta.TryGetProperty("role", out var roleEl) && roleEl.ValueKind == JsonValueKind.String)
                role = roleEl.GetString();

            if (role is null &&
                doc.RootElement.TryGetProperty("user_metadata", out var userMeta) &&
                userMeta.TryGetProperty("role", out var roleEl2) && roleEl2.ValueKind == JsonValueKind.String)
                role = roleEl2.GetString();

            role ??= "user";
            return (true, userObj, role, 200, null);
        }

        private HttpClient CreateAuthClient()
        {
            var c = _httpFactory.CreateClient("supabase-auth");
            c.DefaultRequestHeaders.Add("apikey", _anonKey);
            return c;
        }
    }
}
