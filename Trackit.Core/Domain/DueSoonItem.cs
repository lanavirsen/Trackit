namespace Trackit.Core.Domain
{
    // Record is a reference type that provides built-in functionality for encapsulating data.
    // DueSoonItem represents a task or item that is due soon.

    // In this case, DueSoonItem is a record because it’s meant to be a simple, immutable data container —
    // a value object representing “a work order that’s due soon”.
    public sealed record DueSoonItem(int Id, string Summary, DateTimeOffset DueAtUtc, Priority Priority);
}
