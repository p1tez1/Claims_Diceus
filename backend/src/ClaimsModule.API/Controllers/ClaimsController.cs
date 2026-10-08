using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Documents;
using ClaimsModule.Application.Models;
using ClaimsModule.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Authorize(Policy = "HandlerOrAbove")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
[Route("api/claims")]
public sealed class ClaimsController(ISender sender) : ControllerBase
{
    /// <summary>Create a Draft FNOL claim. POST accepts Idempotency-Key (24-hour replay).</summary>
    [HttpPost]
    [ProducesResponseType<ClaimDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateClaimCommand request, CancellationToken token)
    {
        var id = await sender.Send(request, token);
        return CreatedAtAction(nameof(Get), new { id }, await sender.Send(new GetClaimDetailQuery(id), token));
    }

    /// <summary>Validate an FNOL request without persistence. Idempotency-Key is ignored.</summary>
    [HttpPost("validate")]
    [ProducesResponseType<ClaimValidationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ClaimValidationDto>> Validate(CreateClaimCommand request, CancellationToken token) =>
        Ok(await sender.Send(new ValidateClaimQuery(request), token));

    /// <summary>Get full claim detail including parties, risks, reserves, documents, and recent audit.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClaimDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClaimDetailDto>> Get(Guid id, CancellationToken token) =>
        Ok(await sender.Send(new GetClaimDetailQuery(id), token));

    /// <summary>List claims using tenant-scoped filters and pagination.</summary>
    [HttpGet]
    [ProducesResponseType<PageDto<ClaimListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PageDto<ClaimListItemDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] ClaimStatus[]? status = null, [FromQuery] string? dateFrom = null, [FromQuery] string? dateTo = null,
        [FromQuery] Guid? assignedHandlerId = null, [FromQuery] string? causeOfLossCode = null, [FromQuery] Guid? policyId = null,
        [FromQuery] string? search = null, CancellationToken token = default) =>
        Ok(await sender.Send(new ListClaimsQuery(page, pageSize, status, ParseDateFilter(dateFrom, "DateFrom"),
            ParseDateFilter(dateTo, "DateTo"), assignedHandlerId, causeOfLossCode, policyId, search), token));

