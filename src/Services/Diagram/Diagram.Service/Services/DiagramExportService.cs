using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using UMLForge.Diagram.Repositories.Interfaces;
using UMLForge.Diagram.Service.Exceptions;
using UMLForge.Diagram.Service.Interfaces;

namespace UMLForge.Diagram.Service.Services;

public class DiagramExportService : IDiagramExportService
{
    private static readonly Regex InvalidCharsRegex = new(@"[\\/:*?""<>|]", RegexOptions.Compiled);

    private readonly IDiagramRepository _diagramRepository;
    private readonly IMemberAccessRepository _memberAccessRepository;
    private readonly ICurrentUser _currentUser;

    public DiagramExportService(
        IDiagramRepository diagramRepository,
        IMemberAccessRepository memberAccessRepository,
        ICurrentUser currentUser)
    {
        _diagramRepository = diagramRepository;
        _memberAccessRepository = memberAccessRepository;
        _currentUser = currentUser;
    }

    public async Task<(byte[] FileBytes, string FileName)> ExportProjectDiagramsZipAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await EnsureProjectAccessAsync(projectId, cancellationToken);

        var diagrams = await _diagramRepository.GetQueryable()
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (diagrams.Count == 0)
            {
                var emptyEntry = archive.CreateEntry("README.txt");
                using var writer = new StreamWriter(emptyEntry.Open(), Encoding.UTF8);
                writer.WriteLine($"Project {projectId} contains no diagrams.");
            }
            else
            {
                var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var diagram in diagrams)
                {
                    var baseTitle = SanitizeFileName(diagram.Title);
                    if (string.IsNullOrWhiteSpace(baseTitle))
                    {
                        baseTitle = $"diagram_{diagram.Id}";
                    }

                    if (!nameCounts.TryGetValue(baseTitle, out var count))
                    {
                        nameCounts[baseTitle] = 1;
                    }
                    else
                    {
                        nameCounts[baseTitle] = count + 1;
                        baseTitle = $"{baseTitle}_{count + 1}";
                    }

                    // 1. JSON entry
                    if (!string.IsNullOrWhiteSpace(diagram.ModelJson))
                    {
                        var jsonEntry = archive.CreateEntry($"{baseTitle}.json");
                        using var entryStream = jsonEntry.Open();
                        var bytes = Encoding.UTF8.GetBytes(diagram.ModelJson);
                        entryStream.Write(bytes, 0, bytes.Length);
                    }

                    // 2. PUML entry
                    if (!string.IsNullOrWhiteSpace(diagram.PlantUml))
                    {
                        var pumlEntry = archive.CreateEntry($"{baseTitle}.puml");
                        using var entryStream = pumlEntry.Open();
                        var bytes = Encoding.UTF8.GetBytes(diagram.PlantUml);
                        entryStream.Write(bytes, 0, bytes.Length);
                    }

                    // 3. SVG entry
                    if (!string.IsNullOrWhiteSpace(diagram.SvgXml))
                    {
                        var svgEntry = archive.CreateEntry($"{baseTitle}.svg");
                        using var entryStream = svgEntry.Open();
                        var bytes = Encoding.UTF8.GetBytes(diagram.SvgXml);
                        entryStream.Write(bytes, 0, bytes.Length);
                    }
                }
            }
        }

        memoryStream.Seek(0, SeekOrigin.Begin);
        var zipBytes = memoryStream.ToArray();
        var fileName = $"project_{projectId}_diagrams.zip";

        return (zipBytes, fileName);
    }

    private async Task EnsureProjectAccessAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var isAdmin = string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        if (isAdmin) return;

        var hasAccess = await _memberAccessRepository.HasAccessAsync(projectId, _currentUser.UserId, cancellationToken);
        if (!hasAccess)
        {
            throw new BusinessRuleException("BR-11", "You do not have access to this project.", 403);
        }
    }

    private static string SanitizeFileName(string name)
    {
        var sanitized = InvalidCharsRegex.Replace(name, "_").Trim();
        return sanitized.Length > 100 ? sanitized[..100] : sanitized;
    }
}
