using Trackit.Core.Auth;
using Trackit.Core.Domain;
using Trackit.Core.Services;
using Trackit.Data.Repositories;
using Trackit.Data.Sqlite;

Console.WriteLine("Trackit – temporary manual CLI (SQLite)");
Console.WriteLine("=======================================");

var dbPath = Path.Combine(Environment.CurrentDirectory, "trackit.db");
var connStr = $"Data Source={dbPath};Cache=Shared;";

var factory = new DapperConnectionFactory(connStr);
await DbBootstrap.EnsureCreatedAsync(factory);

// Repositories + services
var userRepo = new SqliteUserRepository(factory);
var hasher = new PasswordHasher();
var userSvc = new UserService(userRepo, hasher);

var workRepo = new SqliteWorkOrderRepository(factory);
var workSvc = new WorkOrderService(workRepo);

// runtime state
int? currentUserId = null;
string? currentUsername = null;

while (true)
{
    Console.WriteLine();
    Console.WriteLine("Main menu");
    Console.WriteLine("1. Register user");
    Console.WriteLine("2. Login user");
    Console.WriteLine("3. Add work order");
    Console.WriteLine("4. List open work orders");
    Console.WriteLine("5. Close work order");
    Console.WriteLine("6. Exit");
    Console.Write("> ");
    var choice = Console.ReadLine();

    switch (choice)
    {
        case "1":
            await RegisterAsync();
            break;
        case "2":
            await LoginAsync();
            break;
        case "3":
            if (!EnsureLogin()) break;
            await AddWorkOrderAsync();
            break;
        case "4":
            if (!EnsureLogin()) break;
            await ListWorkOrdersAsync();
            break;
        case "5":
            if (!EnsureLogin()) break;
            await CloseWorkOrderAsync();
            break;
        case "6":
            return;
        default:
            Console.WriteLine("Unknown choice.");
            break;
    }
}


// Helper methods

bool EnsureLogin()
{
    if (currentUserId is null)
    {
        Console.WriteLine("You must log in first.");
        return false;
    }
    return true;
}

async Task RegisterAsync()
{
    Console.Write("Username: ");
    var username = Console.ReadLine() ?? "";
    Console.Write("Email (optional): ");
    var email = Console.ReadLine();
    Console.Write("Password: ");
    var password = ReadPassword();

    try
    {
        var id = await userSvc.RegisterAsync(username, email, password);
        Console.WriteLine($"Registered user #{id}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

async Task LoginAsync()
{
    Console.Write("Username: ");
    var username = Console.ReadLine() ?? "";
    Console.Write("Password: ");
    var password = ReadPassword();

    var result = await userSvc.LoginAsync(username, password);
    if (result.IsSuccess)
    {
        currentUserId = result.User!.Id;
        currentUsername = result.User.Username;
        Console.WriteLine($"Logged in as {currentUsername}");
    }
    else
    {
        Console.WriteLine($"Login failed: {result.Error}");
    }
}

async Task AddWorkOrderAsync()
{
    Console.Write("Summary: ");
    var summary = Console.ReadLine() ?? "";
    Console.Write("Details (optional): ");
    var details = Console.ReadLine();
    Console.Write("Due date (ISO 8601, e.g. 2025-10-12T18:00:00Z): ");
    var dueInput = Console.ReadLine();
    if (!DateTimeOffset.TryParse(dueInput, out var due))
    {
        Console.WriteLine("Invalid date.");
        return;
    }

    var id = await workSvc.AddAsync(currentUserId!.Value, summary, details, due);
    Console.WriteLine($"Work order created (Id={id}).");
}

async Task ListWorkOrdersAsync()
{
    var list = await workSvc.ListOpenAsync(currentUserId!.Value);
    if (list.Count == 0)
    {
        Console.WriteLine("No open work orders.");
        return;
    }

    Console.WriteLine("Open work orders:");
    foreach (var w in list)
    {
        Console.WriteLine($"{w.Id,3} | {w.Summary,-30} | Due {w.DueAtUtc:u} | {w.Priority}");
    }
}

async Task CloseWorkOrderAsync()
{
    Console.Write("Work order Id to close: ");
    if (!int.TryParse(Console.ReadLine(), out var id))
    {
        Console.WriteLine("Invalid Id.");
        return;
    }

    try
    {
        await workSvc.CloseAsync(id, currentUserId!.Value, CloseReason.Resolved);
        Console.WriteLine("Closed.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

static string ReadPassword()
{
    var buf = new Stack<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }
        if (key.Key == ConsoleKey.Backspace)
        {
            if (buf.Count > 0) { Console.Write("\b \b"); buf.Pop(); }
        }
        else
        {
            buf.Push(key.KeyChar);
            Console.Write("*");
        }
    }
    return new string(buf.Reverse().ToArray());
}
