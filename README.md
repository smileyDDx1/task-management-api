# task-management-api

A REST API for teams to manage projects and tasks: users, roles, project members, task assignments, due dates, filtering and pagination.
Built with ASP.NET Core (.NET 10) following Clean Architecture, with JWT auth, FluentValidation, Serilog and SQL Server, running in Docker.

I built this to learn how a "real" backend is structured beyond a single-project CRUD app, and to practise testing (unit + integration).

## Features

- Register / login with **JWT** access tokens (passwords hashed with BCrypt)
- **Role based authorization**: `Admin`, `Manager`, `Member` + per-project permission checks
- Projects with members (many-to-many), owner can add/remove members
- Tasks with status, priority, due date, assignee
- Task list **filtering** (project, status, priority, assignee, due date range, overdue, title search), **sorting** and **server-side pagination**
- **Global exception handling** middleware returning `ProblemDetails` (RFC 7807)
- Request validation with **FluentValidation**
- **Structured logging** with Serilog (console + JSON file), correlation id per request
- **Swagger UI** with the "Authorize" button for JWT
- Health check endpoint (`/health`) that also checks the database
- **Docker Compose** setup (API + SQL Server), migrations applied on startup
- 22 endpoints, unit tests (xUnit + NSubstitute) and integration tests (WebApplicationFactory)

## Tech Stack

| | |
|---|---|
| Language / framework | C#, ASP.NET Core (.NET 10), Web API controllers |
| Database | SQL Server 2022, EF Core 10 (code first migrations) |
| Auth | JWT bearer tokens, BCrypt.Net |
| Validation | FluentValidation 12 |
| Logging | Serilog |
| API docs | Swagger (Swashbuckle) |
| Testing | xUnit, NSubstitute, WebApplicationFactory, SQLite in-memory, coverlet |
| DevOps | Docker, Docker Compose, GitHub Actions |

## Architecture

```
                 HTTP request
                      |
                      v
+---------------------------------------------------+
|                 TaskManagement.Api                |
| - Controllers (Auth, Users, Projects, Tasks)      |
| - JWT auth + role policies                        |
| - Middleware: CorrelationId -> Serilog request    |
|   logging -> ExceptionHandling                    |
| - ValidationFilter (runs FluentValidation)        |
+---------------------------------------------------+
                      |
                      v
+---------------------------------------------------+
|             TaskManagement.Application            |
| - Services (AuthService, ProjectService, ...)     |
| - DTOs, validators, custom exceptions             |
| - Interfaces: IProjectRepository, ITaskRepository,|
|   IPasswordHasher, IJwtTokenGenerator ...         |
+---------------------------------------------------+
          |                              ^
          v                              | implements interfaces
+--------------------+    +---------------------------------------+
| TaskManagement     |    |      TaskManagement.Infrastructure    |
| .Domain            |    | - AppDbContext + entity configurations|
| - Entities         |    | - Repositories (EF Core)              |
| - Enums            |    | - JwtTokenGenerator, BCrypt hasher    |
| (no dependencies)  |    | - Migrations, DbInitializer, seeding  |
+--------------------+    +---------------------------------------+
                                         |
                                         v
                                  [ SQL Server ]
```

The dependency rule: Domain depends on nothing, Application only on Domain, Infrastructure and Api depend on Application. The Application layer only knows interfaces, so services are unit tested with NSubstitute fakes instead of a database.

## Project Structure

```
task-management-api/
├── src/
│   ├── TaskManagement.Domain/          # entities + enums
│   ├── TaskManagement.Application/     # services, DTOs, validators, interfaces
│   │   ├── Auth/  Users/  Projects/  Tasks/
│   │   └── Common/ (Exceptions, Interfaces, Models)
│   ├── TaskManagement.Infrastructure/  # EF Core, repositories, JWT, BCrypt
│   │   ├── Persistence/ (Configurations, Repositories, Migrations, Seed)
│   │   └── Security/
│   └── TaskManagement.Api/             # controllers, middleware, Program.cs
├── tests/
│   ├── TaskManagement.UnitTests/
│   └── TaskManagement.IntegrationTests/
├── tools/benchmark/                    # script used for the performance numbers below
├── docs/                               # notes: tool choices, future plans
├── Dockerfile
└── docker-compose.yml
```

