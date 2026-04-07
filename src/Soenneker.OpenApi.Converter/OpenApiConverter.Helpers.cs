using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Soenneker.OpenApi.Converter;

public sealed partial class OpenApiConverter
{
    private string[] ResolveEffectiveConsumesValues(JsonObject sourceRoot, JsonObject? pathItem, JsonObject? operationObject)
    {
        return ResolveEffectiveMediaTypes("consumes", sourceRoot, pathItem, operationObject);
    }

    private string[] ResolveEffectiveProducesValues(JsonObject sourceRoot, JsonObject? pathItem, JsonObject? operationObject)
    {
        return ResolveEffectiveMediaTypes("produces", sourceRoot, pathItem, operationObject);
    }

    private string[] ResolveEffectiveMediaTypes(string propertyName, JsonObject sourceRoot, JsonObject? pathItem, JsonObject? operationObject)
    {
        string[] operationValues = ReadMediaTypes(operationObject, propertyName);

        if (operationValues.Length > 0)
            return operationValues;

        string[] pathValues = ReadMediaTypes(pathItem, propertyName);

        if (pathValues.Length > 0)
            return pathValues;

        return ReadMediaTypes(sourceRoot, propertyName);
    }

    private JsonObject CreateContentObject(JsonNode? schemaNode, string[] mediaTypes)
    {
        var content = new JsonObject();

        for (int i = 0; i < mediaTypes.Length; i++)
        {
            string mediaType = mediaTypes[i];

            if (string.IsNullOrWhiteSpace(mediaType))
                continue;

            var mediaTypeObject = new JsonObject();

            if (schemaNode != null)
                mediaTypeObject["schema"] = DeepCloneNode(schemaNode);

            content[mediaType] = mediaTypeObject;
        }

        return content;
    }

    private static void ApplyBodyParameterExamples(JsonObject content, JsonObject bodyParameter)
    {
        if (content.Count == 0)
            return;

        if (TryGetJsonObjectValue(bodyParameter, "examples") is JsonObject examplesObject && examplesObject.Count > 0)
        {
            foreach ((string mediaType, JsonNode? exampleNode) in examplesObject)
            {
                if (exampleNode == null)
                    continue;

                if (!content.TryGetPropertyValue(mediaType, out JsonNode? mediaTypeNode) || mediaTypeNode is not JsonObject mediaTypeObject)
                    continue;

                mediaTypeObject["example"] = DeepCloneNode(exampleNode);
            }

            return;
        }

        if (!bodyParameter.TryGetPropertyValue("example", out JsonNode? exampleValue) || exampleValue == null)
            return;

        foreach ((_, JsonNode? mediaTypeNode) in content)
        {
            if (mediaTypeNode is JsonObject mediaTypeObject)
                mediaTypeObject["example"] = DeepCloneNode(exampleValue);
        }
    }

    private static JsonNode? DeepCloneNode(JsonNode? node)
    {
        return node?.DeepClone();
    }

    private static void CopyFieldIfPresent(JsonObject source, JsonObject target, string propertyName)
    {
        if (!source.TryGetPropertyValue(propertyName, out JsonNode? node) || node == null)
            return;

        target[propertyName] = DeepCloneNode(node);
    }

    private static void CopyExampleFields(JsonObject source, JsonObject target)
    {
        CopyFieldIfPresent(source, target, "example");
        CopyFieldIfPresent(source, target, "examples");
    }

    private static void CopyVendorExtensions(JsonObject source, JsonObject target)
    {
        foreach ((string propertyName, JsonNode? node) in source)
        {
            if (!propertyName.StartsWith("x-", StringComparison.Ordinal) || node == null)
                continue;

            target[propertyName] = DeepCloneNode(node);
        }
    }

    private static string[] ResolveServerSchemes(JsonObject sourceRoot)
    {
        string[] schemes = ReadMediaTypes(sourceRoot, "schemes");

        if (schemes.Length > 0)
            return schemes;

        return ["https"];
    }

    private static string BuildServerUrl(string scheme, string host, string basePath)
    {
        string normalizedScheme = scheme.Trim().TrimEnd(':');
        string normalizedHost = host.Trim().TrimEnd('/');
        string normalizedBasePath = NormalizeBasePath(basePath);
        return $"{normalizedScheme}://{normalizedHost}{normalizedBasePath}";
    }

