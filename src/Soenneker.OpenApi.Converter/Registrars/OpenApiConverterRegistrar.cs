using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.OpenApi.Converter.Abstract;
using Soenneker.Utils.File.Registrars;

namespace Soenneker.OpenApi.Converter.Registrars;

/// <summary>
/// A .NET converter for OpenAPI 2 (Swagger) to OpenAPI 3
/// </summary>
public static class OpenApiConverterRegistrar
{
    /// <summary>
    /// Adds <see cref="IOpenApiConverter"/> as a singleton service. <para/>
    /// </summary>
    public static IServiceCollection AddOpenApiConverterAsSingleton(this IServiceCollection services)
    {
        services.AddFileUtilAsSingleton();
        services.TryAddSingleton<IOpenApiConverter, OpenApiConverter>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="IOpenApiConverter"/> as a scoped service. <para/>
    /// </summary>
    public static IServiceCollection AddOpenApiConverterAsScoped(this IServiceCollection services)
    {
        services.AddFileUtilAsScoped();
        services.TryAddScoped<IOpenApiConverter, OpenApiConverter>();

        return services;
    }
}
