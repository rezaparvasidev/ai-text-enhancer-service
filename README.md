# AI Text Enhancer Service

A small ASP.NET Core Minimal API that turns raw, informal field-technician notes into polished,
bulleted service reports using an LLM. Every interaction (success or failure) is persisted to
SQLite for auditability.

Built for the Granum LLC AI Engineer take-home assignment.

---

## Features

- **POST `/api/enhance`** — synchronous enhancement. Returns the bulleted report plus model name,
  token counts, and request latency.
- **POST `/api/enhance/stream`** — Server-Sent Events streaming variant (bonus).
- **GET `/api/history?page=&pageSize=`** — paginated audit log, newest first.
- **PII guard** (bonus) — rejects notes containing emails / phone numbers / SSN-shaped strings
  *before* they reach the LLM, and still logs the rejection.
- **Relevance guard** — a fast LLM-based classifier rejects inputs that aren't plausible
  landscaping field-technician notes (chitchat, recipes, prompt-injection attempts), and the
  rejection is logged with `status=OffTopicRejected`.
- **Demo password gate** — interviewers receive a single password (env var `DEMO_PASSWORD`) and a
  cookie-based login page; everything else is gated behind it.
- **Basic web UI** at `/` (bonus) — submit notes, view streamed output, browse history.
- **Swagger UI** at `/swagger` for direct API exploration.

---

## Tech stack

| Concern        | Choice                                                |
|----------------|-------------------------------------------------------|
| Runtime        | .NET 8 (LTS)                                          |
| Web framework  | ASP.NET Core Minimal API                              |
| LLM            | Azure OpenAI via `Azure.AI.OpenAI` v2 SDK (gpt-4o)    |
| Database       | SQLite via EF Core                                    |
| Tests          | xUnit + `Microsoft.AspNetCore.Mvc.Testing` + Moq      |

---

## Repo layout

```
src/TextEnhancer.Api/
  Program.cs                       composition root, DI, middleware order, seed-on-boot
  Prompts/SystemPrompt.cs          single source of truth for the LLM system prompt
  Services/
    AzureOpenAIChatCompletionClient.cs   the only file that imports the SDK
    TextEnhancementService.cs            orchestrates the LLM call + latency capture
    PiiGuard.cs                          regex-based PII detection
    LlmRelevanceGuard.cs                 LLM-based off-topic input rejector
    InteractionLogger.cs                 writes to SQLite, never throws
  Endpoints/
    EnhanceEndpoints.cs            /api/enhance + /api/enhance/stream (SSE)
    HistoryEndpoints.cs            /api/history (pagination)
    AuthEndpoints.cs               /login (GET + POST), /logout
  Middleware/DemoPasswordMiddleware.cs   cookie-based gate, allowlists /login & /healthz
  Data/                            EF Core: AppDbContext, Interaction, InteractionStatus
  Seed/DbSeeder.cs                 idempotent ≥5-row seeder
  wwwroot/index.html               vanilla-JS demo UI

tests/TextEnhancer.Tests/
  TestSupport/                     WebApplicationFactory + FakeChatCompletionClient
  PiiGuardTests.cs
  TextEnhancementServiceTests.cs   verifies prompt forwarding, mapping, error propagation
  EnhanceEndpointTests.cs          happy path + validation + PII reject + LLM failure + SSE stream
  HistoryEndpointTests.cs          empty / pagination / clamp / negative-page edge cases

data/interactions.db               pre-populated sample (6 rows: 4 success, 1 LlmError, 1 PiiRejected)
```

---

## Local setup (5 minutes)

