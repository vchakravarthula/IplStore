using IplStore.Domain.Common;

namespace IplStore.Domain.Events;

public abstract record DomainEventBase : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}

public sealed record OrderPlaced(Guid OrderId, string UserId, string ShortOrderNumber, decimal TotalAmount) : DomainEventBase;

public sealed record OrderPaid(Guid OrderId, string UserId, string ShortOrderNumber, decimal TotalAmount, string PaymentId) : DomainEventBase;
