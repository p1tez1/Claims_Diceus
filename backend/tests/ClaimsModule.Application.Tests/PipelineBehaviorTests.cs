using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Behaviors;
using FluentValidation;
using MediatR;
using NSubstitute;
using Shouldly;

namespace ClaimsModule.Application.Tests;

public sealed class PipelineBehaviorTests
{
    private sealed record TestCommand(string Value) : ICommand<string>;
    private sealed record TestQuery : IQuery<string>;
    private sealed class TestValidator : AbstractValidator<TestCommand>
    {
        public TestValidator()
        {
            RuleFor(x => x.Value).NotEmpty().WithSeverity(Severity.Error);
            RuleFor(x => x.Value).Must(x => x != "warn").WithMessage("Review this value").WithSeverity(Severity.Warning);
        }
    }

    [Fact]
    public async Task WarningIsCollectedAndHandlerRuns()
    {
        var warnings = Substitute.For<IValidationWarnings>();
        var collected = new List<FluentValidation.Results.ValidationFailure>();
        warnings.When(x => x.AddRange(Arg.Any<IEnumerable<FluentValidation.Results.ValidationFailure>>()))
            .Do(call => collected.AddRange(call.Arg<IEnumerable<FluentValidation.Results.ValidationFailure>>()));
        var behavior = new ValidationBehavior<TestCommand, string>([new TestValidator()], warnings);
        var result = await behavior.Handle(new("warn"), _ => Task.FromResult("handled"), default);
        result.ShouldBe("handled");
        collected.Count.ShouldBe(1);
        collected[0].Severity.ShouldBe(Severity.Warning);
    }

    [Fact]
    public async Task ErrorStopsHandlerWithValidationException()
    {
        var warnings = Substitute.For<IValidationWarnings>();
        var behavior = new ValidationBehavior<TestCommand, string>([new TestValidator()], warnings);
        var ran = false;
        await Should.ThrowAsync<ValidationException>(() => behavior.Handle(new(""), _ =>
        { ran = true; return Task.FromResult("handled"); }, default));
        ran.ShouldBeFalse();
    }

    [Fact]
    public async Task QuerySkipsTransactionAndCommandUsesUnitOfWork()
    {
        var unit = Substitute.For<IUnitOfWork>();
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        var log = Substitute.For<IRequestLog>();
        var query = new TransactionBehavior<TestQuery, string>(unit, dispatcher, log);
        (await query.Handle(new(), _ => Task.FromResult("query"), default)).ShouldBe("query");
        await unit.DidNotReceiveWithAnyArgs().ExecuteAsync<string>(default!, default!, default);

        unit.ExecuteAsync(Arg.Any<Func<CancellationToken, Task<string>>>(), dispatcher, Arg.Any<CancellationToken>())
            .Returns(async call => new TransactionResult<string>(await call.Arg<Func<CancellationToken, Task<string>>>()(default), new CommitResult([])));
        var command = new TransactionBehavior<TestCommand, string>(unit, dispatcher, log);
        (await command.Handle(new("value"), _ => Task.FromResult("command"), default)).ShouldBe("command");
        await unit.Received(1).ExecuteAsync(Arg.Any<Func<CancellationToken, Task<string>>>(), dispatcher, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoggingBehaviorRecordsStartAndCompletion()
    {
        var log = Substitute.For<IRequestLog>();
        var behavior = new LoggingBehavior<TestQuery, string>(log);
        (await behavior.Handle(new(), _ => Task.FromResult("done"), default)).ShouldBe("done");
        Received.InOrder(() =>
        {
            log.Information("Handling", nameof(TestQuery));
            log.Information("Handled", nameof(TestQuery));
        });
    }
}
