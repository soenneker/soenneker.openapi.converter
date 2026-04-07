[![](https://img.shields.io/nuget/v/soenneker.openapi.converter.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.openapi.converter/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.openapi.converter/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.openapi.converter/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/soenneker.openapi.converter.svg?style=for-the-badge)](https://www.nuget.org/packages/soenneker.openapi.converter/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.OpenApi.Converter
### A .NET converter for OpenAPI 2 (Swagger) to OpenAPI 3

Converts Swagger 2.0 JSON documents into OpenAPI 3 JSON.

This package is useful when you need to:

- modernize older Swagger 2 specifications
- feed legacy Swagger documents into OpenAPI 3 tooling
- convert specs during migrations, automation, or build workflows

## Installation

```bash
dotnet add package Soenneker.OpenApi.Converter
```

## Features

- Converts Swagger 2.0 JSON payloads into OpenAPI 3 JSON
- Converts files directly from disk
- Rewrites common Swagger 2 references into OpenAPI 3 component references
- Translates schemas, parameters, request bodies, responses, and security definitions
- Supports dependency injection registration helpers

## Usage

### Convert a JSON string

```csharp
using Soenneker.OpenApi.Converter;
using Soenneker.Utils.File;

var fileUtil = new FileUtil();
var converter = new OpenApiConverter(fileUtil);

string openApi3Json = await converter.Convert(swaggerJson, cancellationToken);
```

### Convert a file

```csharp
using Soenneker.OpenApi.Converter;
using Soenneker.Utils.File;

var fileUtil = new FileUtil();
var converter = new OpenApiConverter(fileUtil);

string openApi3Json = await converter.ConvertFile("openapi2.json", "openapi3.json", cancellationToken);
```

### Register with dependency injection

```csharp
using Soenneker.OpenApi.Converter.Registrars;

builder.Services.AddOpenApiConverterAsSingleton();
```

Or:

```csharp
services.AddOpenApiConverterAsScoped();
```

Then consume it through `IOpenApiConverter`:

```csharp
using Soenneker.OpenApi.Converter.Abstract;

public sealed class MyService
{
    private readonly IOpenApiConverter _openApiConverter;

    public MyService(IOpenApiConverter openApiConverter)
    {
        _openApiConverter = openApiConverter;
    }
}
```

## API

`IOpenApiConverter` exposes:

- `ValueTask<string> Convert(string swaggerJson, CancellationToken cancellationToken = default)`
- `ValueTask<string> ConvertFile(string sourcePath, string targetPath, CancellationToken cancellationToken = default)`

Both methods return the converted OpenAPI 3 JSON directly, so the converter instance does not need to hold onto a "last converted" payload.

## What Gets Converted

The converter currently handles common Swagger 2 to OpenAPI 3 translation tasks, including:

- root document metadata such as `info`, `tags`, `security`, and `externalDocs`
- `host`, `basePath`, and `schemes` into OpenAPI 3 `servers`
- `definitions` into `components.schemas`
- non-body parameters into `components.parameters`
- body and form-data parameters into `requestBody` and `components.requestBodies`
- responses into OpenAPI 3 response content
- `securityDefinitions` into `components.securitySchemes`
- `$ref` values from Swagger 2 sections into OpenAPI 3 component references

## Notes

- Input must be valid Swagger 2.0 JSON.
- Only Swagger 2.0 documents are supported.
- The converter outputs JSON, not YAML.
- Invalid or unsupported input throws an exception rather than silently continuing.

## Output

The resulting OpenAPI 3 JSON is returned directly from `Convert()` and `ConvertFile()`, so it can be written to disk, logged, or passed to downstream tooling immediately.