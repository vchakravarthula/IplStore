namespace IplStore.Domain.Entities;

/// <summary>Input to the Order constructor: the product facts to snapshot at purchase time.</summary>
public readonly record struct OrderLine(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
