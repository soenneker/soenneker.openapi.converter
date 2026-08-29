using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.OpenApi.Converter.Abstract;

/// <summary>
/// A .NET converter for OpenAPI 2 (Swagger) to OpenAPI 3
/// </summary>
public interface IOpenApiConverter
{
    /// <summary>
    /// Converts Swagger 2.0 JSON into OpenAPI 3 JSON and returns the converted payload.
    /// </summary>
    /// <param name="swaggerJson">Swagger JSON for the convert operation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the text returned by convert.</returns>
    ValueTask<string> Convert(string swaggerJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a Swagger 2.0 JSON file into an OpenAPI 3 JSON file, writes it to <paramref name="targetPath"/>,
    /// and returns the converted payload.
    /// </summary>
    /// <param name="sourcePath">Path of the source to use.</param>
    /// <param name="targetPath">Path of the target to use.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the text returned by convert File.</returns>
    ValueTask<string> ConvertFile(string sourcePath, string targetPath, CancellationToken cancellationToken = default);
}
