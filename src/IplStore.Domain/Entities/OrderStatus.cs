namespace IplStore.Domain.Entities;

public enum OrderStatus
{
    PendingPayment,
    Paid,
    Processing,
    Completed,
    Failed,
    Cancelled
}
