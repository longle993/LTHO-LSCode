using LSCode.Application;
using Microsoft.Extensions.DependencyInjection;

namespace LSCode.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLSCodeInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentWorkspace, DocumentWorkspace>();
        services.AddSingleton<IDeveloperTools, DeveloperTools>();
        return services;
    }
}
