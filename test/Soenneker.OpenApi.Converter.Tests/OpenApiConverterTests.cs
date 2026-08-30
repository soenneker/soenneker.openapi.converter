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
}

