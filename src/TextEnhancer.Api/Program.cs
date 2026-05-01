using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using TextEnhancer.Api.Data;
using TextEnhancer.Api.Endpoints;
using TextEnhancer.Api.Middleware;
using TextEnhancer.Api.Seed;
using TextEnhancer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection(AzureOpenAIOptions.SectionName));

// DemoAuth:Password is the canonical config key; DEMO_PASSWORD is an env-var-friendly alias.
// Bind both at resolution time so test fixtures can supply either form.
builder.Services.AddOptions<DemoPasswordOptions>()
    .Bind(builder.Configuration.GetSection(DemoPasswordOptions.SectionName))
    .Configure<IConfiguration>((opts, config) =>
    {
        if (string.IsNullOrEmpty(opts.Password))
            opts.Password = config["DEMO_PASSWORD"] ?? string.Empty;
    });

var connectionString = builder.Configuration.GetConnectionString("Sqlite")
    ?? "Data Source=./data/interactions.db";
builder.Services.AddDbContext<AppDbContext>(opts => opts.UseSqlite(connectionString));

builder.Services.AddSingleton<IChatCompletionClient, AzureOpenAIChatCompletionClient>();
builder.Services.AddScoped<ITextEnhancementService, TextEnhancementService>();
builder.Services.AddScoped<IInteractionLogger, InteractionLogger>();
builder.Services.AddSingleton<IPiiGuard, PiiGuard>();
builder.Services.AddScoped<IRelevanceGuard, LlmRelevanceGuard>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AI Text Enhancer",
        Version = "v1",
        Description = "Enhances raw technician notes into polished, bulleted reports via Azure OpenAI."
    });
});

var app = builder.Build();

// Ensure SQLite file directory exists, then create schema and seed sample rows.
EnsureSqliteFolderExists(connectionString);
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    if (!app.Environment.IsEnvironment("Testing"))
    {
        await DbSeeder.SeedIfEmptyAsync(db, CancellationToken.None);
    }
}

app.UseDemoPasswordGate();

app.UseDefaultFiles();   // serves wwwroot/index.html for "/"
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();

app.MapAuthEndpoints();
app.MapEnhanceEndpoints();
app.MapHistoryEndpoints();

app.Run();

static void EnsureSqliteFolderExists(string connectionString)
{
    // Connection string format: "Data Source=./data/interactions.db" — pull the folder out and mkdir.
    const string token = "Data Source=";
    var idx = connectionString.IndexOf(token, StringComparison.OrdinalIgnoreCase);
    if (idx < 0) return;
    var path = connectionString[(idx + token.Length)..].Split(';', 2)[0].Trim();
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        Directory.CreateDirectory(dir);
}

// Exposed so WebApplicationFactory<Program> can reach it from the test project.
public partial class Program { }
