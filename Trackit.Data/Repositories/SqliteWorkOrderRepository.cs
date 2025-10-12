using Dapper;
using Trackit.Core.Domain;
using Trackit.Core.Ports;
using Trackit.Data.Sqlite;

namespace Trackit.Data.Repositories
{
    // Implement IWorkOrderRepository using SQLite and Dapper for data access.
    // Dapper is a lightweight ORM (Object-Relational Mapper) for .NET.
    public sealed class SqliteWorkOrderRepository : IWorkOrderRepository
    {
        private readonly DapperConnectionFactory _factory;

        // Constructor that takes a DapperConnectionFactory to create database connections.
        public SqliteWorkOrderRepository(DapperConnectionFactory factory) => _factory = factory;

        // Add a new WorkOrder to the database and return the generated Id.
        public async Task<int> AddAsync(WorkOrder wo, CancellationToken ct = default)
        {
            const string sql = @"
INSERT INTO WorkOrders (CreatorUserId, Summary, Details, DueAtUtc, Priority, Stage, Closed, ClosedAtUtc, ClosedReason, CreatedAtUtc, UpdatedAtUtc)
VALUES (@CreatorUserId, @Summary, @Details, @DueAtUtc, @Priority, @Stage, @Closed, @ClosedAtUtc, @ClosedReason, @CreatedAtUtc, @UpdatedAtUtc);
SELECT last_insert_rowid();";
            using var conn = _factory.Create();
            var id = await conn.ExecuteScalarAsync<long>(sql, ToRow(wo));
            return checked((int)id);
        }

        // Retrieve a WorkOrder by its ID from the database.
        public async Task<WorkOrder?> GetAsync(int id, CancellationToken ct = default)
        {
            const string sql = @"SELECT * FROM WorkOrders WHERE Id=@id LIMIT 1;";
            using var conn = _factory.Create();
            var row = await conn.QuerySingleOrDefaultAsync<Row>(sql, new { id });
            return row?.ToDomain();
        }

        // List all open WorkOrders for a specific creator user, ordered by due date.
        public async Task<IReadOnlyList<WorkOrder>> ListOpenAsync(int creatorUserId, CancellationToken ct = default)
        {
            const string sql = @"SELECT * FROM WorkOrders
                             WHERE CreatorUserId=@u AND Closed=0
                             ORDER BY DueAtUtc ASC;";
            using var conn = _factory.Create();
            var rows = await conn.QueryAsync<Row>(sql, new { u = creatorUserId });

            // Map each Row to a WorkOrder domain object and return as a list.
            return rows.Select(r => r.ToDomain()).ToList();
        }

        // Update an existing WorkOrder in the database.
        public async Task UpdateAsync(WorkOrder wo, CancellationToken ct = default)
        {
            const string sql = @"
                             UPDATE WorkOrders SET
                             Summary=@Summary,
                             Details=@Details,
                             DueAtUtc=@DueAtUtc,
                             Priority=@Priority,
                             Stage=@Stage,
                             Closed=@Closed,
                             ClosedAtUtc=@ClosedAtUtc,
                             ClosedReason=@ClosedReason,
                             UpdatedAtUtc=@UpdatedAtUtc
                             WHERE Id=@Id;";
            using var conn = _factory.Create();

            // Execute the update command with the WorkOrder data.
            await conn.ExecuteAsync(sql, ToRow(wo));
        }

        // Convert a WorkOrder domain object to an anonymous object matching the database schema.
        private static object ToRow(WorkOrder wo) => new
        {
            wo.Id,
            wo.CreatorUserId,
            wo.Summary,
            wo.Details,
            DueAtUtc = wo.DueAtUtc.UtcDateTime.ToString("O"),
            Priority = (int)wo.Priority,
            Stage = (int)wo.Stage,
            Closed = wo.Closed ? 1 : 0,
            ClosedAtUtc = wo.ClosedAtUtc?.UtcDateTime.ToString("O"),
            ClosedReason = wo.ClosedReason.HasValue ? (int)wo.ClosedReason.Value : (int?)null,
            CreatedAtUtc = wo.CreatedAtUtc.UtcDateTime.ToString("O"),
            UpdatedAtUtc = wo.UpdatedAtUtc.UtcDateTime.ToString("O"),
        };

        // Internal class representing a row in the WorkOrders table.
        private sealed class Row
        {
            public long Id { get; init; }
            public long CreatorUserId { get; init; }
            public string Summary { get; init; } = null!;
            public string? Details { get; init; }
            public string DueAtUtc { get; init; } = null!;
            public int Priority { get; init; }
            public int Stage { get; init; }
            public int Closed { get; init; }
            public string? ClosedAtUtc { get; init; }
            public int? ClosedReason { get; init; }
            public string CreatedAtUtc { get; init; } = null!;
            public string UpdatedAtUtc { get; init; } = null!;

            // Convert this Row to a WorkOrder domain object.
            public WorkOrder ToDomain() => new()
            {
                Id = checked((int)Id),
                CreatorUserId = checked((int)CreatorUserId),
                Summary = Summary,
                Details = Details,
                DueAtUtc = DateTimeOffset.Parse(DueAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
                Priority = (Priority)Priority,
                Stage = (Stage)Stage,
                Closed = Closed != 0,
                ClosedAtUtc = ClosedAtUtc is null ? null :
                    DateTimeOffset.Parse(ClosedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
                ClosedReason = ClosedReason is null ? null : (CloseReason)ClosedReason,
                CreatedAtUtc = DateTimeOffset.Parse(CreatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
                UpdatedAtUtc = DateTimeOffset.Parse(UpdatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
            };
        }
    }
}
