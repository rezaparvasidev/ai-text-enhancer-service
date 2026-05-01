using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace TextEnhancer.Api.Middleware;

/// <summary>
/// Cookie-based demo password gate. Pages get a 302 redirect to <c>/login</c>; API endpoints
/// (paths starting with <c>/api/</c>) get a JSON 401 instead so curl / Swagger users get a clean
/// error.
/// </summary>
public class DemoPasswordMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _expectedToken;

    public DemoPasswordMiddleware(RequestDelegate next, IOptions<DemoPasswordOptions> options)
    {
        _next = next;
        var password = options.Value.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "DemoAuth:Password (env DEMO_PASSWORD) must be configured. The demo gate refuses to start without a password.");
        }
        _expectedToken = ComputeToken(password);
    }

    public static string ComputeToken(string password)
    {
        // Deterministic hash — same password produces the same token across restarts, so issued
        // cookies remain valid. Salt is a fixed app-internal constant; rotating it invalidates
        // all sessions, which is what we want when the password changes.
        var bytes = Encoding.UTF8.GetBytes("text-enhancer.demo-auth.v1::" + password);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "/";

        if (IsAllowlisted(path))
            return _next(ctx);

        if (ctx.Request.Cookies.TryGetValue(DemoPasswordOptions.CookieName, out var token)
            && CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(token),
                Encoding.ASCII.GetBytes(_expectedToken)))
        {
            return _next(ctx);
        }

        return RejectAsync(ctx, path);
    }

    private static bool IsAllowlisted(string path)
    {
        return path.Equals("/login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/logout", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/healthz", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }

    private static Task RejectAsync(HttpContext ctx, string path)
    {
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return ctx.Response.WriteAsJsonAsync(new
            {
                code = "unauthorized",
                message = "Demo password required. POST password to /login first."
            });
        }

        ctx.Response.Redirect("/login");
        return Task.CompletedTask;
    }
}

public static class DemoPasswordMiddlewareExtensions
{
    public static IApplicationBuilder UseDemoPasswordGate(this IApplicationBuilder app)
        => app.UseMiddleware<DemoPasswordMiddleware>();
}
