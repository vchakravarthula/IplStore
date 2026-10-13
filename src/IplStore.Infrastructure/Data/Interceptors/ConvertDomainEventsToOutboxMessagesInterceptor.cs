using System.Text.Json;
using IplStore.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IplStore.Infrastructure.Data.Interceptors;

/// <summary>
/// Transactional outbox: domain events raised by aggregates are written as OutboxMessage rows inside the SAME
/// SaveChanges (same transaction) as the state change. Either both commit or neither does.
/// </summary>
public class ConvertDomainEventsToOutboxMessagesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Convert(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Convert(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Convert(DbContext? db)
    {
        if (db is null) return;

        var aggregates = db.ChangeTracker.Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                // Serialize by the RUNTIME type; serializing via the interface would drop every event property.
                var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());
                db.Set<OutboxMessage>().Add(new OutboxMessage(domainEvent.GetType().Name, payload));
            }

            aggregate.ClearDomainEvents();
        }
    }
}
