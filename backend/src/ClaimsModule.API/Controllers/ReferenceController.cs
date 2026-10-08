using ClaimsModule.Application.Models;
using ClaimsModule.Application.Reference;
using ClaimsModule.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Authorize]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
[Route("api/reference")]
public sealed class ReferenceController(ISender sender) : ControllerBase
{
    /// <summary>List active cause-of-loss codes, optionally filtered by peril category.</summary>
    [HttpGet("cause-of-loss-codes")]
    [ProducesResponseType<IReadOnlyList<CauseOfLossDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCauseOfLossCodes([FromQuery] string? perilCategory, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetCauseOfLossCodesQuery(perilCategory), cancellationToken));

    /// <summary>List claim statuses and their configured transitions.</summary>
    [HttpGet("claim-statuses")]
    [ProducesResponseType<IReadOnlyList<ClaimStatusDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClaimStatuses(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetClaimStatusesQuery(), cancellationToken));

    /// <summary>List tenant users, optionally filtered by exact role.</summary>
    [HttpGet("users")]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] UserRole? role, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetUsersQuery(role), cancellationToken));
}
