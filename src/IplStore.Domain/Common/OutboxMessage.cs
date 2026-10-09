using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Domain.Common
{
    public class OutboxMessage : Entity
    {
        public string EventType { get; private set; } = default!;
        public string Payload { get; private set; } = default!;
        public DateTime? ProcessedAtUtc { get; private set; }
        public string? Error { get; private set; }

        private OutboxMessage() { }

        public OutboxMessage(string eventType, string payload)
        {
            EventType = eventType;
            Payload = payload;
        }

        public void MarkAsProcessed()
        {
            ProcessedAtUtc = DateTime.UtcNow;
        }

        public void MarkAsFailed(string error)
        {
            Error = error;
        }
    }
}
