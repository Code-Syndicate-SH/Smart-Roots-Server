namespace Smart_Roots_Server.Infrastructure.Dtos
{
    // Public payload for GETs (never includes password or sensitive fields)
    public sealed class TentPublicDto
    {
        public string MacAddress { get; set; } = default!;
        public string? Name { get; set; }
        public string? Location { get; set; }
        public string Country { get; set; }
        public string? TentType { get; set; }
        public string? OrganizationName { get; set; }

    }
}
