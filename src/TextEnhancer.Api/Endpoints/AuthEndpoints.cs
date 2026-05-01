using Microsoft.Extensions.Options;
using TextEnhancer.Api.Middleware;

namespace TextEnhancer.Api.Endpoints;

public static class AuthEndpoints
{
    private const string LoginHtml = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>Text Enhancer — Demo Login</title>
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <style>
            body { font-family: system-ui, sans-serif; background: #f4f6f9; margin: 0; display: flex;
                   min-height: 100vh; align-items: center; justify-content: center; }
            .card { background: white; border-radius: 8px; padding: 32px 36px; min-width: 320px;
                    box-shadow: 0 4px 16px rgba(0,0,0,0.08); }
            h1 { font-size: 1.25rem; margin: 0 0 4px 0; }
            p { color: #555; font-size: 0.9rem; margin: 0 0 18px 0; }
            label { font-size: 0.85rem; color: #333; display: block; margin-bottom: 6px; }
            input[type=password] { width: 100%; padding: 10px 12px; font-size: 1rem;
                                   border: 1px solid #ccc; border-radius: 6px; box-sizing: border-box; }
            button { margin-top: 16px; width: 100%; padding: 10px; font-size: 1rem; border: 0;
                     border-radius: 6px; background: #2563eb; color: white; cursor: pointer; }
            button:hover { background: #1d4ed8; }
            .err { color: #b91c1c; font-size: 0.85rem; margin-top: 12px; }
          </style>
        </head>
        <body>
          <form class="card" method="post" action="/login">
            <h1>AI Text Enhancer</h1>
            <p>Enter the demo password to continue.</p>
            <label for="password">Password</label>
            <input id="password" name="password" type="password" autofocus required>
            <button type="submit">Sign in</button>
            {ERROR}
          </form>
        </body>
        </html>
        """;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/login", (HttpContext ctx) =>
        {
            var failed = ctx.Request.Query.ContainsKey("failed");
            var html = LoginHtml.Replace(
                "{ERROR}",
                failed ? "<div class=\"err\">Incorrect password.</div>" : string.Empty);
            return Results.Content(html, "text/html");
        }).ExcludeFromDescription();

        app.MapPost("/login", async (HttpContext ctx, IOptions<DemoPasswordOptions> opts) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var submitted = form["password"].ToString();

            if (string.IsNullOrEmpty(submitted) || submitted != opts.Value.Password)
            {
                return Results.Redirect("/login?failed=1");
            }

            ctx.Response.Cookies.Append(
                DemoPasswordOptions.CookieName,
                DemoPasswordMiddleware.ComputeToken(opts.Value.Password),
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = ctx.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(8),
                    Path = "/"
                });

            return Results.Redirect("/");
        }).ExcludeFromDescription();

        app.MapPost("/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete(DemoPasswordOptions.CookieName);
            return Results.Redirect("/login");
        }).ExcludeFromDescription();

        return app;
    }
}