    private static string NormalizeBasePath(string? basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath) || string.Equals(basePath, "/", StringComparison.Ordinal))
            return string.Empty;

        string trimmed = basePath.Trim();

        if (!trimmed.StartsWith("/", StringComparison.Ordinal))
            trimmed = "/" + trimmed;

        return trimmed.TrimEnd('/');
    }

    private static string[] ReadMediaTypes(JsonObject? sourceObject, string propertyName)
    {
        if (sourceObject == null || !sourceObject.TryGetPropertyValue(propertyName, out JsonNode? node) || node is not JsonArray jsonArray)
            return [];

        var values = new List<string>(jsonArray.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < jsonArray.Count; i++)
        {
            if (jsonArray[i] is not JsonValue jsonValue || !jsonValue.TryGetValue(out string? value) || string.IsNullOrWhiteSpace(value))
                continue;

            string normalized = value.Trim();

            if (seen.Add(normalized))
                values.Add(normalized);
        }

        return values.ToArray();
    }

    private static bool TryGetJsonObject(JsonObject source, string propertyName, out JsonObject? value)
    {
        value = null;

        if (!source.TryGetPropertyValue(propertyName, out JsonNode? node) || node is not JsonObject jsonObject)
            return false;

        value = jsonObject;
        return true;
    }

    private static bool TryGetStringValue(JsonObject source, string propertyName, out string? value)
    {
        value = null;

        if (!source.TryGetPropertyValue(propertyName, out JsonNode? node) ||
            node is not JsonValue jsonValue ||
            !jsonValue.TryGetValue(out string? stringValue) ||
            string.IsNullOrWhiteSpace(stringValue))
        {
            return false;
        }

        value = stringValue;
        return true;
    }

    private static bool TryGetSwaggerVersion(JsonObject source, out string? value)
    {
        value = null;

        if (!source.TryGetPropertyValue("swagger", out JsonNode? node) || node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue(out string? stringValue) && !string.IsNullOrWhiteSpace(stringValue))
        {
            value = stringValue.Trim();
            return true;
        }

        if (jsonValue.TryGetValue(out int intValue))
        {
            value = intValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue(out long longValue))
        {
            value = longValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue(out decimal decimalValue))
        {
            value = decimalValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        if (jsonValue.TryGetValue(out double doubleValue))
        {
            value = doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private static bool IsSwagger2Version(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return string.Equals(value, "2.0", StringComparison.Ordinal) ||
               string.Equals(value, "2", StringComparison.Ordinal);
    }

    private static bool TryGetBooleanValue(JsonObject source, string propertyName, out bool value)
    {
        value = false;

        if (!source.TryGetPropertyValue(propertyName, out JsonNode? node) ||
            node is not JsonValue jsonValue ||
            !jsonValue.TryGetValue(out bool boolValue))
        {
            return false;
        }

        value = boolValue;
        return true;
    }

    private static string GetStringValueOrDefault(JsonObject source, string propertyName, string defaultValue)
    {
        return TryGetStringValue(source, propertyName, out string? value) ? value! : defaultValue;
    }

    private static bool TryGetRefValue(JsonObject source, out string? reference)
    {
        reference = null;

        if (!source.TryGetPropertyValue("$ref", out JsonNode? node) ||
            node is not JsonValue jsonValue ||
            !jsonValue.TryGetValue(out string? stringValue) ||
            string.IsNullOrWhiteSpace(stringValue))
        {
            return false;
        }

        reference = stringValue;
        return true;
    }

    private static bool IsPureRefObject(JsonObject source)
    {
        return source.Count == 1 && TryGetRefValue(source, out _);
    }

    private static bool TryGetTopLevelParameterName(string reference, out string? parameterName)
    {
        parameterName = null;

        const string prefix = "#/parameters/";

        if (!reference.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        string remainder = reference[prefix.Length..];
        int slashIndex = remainder.IndexOf('/', StringComparison.Ordinal);

        parameterName = slashIndex >= 0 ? remainder[..slashIndex] : remainder;
        return !string.IsNullOrWhiteSpace(parameterName);
    }

    private static string EncodeJsonPointerSegment(string value)
    {
        return value.Replace("~", "~0", StringComparison.Ordinal)
                    .Replace("/", "~1", StringComparison.Ordinal);
    }

    private static JsonObject? TryGetJsonObjectValue(JsonObject source, string propertyName)
    {
        return source.TryGetPropertyValue(propertyName, out JsonNode? node) && node is JsonObject jsonObject ? jsonObject : null;
    }

    private static string DetermineFormDataMediaType(string[] consumes, bool containsFile)
    {
        for (int i = 0; i < consumes.Length; i++)
        {
            string mediaType = consumes[i];

            if (string.Equals(mediaType, _multipartMediaType, StringComparison.OrdinalIgnoreCase))
                return _multipartMediaType;

            if (string.Equals(mediaType, _urlEncodedMediaType, StringComparison.OrdinalIgnoreCase) && !containsFile)
                return _urlEncodedMediaType;
        }

        return containsFile ? _multipartMediaType : _urlEncodedMediaType;
    }

    private static void ApplyParameterCollectionFormat(JsonObject sourceParameter, JsonObject targetParameter)
    {
        if (!TryGetStringValue(sourceParameter, "type", out string? type) ||
            !string.Equals(type, "array", StringComparison.Ordinal))
        {
            return;
        }

        string location = GetStringValueOrDefault(sourceParameter, "in", string.Empty);
        string collectionFormat = GetStringValueOrDefault(sourceParameter, "collectionFormat", string.Empty);

        if (string.Equals(location, "query", StringComparison.Ordinal))
        {
            if (string.Equals(collectionFormat, "multi", StringComparison.Ordinal))
            {
                targetParameter["style"] = "form";
                targetParameter["explode"] = true;
                return;
            }

            if (string.Equals(collectionFormat, "ssv", StringComparison.Ordinal))
            {
                targetParameter["style"] = "spaceDelimited";
                return;
            }

            if (string.Equals(collectionFormat, "pipes", StringComparison.Ordinal))
            {
                targetParameter["style"] = "pipeDelimited";
                return;
            }

            targetParameter["style"] = "form";
            targetParameter["explode"] = false;
            return;
        }

        if (string.Equals(location, "path", StringComparison.Ordinal) ||
            string.Equals(location, "header", StringComparison.Ordinal))
        {
            targetParameter["style"] = "simple";
            targetParameter["explode"] = false;
        }
    }
}
