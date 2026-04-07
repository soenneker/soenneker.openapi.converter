using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.OpenApi.Converter.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Extensions.String;

namespace Soenneker.OpenApi.Converter;

/// <inheritdoc cref="IOpenApiConverter"/>
public sealed partial class OpenApiConverter : IOpenApiConverter
{
    private const string _defaultBodyMediaType = "application/json";
    private const string _multipartMediaType = "multipart/form-data";
    private const string _urlEncodedMediaType = "application/x-www-form-urlencoded";

    private static readonly string[] _httpMethods = ["get", "put", "post", "delete", "options", "head", "patch"];

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly IFileUtil _fileUtil;

    public OpenApiConverter(IFileUtil fileUtil)
    {
        _fileUtil = fileUtil;
    }

    public async ValueTask<string> Convert(string swaggerJson, CancellationToken cancellationToken = default)
    {
        if (swaggerJson is null)
            throw new ArgumentNullException(nameof(swaggerJson));

        swaggerJson.ThrowIfNullOrWhiteSpace();

        cancellationToken.ThrowIfCancellationRequested();

        JsonObject sourceRoot = ParseSwaggerJson(swaggerJson);
        ValidateSwagger2Document(sourceRoot);

        JsonObject targetRoot = BuildRootOpenApiDocument(sourceRoot, cancellationToken);
        RewriteRefsRecursively(targetRoot);

        OpenApiDocument document = await BuildOpenApiDocument(targetRoot)
            .NoSync();
        return await SerializeOpenApiDocument(document)
            .NoSync();
    }

    public async ValueTask<string> ConvertFile(string sourcePath, string targetPath, CancellationToken cancellationToken = default)
    {
        sourcePath.ThrowIfNullOrWhiteSpace();
        targetPath.ThrowIfNullOrWhiteSpace();

        cancellationToken.ThrowIfCancellationRequested();

        string swaggerJson = await _fileUtil.Read(sourcePath, cancellationToken: cancellationToken)
                                            .NoSync();
        string convertedJson = await Convert(swaggerJson, cancellationToken)
            .NoSync();

        if (convertedJson.IsNullOrWhiteSpace())
            throw new InvalidOperationException("Conversion completed without producing OpenAPI 3 JSON.");

        await _fileUtil.Write(targetPath, convertedJson, cancellationToken: cancellationToken)
                       .NoSync();

        return convertedJson;
    }

    private static JsonObject ParseSwaggerJson(string swaggerJson)
    {
        try
        {
            JsonNode? node = JsonNode.Parse(swaggerJson);

            if (node is not JsonObject jsonObject)
                throw new InvalidOperationException("Swagger JSON must contain a JSON object as the root document.");

            return jsonObject;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Swagger JSON is invalid.", ex);
        }
    }

    private static void ValidateSwagger2Document(JsonObject sourceRoot)
    {
        if (!TryGetSwaggerVersion(sourceRoot, out string? swaggerVersion))
            throw new InvalidOperationException("Swagger JSON is missing the required 'swagger' field.");

        if (!IsSwagger2Version(swaggerVersion))
            throw new NotSupportedException($"Only Swagger 2.0 documents are supported. Found '{swaggerVersion}'.");
    }

    private static async ValueTask<OpenApiDocument> BuildOpenApiDocument(JsonObject targetRoot)
    {
        string json = targetRoot.ToJsonString(_jsonSerializerOptions);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        ReadResult readResult = await OpenApiDocument.LoadAsync(stream, OpenApiConstants.Json, new OpenApiReaderSettings())
                                                     .NoSync();

        if (readResult.Document == null)
            throw new InvalidOperationException("Failed to build an OpenAPI 3 document from the converted JSON.");

        return readResult.Document;
    }

    private static async ValueTask<string> SerializeOpenApiDocument(OpenApiDocument document)
    {
        await using var stringWriter = new StringWriter(new StringBuilder(4096));
        var writer = new OpenApiJsonWriter(stringWriter);
        document.SerializeAsV3(writer);

        return stringWriter.ToString();
    }
}