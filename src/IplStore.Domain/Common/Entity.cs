using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Domain.Common
{
    public abstract class Entity
    {
        public Guid Id { get; protected init; } = Guid.NewGuid();
        public DateTime CreatedAtUtc { get; protected init; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; protected set; }
    }
}
