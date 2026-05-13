namespace DirectorySelfService.Options;

public sealed class RateLimitOptions
{
    public int PermitLimit { get; set; } = 5;
    public int WindowMinutes { get; set; } = 15;
    public int UsernamePermitLimit { get; set; } = 5;
}
