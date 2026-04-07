using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;

namespace Soenneker.OpenApi.Converter;

public sealed partial class OpenApiConverter
{
    private JsonNode? ConvertSchemaNode(JsonNode? schemaNode, CancellationToken cancellationToken)
    {
        if (schemaNode == null)
            return null;

        JsonNode? clone = DeepCloneNode(schemaNode);
        WalkSchemaRecursively(clone, cancellationToken);
        return clone;
    }

    private void WalkSchemaRecursively(JsonNode? schemaNode, CancellationToken cancellationToken)
    {
        if (schemaNode == null)
            return;

        cancellationToken.ThrowIfCancellationRequested();

        if (schemaNode is JsonObject schemaObject)
        {
            if (TryGetRefValue(schemaObject, out string? reference))
                schemaObject["$ref"] = RewriteRefString(reference!);

            UpgradeSchemaDiscriminator(schemaObject);

            if (TryGetStringValue(schemaObject, "type", out string? type) &&
                string.Equals(type, "file", StringComparison.Ordinal))
            {
                schemaObject["type"] = "string";
                schemaObject["format"] = "binary";
            }

            if (TryGetJsonObject(schemaObject, "properties", out JsonObject? propertiesObject))
            {
                foreach ((_, JsonNode? propertySchema) in propertiesObject!)
                {
                    WalkSchemaRecursively(propertySchema, cancellationToken);
                }
            }

            WalkSchemaProperty(schemaObject, "items", cancellationToken);
            WalkSchemaProperty(schemaObject, "additionalProperties", cancellationToken);
            WalkSchemaProperty(schemaObject, "not", cancellationToken);
            WalkSchemaArray(schemaObject, "allOf", cancellationToken);
            WalkSchemaArray(schemaObject, "oneOf", cancellationToken);
            WalkSchemaArray(schemaObject, "anyOf", cancellationToken);
        }
        else if (schemaNode is JsonArray schemaArray)
        {
            for (int i = 0; i < schemaArray.Count; i++)
            {
                WalkSchemaRecursively(schemaArray[i], cancellationToken);
            }
        }
    }

    private void WalkSchemaProperty(JsonObject schemaObject, string propertyName, CancellationToken cancellationToken)
    {
        if (schemaObject.TryGetPropertyValue(propertyName, out JsonNode? node))
            WalkSchemaRecursively(node, cancellationToken);
    }

    private void WalkSchemaArray(JsonObject schemaObject, string propertyName, CancellationToken cancellationToken)
    {
        if (!schemaObject.TryGetPropertyValue(propertyName, out JsonNode? arrayNode) || arrayNode is not JsonArray schemaArray)
            return;

        for (int i = 0; i < schemaArray.Count; i++)
        {
            WalkSchemaRecursively(schemaArray[i], cancellationToken);
        }
    }

    private JsonNode? BuildSchemaFromTypeFields(JsonObject source, bool includeDescription, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var schema = new JsonObject();

        if (TryGetStringValue(source, "type", out string? type))
        {
            if (string.Equals(type, "file", StringComparison.Ordinal))
            {
                schema["type"] = "string";
                schema["format"] = "binary";
            }
            else
            {
                schema["type"] = type;
                CopyFieldIfPresent(source, schema, "format");
            }
        }
        else
        {
            CopyFieldIfPresent(source, schema, "format");
        }

        if (includeDescription)
            CopyFieldIfPresent(source, schema, "description");

        CopyFieldIfPresent(source, schema, "enum");
        CopyFieldIfPresent(source, schema, "default");
        CopyFieldIfPresent(source, schema, "minimum");
        CopyFieldIfPresent(source, schema, "maximum");
        CopyFieldIfPresent(source, schema, "exclusiveMinimum");
        CopyFieldIfPresent(source, schema, "exclusiveMaximum");
        CopyFieldIfPresent(source, schema, "minLength");
        CopyFieldIfPresent(source, schema, "maxLength");
        CopyFieldIfPresent(source, schema, "pattern");
        CopyFieldIfPresent(source, schema, "minItems");
        CopyFieldIfPresent(source, schema, "maxItems");
        CopyFieldIfPresent(source, schema, "uniqueItems");
        CopyFieldIfPresent(source, schema, "multipleOf");
        CopyFieldIfPresent(source, schema, "readOnly");
        CopyFieldIfPresent(source, schema, "writeOnly");

        if (source["items"] != null)
            schema["items"] = ConvertSchemaNode(source["items"], cancellationToken);

        if (source["additionalProperties"] != null)
            schema["additionalProperties"] = ConvertSchemaNode(source["additionalProperties"], cancellationToken);

        if (source["allOf"] != null)
            schema["allOf"] = ConvertSchemaNode(source["allOf"], cancellationToken);

        CopyVendorExtensions(source, schema);

        return schema.Count == 0 ? null : schema;
    }

    private (JsonObject Schema, bool ContainsFile) BuildFormDataSchema(List<ParameterContext> formDataParameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var schema = new JsonObject
        {
            ["type"] = "object"
        };

        var properties = new JsonObject();
        var required = new JsonArray();
        var requiredNames = new HashSet<string>(StringComparer.Ordinal);
        bool containsFile = false;

        for (int i = 0; i < formDataParameters.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            JsonObject parameter = formDataParameters[i].Effective;

            if (!TryGetStringValue(parameter, "name", out string? propertyName))
                continue;

            JsonNode? propertySchema = BuildSchemaFromTypeFields(parameter, includeDescription: true, cancellationToken) ?? new JsonObject();
            properties[propertyName!] = propertySchema;

            if (TryGetBooleanValue(parameter, "required", out bool isRequired) && isRequired && requiredNames.Add(propertyName!))
                required.Add(propertyName);

            if (TryGetStringValue(parameter, "type", out string? type) &&
                string.Equals(type, "file", StringComparison.Ordinal))
            {
                containsFile = true;
            }
        }

        schema["properties"] = properties;

        if (required.Count > 0)
            schema["required"] = required;

        return (schema, containsFile);
    }

    private static void UpgradeSchemaDiscriminator(JsonObject schemaObject)
    {
        if (!schemaObject.TryGetPropertyValue("discriminator", out JsonNode? discriminatorNode) || discriminatorNode == null)
            return;

        if (discriminatorNode is JsonObject)
            return;

        if (discriminatorNode is JsonValue discriminatorValue &&
            discriminatorValue.TryGetValue(out string? propertyName) &&
            !string.IsNullOrWhiteSpace(propertyName))
        {
            schemaObject["discriminator"] = new JsonObject
            {
                ["propertyName"] = propertyName
            };
        }
    }

    private static bool SchemaHasRequiredProperties(JsonObject schema)
    {
        return schema.TryGetPropertyValue("required", out JsonNode? requiredNode) &&
               requiredNode is JsonArray requiredArray &&
               requiredArray.Count > 0;
    }
}
