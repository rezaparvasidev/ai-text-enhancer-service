namespace TextEnhancer.Api.Middleware;

public class DemoPasswordOptions
{
    public const string SectionName = "DemoAuth";
    public const string CookieName = "demo_auth";

    /// <summary>The demo password interviewers enter on the login page. Required.</summary>
    public string Password { get; set; } = string.Empty;
}
