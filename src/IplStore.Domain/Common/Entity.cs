using System.ComponentModel.DataAnnotations.Schema;

namespace IplStore.Domain.Common;

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    // Version 7 GUIDs are time-ordered: far fewer clustered-index page splits than Guid.NewGuid().
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
    public DateTime CreatedAtUtc { get; protected init; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; protected set; }

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
