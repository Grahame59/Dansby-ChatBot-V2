// Infrastructure/ServiceCollectionExtensions.cs
// This script auto-registers all handlers (This will help keep Program.cs smaller)

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Dansby.Shared;

namespace Dansby.Core.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAllIntentHandlersFrom(
        this IServiceCollection services, params Assembly[] asms)
    {
        var handlerType = typeof(IIntentHandler); // Referencing the interface in shared
        var manualAttr  = typeof(ManualRegistrationAttribute);

        foreach (var asm in asms)
        foreach (var type in asm.GetTypes().Where(x => handlerType.IsAssignableFrom(x) && !x.IsAbstract && !x.IsInterface))
        {
            if (type.GetCustomAttribute(manualAttr) != null)
                continue; // skip: we’ll register these manually (e.g., ReplyHandler instances)

            services.AddSingleton(typeof(IIntentHandler), type);
        }
        return services;
    }
}