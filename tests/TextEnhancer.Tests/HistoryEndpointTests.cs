using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TextEnhancer.Api.Data;
using TextEnhancer.Api.Models;
using TextEnhancer.Tests.TestSupport;

namespace TextEnhancer.Tests;

public class HistoryEndpointTests : IClassFixture<TextEnhancerWebAppFactory>
{
    private readonly TextEnhancerWebAppFactory _factory;

    public HistoryEndpointTests(TextEnhancerWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EmptyDb_ReturnsEmptyPage()
    {
        await ResetDbAsync();
        var client = _factory.CreateAuthenticatedClient();

        var page = await client.GetFromJsonAsync<HistoryPage>("/api/history");

        Assert.NotNull(page);
        Assert.Empty(page!.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task PaginationAndOrdering_WorkAsExpected()
    {
        await ResetDbAsync();
        await SeedAsync(count: 25);
        var client = _factory.CreateAuthenticatedClient();

        var p1 = await client.GetFromJsonAsync<HistoryPage>("/api/history?page=1&pageSize=10");
        Assert.NotNull(p1);
        Assert.Equal(25, p1!.TotalCount);
        Assert.Equal(10, p1.Items.Count);
        // newest first — index 24 was inserted last with the most recent CreatedUtc
        Assert.Equal("note 24", p1.Items[0].InputText);

        var p3 = await client.GetFromJsonAsync<HistoryPage>("/api/history?page=3&pageSize=10");
        Assert.Equal(5, p3!.Items.Count); // 25 % 10 == 5 on last page

        var pBig = await client.GetFromJsonAsync<HistoryPage>("/api/history?pageSize=200");
        Assert.Equal(100, pBig!.PageSize); // clamped to 100
    }

    [Fact]
    public async Task NegativePageDefaultsToOne()
    {
        await ResetDbAsync();
        await SeedAsync(3);
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/history?page=-5&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<HistoryPage>();
        Assert.Equal(1, page!.Page);
        Assert.Equal(2, page.Items.Count);
    }

    private async Task ResetDbAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Interactions.RemoveRange(db.Interactions);
        await db.SaveChangesAsync();
    }

    private async Task SeedAsync(int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var baseTime = DateTime.UtcNow.AddMinutes(-count);
        for (var i = 0; i < count; i++)
        {
            db.Interactions.Add(new Interaction
            {
                CreatedUtc = baseTime.AddMinutes(i),
                InputText = $"note {i}",
                OutputText = $"- bullet {i}",
                Model = "gpt-4o-test",
                PromptTokens = 10, CompletionTokens = 5, LatencyMs = 100,
                Status = InteractionStatus.Success
            });
        }
        await db.SaveChangesAsync();
    }
}
