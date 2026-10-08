using AutoMapper;
using AutoMapper.Internal;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Behaviors;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Reserves;
using ClaimsModule.Application.Storage;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace ClaimsModule.Application.Tests;

public sealed class CheckpointValidationTests
{
    [Fact]
    public void EveryProductionCommandHasRegisteredHandlerAndValidator()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        var assembly = typeof(CreateClaimCommand).Assembly;
        var commands = assembly.GetTypes().Where(x => x.GetInterfaces().Any(i => i.IsGenericType
            && i.GetGenericTypeDefinition() == typeof(ICommand<>))).ToArray();
        commands.ShouldNotBeEmpty();
        foreach (var command in commands)
        {
            var response = command.GetInterfaces().Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>))
                .GetGenericArguments()[0];
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(command, response);
            assembly.GetTypes().Count(handlerType.IsAssignableFrom).ShouldBe(1, command.Name);
            services.ShouldContain(s => s.ServiceType == typeof(IValidator<>).MakeGenericType(command), command.Name);
        }
        var maps = new MapperConfiguration(config => config.AddProfile<ClaimMappingProfile>()).Internal().GetAllTypeMaps();
        maps.Where(x => !x.HasTypeConverter).ShouldAllBe(x => x.MaxDepth > 0 && x.MaxDepth <= 32);
    }

    [Fact]
    public async Task EmptyIdsInSimpleCommandsAreRejectedBeforeTheHandlerRuns()
    {
        var id = Guid.NewGuid();
        await Reject(new RemovePartyCommand(Guid.Empty, id), new RemovePartyCommandValidator());
        await Reject(new RemovePartyCommand(id, Guid.Empty), new RemovePartyCommandValidator());
        await Reject(new UpdateClaimNotesCommand(Guid.Empty, null), new UpdateClaimNotesCommandValidator());
        await Reject(new ApproveReserveCommand(Guid.Empty, id), new ApproveReserveCommandValidator());
        await Reject(new ApproveReserveCommand(id, Guid.Empty), new ApproveReserveCommandValidator());
        await Reject(new RetractReserveCommand(Guid.Empty, id), new RetractReserveCommandValidator());
        await Reject(new RetractReserveCommand(id, Guid.Empty), new RetractReserveCommandValidator());
        await Reject(new RetryGlPostingCommand(Guid.Empty, id), new RetryGlPostingCommandValidator());
        await Reject(new RetryGlPostingCommand(id, Guid.Empty), new RetryGlPostingCommandValidator());
        await Reject(new SetReserveOverrideCommand(Guid.Empty), new SetReserveOverrideCommandValidator());
    }

    private static async Task Reject<T>(T command, IValidator<T> validator) where T : notnull
    {
        var ran = false;
        var pipeline = new ValidationBehavior<T, Unit>([validator], Substitute.For<IValidationWarnings>());
        await Should.ThrowAsync<ValidationException>(() => pipeline.Handle(command, _ =>
        {
            ran = true;
            return Task.FromResult(Unit.Value);
        }, default));
        ran.ShouldBeFalse();
    }
}
