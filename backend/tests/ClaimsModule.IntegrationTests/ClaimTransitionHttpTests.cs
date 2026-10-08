using System.Text.Json;
using ClaimsModule.API;
using ClaimsModule.Application.Exceptions;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Rules;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace ClaimsModule.IntegrationTests;

public sealed class ClaimTransitionHttpTests
{
    [Fact]
    public async Task EveryInvalidStatePairReturns422AndItsValidNextList()
    {
        var machine = new ClaimStateMachine();
        foreach (var from in Enum.GetValues<ClaimStatus>())
            foreach (var to in Enum.GetValues<ClaimStatus>())
            {
                if (machine.Transitions.Any(edge => edge.From == from && edge.To == to)) continue;
                var evaluation = machine.Evaluate(from, to,
                    new(UserRole.Manager, true, false, false, true, true, false, false));
                var context = new DefaultHttpContext();
                context.Response.Body = new MemoryStream();
                await ApiExceptionMiddleware.WriteExceptionAsync(context, new TransitionBlockedException(from, to, evaluation), false);
                context.Response.StatusCode.ShouldBe(422);
                context.Response.Body.Position = 0;
                using var json = await JsonDocument.ParseAsync(context.Response.Body);
                json.RootElement.GetProperty("title").GetString().ShouldBe($"Transition from {from} to {to} is not permitted.");
                var next = json.RootElement.GetProperty("errors").GetProperty("ValidNextStatuses")
                    .EnumerateArray().Select(x => x.GetString()).ToArray();
                next.ShouldBe(machine.Transitions.Where(edge => edge.From == from).Select(edge => edge.To.ToString()).ToArray());
            }
    }

    [Fact]
    public async Task InvalidEdgeHasExactTitleAndValidNextStatuses()
    {
        var evaluation = new ClaimStateMachine().Evaluate(ClaimStatus.Draft, ClaimStatus.Closed,
            new(UserRole.Handler, true, false, false, true, false, false, false));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await ApiExceptionMiddleware.WriteExceptionAsync(context,
            new TransitionBlockedException(ClaimStatus.Draft, ClaimStatus.Closed, evaluation), false);
        context.Response.StatusCode.ShouldBe(422);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        json.RootElement.GetProperty("title").GetString().ShouldBe("Transition from Draft to Closed is not permitted.");
        json.RootElement.GetProperty("errors").GetProperty("ValidNextStatuses")[0].GetString().ShouldBe("Open");
    }

    [Fact]
    public async Task ClosureBlockersRemainIndividuallyKeyed()
    {
        var evaluation = new ClaimStateMachine().Evaluate(ClaimStatus.Open, ClaimStatus.Closed,
            new(UserRole.Handler, false, true, false, true, false, true, true));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await ApiExceptionMiddleware.WriteExceptionAsync(context,
            new TransitionBlockedException(ClaimStatus.Open, ClaimStatus.Closed, evaluation), false);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        var errors = json.RootElement.GetProperty("errors");
        foreach (var code in new[] { "CC-01", "CC-02", "CC-03", "CC-04" })
            errors.GetProperty(code).GetArrayLength().ShouldBe(1);
    }
}
