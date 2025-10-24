using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Smart_Roots_Server.Infrastructure.Dtos;

namespace Smart_Roots_Server.Data
{
    public interface ITentRepository
    {
        Task<IReadOnlyList<TentUpsertDto>> GetAllAsync(CancellationToken ct);
        Task<TentUpsertDto?> GetByMacAsync(string mac, CancellationToken ct);
        Task<bool> InsertAsync(TentUpsertDto dto, string? userToken, CancellationToken ct);
        Task<bool> UpdateAsync(string mac, TentUpsertDto dto, string? userToken, CancellationToken ct);
        Task<bool> DeleteAsync(string mac, string? userToken, CancellationToken ct);
    }

    public sealed class TentRepositorySupabase : ITentRepository
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly string _url;
        private readonly string _anonKey;
        private readonly string _serviceRoleKey;

        public TentRepositorySupabase(IHttpClientFactory httpFactory, IConfiguration cfg)
        {
            _httpFactory = httpFactory;
            _url = cfg["SUPABASE:URL"] ?? throw new InvalidOperationException("SUPABASE:URL missing");
            _anonKey = cfg["SUPABASE:KEY"] ?? throw new InvalidOperationException("SUPABASE:KEY missing");
            _serviceRoleKey = cfg["SUPABASE:SERVICE_ROLE_KEY"] ?? ""; // optional if you prefer pass-through user token + RLS
        }

        // ---------- Helpers ----------
        private HttpClient DbClient(string? bearer = null)
        {
            var c = _httpFactory.CreateClient("supabase-db");
            c.BaseAddress = new Uri($"{_url}/rest/v1/");
            c.DefaultRequestHeaders.Add("apikey", _anonKey);

            // Prefer passing the **user's** token so RLS enforces role automatically.
            // If you want the backend to bypass RLS, set SERVICE_ROLE_KEY and use it instead.
            if (!string.IsNullOrWhiteSpace(bearer))
                c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            else if (!string.IsNullOrWhiteSpace(_serviceRoleKey))
                c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);

            return c;
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

        private static TentUpsertDto Map(JsonElement e) => new()
        {
            MacAddress = e.GetProperty("mac_address").GetString()!,
            Name = e.TryGetProperty("name", out var n) ? n.GetString() : null,
            Location = e.TryGetProperty("location", out var l) ? l.GetString() : null,
            Country = e.TryGetProperty("country", out var c) ? c.GetString() : null,
            OrganizationName = e.TryGetProperty("organization_name", out var o) ? o.GetString() : null,
            TentType = e.TryGetProperty("tent_type", out var t) ? t.GetString() : null
        };

        // ---------- CRUD ----------
        public async Task<IReadOnlyList<TentUpsertDto>> GetAllAsync(CancellationToken ct)
        {
            var c = DbClient(); // anon ok
            var res = await c.GetAsync("tents?select=*", ct);
            res.EnsureSuccessStatusCode();
            var txt = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(txt);
            return doc.RootElement.EnumerateArray().Select(Map).ToList();
        }

        public async Task<TentUpsertDto?> GetByMacAsync(string mac, CancellationToken ct)
        {
            var c = DbClient();
            var res = await c.GetAsync($"tents?mac_address=eq.{Uri.EscapeDataString(mac)}&select=*&limit=1", ct);
            res.EnsureSuccessStatusCode();
            var txt = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(txt);
            var arr = doc.RootElement.EnumerateArray();
            if (!arr.Any()) return null;
            return Map(arr.First());
        }

        public async Task<bool> InsertAsync(TentUpsertDto dto, string? userToken, CancellationToken ct)
        {
            var c = DbClient(userToken);
            var payload = new
            {
                mac_address = dto.MacAddress,
                name = dto.Name,
                location = dto.Location,
                country = dto.Country,
                organization_name = dto.OrganizationName,
                tent_type = dto.TentType
            };
            var req = new HttpRequestMessage(HttpMethod.Post, "tents")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Prefer", "return=representation");

            var res = await c.SendAsync(req, ct);
            if (res.IsSuccessStatusCode) return true;
            if ((int)res.StatusCode == 409) return false; // conflict (duplicate mac)
            return false;
        }

        public async Task<bool> UpdateAsync(string mac, TentUpsertDto dto, string? userToken, CancellationToken ct)
        {
            var c = DbClient(userToken);
            var payload = new
            {
                name = dto.Name,
                location = dto.Location,
                country = dto.Country,
                organization_name = dto.OrganizationName,
                tent_type = dto.TentType
            };
            var req = new HttpRequestMessage(HttpMethod.Patch, $"tents?mac_address=eq.{Uri.EscapeDataString(mac)}")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Prefer", "return=representation");

            var res = await c.SendAsync(req, ct);
            if (res.IsSuccessStatusCode) return true;
            if ((int)res.StatusCode == 404) return false;
            return false;
        }

        public async Task<bool> DeleteAsync(string mac, string? userToken, CancellationToken ct)
        {
            var c = DbClient(userToken);
            var res = await c.DeleteAsync($"tents?mac_address=eq.{Uri.EscapeDataString(mac)}", ct);
            if (res.IsSuccessStatusCode) return true;
            if ((int)res.StatusCode == 404) return false;
            return false;
        }
    }
}
