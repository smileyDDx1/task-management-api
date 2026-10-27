# Future plans

Things I want to add next, roughly in order.

1. **Refresh tokens** - right now the access token lasts 60 minutes and then the user has to log in again. Also needed so role changes / deactivation take effect without waiting for the token to expire.
2. **Rate limiting on /api/auth/login** - .NET has a built-in rate limiter (`AddRateLimiter`), would stop password brute forcing.
3. **GitHub Actions** - CI already builds and runs the tests, next step is building the Docker image and pushing it to a registry.
4. **Testcontainers for integration tests** - run the integration tests against a real SQL Server container instead of SQLite so the migrations are tested too.
5. **Task comments and activity history** - who changed what and when (audit log table).
6. **Soft delete for projects** - deleting a project currently deletes all its tasks for good.
7. **Caching** - `IMemoryCache` / Redis for the project list, it doesn't change often.
8. **Try MediatR / CQRS** - split the services into commands and queries to see if it makes the code cleaner as the API grows.
9. **API versioning** (`/api/v1/...`) before making breaking changes.
10. **Email notifications** when a task is assigned (background job with Hangfire or a hosted service).
11. **Cursor based pagination** for the task list - `Skip/Take` gets slower on very deep pages.
12. **Index on `Tasks.CreatedAt`** - it's the default sort of the task list and the benchmark shows the admin "all tasks" page is still the slowest one (SQL Server sorts all rows for every page).
