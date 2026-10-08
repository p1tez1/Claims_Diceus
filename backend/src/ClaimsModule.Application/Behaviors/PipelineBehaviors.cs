using ClaimsModule.Application.Abstractions;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(IRequestLog logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        logger.Information("Handling", typeof(TRequest).Name);
        try
        {
            var response = await next(cancellationToken);
            logger.Information("Handled", typeof(TRequest).Name);
            return response;
        }
        catch (Exception exception)
        {
            logger.Failure(exception, "Failed", typeof(TRequest).Name);
            throw;
        }
    }
}

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators,
    IValidationWarnings warnings) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken);
            failures.AddRange(result.Errors);
        }
        warnings.AddRange(failures.Where(x => x.Severity == Severity.Warning));
        var errors = failures.Where(x => x.Severity == Severity.Error).ToArray();
        if (errors.Length != 0) throw new ValidationException(errors);
        return await next(cancellationToken);
    }
}

public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork,
    IDomainEventDispatcher dispatcher, IRequestLog logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var isCommand = request is ICommand<TResponse>;
        if (request is not IQuery<TResponse> && !isCommand)
            throw new InvalidOperationException($"{typeof(TRequest).Name} must implement ICommand<T> or IQuery<T>.");
        if (!isCommand) return await next(cancellationToken);
        var result = await unitOfWork.ExecuteAsync(token => next(token), dispatcher, cancellationToken);
        foreach (var error in result.Commit.PostCommitErrors)
            logger.Failure(error, "Post-commit action failed", typeof(TRequest).Name);
        return result.Value;
    }
}
