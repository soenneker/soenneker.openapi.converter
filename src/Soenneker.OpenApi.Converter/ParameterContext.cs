using System.Text.Json.Nodes;

namespace Soenneker.OpenApi.Converter;

internal sealed class ParameterContext
{
    public ParameterContext(JsonObject original, JsonObject effective)
    {
        Original = original;
        Effective = effective;
    }

    public JsonObject Original { get; }
    public JsonObject Effective { get; }
}
