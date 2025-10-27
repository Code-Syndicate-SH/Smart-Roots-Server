namespace Smart_Roots_Server.Infrastructure.Dtos
{
    public sealed class TentCreateDto
    {
        public string MacAddress { get; set; } = default!;
        public string? Name { get; set; }
        public string? Location { get; set; }
        public string? Country { get; set; }
        public string? OrganizationName { get; set; }
        public string? TentType { get; set; }  // "veg" | "fodder" (optional)
        public string Password { get; set; } = default!; // plaintext in request ONLY (we hash before save)
    }
}
