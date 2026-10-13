using IplStore.Domain.Common;
using IplStore.Domain.Exceptions;

namespace IplStore.Domain.Entities;

public class Category : Entity, IAggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;

    private Category() { }

    public Category(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ValidationException("Category name is required.");
        if (string.IsNullOrWhiteSpace(slug)) throw new ValidationException("Category slug is required.");

        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
    }
}