## Database Schema

```
Users (Id, FullName, Email [unique], PasswordHash, Role, IsActive, CreatedAt, UpdatedAt)
  |
  |-< ProjectMembers (ProjectId, UserId) [composite PK], JoinedAt
  |
Projects (Id, Name, Description, OwnerId -> Users, CreatedAt, UpdatedAt)
  |
  |-< Tasks (Id, Title, Description, Status, Priority, DueDate, CompletedAt,
             ProjectId -> Projects, AssigneeId -> Users (nullable), CreatedById -> Users,
             CreatedAt, UpdatedAt)
             indexes: (ProjectId, Status), DueDate, AssigneeId, CreatedById
```

Deleting a project cascades to its tasks and memberships. Users are never hard deleted (they are deactivated) because tasks and projects reference them.

## Roles & Permissions

| Action | Admin | Manager | Member |
|---|:---:|:---:|:---:|
| Register / login | ✅ | ✅ | ✅ |
| List users | ✅ | ✅ | ❌ |
| Change roles, deactivate users | ✅ | ❌ | ❌ |
| See projects | all | own projects | own projects |
| Create project | ✅ | ✅ (becomes owner) | ❌ |
| Edit / delete project, manage members | ✅ | only if owner | ❌ |
| Create / edit / delete / assign tasks | ✅ | in projects they belong to | ❌ |
| Change task status | ✅ | in projects they belong to | only tasks assigned to them |

New accounts are always `Member`. An admin is seeded on first start from the `Seed` settings.

## API Endpoints

| Method | Route | Who | Description |
|---|---|---|---|
| POST | `/api/auth/register` | anyone | Create account (Member role) |
| POST | `/api/auth/login` | anyone | Get a JWT |
| GET | `/api/users` | Admin, Manager | List users (search, role, isActive, paging) |
| GET | `/api/users/me` | logged in | Current user |
| GET | `/api/users/{id}` | Admin, Manager | User by id |
| PUT | `/api/users/{id}/role` | Admin | Change role |
| DELETE | `/api/users/{id}` | Admin | Deactivate user (soft delete) |
| GET | `/api/projects` | logged in | List projects (search, paging) |
| GET | `/api/projects/{id}` | member of project | Project details with counts |
| POST | `/api/projects` | Admin, Manager | Create project |
| PUT | `/api/projects/{id}` | owner / Admin | Update project |
| DELETE | `/api/projects/{id}` | owner / Admin | Delete project + its tasks |
| GET | `/api/projects/{id}/members` | member of project | List members |
| POST | `/api/projects/{id}/members` | owner / Admin | Add member |
| DELETE | `/api/projects/{id}/members/{userId}` | owner / Admin | Remove member (their tasks get unassigned) |
| GET | `/api/tasks` | logged in | List tasks with filters, sorting, paging |
| GET | `/api/tasks/{id}` | member of project | Task by id |
| POST | `/api/tasks` | Admin, Manager | Create task |
| PUT | `/api/tasks/{id}` | Admin, Manager | Update task |
| PATCH | `/api/tasks/{id}/status` | assignee / Manager / Admin | Change status |
| PATCH | `/api/tasks/{id}/assignee` | Admin, Manager | Assign / unassign |
| DELETE | `/api/tasks/{id}` | Admin, Manager | Delete task |
| GET | `/health` | anyone | Health check (API + database) |

### Task list query parameters

`GET /api/tasks?projectId=1&status=InProgress&priority=High&assigneeId=3&assignedToMe=false&dueFrom=2026-11-01&dueTo=2026-11-30&isOverdue=true&search=login&sortBy=dueDate&sortDirection=asc&page=1&pageSize=20`

