using System;
using System.Collections.Generic;
using System.Text;

namespace IplStore.Domain.Entities
{
    public enum OrderStatus
    {
        PendingPayment,
        Paid,
        Processing,
        Completed,
        Failed,
        Cancelled
    }
}
