using System.Linq;
using Trackit.Core.Domain;
using Trackit.Core.Ports;

namespace Trackit.Core.Services
{
    // Service class encapsulating business logic for managing work orders.
    public sealed class WorkOrderService
    {
        private readonly IWorkOrderRepository _repo;
        private readonly Func<DateTimeOffset> _nowUtc;
        private readonly INotificationService? _notifications;

        private const int SummaryMax = 200;
        private const int DetailsMax = 4000;

        private static string Norm(string? s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();

        private static void ValidateSummary(string summary)
        {
            if (summary.Length is < 1 or > SummaryMax)
                throw new ArgumentException($"Summary must be 1..{SummaryMax} characters.");
        }

        private static void ValidateDetails(string? details)
        {
            if (details is { Length: > DetailsMax })
                throw new ArgumentException($"Details must be ≤ {DetailsMax} characters.");
        }

        // Constructor accepting a repository and an optional function to get the current UTC time.
        public WorkOrderService(IWorkOrderRepository repo, Func<DateTimeOffset>? nowUtc = null, INotificationService? notifications = null)
        {
            _repo = repo;
            _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
            _notifications = notifications;
        }

        // Suggests a priority level based on the due date.
        public Priority SuggestPriority(DateTimeOffset dueAtUtc)
        {
            var now = _nowUtc();
            var delta = dueAtUtc - now;
            if (delta < TimeSpan.Zero) return Priority.High;
            if (delta < TimeSpan.FromHours(24)) return Priority.High;
            if (delta < TimeSpan.FromHours(72)) return Priority.Medium;
            return Priority.Low;
        }

        // Adds a new work order and returns the generated database ID.
        public async Task<int> AddAsync(int creatorUserId, string summary, string? details, DateTimeOffset dueAtUtc, Priority? priority = null, bool allowPastDue = false, CancellationToken ct = default)
        {
            // normalize & validate
            summary = Norm(summary);
            details = Norm(details) is "" ? null : Norm(details);
            ValidateSummary(summary);
            ValidateDetails(details);

            var now = _nowUtc();
            var dueUtc = dueAtUtc.ToUniversalTime();
            if (!allowPastDue && dueUtc < now)
                throw new InvalidOperationException("Due cannot be in the past.");

            var wo = new WorkOrder
            {
                CreatorUserId = creatorUserId,
                Summary = summary,
                Details = details,
                DueAtUtc = dueUtc,
                Priority = priority ?? SuggestPriority(dueUtc),
                Stage = Stage.Open,
                Closed = false,
                ClosedAtUtc = null,
                ClosedReason = null,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            return await _repo.AddAsync(wo, ct);
        }

        // Lists all open work orders for a specific creator user.
        public Task<IReadOnlyList<WorkOrder>> ListOpenAsync(int creatorUserId, CancellationToken ct = default)
            => _repo.ListOpenAsync(creatorUserId, ct);

        // Retrieves a work order by its ID.
        public Task<WorkOrder?> GetByIdAsync(int id, CancellationToken ct = default)
            => _repo.GetAsync(id, ct);

        public async Task CloseAsync(int id, int actorUserId, CloseReason reason, CancellationToken ct = default)
        {
            var existing = await _repo.GetAsync(id, ct) ?? throw new InvalidOperationException("Work order not found.");
            if (existing.CreatorUserId != actorUserId) throw new InvalidOperationException("Not owner.");
            if (existing.Closed) throw new InvalidOperationException("Already closed.");

            var now = _nowUtc();
            var updated = existing with
            {
                Stage = Stage.Closed,
                Closed = true,
                ClosedAtUtc = now,
                ClosedReason = reason,
                UpdatedAtUtc = now
            };
            await _repo.UpdateAsync(updated, ct);
        }

        // Changes the stage of a work order.
        public async Task ChangeStageAsync(int id, int actorUserId, Stage newStage, CancellationToken ct = default)
        {
            var existing = await _repo.GetAsync(id, ct) ?? throw new InvalidOperationException("Work order not found.");
            if (existing.CreatorUserId != actorUserId) throw new InvalidOperationException("Not owner.");
            if (existing.Closed && newStage != Stage.Closed) throw new InvalidOperationException("Closed items cannot move stages.");

            var now = _nowUtc();
            var updated = existing with
            {
                Stage = newStage,
                // keep Closed flags consistent
                Closed = newStage == Stage.Closed || existing.Closed,
                ClosedAtUtc = newStage == Stage.Closed && existing.ClosedAtUtc is null ? now : existing.ClosedAtUtc,
                UpdatedAtUtc = now
            };
            await _repo.UpdateAsync(updated, ct);
        }
        // Idempotent due-soon notifications using NotificationLog and INotificationService.
        public async Task<IReadOnlyList<DueSoonItem>> SendDueNotificationsAsync(int userId, string toEmail, TimeSpan window, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) throw new ArgumentException("Recipient email required.", nameof(toEmail));
            if (_notifications is null) throw new InvalidOperationException("Email sender not configured.");
            if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window), "Window must be positive.");

            var now = _nowUtc();
            var until = now.Add(window);
            var windowTag = $"{(int)window.TotalHours}h";

            var items = (await _repo.ListDueSoonAsync(userId, now, until, windowTag, ct)).ToList();
            foreach (var item in items)
            {
                var localDue = item.DueAtUtc.ToLocalTime();
                await _notifications.SendWorkOrderDueNotificationAsync(toEmail, item.Summary, localDue, ct);
                await _repo.AddNotificationLogAsync(item.Id, windowTag, now, ct);
            }
            return items;
        }

        // Aggregate counts of work orders by stage for a specific user.
        public async Task<(int Total, int Open, int InProgress, int AwaitingParts, int Closed)> GetStageCountsAsync(int userId, CancellationToken ct = default)
        {
            var all = await _repo.ListByUserAsync(userId, ct);
            var open = all.Count(x => x.Stage == Stage.Open);
            var prog = all.Count(x => x.Stage == Stage.InProgress);
            var parts = all.Count(x => x.Stage == Stage.AwaitingParts);
            var closed = all.Count(x => x.Stage == Stage.Closed);
            return (all.Count, open, prog, parts, closed);
        }

        // Aggregate counts of work orders by priority for a specific user.
        public async Task<(int High, int Medium, int Low)> GetPriorityCountsAsync(int userId, CancellationToken ct = default)
        {
            var all = await _repo.ListByUserAsync(userId, ct);
            var high = all.Count(x => x.Priority == Priority.High);
            var med = all.Count(x => x.Priority == Priority.Medium);
            var low = all.Count(x => x.Priority == Priority.Low);
            return (high, med, low);
        }

    }
}
