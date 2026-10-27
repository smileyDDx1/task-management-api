# Benchmark results (list endpoints)

Raw output of `tools/benchmark/ListEndpointsBenchmark.cs`, before and after the query optimization commit.

**Setup**

- Laptop, Docker Desktop (WSL2 VM with 4 CPUs / 3.6 GB RAM)
- SQL Server 2022 (Developer) container + the API (Release build) in a container on the same Docker network
- Benchmark sent requests from a third container on that network (avoids Docker Desktop port forwarding)
- Demo data from `DemoDataSeeder` (fixed random seed): 30 projects, 45 users, 20,000 tasks
- Per endpoint: 10 warm-up requests, N sequential requests (latency), then N requests from 10 concurrent clients (throughput). HttpClient timeout 2 minutes.

## Before (Include + tracking + paging in memory)

```
dotnet run ListEndpointsBenchmark.cs -- http://api:8080 100
```

| Endpoint | Total rows | Mean (ms) | p50 (ms) | p95 (ms) | p99 (ms) | Throughput @10 clients (req/s) |
|---|---|---|---|---|---|---|
| GET /api/tasks (admin, page 1 of all tasks) | 20000 | 511.8 | 489.6 | 681.5 | 783.1 | 2 |
| GET /api/tasks (admin, status + sort by due date) | 4964 | 359.6 | 355.5 | 631.3 | 791.5 | 12 |
| GET /api/tasks (member, only own projects) | 3389 | 93.3 | 79.3 | 157.8 | 199.9 | 16 |

The projects list was too slow for 100 requests. With 10 concurrent clients the requests took longer than the 2 minute client timeout, so it was run on its own with N = 20:

```
dotnet run ListEndpointsBenchmark.cs -- http://api:8080 20 /api/projects
```

| Endpoint | Total rows | Mean (ms) | p50 (ms) | p95 (ms) | p99 (ms) | Throughput @10 clients (req/s) |
|---|---|---|---|---|---|---|
| GET /api/projects (admin) | 30 | 4879.2 | 3941.5 | 9702.5 | 12873.1 | < 1 |

(The old version of the tool printed throughput without decimals, so this showed as `0`.)

EF Core also logged this warning for the old projects query:

```
Compiling a query which loads related collections for more than one collection navigation, either via 'Include'
or through projection, but no 'QuerySplittingBehavior' has been configured. ...
```

## After (AsNoTracking + Select projection + filtering/sorting/paging in SQL)

```
dotnet run ListEndpointsBenchmark.cs -- http://api:8080 100
```

| Endpoint | Total rows | Mean (ms) | p50 (ms) | p95 (ms) | p99 (ms) | Throughput @10 clients (req/s) |
|---|---|---|---|---|---|---|
| GET /api/tasks (admin, page 1 of all tasks) | 20000 | 77.7 | 51.5 | 187.3 | 275.4 | 38 |
| GET /api/tasks (admin, status + sort by due date) | 4964 | 15.8 | 13.4 | 28.8 | 31.6 | 143 |
| GET /api/tasks (member, only own projects) | 3389 | 24.7 | 23.6 | 31.2 | 35.0 | 89 |
| GET /api/projects (admin) | 30 | 16.0 | 10.8 | 35.2 | 42.7 | 123 |

## Summary

| Endpoint | Mean | p95 |
|---|---|---|
| Tasks, admin, all 20k | -85% | -73% |
| Tasks, status filter + due date sort | -96% | -95% |
| Tasks, member view | -74% | -80% |
| Projects list | -99.7% | -99.6% |

These are numbers from one laptop, so the absolute milliseconds will be different on other machines. The relative difference is the interesting part.
