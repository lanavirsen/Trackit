using System.Net.Http;
using Trackit.Cli.Ui;
using Trackit.Core.Auth;
using Trackit.Core.Services;
using Trackit.Data.Repositories;
using Trackit.Data.Services;
using Trackit.Data.Sqlite;
using Trackit.Cli.Infrastructure;

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

            var factory = new DapperConnectionFactory(connStr);
            await DbBootstrap.EnsureCreatedAsync(factory);

            var userRepo = new SqliteUserRepository(factory);
            var workRepo = new SqliteWorkOrderRepository(factory);

            var hasher = new PasswordHasher();
            var userSvc = new UserService(userRepo, hasher);

            // Email sender setup (IEmailSender, used by WorkOrderService).
            var resendApiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY") ?? "";
            var resendFrom = Environment.GetEnvironmentVariable("RESEND_FROM") ?? "onboarding@resend.dev"; // fallback for local tests
            
            var httpClient = new HttpClient();
            var emailSender = new ResendEmailSender(httpClient, resendApiKey, resendFrom);

            // Work order service setup.
            var workSvc = new WorkOrderService(workRepo, null, emailSender);
            
            var ui = new UiShell(userSvc, workSvc, emailSender);
            await ui.RunAsync();
        }
    }
}
