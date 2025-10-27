using Trackit.Cli.Ui;
using Trackit.Core.Auth;
using Trackit.Core.Services;
using Trackit.Data.Repositories;
using Trackit.Data.Services;
using Trackit.Data.Sqlite;

namespace Trackit.Cli
{
    public static class Program
    {
        public static async Task Main()
        {
            // Database setup (stable per-user path).
            var dataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Trackit");
            Directory.CreateDirectory(dataRoot);
            var dbPath = Path.Combine(dataRoot, "trackit.db");
            var connStr = $"Data Source={dbPath};Cache=Shared;";
            Console.WriteLine($"Database: {dbPath}");

            // Ensure database and schema exist.
            var factory = new DapperConnectionFactory(connStr);
            await DbBootstrap.EnsureCreatedAsync(factory);

            // Repository setup.
            var userRepo = new SqliteUserRepository(factory);
            var workRepo = new SqliteWorkOrderRepository(factory);

            // User service setup.
            var hasher = new PasswordHasher();
            var userSvc = new UserService(userRepo, hasher);

            // Email sender setup (used by WorkOrderService).
            var resendApiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY") ?? "";
            var resendFrom = Environment.GetEnvironmentVariable("RESEND_FROM") ?? "onboarding@resend.dev"; // fallback for local tests.

            // Resend email service.
            var notificationService = new ResendNotificationService(resendApiKey, resendFrom);

            // Work order service setup.
            var workSvc = new WorkOrderService(workRepo, null, notificationService);

            // TFA and UI setup.
            var totpService = new TotpService();
            var tfaManager = new UserTwoFactorManager(userRepo);

            // UI shell.
            var ui = new UiShell(userSvc, workSvc, tfaManager, totpService);
            await ui.RunAsync();
        }
    }
}
