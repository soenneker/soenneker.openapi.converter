using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.OpenApi.Converter.Abstract;

/// <summary>
/// Converts Swagger 2.0 JSON documents to OpenAPI 3.0 JSON.
/// </summary>
public interface IOpenApiConverter
{
    /// <summary>
    /// Converts Swagger 2.0 JSON into OpenAPI 3 JSON and returns the converted payload.
    /// </summary>
    /// <param name="swaggerJson">The complete Swagger 2.0 JSON document.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The converted OpenAPI 3.0 JSON.</returns>
    ValueTask<string> Convert(string swaggerJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a Swagger 2.0 JSON file into an OpenAPI 3 JSON file, writes it to <paramref name="targetPath"/>,
    /// and returns the converted payload.
    /// </summary>
    /// <param name="sourcePath">The Swagger 2.0 JSON file to read.</param>
    /// <param name="targetPath">The file to which the OpenAPI 3.0 JSON is written.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The converted OpenAPI 3.0 JSON written to <paramref name="targetPath"/>.</returns>
    ValueTask<string> ConvertFile(string sourcePath, string targetPath, CancellationToken cancellationToken = default);
}
