# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# copy only the project files first so "dotnet restore" is cached
# and doesn't run again every time a .cs file changes
COPY src/TaskManagement.Domain/TaskManagement.Domain.csproj src/TaskManagement.Domain/
COPY src/TaskManagement.Application/TaskManagement.Application.csproj src/TaskManagement.Application/
COPY src/TaskManagement.Infrastructure/TaskManagement.Infrastructure.csproj src/TaskManagement.Infrastructure/
COPY src/TaskManagement.Api/TaskManagement.Api.csproj src/TaskManagement.Api/
RUN dotnet restore src/TaskManagement.Api/TaskManagement.Api.csproj

COPY src/ src/
RUN dotnet publish src/TaskManagement.Api/TaskManagement.Api.csproj -c Release -o /app/publish --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# the aspnet image listens on 8080 by default and has a non-root "app" user
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# serilog writes json log files to /app/logs
RUN mkdir -p /app/logs && chown app /app/logs
USER app

ENTRYPOINT ["dotnet", "TaskManagement.Api.dll"]
