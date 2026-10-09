using IplStore.Domain.Common;
using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace IplStore.Infrastructure.Data.Interceptors
{
    public class ConvertDomainEventsToOutboxMessagesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
        {
            DbContext? dbContext = eventData.Context;

            if (dbContext is null)
            {
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            // Intercept pending outbox operations or transactional events
            var outboxMessages = dbContext.ChangeTracker
                .Entries<Order>()
                .Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added)
                .Select(e => new OutboxMessage(
                    eventType: "OrderCreated",
                    payload: JsonSerializer.Serialize(new { OrderId = e.Entity.Id, e.Entity.UserId, e.Entity.TotalAmount })
                ))
                .ToList();

            if (outboxMessages.Any())
            {
                dbContext.Set<OutboxMessage>().AddRange(outboxMessages);
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
