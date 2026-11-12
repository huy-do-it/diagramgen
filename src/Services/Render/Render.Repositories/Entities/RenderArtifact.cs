namespace UMLForge.Render.Repositories.Entities;

public sealed class RenderArtifact
{
    public Guid JobId { get; set; }
    public string PlantUml { get; set; } = string.Empty;
    public string SvgXml { get; set; } = string.Empty;
}
