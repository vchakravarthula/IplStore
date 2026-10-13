namespace IplStore.Application.Products.Queries;

public sealed record ProductDto(
    Guid Id,
    string Name,
    string SKU,
    string Description,
    decimal Price,
    int StockQuantity,
    string ImageUrl,
    Guid FranchiseId,
    string FranchiseName,
    string FranchiseCode,
    Guid CategoryId,
    string CategoryName);
