namespace Trackit.Core.Domain
{
    public sealed record DueSoonItem(int Id, string Summary, DateTimeOffset DueAtUtc, Priority Priority);
}
