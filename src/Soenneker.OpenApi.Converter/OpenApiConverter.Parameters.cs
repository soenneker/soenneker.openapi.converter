using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;

namespace Soenneker.OpenApi.Converter;

public sealed partial class OpenApiConverter
{
    private List<ParameterContext> MergeParameters(JsonObject sourceRoot, JsonObject pathItem, JsonObject operationObject, CancellationToken cancellationToken)
    {
        var merged = new List<(string Key, ParameterContext Context)>();

        AddParameterContexts(sourceRoot, pathItem["parameters"] as JsonArray, merged, cancellationToken);
        AddParameterContexts(sourceRoot, operationObject["parameters"] as JsonArray, merged, cancellationToken);

        var result = new List<ParameterContext>(merged.Count);

        for (int i = 0; i < merged.Count; i++)
        {
            result.Add(merged[i].Context);
        }

        return result;
    }

    private void AddParameterContexts(JsonObject sourceRoot, JsonArray? parametersArray, List<(string Key, ParameterContext Context)> merged,
        CancellationToken cancellationToken)
    {
        if (parametersArray == null || parametersArray.Count == 0)
            return;

        for (int i = 0; i < parametersArray.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (parametersArray[i] is not JsonObject originalParameter)
                continue;

            JsonObject effectiveParameter = ResolveEffectiveParameter(sourceRoot, originalParameter);
            string mergeKey = GetParameterMergeKey(originalParameter, effectiveParameter);
            var context = new ParameterContext(originalParameter, effectiveParameter);
            int existingIndex = FindParameterContextIndex(merged, mergeKey);

            if (existingIndex >= 0)
                merged[existingIndex] = (mergeKey, context);
            else
                merged.Add((mergeKey, context));
        }
    }

    private JsonArray ConvertParameters(List<ParameterContext> parameters, CancellationToken cancellationToken)
    {
        var result = new JsonArray();

        for (int i = 0; i < parameters.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(ConvertParameter(parameters[i], cancellationToken));
        }

        return result;
    }

    private JsonObject ConvertParameter(ParameterContext parameterContext, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsPureRefObject(parameterContext.Original) && TryGetRefValue(parameterContext.Original, out string? reference))
            return new JsonObject { ["$ref"] = RewriteRefString(reference!) };

