using LSCode.Application.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LSCode.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddLSCodeApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<ExecuteStudioCommand>());
        return services;
    }
}
