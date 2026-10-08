using FluentValidation.Results;
using MediatR;

namespace ClaimsModule.Application.Abstractions;

public interface ICommand<out T> : IRequest<T>;
public interface IQuery<out T> : IRequest<T>;

public interface IValidationWarnings
{
    IReadOnlyList<ValidationFailure> Items { get; }
    void AddRange(IEnumerable<ValidationFailure> warnings);
}

public interface IRequestLog
{
    void Information(string action, string requestType);
    void Failure(Exception exception, string action, string requestType);
}

internal sealed class ValidationWarnings : IValidationWarnings
{
    private readonly List<ValidationFailure> _items = [];
    public IReadOnlyList<ValidationFailure> Items => _items;
    public void AddRange(IEnumerable<ValidationFailure> warnings) => _items.AddRange(warnings);
}
