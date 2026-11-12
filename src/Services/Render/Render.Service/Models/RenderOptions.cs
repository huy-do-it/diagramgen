namespace UMLForge.Render.Service.Models;

public sealed class RenderOptions
{
    public const string SectionName = "Render";
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class DiagramEngineOptions
{
    public const string SectionName = "DiagramEngine";
    public string Url { get; set; } = "http://localhost:5006";
    public bool UseFake { get; set; }
}

public sealed class PlantUmlOptions
{
    public const string SectionName = "PlantUml";
    public string BaseUrl { get; set; } = "http://localhost:8085/";
}
