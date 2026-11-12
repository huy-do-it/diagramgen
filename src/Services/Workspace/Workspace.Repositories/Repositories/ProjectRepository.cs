using Microsoft.EntityFrameworkCore;
using Workspace.Repositories.Data;
using Workspace.Repositories.Entities;

namespace Workspace.Repositories.Repositories;

public class ProjectRepository : GenericRepository<Project>, IProjectRepository
{
    public ProjectRepository(WorkspaceDbContext context) : base(context) { }

    public async Task<Project?> GetByIdWithMembersAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<(IReadOnlyList<Project> Items, int TotalCount)> GetPagedProjectsForUserAsync(
        Guid userId, string? search, string? role, string? sort, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Projects
            .Include(p => p.Members)
            .Where(p => p.Members.Any(m => m.UserId == userId) || p.LecturerId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(searchLower) ||
                                     (p.Description != null && p.Description.ToLower().Contains(searchLower)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleTrimmed = role.Trim();
            if (string.Equals(roleTrimmed, "Leader", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.OwnerId == userId || p.Members.Any(m => m.UserId == userId && m.Role == "Leader"));
            }
            else if (string.Equals(roleTrimmed, "Member", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.Members.Any(m => m.UserId == userId && m.Role == "Member"));
            }
            else if (string.Equals(roleTrimmed, "Lecturer", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.LecturerId == userId || p.Members.Any(m => m.UserId == userId && m.Role == "Lecturer"));
            }
        }

        var totalCount = await query.CountAsync(ct);

        query = sort?.ToLowerInvariant() switch
        {
            "name_asc" => query.OrderBy(p => p.Name),
            "name_desc" => query.OrderByDescending(p => p.Name),
            "created_asc" => query.OrderBy(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        return await _context.Projects
            .AnyAsync(p => p.Id == projectId && (p.LecturerId == userId || p.Members.Any(m => m.UserId == userId)), ct);
    }

    public async Task<bool> IsLeaderAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        return await _context.Projects
            .AnyAsync(p => p.Id == projectId && (p.OwnerId == userId || p.Members.Any(m => m.UserId == userId && m.Role == "Leader")), ct);
    }

    public async Task<ProjectMember?> GetMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        return await _context.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, ct);
    }

    public async Task<IReadOnlyList<ProjectMember>> GetMembersAsync(Guid projectId, CancellationToken ct = default)
    {
        return await _context.ProjectMembers
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(ct);
    }

    public async Task AddMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        await _context.ProjectMembers.AddAsync(member, ct);
    }

    public void RemoveMember(ProjectMember member)
    {
        _context.ProjectMembers.Remove(member);
    }

    public async Task<int> GetActiveStudentMemberCountAsync(Guid projectId, CancellationToken ct = default)
    {
        return await _context.ProjectMembers
            .CountAsync(m => m.ProjectId == projectId && m.Role != "Lecturer", ct);
    }

    public async Task<int> GetActiveMemberCountAsync(Guid projectId, CancellationToken ct = default)
    {
        return await _context.ProjectMembers
            .CountAsync(m => m.ProjectId == projectId, ct);
    }
}
