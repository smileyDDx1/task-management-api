# Questions & Answers (why I picked each tool)

Notes I wrote while building the project, mostly so I can explain my choices in interviews.

## Clean Architecture

**What it is:** the code is split into 4 projects and the dependencies only point inwards:

```
Api  ->  Application  ->  Domain
Infrastructure  ->  Application
```

- **Domain**: entities (User, Project, ProjectMember, TaskItem) and enums. No NuGet packages at all.
- **Application**: business rules. Services, DTOs, validators and *interfaces* for things it needs (repositories, password hasher, token generator). It doesn't know EF Core or ASP.NET exist.
- **Infrastructure**: the actual implementations (EF Core DbContext, repositories, BCrypt, JWT).
- **Api**: controllers, middleware, auth setup. Wires everything together in Program.cs.

**Why:** the services can be unit tested with fake repositories (NSubstitute), and if I switched SQL Server to Postgres only Infrastructure would change.

**vs. a single project (N-layer in one project):** simpler for a small app, but nothing stops a controller from using the DbContext directly, so the layers slowly mix together.

**Trade-off I noticed:** more files and more mapping code for a small API. For a CRUD app this size it is honestly a bit much, but I wanted to learn the structure.

## Why no MediatR / CQRS?

I looked at it (a lot of Clean Architecture templates use it). For 4 services it felt like extra indirection I didn't need yet, and MediatR has a commercial license since v13. Plain service classes with interfaces are easier to read and debug. It's in [future-plans.md](future-plans.md) to try later.

## ASP.NET Core (.NET 10)

- .NET 10 is the current LTS release (supported for 3 years).
- Built-in dependency injection, configuration, logging and middleware pipeline, so I didn't need extra frameworks.
- Controllers instead of Minimal APIs because `[Authorize(Policy = ...)]`, `[ProducesResponseType]` and filters are easy to organise per controller.

## EF Core

**Alternatives:** Dapper, raw ADO.NET.

- **vs. Dapper:** Dapper is faster for raw reads but I'd write every SQL query by hand plus the migrations. EF Core gives migrations, change tracking and LINQ.
- **Performance:** EF Core can be slow if used carelessly (that happened to me, see README "Performance"). `AsNoTracking()`, projecting with `Select()` and paging in the database fixed it.
- `ExecuteUpdateAsync` (EF 7+) is used to unassign tasks in one UPDATE statement instead of loading them.

## SQL Server

- Common in .NET jobs, and EF Core support is the most mature.
- Runs in Docker using the official `mssql/server:2022` image (Developer edition, free).
- Integration tests use **SQLite in-memory** instead so they run anywhere without Docker. Downside: SQLite is not 100% the same as SQL Server (e.g. migrations aren't tested, `EnsureCreated()` is used instead).

## JWT authentication

**Alternatives:** cookie auth, ASP.NET Core Identity, an external provider (Auth0, Azure AD B2C, Keycloak).

- Stateless: the API doesn't store sessions, the role is inside the token.
- Works for any client (SPA, mobile, Postman).
- **Downside:** a token can't be revoked before it expires, and a role change only shows up after the next login. Refresh tokens are on the future list.
- I didn't use ASP.NET Core Identity because I wanted to understand hashing + token creation myself. For a real product I'd use Identity or an identity provider.

## BCrypt for passwords

- Slow on purpose (work factor), so brute forcing leaked hashes is expensive.
- Salt is generated and stored inside the hash automatically.
- Never store passwords with SHA256/MD5, those are too fast.

## Role based authorization (Admin / Manager / Member)

Two levels of checks:

1. **Policies on the controller** (`[Authorize(Policy = Policies.ManagerOrAdmin)]`): quick check on the role claim, returns 403 before any code runs.
2. **Resource checks in the services**: e.g. a Manager can only edit projects they own, and a Member can only change the status of tasks assigned to them. Roles alone can't express that, so it lives in the Application layer (and is unit tested).

## FluentValidation

**Alternative:** DataAnnotations (`[Required]`, `[MaxLength]`).

- Validation rules live in separate classes, not on the DTOs, so the DTOs stay clean records.
- Easier to write conditional rules (`When(...)`) and custom messages.
- Easy to unit test with `TestValidate()`.
- The `FluentValidation.AspNetCore` auto-validation package is deprecated, so I wrote a small `ValidationFilter` that finds the validator for each action argument and runs it.

## Global exception handling middleware

- Services throw exceptions like `NotFoundException` or `ForbiddenException`.
- `ExceptionHandlingMiddleware` catches them and maps them to status codes (404, 403, ...) using the standard `ProblemDetails` format (RFC 7807).
- Controllers stay small (no try/catch everywhere) and every error response has the same shape.
- 500 errors don't return the exception message outside Development.

## Serilog

**Alternative:** the built-in `ILogger` console provider.

- Structured logging: `_logger.LogInformation("Task {TaskId} created", id)` keeps `TaskId` as a separate field, not just text, so logs can be searched/filtered (Seq, Elastic, etc.).
- Config driven: sinks and levels come from appsettings.json.
- `UseSerilogRequestLogging()` writes one clean line per request instead of several noisy ones.
- I added a correlation id middleware so all log lines from one request share the same `CorrelationId`.

## xUnit + NSubstitute

- **xUnit**: the default in the .NET templates, a new class instance per test so tests don't share state.
- **NSubstitute vs Moq:** simpler syntax (`repo.GetByIdAsync(1).Returns(user)`), and I wanted to avoid Moq after the SponsorLink controversy in 2023.
- I used plain `Assert` instead of FluentAssertions because FluentAssertions v8 needs a paid license for commercial use.

## Integration tests with WebApplicationFactory

- Starts the real API in memory (real middleware, auth, validation, EF Core) and sends real HTTP requests.
- Catches bugs unit tests can't, e.g. wrong route, missing `[Authorize]`, JSON enum serialisation.
- The SQL Server DbContext is replaced with SQLite in-memory in `CustomWebApplicationFactory`.

## Docker

- `docker compose up` starts SQL Server + the API, nobody needs SQL Server installed.
- Multi-stage Dockerfile: the SDK image builds, the much smaller aspnet image runs it.
- The API waits for the SQL Server healthcheck before starting, and applies migrations on startup.
