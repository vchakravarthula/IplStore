using IplStore.Domain.Common;
using IplStore.Domain.Exceptions;

namespace IplStore.Domain.Entities;

public class Franchise : Entity, IAggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!; // e.g. "CSK", "MI"
    public string LogoUrl { get; private set; } = default!;

    private Franchise() { }

    public Franchise(string name, string code, string logoUrl)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ValidationException("Franchise name is required.");
        if (string.IsNullOrWhiteSpace(code)) throw new ValidationException("Franchise code is required.");

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        LogoUrl = logoUrl ?? string.Empty;
    }
}