- `sortBy`: `createdAt` (default), `dueDate`, `priority`, `title`, `status`
- `sortDirection`: `asc` / `desc` (default)
- `pageSize`: 1 to 50 (default 10)

Response:

```json
{
  "items": [ { "id": 42, "title": "Fix login page", "status": "InProgress", "priority": "High", "isOverdue": false, "projectName": "Website", "assigneeName": "Jane Doe", "...": "..." } ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 135,
  "totalPages": 7,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

### Error format

Every error uses `ProblemDetails`:

```json
{
  "status": 400,
  "title": "Validation failed",
  "detail": "One or more validation errors occurred.",
  "instance": "/api/auth/register",
  "traceId": "0HN7...",
  "errors": {
    "Password": [ "Password must contain at least one uppercase letter." ]
  }
}
```

| Exception (Application layer) | Status |
|---|---|
| `ValidationException`, `BadRequestException` | 400 |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| anything else | 500 (details hidden outside Development) |

## Getting Started

### Run with Docker (easiest)

Requirements: Docker Desktop.

```bash
git clone https://github.com/smileyDDx1/task-management-api.git
cd task-management-api
docker compose up --build
```

- Swagger UI: http://localhost:5000/swagger
- Health check: http://localhost:5000/health
- Default admin: `admin@taskmanager.local` / `Admin123!` (change it with the `ADMIN_PASSWORD` env variable)

SQL Server takes ~30 seconds to start the first time; the API waits for its healthcheck and then applies the migrations.

To start with demo data (5 managers, 40 members, 30 projects, 20,000 tasks, password `Password123!` for all demo users):

```bash
SEED_DEMO_DATA=true docker compose up --build
```

### Run locally without Docker for the API

Requirements: .NET 10 SDK and a SQL Server (the one from compose works: `docker compose up sqlserver`).

```bash
dotnet run --project src/TaskManagement.Api
```

Swagger opens at http://localhost:5080/swagger. Settings are in `appsettings.json` / `appsettings.Development.json`; override secrets with environment variables, e.g. `Jwt__Secret`.

### Using Swagger with JWT

1. `POST /api/auth/login` with the admin account
2. Copy `accessToken` from the response
3. Click **Authorize** (top right) and paste the token
4. All requests now send `Authorization: Bearer <token>`

There is also a `TaskManagement.Api.http` file with example requests for the VS / Rider HTTP client.

### Migrations

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> --project src/TaskManagement.Infrastructure --startup-project src/TaskManagement.Api --output-dir Persistence/Migrations
```

Migrations are applied automatically on startup (`DbInitializer`).

## Testing

```bash
dotnet test
```

- **Unit tests** (`TaskManagement.UnitTests`): services with NSubstitute fakes (auth, users, projects, tasks, permission rules), FluentValidation validators, domain logic, JWT generation and BCrypt hashing.
- **Integration tests** (`TaskManagement.IntegrationTests`): start the whole API in memory with `WebApplicationFactory`, SQL Server swapped for SQLite in-memory, and call the endpoints over HTTP (auth, 401/403/404/409 cases, validation errors, filters, paging, sorting, middleware).

Coverage report:

```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"tests/**/coverage.cobertura.xml" -targetdir:coverage-report -reporttypes:"Html;TextSummary"
```

Current results: **124 tests** (79 unit + 45 integration), all passing. Combined line coverage **89.2%** (branch 71.4%), with the generated EF migrations excluded:

| Project | Line coverage |
|---|---|
| TaskManagement.Domain | 100% |
| TaskManagement.Application | 99.5% |
| TaskManagement.Api | 92.7% |
| TaskManagement.Infrastructure | 75.4% (the demo data seeder is not covered by tests) |

## Performance

My first version of the list endpoints worked fine with a few rows but got slow once there was real data:

