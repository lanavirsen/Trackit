using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Trackit.Core.Domain;
using Trackit.Core.Services;
using Trackit.Core.Ports;
using Trackit.Data.Repositories;
using Trackit.Data.Sqlite;
using Xunit;

namespace Trackit.Tests
{
    public class WorkOrderServiceTests
    {
        // Helper method to create a new WorkOrderService with a temporary SQLite database.
        private static async Task<(WorkOrderService svc, int userId)> NewSvcAsync()
        {
            var db = Path.GetTempFileName();
            var factory = new DapperConnectionFactory($"Data Source={db};Cache=Shared;");
            await DbBootstrap.EnsureCreatedAsync(factory);

            var users = new SqliteUserRepository(factory);
            var work = new SqliteWorkOrderRepository(factory);

            // Create a test user in the database.
            var userId = await users.AddAsync(new Trackit.Core.Domain.User
            {
                Username = "lana",
                PasswordHash = new byte[] { 1 },
                PasswordSalt = new byte[] { 2 },
                CreatedAtUtc = System.DateTimeOffset.UtcNow
            });

            var svc = new WorkOrderService(work, () => System.DateTimeOffset.Parse("2025-10-10T00:00:00Z"));
            return (svc, userId);
        }

        [Fact]
        // Test adding a work order and listing open work orders.
        public async Task Add_and_list_open_roundtrip()
        {
            var (svc, userId) = await NewSvcAsync();
            var id = await svc.AddAsync(userId, "GA4 conversion", "Fix tag", System.DateTimeOffset.Parse("2025-10-12T00:00:00Z"));
            id.Should().BeGreaterThan(0);

            var list = await svc.ListOpenAsync(userId);
            list.Should().ContainSingle(w => w.Id == id && !w.Closed);
        }

        [Fact]
        // Test closing a work order and ensuring it cannot be closed again.
        public async Task Close_prevents_double_close_and_checks_owner()
        {
            var (svc, userId) = await NewSvcAsync();
            var id = await svc.AddAsync(userId, "Bug", null, System.DateTimeOffset.Parse("2025-10-11T00:00:00Z"));

            await svc.CloseAsync(id, userId, CloseReason.Resolved);

            var again = async () => await svc.CloseAsync(id, userId, CloseReason.Resolved);
            await again.Should().ThrowAsync<System.InvalidOperationException>();
        }

        [Theory]
        [InlineData("2025-10-09T23:00:00Z", Priority.High)]   // <24h
        [InlineData("2025-10-11T00:00:00Z", Priority.Medium)] // <72h
        [InlineData("2025-10-15T00:00:00Z", Priority.Low)]    // >72h

        // Test the SuggestPriority method with various due dates.
        public void SuggestPriority_rules(string dueIso, Priority expected)
        {
            var svc = new WorkOrderService(new SqliteWorkOrderRepository(new DapperConnectionFactory("Data Source=:memory:")), () => System.DateTimeOffset.Parse("2025-10-10T00:00:00Z"));
            svc.SuggestPriority(System.DateTimeOffset.Parse(dueIso)).Should().Be(expected);
        }

        [Fact]
        public void Exactly_24h_is_Medium()
        {
            var svc = new WorkOrderService(repo: null!, () => DateTimeOffset.Parse("2025-10-10T00:00:00Z"));
            svc.SuggestPriority(DateTimeOffset.Parse("2025-10-11T00:00:00Z")).Should().Be(Priority.Medium);
        }

        [Fact]
        public void Exactly_72h_is_Low()
        {
            var svc = new WorkOrderService(repo: null!, () => DateTimeOffset.Parse("2025-10-10T00:00:00Z"));
            svc.SuggestPriority(DateTimeOffset.Parse("2025-10-13T00:00:00Z")).Should().Be(Priority.Low);
        }

        [Fact]
        public async Task Reporting_counts_reflect_current_state()
        {
            var now = DateTimeOffset.Parse("2025-10-10T00:00:00Z");
            var repo = new InMemoryWorkOrderRepository();
            var svc = new WorkOrderService(repo, () => now);
            const int userId = 42;

            var openId = await svc.AddAsync(userId, "Open item", null, now.AddHours(6), Priority.High);
            var progressId = await svc.AddAsync(userId, "In progress item", null, now.AddHours(30), Priority.Medium);
            var closedId = await svc.AddAsync(userId, "Closed item", null, now.AddHours(-2), priority: Priority.Low, allowPastDue: true);

            await svc.ChangeStageAsync(progressId, userId, Stage.InProgress);
            await svc.CloseAsync(closedId, userId, CloseReason.Resolved);

            var stageCounts = await svc.GetStageCountsAsync(userId);
            stageCounts.Total.Should().Be(3);
            stageCounts.Open.Should().Be(1);
            stageCounts.InProgress.Should().Be(1);
            stageCounts.AwaitingParts.Should().Be(0);
            stageCounts.Closed.Should().Be(1);

            var priorityCounts = await svc.GetPriorityCountsAsync(userId);
            priorityCounts.High.Should().Be(1);
            priorityCounts.Medium.Should().Be(1);
            priorityCounts.Low.Should().Be(1);
        }

        [Fact]
        public async Task SendDueNotifications_sends_and_is_idempotent()
        {
            var now = DateTimeOffset.Parse("2025-10-10T00:00:00Z");
            var db = Path.GetTempFileName();
            var factory = new DapperConnectionFactory($"Data Source={db};Cache=Shared;");
            await DbBootstrap.EnsureCreatedAsync(factory);

            var userRepo = new SqliteUserRepository(factory);
            var workRepo = new SqliteWorkOrderRepository(factory);

            var userId = await userRepo.AddAsync(new Trackit.Core.Domain.User
            {
                Username = "lana",
                Email = "lana@example.com",
                PasswordHash = new byte[] { 1 },
                PasswordSalt = new byte[] { 2 },
                CreatedAtUtc = now
            });

            var email = new FakeEmailSender();
            var svc = new WorkOrderService(workRepo, () => now, email);

            await svc.AddAsync(userId, "Due soon", null, now.AddHours(2));

            var first = await svc.SendDueNotificationsAsync(userId, "lana@example.com", TimeSpan.FromHours(24));
            first.Should().HaveCount(1);
            email.Sent.Count.Should().Be(1);

            var second = await svc.SendDueNotificationsAsync(userId, "lana@example.com", TimeSpan.FromHours(24));
            second.Should().BeEmpty();
            email.Sent.Count.Should().Be(1);
        }

        private sealed class FakeEmailSender : IEmailSender
        {
            public List<(string To, string Subject, string Html)> Sent { get; } = new();

            public Task SendEmailAsync(string to, string subject, string htmlContent, string? textContent = null, CancellationToken ct = default)
            {
                Sent.Add((to, subject, htmlContent));
                return Task.CompletedTask;
            }
        }

    }
}
