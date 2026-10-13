namespace IplStore.Domain.Exceptions;

/// <summary>Valid request that conflicts with current state (illegal transition, duplicate...). Maps to HTTP 409.</summary>
public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}
