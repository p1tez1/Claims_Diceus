using ClaimsModule.Application.Models;
using ClaimsModule.Application.Reserves;
using ClaimsModule.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Authorize(Policy = "HandlerOrAbove")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
[Route("api/claims/{id:guid}")]
public sealed class ReservesController(ISender sender) : ControllerBase
{
    /// <summary>Get reserve balances and immutable transaction history for a claim.</summary>
    [HttpGet("reserves")]
    [ProducesResponseType<IReadOnlyList<ReserveComponentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ReserveComponentDto>>> Get(Guid id, CancellationToken token) =>
        Ok(await sender.Send(new GetClaimReservesQuery(id), token));

    /// <summary>Append a signed-delta transaction. POST accepts Idempotency-Key.</summary>
    [HttpPost("reserves")]
    [ProducesResponseType<ReserveTransactionResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReserveTransactionResult>> Submit(Guid id, SubmitReserveRequest request, CancellationToken token)
    {
        var result = await sender.Send(new SubmitReserveCommand(id, request.Component, request.Amount,
            request.ChangeReason!, request.TransactionType, request.ResubmissionOfId), token);
        return CreatedAtAction(nameof(Get), new { id }, result);
    }

    /// <summary>Compatibility adapter: appends an Adjust delta; never edits existing history. Idempotency-Key is ignored.</summary>
    [HttpPut("reserves/{componentId:guid}")]
    [ProducesResponseType<ReserveTransactionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReserveTransactionResult>> Adjust(Guid id, Guid componentId, AdjustReserveRequest request, CancellationToken token) =>
        Ok(await sender.Send(new AdjustReserveCommand(id, componentId, request.Amount, request.ChangeReason!, request.ResubmissionOfId), token));

    /// <summary>Approve a pending reserve. Requires Supervisor or Manager role and authority for the amount.</summary>
    [HttpPost("reserves/{txnId:guid}/approve")]
    [Authorize(Policy = "SupervisorOrAbove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Approve(Guid id, Guid txnId, CancellationToken token)
    {
        await sender.Send(new ApproveReserveCommand(id, txnId), token);
        return NoContent();
    }

    /// <summary>Reject a pending reserve. Requires Supervisor or Manager role.</summary>
    [HttpPost("reserves/{txnId:guid}/reject")]
    [Authorize(Policy = "SupervisorOrAbove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reject(Guid id, Guid txnId, RejectReserveRequest request, CancellationToken token)
    {
        await sender.Send(new RejectReserveCommand(id, txnId, request.RejectionReason!), token);
        return NoContent();
    }

    /// <summary>Retract a pending reserve submitted by the current user.</summary>
    [HttpPost("reserves/{txnId:guid}/retract")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Retract(Guid id, Guid txnId, CancellationToken token)
    {
        await sender.Send(new RetractReserveCommand(id, txnId), token);
        return NoContent();
    }

    /// <summary>Any authenticated claims role can retry Failed posting within its tenant.</summary>
    [HttpPost("reserves/{txnId:guid}/retry-posting")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Retry(Guid id, Guid txnId, CancellationToken token)
    {
        await sender.Send(new RetryGlPostingCommand(id, txnId), token);
        return NoContent();
    }

    /// <summary>Set the aggregate reserve override. Requires Manager role.</summary>
    [HttpPost("reserve-override")]
    [Authorize(Policy = "ManagerOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Override(Guid id, ReserveOverrideRequest request, CancellationToken token)
    {
        await sender.Send(new SetReserveOverrideCommand(id, request.Enabled), token);
        return NoContent();
    }
}

// Nullable wire fields let FluentValidation produce the canonical 422 for missing reasons.
public sealed record SubmitReserveRequest(ReserveComponentType Component, decimal Amount, string? ChangeReason,
    ReserveTransactionType TransactionType, Guid? ResubmissionOfId = null);
public sealed record AdjustReserveRequest(decimal Amount, string? ChangeReason, Guid? ResubmissionOfId = null);
public sealed record RejectReserveRequest(string? RejectionReason);
public sealed record ReserveOverrideRequest(bool Enabled = true);
