using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;

namespace Soenneker.OpenApi.Converter;

public sealed partial class OpenApiConverter
{
    private JsonObject BuildRootOpenApiDocument(JsonObject sourceRoot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetJsonObject(sourceRoot, "info", out JsonObject? infoObject))
            throw new InvalidOperationException("Swagger 2 document is missing the required 'info' object.");

        var targetRoot = new JsonObject
        {
            ["openapi"] = "3.0.3",
            ["info"] = DeepCloneNode(infoObject)
        };

        CopyFieldIfPresent(sourceRoot, targetRoot, "tags");
        CopyFieldIfPresent(sourceRoot, targetRoot, "externalDocs");
        CopyFieldIfPresent(sourceRoot, targetRoot, "security");
        CopyVendorExtensions(sourceRoot, targetRoot);

        JsonArray? servers = ConvertServers(sourceRoot);

        if (servers != null && servers.Count > 0)
            targetRoot["servers"] = servers;

        JsonObject? components = ConvertComponents(sourceRoot, cancellationToken);

        if (components != null && components.Count > 0)
            targetRoot["components"] = components;

        targetRoot["paths"] = ConvertPaths(sourceRoot, cancellationToken);

        return targetRoot;
    }

    private JsonArray? ConvertServers(JsonObject sourceRoot)
    {
        if (!TryGetStringValue(sourceRoot, "host", out string? host))
            return null;

        string[] schemes = ResolveServerSchemes(sourceRoot);
        string basePath = GetStringValueOrDefault(sourceRoot, "basePath", string.Empty);
        var servers = new JsonArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < schemes.Length; i++)
        {
            string scheme = schemes[i];

            if (string.IsNullOrWhiteSpace(scheme))
                continue;

            string url = BuildServerUrl(scheme, host!, basePath);

            if (seen.Add(url))
                servers.Add(new JsonObject { ["url"] = url });
        }

        return servers.Count == 0 ? null : servers;
    }

    private JsonObject? ConvertComponents(JsonObject sourceRoot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var components = new JsonObject();

        JsonObject? schemas = ConvertSchemaComponents(sourceRoot["definitions"], cancellationToken);

        if (schemas != null && schemas.Count > 0)
            components["schemas"] = schemas;

        JsonObject? parameters = ConvertParameterComponents(sourceRoot, cancellationToken);

        if (parameters != null && parameters.Count > 0)
            components["parameters"] = parameters;

        JsonObject? requestBodies = ConvertRequestBodyComponents(sourceRoot, cancellationToken);

        if (requestBodies != null && requestBodies.Count > 0)
            components["requestBodies"] = requestBodies;

        JsonObject? responses = ConvertResponseComponents(sourceRoot, cancellationToken);

        if (responses != null && responses.Count > 0)
            components["responses"] = responses;

        JsonObject? securitySchemes = ConvertSecuritySchemes(sourceRoot["securityDefinitions"], cancellationToken);

        if (securitySchemes != null && securitySchemes.Count > 0)
            components["securitySchemes"] = securitySchemes;

        return components.Count == 0 ? null : components;
    }

    private JsonObject? ConvertSchemaComponents(JsonNode? definitionsNode, CancellationToken cancellationToken)
    {
        if (definitionsNode is not JsonObject definitionsObject)
            return null;

        var target = new JsonObject();

        foreach ((string schemaName, JsonNode? schemaNode) in definitionsObject)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (schemaNode == null)
                continue;

            JsonNode? convertedSchema = ConvertSchemaNode(schemaNode, cancellationToken);

            if (convertedSchema != null)
                target[schemaName] = convertedSchema;
        }

        return target.Count == 0 ? null : target;
    }

    private JsonObject? ConvertParameterComponents(JsonObject sourceRoot, CancellationToken cancellationToken)
    {
        if (!TryGetJsonObject(sourceRoot, "parameters", out JsonObject? parametersObject))
            return null;

        var target = new JsonObject();

        foreach ((string parameterName, JsonNode? parameterNode) in parametersObject!)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (parameterNode is not JsonObject parameterObject)
                continue;

            JsonObject effectiveParameter = ResolveEffectiveParameter(sourceRoot, parameterObject);
            string location = GetParameterLocation(effectiveParameter);

            if (!IsNonBodyParameterLocation(location))
                continue;

            if (IsPureRefObject(parameterObject) && TryGetRefValue(parameterObject, out string? reference))
                target[parameterName] = new JsonObject { ["$ref"] = RewriteRefString(reference!) };
            else
                target[parameterName] = ConvertParameterObject(effectiveParameter, cancellationToken);
        }

        return target.Count == 0 ? null : target;
    }

    private JsonObject? ConvertRequestBodyComponents(JsonObject sourceRoot, CancellationToken cancellationToken)
    {
        if (!TryGetJsonObject(sourceRoot, "parameters", out JsonObject? parametersObject))
            return null;

        var target = new JsonObject();
        string[] globalConsumes = ResolveEffectiveConsumesValues(sourceRoot, null, null);

        foreach ((string parameterName, JsonNode? parameterNode) in parametersObject!)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (parameterNode is not JsonObject parameterObject)
                continue;

            JsonObject effectiveParameter = ResolveEffectiveParameter(sourceRoot, parameterObject);
            string location = GetParameterLocation(effectiveParameter);

            if (!string.Equals(location, "body", StringComparison.Ordinal) &&
                !string.Equals(location, "formData", StringComparison.Ordinal))
            {
                continue;
            }

            JsonObject? requestBody;

            if (IsPureRefObject(parameterObject) && TryGetRefValue(parameterObject, out string? reference))
            {
                requestBody = new JsonObject
                {
                    ["$ref"] = RewriteRequestBodyRefString(reference!)
                };
            }
            else
            {
                var parameterContext = new ParameterContext(parameterObject, effectiveParameter);
                requestBody = string.Equals(location, "body", StringComparison.Ordinal)
                    ? ConvertSingleBodyRequestBody(parameterContext, globalConsumes, cancellationToken)
                    : ConvertFormDataRequestBody([parameterContext], globalConsumes, cancellationToken);
            }

            if (requestBody != null)
                target[parameterName] = requestBody;
        }

        return target.Count == 0 ? null : target;
    }

    private JsonObject? ConvertResponseComponents(JsonObject sourceRoot, CancellationToken cancellationToken)
    {
        if (!TryGetJsonObject(sourceRoot, "responses", out JsonObject? responsesObject))
            return null;

        string[] produces = ResolveEffectiveProducesValues(sourceRoot, null, null);
        return ConvertResponses(responsesObject!, produces, cancellationToken);
    }

    private JsonObject ConvertPaths(JsonObject sourceRoot, CancellationToken cancellationToken)
    {
        var targetPaths = new JsonObject();

        if (!TryGetJsonObject(sourceRoot, "paths", out JsonObject? sourcePaths))
            return targetPaths;

        foreach ((string pathKey, JsonNode? pathNode) in sourcePaths!)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pathNode is not JsonObject sourcePathItem)
                continue;

            var targetPathItem = new JsonObject();

            CopyFieldIfPresent(sourcePathItem, targetPathItem, "summary");
            CopyFieldIfPresent(sourcePathItem, targetPathItem, "description");
            CopyVendorExtensions(sourcePathItem, targetPathItem);

            for (int i = 0; i < _httpMethods.Length; i++)
            {
                string method = _httpMethods[i];

                if (!sourcePathItem.TryGetPropertyValue(method, out JsonNode? operationNode) || operationNode is not JsonObject operationObject)
                    continue;

                targetPathItem[method] = ConvertOperation(sourceRoot, sourcePathItem, operationObject, cancellationToken);
            }

            if (targetPathItem.Count > 0)
                targetPaths[pathKey] = targetPathItem;
        }

        return targetPaths;
    }

    private JsonObject ConvertOperation(JsonObject sourceRoot, JsonObject pathItem, JsonObject operationObject, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targetOperation = new JsonObject();

        CopyFieldIfPresent(operationObject, targetOperation, "summary");
        CopyFieldIfPresent(operationObject, targetOperation, "description");
        CopyFieldIfPresent(operationObject, targetOperation, "tags");
        CopyFieldIfPresent(operationObject, targetOperation, "operationId");
        CopyFieldIfPresent(operationObject, targetOperation, "deprecated");
        CopyFieldIfPresent(operationObject, targetOperation, "security");
        CopyFieldIfPresent(operationObject, targetOperation, "externalDocs");
        CopyVendorExtensions(operationObject, targetOperation);

        List<ParameterContext> mergedParameters = MergeParameters(sourceRoot, pathItem, operationObject, cancellationToken);
        string[] consumes = ResolveEffectiveConsumesValues(sourceRoot, pathItem, operationObject);
        string[] produces = ResolveEffectiveProducesValues(sourceRoot, pathItem, operationObject);

        var nonBodyParameters = new List<ParameterContext>();
        var formDataParameters = new List<ParameterContext>();
        ParameterContext? bodyParameter = null;

        for (int i = 0; i < mergedParameters.Count; i++)
        {
            ParameterContext parameter = mergedParameters[i];
            string location = GetParameterLocation(parameter.Effective);

            if (IsNonBodyParameterLocation(location))
            {
                nonBodyParameters.Add(parameter);
                continue;
            }

            if (string.Equals(location, "body", StringComparison.Ordinal))
            {
                bodyParameter ??= parameter;
                continue;
            }

            if (string.Equals(location, "formData", StringComparison.Ordinal))
                formDataParameters.Add(parameter);
        }

        JsonArray parameters = ConvertParameters(nonBodyParameters, cancellationToken);

        if (parameters.Count > 0)
            targetOperation["parameters"] = parameters;

        JsonObject? requestBody = ConvertRequestBody(sourceRoot, bodyParameter, formDataParameters, consumes, cancellationToken);

        if (requestBody != null)
            targetOperation["requestBody"] = requestBody;

        if (TryGetJsonObject(operationObject, "responses", out JsonObject? responsesObject))
            targetOperation["responses"] = ConvertResponses(responsesObject!, produces, cancellationToken);
        else
            targetOperation["responses"] = new JsonObject();

        return targetOperation;
    }
}
