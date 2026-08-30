[![](https://img.shields.io/nuget/v/soenneker.openapi.converter.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.openapi.converter/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.openapi.converter/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.openapi.converter/actions/workflows/publish-package.yml)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.openapi.converter/codeql.yml?label=codeql&style=for-the-badge)](https://github.com/soenneker/soenneker.openapi.converter/actions/workflows/codeql.yml)
[![](https://img.shields.io/nuget/dt/soenneker.openapi.converter.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.openapi.converter/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.OpenApi.Converter
Convert Swagger 2.0 JSON documents to OpenAPI 3.0 JSON for migrations and downstream tooling.

## Installation

```bash
dotnet add package Soenneker.OpenApi.Converter
```

## Registration

```csharp
using Soenneker.OpenApi.Converter.Registrars;

services.AddOpenApiConverterAsScoped();
```

`AddOpenApiConverterAsSingleton()` is also available when the converter should be shared by the application.

## Convert JSON

Inject `IOpenApiConverter`, then pass it a complete Swagger 2.0 JSON document:

```csharp
using Soenneker.OpenApi.Converter.Abstract;

string openApi3Json = await converter.Convert(swagger2Json, cancellationToken);
```

The returned value is indented OpenAPI 3.0 JSON. Invalid JSON, a non-object root, a missing `swagger` field, or any version other than Swagger 2.0 causes an exception.

## Convert a file

```csharp
string openApi3Json = await converter.ConvertFile(
    "openapi2.json",
    "openapi3.json",
    cancellationToken);
```

`ConvertFile` reads the source, writes the converted document to the target path, and returns the same JSON.

## What Gets Converted

The converter handles common Swagger 2 to OpenAPI 3 translation tasks, including:

- root document metadata such as `info`, `tags`, `security`, and `externalDocs`
- `host`, `basePath`, and `schemes` into OpenAPI 3 `servers`
- `definitions` into `components.schemas`
- non-body parameters into `components.parameters`
- body and form-data parameters into `requestBody` and `components.requestBodies`
- responses into OpenAPI 3 response content
- `securityDefinitions` into `components.securitySchemes`
- `$ref` values from Swagger 2 sections into OpenAPI 3 component references

## Boundaries

- Input must be valid Swagger 2.0 JSON.
- Only Swagger 2.0 documents are supported.
- The converter outputs JSON, not YAML.
- The conversion is structural and does not prove that the source contract accurately describes the API.
- Conversion failures are surfaced to the caller rather than silently returning a partial result.
