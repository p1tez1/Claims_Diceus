using ClaimsModule.Application.Models;
using ClaimsModule.Application.Reference;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Authorize]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
[Route("api/policies")]
public sealed class PoliciesController(ISender sender) : ControllerBase
{
    /// <summary>Search simulated policies by policy number or client name.</summary>
    [HttpGet("search")]
    [ProducesResponseType<IReadOnlyList<PolicyDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery(Name = "q")] string? query, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new SearchPoliciesQuery(query), cancellationToken));

    /// <summary>Get coverage types for a simulated policy.</summary>
    [HttpGet("{id:guid}/coverage")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCoverage(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPolicyCoverageQuery(id), cancellationToken));
}
