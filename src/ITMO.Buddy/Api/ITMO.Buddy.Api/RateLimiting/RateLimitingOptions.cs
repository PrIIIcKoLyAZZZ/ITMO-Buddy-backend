namespace ITMO.Buddy.Api.RateLimiting;

internal sealed class RateLimitingOptions
{
    public int LoginFailureCount { get; set; } = 5;
    public TimeSpan LoginFailureWindow { get; set; } = TimeSpan.FromMinutes(10);
    public int MaxTrackedLoginIps { get; set; } = 10000;
    public RequestLimit Login { get; set; } = new(60, TimeSpan.FromMinutes(1));
    public RequestLimit Register { get; set; } = new(12, TimeSpan.FromMinutes(10));
    public RequestLimit Refresh { get; set; } = new(60, TimeSpan.FromMinutes(1));
    public RequestLimit Tags { get; set; } = new(30, TimeSpan.FromMinutes(1));
    public RequestLimit Photo { get; set; } = new(10, TimeSpan.FromMinutes(1));
    public RequestLimit Messages { get; set; } = new(120, TimeSpan.FromMinutes(1));

    public void Validate(bool development)
    {
        var minimumFailureWindow = development ? TimeSpan.FromSeconds(1) : TimeSpan.FromMinutes(10);
        if (LoginFailureCount is < 1 or > 5 ||
            LoginFailureWindow < minimumFailureWindow || LoginFailureWindow > TimeSpan.FromHours(1) ||
            MaxTrackedLoginIps is < 1 or > 100000 ||
            new[] { Login, Register, Refresh, Tags, Photo, Messages }.Any(limit =>
                limit.PermitLimit is < 1 or > 1000 ||
                limit.Window < TimeSpan.FromSeconds(1) || limit.Window > TimeSpan.FromHours(1)))
        {
            throw new InvalidOperationException("The HTTP rate limiting configuration is outside the permitted range.");
        }
    }
}

internal sealed class RequestLimit(int permitLimit, TimeSpan window)
{
    public int PermitLimit { get; set; } = permitLimit;
    public TimeSpan Window { get; set; } = window;
}
