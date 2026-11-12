namespace UMLForge.Render.Service.Models;

public sealed record RenderResult(string? PlantUml, string? SvgXml, string? Error, long DurationMs)
{
    public bool IsSuccess => Error is null;
}
