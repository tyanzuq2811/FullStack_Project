using FluentValidation;
using IPM.Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using IPM.Application.Services;

namespace IPM.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Validators
        services.AddValidatorsFromAssemblyContaining<IAuthService>();

        // AutoMapper
        services.AddAutoMapper(typeof(DependencyInjection).Assembly);

        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IAiAnalysisService, AiAnalysisService>();
        services.AddScoped<INewsService, NewsService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IResourceService, ResourceService>();

        return services;
    }
}