### 1. Prerequisites
- .NET 8 SDK ([download](https://dotnet.microsoft.com/download))
- An Azure OpenAI resource with a `gpt-4o` deployment (or any chat-capable model — set the deployment
  name accordingly)
- Optional: [DB Browser for SQLite](https://sqlitebrowser.org/) to inspect `data/interactions.db`

### 2. Configure secrets
Create a `.env` in the repo root or set these as user-level env vars:

```bash
AOAI__Endpoint=https://aoai-reza.openai.azure.com/
AOAI__Deployment=gpt-4o
AOAI__ApiKey=<your-key>
DEMO_PASSWORD=<choose-anything>
```

> The `__` (double underscore) is ASP.NET Core's convention for representing nested config keys
> via env vars (`AOAI:ApiKey` → `AOAI__ApiKey`).

Or use `dotnet user-secrets`:
```bash
cd src/TextEnhancer.Api
dotnet user-secrets init
dotnet user-secrets set "AOAI:Endpoint" "https://aoai-reza.openai.azure.com/"
dotnet user-secrets set "AOAI:Deployment" "gpt-4o"
dotnet user-secrets set "AOAI:ApiKey" "<your-key>"
dotnet user-secrets set "DemoAuth:Password" "<choose-anything>"
```

### 3. Run
```bash
dotnet run --project src/TextEnhancer.Api
```
The app listens on `http://localhost:5xxx` (look in the console for the exact port). Browse there,
enter the demo password, and submit a note.

### 4. Run tests
```bash
dotnet test
```
All tests run with a mocked LLM and an in-memory database — no real API calls, no real SQLite file.

---

## Architecture notes

**Three-layer separation.** Endpoints handle HTTP only (parse, validate, call services, format
response). Services own business logic. Data owns persistence. The LLM call, the logger, and the
HTTP layer never reach into each other directly.

**Mockable LLM seam.** The Azure OpenAI SDK exposes `ChatClient` as a concrete sealed type — hard
to mock. We define a thin `IChatCompletionClient` interface, with `AzureOpenAIChatCompletionClient`
as the only adapter. Tests substitute a `FakeChatCompletionClient`. This is the only place in the
codebase that imports the AOAI SDK.

**Logging never masks the real outcome.** `InteractionLogger.LogAsync` swallows its own exceptions
and emits via `ILogger`. A DB hiccup will never turn a successful enhancement into an error
response or hide the real LLM error from the caller.

**Status enum.** Every row has `Success | LlmError | PiiRejected | ValidationError`. This makes
failure modes first-class data and keeps the history endpoint scannable.

**Prompt engineering.** See `src/TextEnhancer.Api/Prompts/SystemPrompt.cs`. Hard rules (preserve
every fact, no invented quantities/dates, no commentary) are stated first, followed by formatting
rules and a worked example mirroring the brief.

**SSE streaming.** The bonus streaming endpoint uses the SDK's `CompleteChatStreamingAsync`. It
emits one `data: {"delta":...}` event per chunk and a final `event: done` with usage metadata.
Even mid-stream failures persist a log row.

**Demo password.** The middleware computes `SHA256("text-enhancer.demo-auth.v1::" + DEMO_PASSWORD)`
once at startup. Successful login mints that token as an HttpOnly cookie. Restarts with the same
password keep existing sessions valid; changing the password invalidates all sessions.

---

## Configuration reference

| Key (env)                      | Key (config) / appsettings    | Required | Notes                                   |
|--------------------------------|-------------------------------|----------|-----------------------------------------|
| `AOAI__Endpoint`               | `AOAI:Endpoint`               | yes      | e.g. `https://aoai-reza.openai.azure.com/` |
| `AOAI__Deployment`             | `AOAI:Deployment`             | yes      | The deployment name (not the model id)  |
| `AOAI__ApiKey`                 | `AOAI:ApiKey`                 | yes      | API key from the AOAI resource          |
| `DEMO_PASSWORD`                | `DemoAuth:Password`           | yes      | App refuses to start without one        |
| `ConnectionStrings__Sqlite`    | `ConnectionStrings:Sqlite`    | no       | Default: `Data Source=App_Data/interactions.db` |

For Azure App Service Linux, set `ConnectionStrings__Sqlite` to `Data Source=/home/data/interactions.db`
so the SQLite file lives on the persistent `/home` mount.

---

## Reviewing the pre-populated database

`data/interactions.db` is committed and contains 6 sample interactions: 4 successful enhancements,
1 PII-rejected, and 1 simulated LLM error. Open it in DB Browser for SQLite, or:

```bash
sqlite3 data/interactions.db "SELECT Status, COUNT(*) FROM Interactions GROUP BY Status;"
```

This DB is independent of the locally-running app's DB (which lives at `App_Data/interactions.db`,
gitignored).

---

## Deployment

Deployed to Azure App Service (F1 Free tier, Linux, .NET 8) under resource group `rg-rezaparvasi`.
The `DEMO_PASSWORD`, `AOAI__*`, and `ConnectionStrings__Sqlite` keys are set as App Service
configuration. SQLite persists at `/home/data/interactions.db` across restarts.

**Future improvements** (not in scope for this take-home):
- Migrate AOAI auth from API key to system-assigned Managed Identity.
- Add a simple GitHub Actions workflow to publish on push to `main`.
- Add structured logging with Application Insights.

---

## Trade-offs and what I'd do with more time

- **Validation** is hand-rolled in the endpoints. With more time I'd reach for FluentValidation or
  the built-in `ValidateOptions` pattern to keep endpoint code thinner.
- **PII detection** is regex-only. A production version would lean on a real DLP library or a
  small classifier model.
- **Streaming error handling** writes a final `event: error` over SSE, which clients should listen
  for. A more polished version would also support reconnection with `Last-Event-ID`.
- **EF Core migrations**: I use `EnsureCreated` instead of migrations because the schema is one
  table. For anything bigger, I'd switch to `dotnet ef migrations`.
- **Authentication**: the demo password gate is intentionally minimal — for a real product I'd
  use Microsoft Entra ID or a proper IdP.