        return ConvertParameterObject(parameterContext.Effective, cancellationToken);
    }

    private JsonObject ConvertParameterObject(JsonObject parameterObject, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var target = new JsonObject();

        CopyFieldIfPresent(parameterObject, target, "name");
        CopyFieldIfPresent(parameterObject, target, "in");
        CopyFieldIfPresent(parameterObject, target, "description");
        CopyFieldIfPresent(parameterObject, target, "deprecated");
        CopyFieldIfPresent(parameterObject, target, "allowEmptyValue");
        CopyExampleFields(parameterObject, target);

        string location = GetParameterLocation(parameterObject);

        if (string.Equals(location, "path", StringComparison.Ordinal))
            target["required"] = true;
        else
            CopyFieldIfPresent(parameterObject, target, "required");

        JsonNode? schemaNode = parameterObject["schema"] != null
            ? ConvertSchemaNode(parameterObject["schema"], cancellationToken)
            : BuildSchemaFromTypeFields(parameterObject, includeDescription: false, cancellationToken);

        if (schemaNode != null)
            target["schema"] = schemaNode;

        ApplyParameterCollectionFormat(parameterObject, target);
        CopyVendorExtensions(parameterObject, target);

        return target;
    }

    private JsonObject? ConvertRequestBody(JsonObject sourceRoot, ParameterContext? bodyParameter, List<ParameterContext> formDataParameters,
        string[] consumes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (formDataParameters.Count > 0)
        {
            if (TryConvertRequestBodyRef(sourceRoot, formDataParameters, out JsonObject? requestBodyRef))
                return requestBodyRef;

            // Swagger 2 should not mix body and formData, but some documents do. Prefer formData so the payload remains representable in OpenAPI 3.
            return ConvertFormDataRequestBody(formDataParameters, consumes, cancellationToken);
        }

        if (bodyParameter == null)
            return null;

        if (TryConvertRequestBodyRef(sourceRoot, [bodyParameter], out JsonObject? bodyRequestBodyRef))
            return bodyRequestBodyRef;

        return ConvertSingleBodyRequestBody(bodyParameter, consumes, cancellationToken);
    }

    private JsonObject? ConvertSingleBodyRequestBody(ParameterContext bodyParameter, string[] consumes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var target = new JsonObject();

        CopyFieldIfPresent(bodyParameter.Effective, target, "description");
        CopyVendorExtensions(bodyParameter.Effective, target);

        if (TryGetBooleanValue(bodyParameter.Effective, "required", out bool required))
            target["required"] = required;

        JsonNode? schemaNode = bodyParameter.Effective["schema"] != null
            ? ConvertSchemaNode(bodyParameter.Effective["schema"], cancellationToken)
            : BuildSchemaFromTypeFields(bodyParameter.Effective, includeDescription: false, cancellationToken);

        string[] mediaTypes = consumes.Length == 0 ? [_defaultBodyMediaType] : consumes;
        JsonObject content = CreateContentObject(schemaNode, mediaTypes);
        ApplyBodyParameterExamples(content, bodyParameter.Effective);
        target["content"] = content;

        return target;
    }

    private JsonObject? ConvertFormDataRequestBody(List<ParameterContext> formDataParameters, string[] consumes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        (JsonObject schema, bool containsFile) = BuildFormDataSchema(formDataParameters, cancellationToken);
        string mediaType = DetermineFormDataMediaType(consumes, containsFile);

        var target = new JsonObject
        {
            ["content"] = new JsonObject
            {
                [mediaType] = new JsonObject
                {
                    ["schema"] = schema
                }
            }
        };

        if (SchemaHasRequiredProperties(schema))
            target["required"] = true;

        return target;
    }

    private bool TryConvertRequestBodyRef(JsonObject sourceRoot, List<ParameterContext> parameters, out JsonObject? requestBodyRef)
    {
        requestBodyRef = null;

        if (parameters.Count != 1)
            return false;

        ParameterContext parameter = parameters[0];

        if (!IsPureRefObject(parameter.Original) || !TryGetRefValue(parameter.Original, out string? reference))
            return false;

        if (!TryGetTopLevelParameterName(reference!, out string? parameterName))
            return false;

        string location = GetParameterLocation(parameter.Effective);

        if (!string.Equals(location, "body", StringComparison.Ordinal) &&
            !string.Equals(location, "formData", StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryGetJsonObject(sourceRoot, "parameters", out JsonObject? parametersObject) ||
            !parametersObject!.TryGetPropertyValue(parameterName!, out JsonNode? _))
        {
            return false;
        }

        requestBodyRef = new JsonObject
        {
            ["$ref"] = $"#/components/requestBodies/{EncodeJsonPointerSegment(parameterName!)}"
        };

        return true;
    }

    private JsonObject ResolveEffectiveParameter(JsonObject sourceRoot, JsonObject parameterObject)
    {
        if (!TryGetRefValue(parameterObject, out string? reference))
            return parameterObject;

        JsonObject? resolved = ResolveTopLevelParameterReference(sourceRoot, reference!, new HashSet<string>(StringComparer.Ordinal));
        return resolved ?? parameterObject;
    }

    private JsonObject? ResolveTopLevelParameterReference(JsonObject sourceRoot, string reference, HashSet<string> visited)
    {
        if (!TryGetTopLevelParameterName(reference, out string? parameterName))
            return null;

        if (!visited.Add(parameterName!))
            return null;

        if (!TryGetJsonObject(sourceRoot, "parameters", out JsonObject? parametersObject) ||
            !parametersObject!.TryGetPropertyValue(parameterName!, out JsonNode? parameterNode) ||
            parameterNode is not JsonObject parameterObject)
        {
            return null;
        }

        if (!TryGetRefValue(parameterObject, out string? nestedReference))
            return parameterObject;

        return ResolveTopLevelParameterReference(sourceRoot, nestedReference!, visited) ?? parameterObject;
    }

    private static int FindParameterContextIndex(List<(string Key, ParameterContext Context)> merged, string mergeKey)
    {
        for (int i = 0; i < merged.Count; i++)
        {
            if (string.Equals(merged[i].Key, mergeKey, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private string GetParameterMergeKey(JsonObject originalParameter, JsonObject effectiveParameter)
    {
        string location = GetParameterLocation(effectiveParameter);

        if (!string.IsNullOrWhiteSpace(location) && TryGetStringValue(effectiveParameter, "name", out string? parameterName))
            return $"{location}:{parameterName}";

        if (TryGetRefValue(originalParameter, out string? reference))
            return $"ref:{RewriteRefString(reference!)}";

        return $"json:{originalParameter.ToJsonString()}";
    }

    private static string GetParameterLocation(JsonObject parameterObject)
    {
        return GetStringValueOrDefault(parameterObject, "in", string.Empty);
    }

    private static bool IsNonBodyParameterLocation(string location)
    {
        return string.Equals(location, "path", StringComparison.Ordinal) ||
               string.Equals(location, "query", StringComparison.Ordinal) ||
               string.Equals(location, "header", StringComparison.Ordinal);
    }
}
