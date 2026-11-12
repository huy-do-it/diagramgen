namespace UMLForge.Render.Repositories.Entities;

public sealed class RenderDailyReport
{
    public DateOnly Date { get; set; }
    public int Total { get; set; }
    public int Done { get; set; }
    public int Failed { get; set; }
    public double AvgMs { get; set; }
    public double SuccessRate { get; set; }
}
