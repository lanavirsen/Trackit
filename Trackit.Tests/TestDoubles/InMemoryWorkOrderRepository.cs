using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trackit.Core.Domain;
using Trackit.Core.Ports;

namespace Trackit.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IWorkOrderRepository"/> used exclusively by tests.
/// </summary>
public sealed class InMemoryWorkOrderRepository : IWorkOrderRepository
{
    private readonly Dictionary<int, WorkOrder> _byId = new();
    private readonly Dictionary<(int WorkOrderId, string WindowTag), DateTimeOffset> _notificationLog = new();
    private readonly object _gate = new();
    private int _nextId;

    public Task<int> AddAsync(WorkOrder wo, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var id = ++_nextId;
            _byId[id] = Copy(wo with { Id = id });
            return Task.FromResult(id);
        }
    }

    public Task<WorkOrder?> GetAsync(int id, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_byId.TryGetValue(id, out var wo) ? Copy(wo) : null);
        }
    }

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

    public Task AddNotificationLogAsync(int workOrderId, string windowTag, DateTimeOffset sentAtUtc, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _notificationLog[(workOrderId, windowTag)] = sentAtUtc;
        }

        return Task.CompletedTask;
    }

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

    private static WorkOrder Copy(WorkOrder source) => source with { };
}
