using System;
using System.Text.Json.Nodes;

namespace Soenneker.OpenApi.Converter;

public sealed partial class OpenApiConverter
{
    private void RewriteRefsRecursively(JsonNode? node)
    {
        if (node == null)
            return;

        if (node is JsonObject jsonObject)
        {
            foreach ((string propertyName, JsonNode? childNode) in jsonObject)
            {
                if (string.Equals(propertyName, "$ref", StringComparison.Ordinal) &&
                    childNode is JsonValue jsonValue &&
                    jsonValue.TryGetValue(out string? reference) &&
                    !string.IsNullOrWhiteSpace(reference))
                {
                    jsonObject[propertyName] = RewriteRefString(reference!);
                    continue;
                }

                RewriteRefsRecursively(childNode);
            }
        }
        else if (node is JsonArray jsonArray)
        {
            for (int i = 0; i < jsonArray.Count; i++)
            {
                RewriteRefsRecursively(jsonArray[i]);
            }
        }
    }

    private string RewriteRefString(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("#/", StringComparison.Ordinal))
            return value;

        if (value.StartsWith("#/definitions/", StringComparison.Ordinal))
            return "#/components/schemas/" + value["#/definitions/".Length..];

        if (value.StartsWith("#/parameters/", StringComparison.Ordinal))
            return "#/components/parameters/" + value["#/parameters/".Length..];

        if (value.StartsWith("#/responses/", StringComparison.Ordinal))
            return "#/components/responses/" + value["#/responses/".Length..];

        if (value.StartsWith("#/securityDefinitions/", StringComparison.Ordinal))
            return "#/components/securitySchemes/" + value["#/securityDefinitions/".Length..];

        return value;
    }

    private string RewriteRequestBodyRefString(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("#/", StringComparison.Ordinal))
            return value;

        if (value.StartsWith("#/parameters/", StringComparison.Ordinal))
            return "#/components/requestBodies/" + value["#/parameters/".Length..];

        return RewriteRefString(value);
    }
}
