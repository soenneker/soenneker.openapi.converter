using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.OpenApi.Converter.Abstract;
using Soenneker.Utils.File.Registrars;
using Soenneker.Utils.MemoryStream.Registrars;

namespace Soenneker.OpenApi.Converter.Registrars;

/// <summary>
/// Registers the Swagger 2.0 to OpenAPI 3.0 converter.
/// </summary>
public static class OpenApiConverterRegistrar
{
    /// <summary>
    /// Adds <see cref="IOpenApiConverter"/> as a singleton service. <para/>
    /// </summary>
    /// <param name="services">Service collection that receives the registration.</param>
    /// <returns>The same service collection, so additional registrations can be chained.</returns>
    public static IServiceCollection AddOpenApiConverterAsSingleton(this IServiceCollection services)
    {
        services.AddFileUtilAsSingleton();
        services.AddMemoryStreamUtilAsSingleton();
        services.TryAddSingleton<IOpenApiConverter, OpenApiConverter>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="IOpenApiConverter"/> as a scoped service. <para/>
    /// </summary>
    /// <param name="services">Service collection that receives the registration.</param>
    /// <returns>The same service collection, so additional registrations can be chained.</returns>
    public static IServiceCollection AddOpenApiConverterAsScoped(this IServiceCollection services)
    {
        services.AddFileUtilAsScoped();
        services.AddMemoryStreamUtilAsScoped();
        services.TryAddScoped<IOpenApiConverter, OpenApiConverter>();

        return services;
    }
}
