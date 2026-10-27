using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Auth;
using TaskManagement.Application.Projects;
using TaskManagement.Application.Tasks;
using TaskManagement.Application.Users;

namespace TaskManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITaskService, TaskService>();

        // registers every AbstractValidator<T> in this project
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