- `GET /api/tasks` used `Include()` for project/assignee/creator, loaded **every matching task** with change tracking, and only then sorted and paged in C#.
- `GET /api/projects` did `Include(p => p.Members).Include(p => p.Tasks)` just to show counts. EF Core turns that into one big JOIN (projects x members x tasks, around 160k rows per request with the demo data) and even logs a `MultipleCollectionIncludeWarning` about it.

What I changed (in `TaskRepository` and `ProjectRepository`):

- `AsNoTracking()` for read-only queries
- `Select()` projection straight into the DTO, so SQL only returns the columns the response needs (no `Include` needed)
- filtering, sorting, `COUNT(*)` and `Skip/Take` (OFFSET/FETCH) run in SQL Server instead of in memory
- member and task counts as `COUNT` subqueries instead of loading the collections
- stable ordering (`ThenBy(Id)`) so paging never skips or repeats rows

**How I measured it:** [`tools/benchmark/ListEndpointsBenchmark.cs`](tools/benchmark/ListEndpointsBenchmark.cs) against the API + SQL Server 2022 in Docker on my laptop, with the demo data (30 projects, 45 users, **20,000 tasks**). 10 warm-up requests, then 100 sequential requests per endpoint, then the same number from 10 concurrent clients. Full output and setup: [docs/benchmark-results.md](docs/benchmark-results.md).

| Endpoint (page size 20) | Mean before → after | p95 before → after | Throughput, 10 clients |
|---|---|---|---|
| `GET /api/tasks` as admin (all 20,000 tasks) | 512 → 78 ms (**-85%**) | 682 → 187 ms (-73%) | 2 → 38 req/s |
| `GET /api/tasks?status=InProgress&sortBy=dueDate` (4,964 matches) | 360 → 16 ms (**-96%**) | 631 → 29 ms (-95%) | 12 → 143 req/s |
| `GET /api/tasks` as a member (3,389 visible tasks) | 93 → 25 ms (**-74%**) | 158 → 31 ms (-80%) | 16 → 89 req/s |
| `GET /api/projects` as admin (30 projects) | 4,879 → 16 ms (**-99.7%**) | 9,703 → 35 ms (-99.6%) | < 1 → 123 req/s |

The admin "all tasks" page is still the slowest one because the default sort is `CreatedAt`, which has no index, so SQL Server sorts all 20k rows for every page. Adding that index is on the [future plans](docs/future-plans.md) list.

## What I learned

- **Clean Architecture** makes unit testing easy (services only see interfaces), but it's a lot of files for a small API. I understand now why people complain about it for simple CRUD apps.
- **EF Core can be slow if you're not careful.** My first version of the list endpoints used `Include()` and paged in memory, which loaded every row. Projections + `AsNoTracking()` + `Skip/Take` in the query fixed it (see Performance).
- **SQL Server "multiple cascade paths" error** when two foreign keys point to `Users` from `Tasks`. Fixed by using `DeleteBehavior.Restrict` and deactivating users instead of deleting them.
- **JWT claim mapping**: by default ASP.NET Core renames `sub`/`role` claims to long XML schema URLs. `MapInboundClaims = false` + setting `RoleClaimType = "role"` keeps them short.
- **Roles alone aren't enough**: "a manager can only edit their own projects" needs resource-based checks in the service layer, not just `[Authorize(Roles = ...)]`.
- **Integration tests caught things unit tests didn't**, like JSON enum serialisation and the order of middleware.
- Naming an entity `Task` clashes with `System.Threading.Tasks.Task`, so it's `TaskItem`.

## Known limitations

- Role changes and deactivation only take effect when the user's token expires (no refresh tokens / token revocation yet).
- No rate limiting on login.
- Integration tests run on SQLite, so SQL Server specific behaviour and migrations are not covered by tests.
- Offset pagination (`Skip/Take`) gets slower on very deep pages.
- `sortBy=dueDate&sortDirection=asc` lists tasks without a due date first, because SQL Server sorts `NULL` before any value.

More ideas in [docs/future-plans.md](docs/future-plans.md). Why I picked each tool: [docs/questions-answers.md](docs/questions-answers.md).
