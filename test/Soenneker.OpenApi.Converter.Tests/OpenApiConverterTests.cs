using System.Threading.Tasks;
using Soenneker.Facts.Local;
using Soenneker.OpenApi.Converter.Abstract;
using Soenneker.Tests.FixturedUnit;
using Xunit;

namespace Soenneker.OpenApi.Converter.Tests;

[Collection("Collection")]
public sealed class OpenApiConverterTests : FixturedUnitTest
{
    private readonly IOpenApiConverter _util;

    public OpenApiConverterTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
        _util = Resolve<IOpenApiConverter>(true);
    }

    [Fact]
    public void Default()
    {

    }

    [LocalFact]
    public async ValueTask ConvertFile_should_convert()
    {
        await _util.ConvertFile("C:\\Users\\jake\\AppData\\Local\\Temp\\temp_5cb4a53cdcd74d28a370ce4f6fd9e803\\openapi2.json",
            "C:\\Users\\jake\\AppData\\Local\\Temp\\temp_5cb4a53cdcd74d28a370ce4f6fd9e803\\openapi3.json", cancellationToken: CancellationToken);
    }
}
