using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Persistence;
using ClaimsModule.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.API;

public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ClaimsDbContext db, ICurrentUser user)
    {
        if (!HttpMethods.IsPost(context.Request.Method) || user.UserId is null ||
            !context.Request.Headers.TryGetValue("Idempotency-Key", out var values) ||
            string.IsNullOrWhiteSpace(values.ToString()) ||
            context.Request.Path.Equals("/api/claims/validate", StringComparison.OrdinalIgnoreCase) ||
            context.Request.HasFormContentType)
        {
            await next(context);
            return;
        }

        var key = values.ToString();
        if (key.Length > 200) { await ApiExceptionMiddleware.WriteAsync(context, "BadRequest", "Idempotency-Key is too long.", 400); return; }
        context.Request.EnableBuffering();
        string hash;
        try
        {
            using var requestBody = new MemoryStream();
            await context.Request.Body.CopyToAsync(requestBody, context.RequestAborted);
            requestBody.Position = 0;
            using var stream = new MemoryStream();
            if (requestBody.Length == 0)
                await stream.WriteAsync("{}"u8.ToArray(), context.RequestAborted);
            else
            {
                using var document = await JsonDocument.ParseAsync(requestBody, cancellationToken: context.RequestAborted);
                using var writer = new Utf8JsonWriter(stream);
                WriteCanonical(writer, document.RootElement);
            }
            hash = Convert.ToHexString(SHA256.HashData(stream.ToArray()));
        }
        catch (JsonException exception)
        {
            await ApiExceptionMiddleware.WriteExceptionAsync(context, exception, false);
            return;
        }
        finally { context.Request.Body.Position = 0; }

        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? context.Request.Path.Value ?? "";
        var existing = await db.IdempotencyRecords.SingleOrDefaultAsync(x => x.UserId == user.UserId && x.Key == key);
        if (existing is not null && db.Entry(existing).Property<DateTimeOffset>("CreatedAt").CurrentValue < DateTimeOffset.UtcNow.AddHours(-24))
        {
            db.IdempotencyRecords.Remove(existing);
            await SaveRecordAsync(db, context.RequestAborted);
            existing = null;
        }
        if (existing is not null)
        {
            if (existing.RequestHash != hash || existing.HttpMethod != context.Request.Method || existing.RouteTemplate != route)
                await ApiExceptionMiddleware.WriteAsync(context, "ValidationError", "Idempotency-Key was used for a different request.", 422,
                    new() { ["Idempotency-Key"] = ["This key belongs to a different request."] });
            else if (existing.Status == IdempotencyStatus.InProgress)
            {
                context.Response.Headers.RetryAfter = "1";
                await ApiExceptionMiddleware.WriteAsync(context, "Conflict", "Request is already in progress.", 409);
            }
            else
            {
                context.Response.StatusCode = existing.StatusCode ?? 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(existing.ResponseBody ?? "");
            }
            return;
        }

        var record = new IdempotencyRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = user.UserId.Value,
            Key = key,
            HttpMethod = context.Request.Method,
            RouteTemplate = route,
            RequestHash = hash,
            Status = IdempotencyStatus.InProgress
        };
        db.IdempotencyRecords.Add(record);
        try { await SaveRecordAsync(db, context.RequestAborted); }
        catch (DbUpdateException)
        {
            context.Response.Headers.RetryAfter = "1";
            await ApiExceptionMiddleware.WriteAsync(context, "Conflict", "Request is already in progress.", 409);
            return;
        }

        var original = context.Response.Body;
        await using var capture = new MemoryStream();
        context.Response.Body = capture;
        try
        {
            try { await next(context); }
            catch (Exception exception) when (!context.Response.HasStarted)
            {
                capture.SetLength(0);
                await ApiExceptionMiddleware.WriteExceptionAsync(context, exception, false);
            }
            if (context.Response.StatusCode is 400 or 401 or 403 or 404 or 409 or 422 && capture.Length == 0)
            {
                await ApiExceptionMiddleware.WriteStatusAsync(context);
                context.Response.ContentLength = capture.Length;
            }
            capture.Position = 0;
            var body = await new StreamReader(capture).ReadToEndAsync(context.RequestAborted);
            // Rollback clears tracking, including the independently committed reservation and its tenant/audit shadows.
            if (db.Entry(record).State == EntityState.Detached)
            {
                var recordId = record.Id;
                record = await db.IdempotencyRecords.SingleAsync(x => x.Id == recordId, context.RequestAborted);
            }
            if (context.Response.StatusCode is >= 200 and < 500)
            {
                record.Status = IdempotencyStatus.Completed;
                record.StatusCode = context.Response.StatusCode;
                record.ResponseBody = body;
                await SaveRecordAsync(db, context.RequestAborted);
            }
            else
            {
                db.IdempotencyRecords.Remove(record);
                await SaveRecordAsync(db, context.RequestAborted);
            }
            context.Response.Body = original;
            await context.Response.WriteAsync(body);
        }
        catch
        {
            context.Response.Body = original;
            try { db.IdempotencyRecords.Remove(record); await SaveRecordAsync(db, CancellationToken.None); }
            catch (Exception cleanup) { Serilog.Log.Error(cleanup, "Could not release idempotency key"); }
            throw;
        }
    }

    private static async Task SaveRecordAsync(ClaimsDbContext db, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            default: element.WriteTo(writer); break;
        }
    }
}
