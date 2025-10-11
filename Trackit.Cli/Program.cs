using Trackit.Core.Auth;
using Trackit.Core.Services;
using Trackit.Data.Repositories;
using Trackit.Data.Sqlite;
using Trackit.Cli.Ui;

namespace Trackit.Cli
{
    public static class Program
    {
        public static async Task Main()
        {
            // Database setup
            var dbPath = Path.Combine(Environment.CurrentDirectory, "trackit.db");
            var connStr = $"Data Source={dbPath};Cache=Shared;";

            var factory = new DapperConnectionFactory(connStr);
            await DbBootstrap.EnsureCreatedAsync(factory);

            // Core services
            var userRepo = new SqliteUserRepository(factory);
            var workRepo = new SqliteWorkOrderRepository(factory);

            var hasher = new PasswordHasher();
            var userSvc = new UserService(userRepo, hasher);
            var workSvc = new WorkOrderService(workRepo);

            // UI
            var ui = new UiShell(userSvc, workSvc);
            await ui.RunAsync();
        }
    }
}