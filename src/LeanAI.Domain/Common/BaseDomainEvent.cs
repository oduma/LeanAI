namespace LeanAI.Domain.Common;

public abstract record BaseDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
