using Trackit.Core.Domain;

namespace Trackit.Core.Ports
{
    // Defines methods for adding, retrieving, listing, and updating WorkOrder entities.
    public interface IWorkOrderRepository
    {
        Task<int> AddAsync(WorkOrder wo, CancellationToken ct = default);
        Task<WorkOrder?> GetAsync(int id, CancellationToken ct = default);
        Task<IReadOnlyList<WorkOrder>> ListOpenAsync(int creatorUserId, CancellationToken ct = default);
        Task UpdateAsync(WorkOrder wo, CancellationToken ct = default);
        Task<IReadOnlyList<DueSoonItem>> ListDueSoonAsync(int userId, DateTimeOffset nowUtc, DateTimeOffset untilUtc, string windowTag, CancellationToken ct = default);
        Task AddNotificationLogAsync(int workOrderId, string windowTag, DateTimeOffset sentAtUtc, CancellationToken ct = default);
}

    /*
    A port is an abstraction (interface) that defines how core logic communicates with the outside world — 
    without knowing the concrete implementation.

    In short: A port is a boundary contract.
    It keeps the domain layer independent of external technology choices.
    */
}
