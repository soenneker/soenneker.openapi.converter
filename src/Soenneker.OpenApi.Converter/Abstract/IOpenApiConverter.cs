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
    ValueTask<string> Convert(string swaggerJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a Swagger 2.0 JSON file into an OpenAPI 3 JSON file, writes it to <paramref name="targetPath"/>,
    /// and returns the converted payload.
    /// </summary>
    ValueTask<string> ConvertFile(string sourcePath, string targetPath, CancellationToken cancellationToken = default);
}
