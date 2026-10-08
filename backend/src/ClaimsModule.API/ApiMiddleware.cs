using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;
using FluentValidation;
using Serilog.Context;

namespace ClaimsModule.API;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public Guid? UserId => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal?.FindFirstValue("sub"), out var id) ? id : null;
    public string Name => Principal?.FindFirstValue("name") ?? string.Empty;
    public UserRole? Role => Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role) ?? Principal?.FindFirstValue("role"), true, out var role) ? role : null;
    public Guid OrganisationId => Guid.TryParse(Principal?.FindFirstValue("org_id"), out var id) ? id : Guid.Empty;
    public Guid? CorrelationId => Guid.TryParse(accessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey]?.ToString(), out var id) ? id : null;
}

public sealed class SerilogRequestLog : IRequestLog
{
    public void Information(string action, string requestType) => Serilog.Log.Information("{Action} {RequestType}", action, requestType);
    public void Failure(Exception exception, string action, string requestType) =>
        Serilog.Log.Error(exception, "{Action} {RequestType}", action, requestType);
}

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string ItemKey = "CorrelationId";
    public async Task InvokeAsync(HttpContext context)
    {
        var raw = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        var id = Guid.TryParse(raw, out var parsed) ? parsed : Guid.NewGuid();
        context.Items[ItemKey] = id;
        context.Response.Headers["X-Correlation-Id"] = id.ToString("D");
        using (LogContext.PushProperty(ItemKey, id)) await next(context);
    }
}

public sealed class UserLogContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var raw = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
        using (LogContext.PushProperty("UserId", Guid.TryParse(raw, out var id) ? id : null))
            await next(context);
    }
}

public sealed record ApiErrorResponse(string Type, string Title, int Status,
    Dictionary<string, string[]> Errors, string? CorrelationId);

public sealed class ModelBindingException(Dictionary<string, string[]> errors) : Exception("The request is malformed.")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}

public sealed class ApiExceptionMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
            if (!context.Response.HasStarted && context.Response.ContentLength is null or 0)
                await WriteStatusAsync(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            if (exception is not ValidationException and not DomainRuleViolationException and not NotFoundException
                and not ConcurrencyException and not TransitionBlockedException and not ModelBindingException)
                Serilog.Log.Error(exception, "Request failed");
            await WriteExceptionAsync(context, exception, environment.IsDevelopment());
        }
    }

    public static Task WriteExceptionAsync(HttpContext context, Exception exception, bool development)
    {
        var (type, title, status, errors) = exception switch
        {
            ModelBindingException e => ("BadRequest", e.Message, 400, e.Errors),
            ValidationException e => ("ValidationError", "One or more validation errors occurred.", 422,
                e.Errors.GroupBy(x => x.PropertyName).ToDictionary(x => x.Key, x => x.Select(y => y.ErrorMessage).ToArray())),
            TransitionBlockedException e => ("ValidationError", e.Message, 422,
                e.BlockingConditions.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(y => y.Message).ToArray())
                    .Concat(new[] { new KeyValuePair<string, string[]>("ValidNextStatuses",
                        e.ValidNextStatuses.Select(x => x.ToString()).ToArray()) }).ToDictionary(x => x.Key, x => x.Value)),
            SelfApprovalException e => ("ValidationError", e.Message, 422, Field(e.Code, e.Message)),
            InsufficientAuthorityException e => ("Forbidden", e.Message, 403, Field(e.Code, e.Message)),
            DomainRuleViolationException e => ("ValidationError", e.Message, 422, Field(e.Code, e.Message)),
            NotFoundException e => ("NotFound", e.Message, 404, Empty()),
            ConcurrencyException e => ("ConcurrencyError", e.Message, 409, Empty()),
            BadHttpRequestException e => ("BadRequest", e.Message, 400, Empty()),
            JsonException e => ("BadRequest", e.Message, 400, Empty()),
            _ => ("ServerError", development ? exception.Message : "An unexpected error occurred.", 500, Empty())
        };
        return WriteAsync(context, type, title, status, errors);
    }

    public static Task WriteStatusAsync(HttpContext context)
    {
        var error = context.Response.StatusCode switch
        {
            400 => ("BadRequest", "The request is malformed."),
            401 => ("Unauthorized", "Authentication is required."),
            403 => ("Forbidden", "Access is denied."),
            404 => ("NotFound", "The requested resource was not found."),
            405 => ("MethodNotAllowed", "The HTTP method is not allowed for this resource."),
            409 => ("Conflict", "The request conflicts with the current state."),
            422 => ("ValidationError", "One or more validation errors occurred."),
            415 => ("UnsupportedMediaType", "The request content type is not supported."),
            >= 400 => ("RequestError", "The request could not be completed."),
            _ => default
        };
        return error.Item1 is null ? Task.CompletedTask : WriteAsync(context, error.Item1, error.Item2, context.Response.StatusCode);
    }

    public static async Task WriteAsync(HttpContext context, string type, string title, int status,
        Dictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            type,
            title,
            status,
            errors = errors ?? Empty(),
            correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
        });
    }
    private static Dictionary<string, string[]> Field(string field, string message) => new() { [field] = [message] };
    private static Dictionary<string, string[]> Empty() => [];
}
