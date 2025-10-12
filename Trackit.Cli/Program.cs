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
            // Database setup (stable per-user path)
            var dataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Trackit");
            Directory.CreateDirectory(dataRoot);
            var dbPath = Path.Combine(dataRoot, "trackit.db");
            var connStr = $"Data Source={dbPath};Cache=Shared;";
            Console.WriteLine($"Database: {dbPath}");

            var factory = new DapperConnectionFactory(connStr);
            await DbBootstrap.EnsureCreatedAsync(factory);

            var userRepo = new SqliteUserRepository(factory);
            var workRepo = new SqliteWorkOrderRepository(factory);

            var hasher = new PasswordHasher();
            var userSvc = new UserService(userRepo, hasher);
            var workSvc = new WorkOrderService(workRepo);

            var ui = new UiShell(userSvc, workSvc);
            await ui.RunAsync();
        }
    }
}
