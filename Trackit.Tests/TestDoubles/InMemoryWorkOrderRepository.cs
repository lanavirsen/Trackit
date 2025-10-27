using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trackit.Core.Domain;
using Trackit.Core.Ports;

namespace Trackit.Tests.TestDoubles;

// In-memory work order repository for testing purposes.
public sealed class InMemoryWorkOrderRepository : IWorkOrderRepository
{
    private readonly Dictionary<int, WorkOrder> _byId = new();
    private readonly Dictionary<(int WorkOrderId, string WindowTag), DateTimeOffset> _notificationLog = new();
    private readonly object _gate = new();
    private int _nextId;

    // AddAsync adds a new work order to the repository and returns the assigned work order ID.
    public Task<int> AddAsync(WorkOrder wo, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var id = ++_nextId;
            _byId[id] = Copy(wo with { Id = id });
            return Task.FromResult(id);
        }
    }

    // GetAsync retrieves a work order by its ID.
    public Task<WorkOrder?> GetAsync(int id, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_byId.TryGetValue(id, out var wo) ? Copy(wo) : null);
        }
    }

    // ListOpenAsync lists all open work orders for a given creator user ID.
    public Task<IReadOnlyList<WorkOrder>> ListOpenAsync(int creatorUserId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var list = _byId.Values
                .Where(w => w.CreatorUserId == creatorUserId && !w.Closed)
                .OrderBy(w => w.DueAtUtc)
                .Select(Copy)
                .ToList()
                .AsReadOnly();

            return Task.FromResult<IReadOnlyList<WorkOrder>>(list);
        }
    }

    // UpdateAsync updates an existing work order in the repository.
    public Task UpdateAsync(WorkOrder wo, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_byId.ContainsKey(wo.Id))
                throw new InvalidOperationException("Work order not found.");

            _byId[wo.Id] = Copy(wo);
        }

        return Task.CompletedTask;
    }

    // ListDueSoonAsync lists work orders that are due soon and have not yet been notified for the specified window tag.
    public Task<IReadOnlyList<DueSoonItem>> ListDueSoonAsync(int userId, DateTimeOffset nowUtc, DateTimeOffset untilUtc, string windowTag, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var items = _byId.Values
                .Where(w => w.CreatorUserId == userId &&
                            !w.Closed &&
                            w.DueAtUtc >= nowUtc &&
                            w.DueAtUtc < untilUtc &&
                            !_notificationLog.ContainsKey((w.Id, windowTag)))
                .OrderBy(w => w.DueAtUtc)
                .Select(w => new DueSoonItem(w.Id, w.Summary, w.DueAtUtc, w.Priority))
                .ToList()
                .AsReadOnly();

            return Task.FromResult<IReadOnlyList<DueSoonItem>>(items);
        }
    }

    // AddNotificationLogAsync records that a notification has been sent for a work order and window tag.
    public Task AddNotificationLogAsync(int workOrderId, string windowTag, DateTimeOffset sentAtUtc, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _notificationLog[(workOrderId, windowTag)] = sentAtUtc;
        }

        return Task.CompletedTask;
    }

    // ListByUserAsync lists all work orders for a given creator user ID.
    public Task<IReadOnlyList<WorkOrder>> ListByUserAsync(int creatorUserId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var list = _byId.Values
                .Where(w => w.CreatorUserId == creatorUserId)
                .OrderBy(w => w.Closed)
                .ThenBy(w => w.DueAtUtc)
                .Select(Copy)
                .ToList()
                .AsReadOnly();

            return Task.FromResult<IReadOnlyList<WorkOrder>>(list);
        }
    }

    // Copy creates a shallow copy of the given work order.
    // This ensures that modifications to the returned work order do not affect the stored version.
    private static WorkOrder Copy(WorkOrder source) => source with { };
}
