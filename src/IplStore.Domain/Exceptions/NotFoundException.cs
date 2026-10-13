namespace IplStore.Domain.Exceptions;

public class NotFoundException : DomainException
{
    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} '{key}' was not found.") { }

    public NotFoundException(string message) : base(message) { }
}
