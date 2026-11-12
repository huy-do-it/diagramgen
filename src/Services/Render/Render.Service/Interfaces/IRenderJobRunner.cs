namespace UMLForge.Render.Service.Interfaces;

public interface IRenderJobRunner
{
    Task<bool> RunNextAsync(CancellationToken cancellationToken = default);
}
