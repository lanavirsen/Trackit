namespace Trackit.Core.Domain
{
    public sealed class WorkOrder
    {
        public int Id { get; init; }
        public int CreatorUserId { get; init; }
        public string Summary { get; init; } = null!;
        public string? Details { get; init; }
        public DateTimeOffset DueAtUtc { get; init; }
        public Priority Priority { get; init; }
        public bool Closed { get; init; }
        public DateTimeOffset? ClosedAtUtc { get; init; }
        public CloseReason? ClosedReason { get; init; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public DateTimeOffset UpdatedAtUtc { get; init; }
    }
}
