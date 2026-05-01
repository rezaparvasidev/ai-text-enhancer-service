using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TextEnhancer.Api.Data;
using TextEnhancer.Api.Middleware;
using TextEnhancer.Api.Services;

namespace TextEnhancer.Tests.TestSupport;

public class TextEnhancerWebAppFactory : WebApplicationFactory<Program>
{
    public const string DemoPassword = "test-pass";

    public FakeChatCompletionClient FakeChat { get; } = new();

    public string AuthCookie => $"{DemoPasswordOptions.CookieName}={DemoPasswordMiddleware.ComputeToken(DemoPassword)}";

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", AuthCookie);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DEMO_PASSWORD"] = DemoPassword,
                ["AOAI:Endpoint"] = "https://test.openai.azure.com/",
                ["AOAI:Deployment"] = "gpt-4o-test",
                ["AOAI:ApiKey"] = "test-key"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the DB with a unique EF Core InMemory store per factory instance.
            var dbOptions = services.Single(s => s.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(dbOptions);
            var dbName = "test-" + Guid.NewGuid();
            services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(dbName));

            // Replace the Azure OpenAI client with a per-factory fake.
            var chat = services.Single(s => s.ServiceType == typeof(IChatCompletionClient));
            services.Remove(chat);
            services.AddSingleton<IChatCompletionClient>(FakeChat);
        });
    }
}
