using Trackit.Core.Domain;
using Trackit.Core.Ports;

namespace Trackit.Core.Services
{
    // Service class encapsulating business logic for managing work orders.
    public sealed class WorkOrderService
    {
        private readonly IWorkOrderRepository _repo;
        private readonly Func<DateTimeOffset> _nowUtc;
        private readonly IEmailSender? _email;

        // Constructor accepting a repository and an optional function to get the current UTC time.
        public WorkOrderService(IWorkOrderRepository repo, Func<DateTimeOffset>? nowUtc = null, IEmailSender? email = null)
        {
            _repo = repo;
            _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
            _email = email;
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
        public async Task<int> AddAsync(int creatorUserId, string summary, string? details, DateTimeOffset dueAtUtc, Priority? priority = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(summary)) throw new ArgumentException("Summary required", nameof(summary));
            var now = _nowUtc();
            var wo = new WorkOrder
            {
                CreatorUserId = creatorUserId,
                Summary = summary.Trim(),
                Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
                DueAtUtc = dueAtUtc.ToUniversalTime(),
                Priority = priority ?? SuggestPriority(dueAtUtc),
                Stage = Stage.Open,
                Closed = false,
                ClosedAtUtc = null,
                ClosedReason = null,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            return await _repo.AddAsync(wo, ct);
        }

        // Retrieves a work order by its ID.
        public Task<IReadOnlyList<WorkOrder>> ListOpenAsync(int creatorUserId, CancellationToken ct = default)
            => _repo.ListOpenAsync(creatorUserId, ct);

        public async Task CloseAsync(int id, int actorUserId, CloseReason reason, CancellationToken ct = default)
        {
            var existing = await _repo.GetAsync(id, ct) ?? throw new InvalidOperationException("Work order not found");
            if (existing.CreatorUserId != actorUserId) throw new InvalidOperationException("Not owner");
            if (existing.Closed) throw new InvalidOperationException("Already closed");

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
            var existing = await _repo.GetAsync(id, ct) ?? throw new InvalidOperationException("Work order not found");
            if (existing.CreatorUserId != actorUserId) throw new InvalidOperationException("Not owner");
            if (existing.Closed && newStage != Stage.Closed) throw new InvalidOperationException("Closed items cannot move stages");

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
        // Idempotent due-soon notifications using NotificationLog and IEmailSender.
        public async Task<int> SendDueNotificationsAsync(int UserId, string toEmail, TimeSpan window, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) throw new ArgumentException("Recipient email required", nameof(toEmail));
            if (_email is null) throw new InvalidOperationException("Email sender not configured");

            var now = _nowUtc();
            var until = now.Add(window);
            var windowTag = $"{(int)window.TotalHours}h";

            var items = await _repo.ListDueSoonAsync(UserId, now, until, windowTag, ct);
            var count = 0;

            foreach (var item in items)
            {
                var localDue = item.DueAtUtc.ToLocalTime();
                var subject = $"Due soon: {item.Summary} ({localDue:yyyy-MM-dd HH:mm})";
                var html = $@"<h3>Work order due soon</h3>
                              <p><strong>{System.Net.WebUtility.HtmlEncode(item.Summary)}</strong></p>
                              <p>Priority: {item.Priority}</p>
                              <p>Due (local): {localDue:yyyy-MM-dd HH:mm}</p>";

                await _email.SendEmailAsync(toEmail, subject, html, null, ct);
                await _repo.AddNotificationLogAsync(item.Id, windowTag, now, ct);
                count++;
            }
            return count;
        }
    }
}
