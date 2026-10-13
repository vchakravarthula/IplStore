namespace IplStore.Domain.Exceptions;

public class InsufficientStockException : DomainException
{
    public string ProductId { get; }
    public int RequestedQuantity { get; }
    public int AvailableQuantity { get; }

    public InsufficientStockException(string productId, int requested, int available)
        : base($"Insufficient stock for product '{productId}'. Requested: {requested}, available: {available}.")
    {
        ProductId = productId;
        RequestedQuantity = requested;
        AvailableQuantity = available;
    }
}
