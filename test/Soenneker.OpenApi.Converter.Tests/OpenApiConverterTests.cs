using System.Threading.Tasks;
using Soenneker.Tests.Attributes.Local;
using Soenneker.OpenApi.Converter.Abstract;
using Soenneker.Tests.HostedUnit;

namespace Soenneker.OpenApi.Converter.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class OpenApiConverterTests : HostedUnitTest
{
    private readonly IOpenApiConverter _util;

    public OpenApiConverterTests(Host host) : base(host)
    {
        _util = Resolve<IOpenApiConverter>(true);
    }

    [Test]
    public void Default()
    {

    }

    [LocalOnly]
    public async ValueTask ConvertFile_should_convert()
    {
        await _util.ConvertFile("C:\\Users\\jake\\AppData\\Local\\Temp\\temp_5cb4a53cdcd74d28a370ce4f6fd9e803\\openapi2.json",
            "C:\\Users\\jake\\AppData\\Local\\Temp\\temp_5cb4a53cdcd74d28a370ce4f6fd9e803\\openapi3.json", cancellationToken: CancellationToken);
    }
}
