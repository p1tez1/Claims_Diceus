using ClaimsModule.Domain.Events;

namespace ClaimsModule.Domain.Common;

public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];
    private byte[] _rowVersion = [];

    protected AggregateRoot(Guid id)
    {
        Id = RuleGuard.Id(id, nameof(id));
    }

    public Guid Id { get; private set; }
    public byte[] RowVersion
    {
        get => (byte[])_rowVersion.Clone();
        private set => _rowVersion = (byte[])value.Clone();
    }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
