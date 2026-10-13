namespace IplStore.Application.Common.Exceptions;

/// <summary>A required dependency (Redis, broker...) is down or timing out. Maps to HTTP 503 + Retry-After.</summary>
public sealed class DependencyUnavailableException : Exception
{
    public string Dependency { get; }

    public DependencyUnavailableException(string dependency, Exception? inner = null)
        : base($"{dependency} is unavailable.", inner)
    {
        Dependency = dependency;
    }
}
