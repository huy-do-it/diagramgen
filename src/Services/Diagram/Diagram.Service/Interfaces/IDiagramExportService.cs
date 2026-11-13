namespace UMLForge.Diagram.Service.Interfaces;

public interface IDiagramExportService
{
    Task<(byte[] FileBytes, string FileName)> ExportProjectDiagramsZipAsync(Guid projectId, CancellationToken cancellationToken = default);
}
