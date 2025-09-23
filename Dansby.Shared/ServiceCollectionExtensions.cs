// ServiceCollectionExtensions.cs
// This script auto-registers all handlers (This will help keep Program.cs smaller)

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dansby.Shared;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAllIntentHandlersFrom(this IServiceCollection services, params Assembly[] asms)
    {
        var t = typeof(IIntentHandler);
        foreach (var asm in asms)
        foreach (var type in asm.GetTypes().Where(x => t.IsAssignableFrom(x) && !x.IsAbstract && !x.IsInterface))
            services.AddSingleton(typeof(IIntentHandler), type);
        return services;
    }
}
