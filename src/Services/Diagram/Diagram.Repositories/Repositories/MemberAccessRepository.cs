using System.Data;
using Microsoft.EntityFrameworkCore;
using UMLForge.Diagram.Repositories.Context;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Interfaces;

namespace UMLForge.Diagram.Repositories.Repositories;

public class MemberAccessRepository : GenericRepository<MemberAccess>, IMemberAccessRepository
{
    public MemberAccessRepository(DiagramDbContext context) : base(context)
    {
    }

    public async Task<MemberAccess?> GetAccessAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        var access = await _dbSet.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);
        if (access != null)
        {
            return access;
        }

        // Fallback for local development when Redis event stream is unavailable
        try
        {
            var conn = _context.Database.GetDbConnection();
            var shouldClose = false;
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync(cancellationToken);
                shouldClose = true;
            }

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT TOP 1 pm.Role 
                    FROM WorkspaceDb.dbo.ProjectMembers pm 
                    WHERE pm.ProjectId = @projectId AND pm.UserId = @userId
                    UNION ALL
                    SELECT TOP 1 'Leader' AS Role
                    FROM WorkspaceDb.dbo.Projects p
                    WHERE p.Id = @projectId AND p.OwnerId = @userId AND NOT EXISTS (
                        SELECT 1 FROM WorkspaceDb.dbo.ProjectMembers pm2 WHERE pm2.ProjectId = @projectId AND pm2.UserId = @userId
                    )";

                var pProj = cmd.CreateParameter();
                pProj.ParameterName = "@projectId";
                pProj.Value = projectId;
                cmd.Parameters.Add(pProj);

                var pUser = cmd.CreateParameter();
                pUser.ParameterName = "@userId";
                pUser.Value = userId;
                cmd.Parameters.Add(pUser);

                var roleObj = await cmd.ExecuteScalarAsync(cancellationToken);
                if (roleObj != null && roleObj != DBNull.Value)
                {
                    var role = roleObj.ToString()!;
                    var synced = new MemberAccess
                    {
                        ProjectId = projectId,
                        UserId = userId,
                        Role = role
                    };
                    _dbSet.Add(synced);
                    await _context.SaveChangesAsync(cancellationToken);
                    return synced;
                }
            }
            finally
            {
                if (shouldClose && conn.State == ConnectionState.Open)
                {
                    await conn.CloseAsync();
                }
            }
        }
        catch
        {
            // Silently fallback if WorkspaceDb is not available or not on same server
        }

        return null;
    }

    public async Task<bool> HasAccessAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        return (await GetAccessAsync(projectId, userId, cancellationToken)) != null;
    }

    public async Task<bool> IsLecturerAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        var access = await GetAccessAsync(projectId, userId, cancellationToken);
        return access != null && string.Equals(access.Role, ProjectRole.Lecturer, StringComparison.OrdinalIgnoreCase);
    }
}
