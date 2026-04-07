using System;
using System.Text.Json.Nodes;
using System.Threading;

namespace Soenneker.OpenApi.Converter;

public sealed partial class OpenApiConverter
{
    private JsonObject? ConvertSecuritySchemes(JsonNode? securityDefinitionsNode, CancellationToken cancellationToken)
    {
        if (securityDefinitionsNode is not JsonObject securityDefinitionsObject)
            return null;

        var target = new JsonObject();

        foreach ((string schemeName, JsonNode? schemeNode) in securityDefinitionsObject)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (schemeNode is not JsonObject schemeObject)
                continue;

            target[schemeName] = ConvertSecurityScheme(schemeObject);
        }

        return target.Count == 0 ? null : target;
    }

    private JsonObject ConvertSecurityScheme(JsonObject schemeObject)
    {
        string type = GetStringValueOrDefault(schemeObject, "type", string.Empty);
        var target = new JsonObject();

        if (TryGetStringValue(schemeObject, "description", out string? description))
            target["description"] = description;

        if (string.Equals(type, "basic", StringComparison.Ordinal))
        {
            target["type"] = "http";
            target["scheme"] = "basic";
        }
        else if (string.Equals(type, "apiKey", StringComparison.Ordinal))
        {
            target["type"] = "apiKey";
            CopyFieldIfPresent(schemeObject, target, "name");
            CopyFieldIfPresent(schemeObject, target, "in");
        }
        else if (string.Equals(type, "oauth2", StringComparison.Ordinal))
        {
            target["type"] = "oauth2";
            target["flows"] = ConvertOAuthFlows(schemeObject);
        }
        else
        {
            target["type"] = type;
        }

        CopyVendorExtensions(schemeObject, target);

        return target;
    }

    private JsonObject ConvertOAuthFlows(JsonObject schemeObject)
    {
        var flows = new JsonObject();
        string swaggerFlow = GetStringValueOrDefault(schemeObject, "flow", string.Empty);
        string? openApiFlowName = swaggerFlow switch
        {
            "implicit" => "implicit",
            "password" => "password",
            "application" => "clientCredentials",
            "accessCode" => "authorizationCode",
            _ => null
        };

        if (openApiFlowName == null)
            return flows;

        var flowObject = new JsonObject();

        if (string.Equals(openApiFlowName, "implicit", StringComparison.Ordinal) ||
            string.Equals(openApiFlowName, "authorizationCode", StringComparison.Ordinal))
        {
            CopyFieldIfPresent(schemeObject, flowObject, "authorizationUrl");
        }

        if (string.Equals(openApiFlowName, "password", StringComparison.Ordinal) ||
            string.Equals(openApiFlowName, "clientCredentials", StringComparison.Ordinal) ||
            string.Equals(openApiFlowName, "authorizationCode", StringComparison.Ordinal))
        {
            CopyFieldIfPresent(schemeObject, flowObject, "tokenUrl");
        }

        if (TryGetJsonObject(schemeObject, "scopes", out JsonObject? scopesObject))
            flowObject["scopes"] = DeepCloneNode(scopesObject);
        else
            flowObject["scopes"] = new JsonObject();

        flows[openApiFlowName] = flowObject;
        return flows;
    }
}
