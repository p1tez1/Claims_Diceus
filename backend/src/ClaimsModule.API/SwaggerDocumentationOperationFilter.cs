using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ClaimsModule.API;

public sealed class SwaggerDocumentationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        NoteAuthorization(operation, context.MethodInfo);

        if (context.MethodInfo.DeclaringType?.Name == "ClaimsController" && context.MethodInfo.Name == "Create")
        {
            SetRequestExample(operation, """
                {"lossDate":"2026-09-30T10:00:00Z","lossDescription":"Rear-end collision damaged the insured vehicle.","causeOfLossCode":"COL-VEH-COL","lossLocation":"Kyiv","policyId":null,"severity":"Standard","parties":[{"role":"Claimant","type":"Person","firstName":"Ada","lastName":"Lovelace","email":"ada@example.com"}],"riskObjects":[{"assetType":"Vehicle","assetDescription":"2024 sedan","damageDescription":"Rear bumper damage","isPrimary":true}]}
                """);
            SetResponseExample(operation, "201", """
                {"id":"0199a447-7c00-7000-8000-000000000001","claimNumber":"CLM-2026-0000001","status":"Draft","severity":"Standard","parties":[{"role":"Claimant","type":"Person","firstName":"Ada","lastName":"Lovelace"}],"riskObjects":[{"assetType":"Vehicle","assetDescription":"2024 sedan","isPrimary":true}],"reserves":[],"documents":[],"audit":[{"eventType":"CLAIM_CREATED","description":"Claim created"}]}
                """);
        }
        else if (context.MethodInfo.DeclaringType?.Name == "ReservesController" && context.MethodInfo.Name == "Submit")
        {
            SetRequestExample(operation, """
                {"component":"Indemnity","amount":12500.00,"changeReason":"Initial repair estimate","transactionType":"Add"}
                """);
            SetResponseExample(operation, "201", """
                {"transaction":{"id":"0199a447-7c00-7000-8000-000000000002","transactionType":"Add","amount":12500.00,"changeSequence":1,"changeReason":"Initial repair estimate","approvalStatus":"PendingApproval","postingStatus":"Pending"},"approvalStatus":"PendingApproval","warnings":[]}
                """);
        }
    }

    private static void NoteAuthorization(OpenApiOperation operation, MethodInfo method)
    {
        var attributes = method.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>(true)
            .Concat(method.GetCustomAttributes<AuthorizeAttribute>(true)).ToArray();
        if (method.GetCustomAttribute<AllowAnonymousAttribute>(true) is not null) return;
        var requirements = attributes.Select(x => x.Policy ?? x.Roles).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        var note = requirements.Length == 0 ? "Requires an authenticated JWT bearer token."
            : $"Requires an authenticated JWT bearer token. Authorization: {string.Join(", ", requirements)}.";
        operation.Description = string.IsNullOrWhiteSpace(operation.Description) ? note : operation.Description + "\n\n" + note;
    }

    private static void SetRequestExample(OpenApiOperation operation, string json)
    {
        if (operation.RequestBody?.Content is not null
            && operation.RequestBody.Content.TryGetValue("application/json", out var media) && media is not null)
            media.Example = JsonNode.Parse(json);
    }

    private static void SetResponseExample(OpenApiOperation operation, string status, string json)
    {
        if (operation.Responses is not null && operation.Responses.TryGetValue(status, out var response)
            && response?.Content is not null && response.Content.TryGetValue("application/json", out var media)
            && media is not null)
            media.Example = JsonNode.Parse(json);
    }
}
