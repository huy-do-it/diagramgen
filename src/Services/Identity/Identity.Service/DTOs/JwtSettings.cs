namespace Identity.Service.DTOs;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = "umlforge-super-secret-key-for-development-2026-min-32-chars!";
    public string Issuer { get; set; } = "umlforge";
    public string Audience { get; set; } = "umlforge-client";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