    private static DateTimeOffset? ParseDateFilter(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AllowWhiteSpaces | System.Globalization.DateTimeStyles.AssumeUniversal, out var result))
            return result;
        if (System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{5,}-\d{2}-\d{2}"))
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(field,
                field == "DateFrom" ? "Date from must be between 1900-01-01 and 2100-12-31." : "Date to must be between 1900-01-01 and 2100-12-31.")]);
        throw new ModelBindingException(new() { [field] = [$"The value '{value}' is not a valid date."] });
    }

    /// <summary>List the immutable audit history for a claim.</summary>
    [HttpGet("{id:guid}/audit")]
    [ProducesResponseType<PageDto<AuditLogDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PageDto<AuditLogDto>>> Audit(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken token = default) => Ok(await sender.Send(new GetClaimAuditQuery(id, page, pageSize), token));

    /// <summary>Upload one claim document as multipart/form-data. Maximum file size is 50 MB.</summary>
    [HttpPost("{id:guid}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52L * 1024 * 1024)]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DocumentDto>> UploadDocument(Guid id, [FromForm] UploadDocumentRequest request,
        CancellationToken token)
    {
        await using var stream = request.File.OpenReadStream();
        var result = await sender.Send(new UploadDocumentCommand(id, request.File.FileName, request.File.ContentType,
            request.File.Length, stream, request.DocumentType, request.Notes), token);
        return CreatedAtAction(nameof(ListDocuments), new { id }, result);
    }

    /// <summary>List document metadata and short-lived download URLs for a claim.</summary>
    [HttpGet("{id:guid}/documents")]
    [ProducesResponseType<IReadOnlyList<DocumentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> ListDocuments(Guid id, CancellationToken token) =>
        Ok(await sender.Send(new ListDocumentsQuery(id), token));

    /// <summary>Add a party to a claim. POST accepts Idempotency-Key (24-hour replay).</summary>
    [HttpPost("{id:guid}/parties")]
    [ProducesResponseType<PartyDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddParty(Guid id, PartyInput party, CancellationToken token)
    {
        var result = await sender.Send(new AddPartyCommand(id, party), token);
        return CreatedAtAction(nameof(Get), new { id }, result);
    }

    /// <summary>Soft-remove a party. Repeated removal is a no-op; Idempotency-Key is ignored.</summary>
    [HttpDelete("{id:guid}/parties/{partyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RemoveParty(Guid id, Guid partyId, CancellationToken token)
    {
        await sender.Send(new RemovePartyCommand(id, partyId), token);
        return NoContent();
    }

    /// <summary>Replace claim notes. PUT endpoints ignore Idempotency-Key (DD-32).</summary>
    [HttpPut("{id:guid}/notes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Notes(Guid id, NotesRequest request, CancellationToken token)
    {
        await sender.Send(new UpdateClaimNotesCommand(id, request.Notes), token);
        return NoContent();
    }

    /// <summary>Link a known policy and resolve the unknown-policy validation warning.</summary>
    [HttpPut("{id:guid}/policy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Policy(Guid id, LinkPolicyRequest request, CancellationToken token)
    {
        await sender.Send(new LinkPolicyCommand(id, request.PolicyId), token);
        return NoContent();
    }

    /// <summary>Assign a handler. Requires Supervisor or Manager role.</summary>
    [HttpPut("{id:guid}/assignee")]
    [Authorize(Policy = "SupervisorOrAbove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Assignee(Guid id, AssigneeRequest request, CancellationToken token)
    {
        await sender.Send(new AssignHandlerCommand(id, request.AssignedHandlerId), token);
        return NoContent();
    }

    /// <summary>Record liability determination for the claim.</summary>
    [HttpPut("{id:guid}/liability")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Liability(Guid id, LiabilityRequest request, CancellationToken token)
    {
        await sender.Send(new DetermineLiabilityCommand(id, request.Determination, request.Notes), token);
        return NoContent();
    }

    /// <summary>Apply a validated claim status transition.</summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType<TransitionStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> TransitionStatus(Guid id, TransitionStatusRequest request, CancellationToken token)
    {
        var status = await sender.Send(new TransitionClaimStatusCommand(id, request.TargetStatus, request.Reason,
            request.AcknowledgeWarnings, request.ClosureJustification), token);
        return Ok(new TransitionStatusResponse(status));
    }

    /// <summary>Preview blockers and warnings for a proposed status transition.</summary>
    [HttpGet("{id:guid}/transitions/{targetStatus}/preflight")]
    [ProducesResponseType<TransitionPreflightDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransitionPreflightDto>> TransitionPreflight(Guid id, ClaimStatus targetStatus,
        [FromQuery] string? reason, [FromQuery] bool acknowledgeWarnings = false,
        [FromQuery] string? closureJustification = null, CancellationToken token = default) =>
        Ok(await sender.Send(new GetTransitionPreflightQuery(id, targetStatus, reason, acknowledgeWarnings,
            closureJustification), token));
}

public sealed record NotesRequest(string? Notes);
public sealed record LinkPolicyRequest(Guid PolicyId);
public sealed record AssigneeRequest(Guid AssignedHandlerId);
public sealed record LiabilityRequest(LiabilityDetermination? Determination, string? Notes);
public sealed record TransitionStatusRequest(ClaimStatus TargetStatus, string? Reason, bool AcknowledgeWarnings,
    string? ClosureJustification);
public sealed record TransitionStatusResponse(ClaimStatus Status);
public sealed record UploadDocumentRequest(IFormFile File, string DocumentType, string? Notes);
