using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UMLForge.Diagram.Service.Interfaces;

namespace UMLForge.Diagram.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/diagram/dev/member-access")]
public class DevMemberAccessController : ControllerBase
{
    private readonly IMemberAccessService _memberAccessService;
    private readonly IWebHostEnvironment _env;

    public DevMemberAccessController(IMemberAccessService memberAccessService, IWebHostEnvironment env)
    {
        _memberAccessService = memberAccessService;
        _env = env;
    }

    public class GrantAccessRequest
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string Role { get; set; } = "Member";
    }

    [HttpPost]
    public async Task<IActionResult> GrantAccess([FromBody] GrantAccessRequest request, CancellationToken cancellationToken)
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        await _memberAccessService.GrantAccessAsync(request.ProjectId, request.UserId, request.Role, cancellationToken);
        return Ok(new { message = $"Access granted for user {request.UserId} on project {request.ProjectId} with role {request.Role}." });
    }
}
