using Trackit.Core.Auth;
using Trackit.Core.Services;
using Trackit.Data.Repositories;

Console.WriteLine("Trackit – temporary manual CLI");
Console.WriteLine("===============================");

var userRepo = new InMemoryUserRepository();
var hasher = new PasswordHasher();
var userSvc = new UserService(userRepo, hasher);

while (true)
{
    Console.WriteLine();
    Console.WriteLine("1. Register user");
    Console.WriteLine("2. Login user");
    Console.WriteLine("3. Exit");
    Console.Write("> ");
    var input = Console.ReadLine();

    if (input == "1")
    {
        Console.Write("Username: ");
        var name = Console.ReadLine() ?? "";
        var norm = Trackit.Core.Services.Normalization.NormalizeUsername(name);

        // quick existence check in UI
        var exists = await userRepo.ExistsAsync(norm);
        if (exists)
        {
            Console.WriteLine("Error: username already exists");
            continue; // back to menu without asking password
        }

        Console.Write("Email (optional): ");
        var email = Console.ReadLine();
        Console.Write("Password: ");
        var pass = ReadPassword();

        try
        {
            var id = await userSvc.RegisterAsync(name, email, pass);
            Console.WriteLine($"User created with Id={id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    else if (input == "2")
    {
        Console.Write("Username: ");
        var name = Console.ReadLine() ?? "";
        Console.Write("Password: ");
        var pass = ReadPassword();

        var res = await userSvc.LoginAsync(name, pass);
        Console.WriteLine(res.IsSuccess
            ? $"Login OK. Hello, {res.User!.Username}!"
            : $"Login failed: {res.Error}");
    }
    else if (input == "3" || string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }
}

static string ReadPassword()
{
    var pass = new Stack<char>();
    ConsoleKeyInfo key;
    while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace)
        {
            if (pass.Count > 0)
            {
                Console.Write("\b \b");
                pass.Pop();
            }
        }
        else
        {
            pass.Push(key.KeyChar);
            Console.Write("*");
        }
    }
    Console.WriteLine();
    return new string(pass.Reverse().ToArray());
}
