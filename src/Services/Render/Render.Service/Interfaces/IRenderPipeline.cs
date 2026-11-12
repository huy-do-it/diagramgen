using UMLForge.Render.Repositories.Entities;
using UMLForge.Render.Service.Models;

namespace UMLForge.Render.Service.Interfaces;

public interface IRenderPipeline
{
    // M-03 chịu trách nhiệm lưu outcome + artifact + Outbox trong cùng transaction.
    Task<RenderResult> ProcessJobAsync(RenderJob job, CancellationToken cancellationToken = default);
}
