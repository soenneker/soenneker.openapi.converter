using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;

namespace Soenneker.OpenApi.Converter;

public sealed partial class OpenApiConverter
{
    private JsonObject ConvertResponses(JsonObject responsesObject, string[] produces, CancellationToken cancellationToken)
    {
        var target = new JsonObject();

        foreach ((string responseCode, JsonNode? responseNode) in responsesObject)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (responseNode is not JsonObject responseObject)
                continue;

            target[responseCode] = ConvertResponse(responseObject, produces, cancellationToken);
        }

        return target;
    }

    private JsonObject ConvertResponse(JsonObject responseObject, string[] produces, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsPureRefObject(responseObject) && TryGetRefValue(responseObject, out string? reference))
            return new JsonObject { ["$ref"] = RewriteRefString(reference!) };

        var target = new JsonObject
        {
            ["description"] = GetStringValueOrDefault(responseObject, "description", string.Empty)
        };

        if (TryGetJsonObject(responseObject, "headers", out JsonObject? headersObject))
        {
            JsonObject headers = ConvertHeaders(headersObject!, cancellationToken);

            if (headers.Count > 0)
                target["headers"] = headers;
        }

        JsonNode? responseSchema = responseObject["schema"] != null
            ? ConvertSchemaNode(responseObject["schema"], cancellationToken)
            : null;
        JsonObject? examplesObject = TryGetJsonObjectValue(responseObject, "examples");
        JsonObject? content = CreateResponseContentObject(responseSchema, produces, examplesObject);

        if (content != null)
            target["content"] = content;

        CopyVendorExtensions(responseObject, target);

        return target;
    }

    private JsonObject ConvertHeaders(JsonObject headersObject, CancellationToken cancellationToken)
    {
        var target = new JsonObject();

        foreach ((string headerName, JsonNode? headerNode) in headersObject)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (headerNode is not JsonObject headerObject)
                continue;

            target[headerName] = ConvertHeader(headerObject, cancellationToken);
        }

        return target;
    }

    private JsonObject ConvertHeader(JsonObject headerObject, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsPureRefObject(headerObject) && TryGetRefValue(headerObject, out string? reference))
            return new JsonObject { ["$ref"] = RewriteRefString(reference!) };

        var target = new JsonObject();

        CopyFieldIfPresent(headerObject, target, "description");
        CopyFieldIfPresent(headerObject, target, "required");
        CopyFieldIfPresent(headerObject, target, "deprecated");
        CopyFieldIfPresent(headerObject, target, "allowEmptyValue");
        CopyExampleFields(headerObject, target);

        JsonNode? schemaNode = headerObject["schema"] != null
            ? ConvertSchemaNode(headerObject["schema"], cancellationToken)
            : BuildSchemaFromTypeFields(headerObject, includeDescription: false, cancellationToken);

        if (schemaNode != null)
            target["schema"] = schemaNode;

        CopyVendorExtensions(headerObject, target);

        return target;
    }

    private JsonObject? CreateResponseContentObject(JsonNode? schemaNode, string[] produces, JsonObject? examplesObject)
    {
        bool hasExamples = examplesObject != null && examplesObject.Count > 0;

        if (schemaNode == null && !hasExamples)
            return null;

        string[] mediaTypes = ResolveResponseMediaTypes(produces, examplesObject, schemaNode != null);

        if (mediaTypes.Length == 0)
            return null;

        JsonObject content = CreateContentObject(schemaNode, mediaTypes);
        ApplyResponseExamples(content, examplesObject);

        return content.Count == 0 ? null : content;
    }

    private static string[] ResolveResponseMediaTypes(string[] produces, JsonObject? examplesObject, bool hasSchema)
    {
        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < produces.Length; i++)
        {
            string mediaType = produces[i];

            if (!string.IsNullOrWhiteSpace(mediaType) && seen.Add(mediaType))
                values.Add(mediaType);
        }

        if (examplesObject != null)
        {
            foreach ((string mediaType, JsonNode? _) in examplesObject)
            {
                if (!string.IsNullOrWhiteSpace(mediaType) && seen.Add(mediaType))
                    values.Add(mediaType);
            }
        }

        if (values.Count == 0 && hasSchema)
            values.Add(_defaultBodyMediaType);

        return values.ToArray();
    }

    private static void ApplyResponseExamples(JsonObject content, JsonObject? examplesObject)
    {
        if (examplesObject == null || examplesObject.Count == 0)
            return;

        foreach ((string mediaType, JsonNode? exampleNode) in examplesObject)
        {
            if (exampleNode == null)
                continue;

            if (!content.TryGetPropertyValue(mediaType, out JsonNode? mediaTypeNode) || mediaTypeNode is not JsonObject mediaTypeObject)
                continue;

            mediaTypeObject["example"] = DeepCloneNode(exampleNode);
        }
    }
}
