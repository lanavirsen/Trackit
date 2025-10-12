using Spectre.Console;
using Trackit.Core.Services;
using Trackit.Core.Domain;
using System.IO;

namespace Trackit.Cli.Ui
{
    // Command-line user interface shell for the Trackit application.
    public sealed class UiShell
    {
        private readonly UserService _users;
        private readonly WorkOrderService _work;
        private int? _currentUserId;
        private string? _currentUsername;

        // Constructor accepting user and work order services.
        public UiShell(UserService users, WorkOrderService work)
        { _users = users; _work = work; }

        // Main loop to run the CLI application.
        public async Task RunAsync()
        {
            AnsiConsole.Write(new FigletText("Trackit").Color(Color.Aqua));
            while (true)
            {
                // Show different menu options based on whether a user is logged in.
                var choices = _currentUserId is null
                    ? new[] { "Register", "Login", "Exit" }
                    : new[] { "Add work order", "List open", "Change stage", "Close work order", "Logout" };

                // Prompt the user to choose an option.
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title(_currentUserId is null ? "[bold]Choose an option[/]" : $"[bold]Hello, {_currentUsername}. Choose an action[/]")
                        .AddChoices(choices));

                switch (choice)
                {
                    case "Register": await RegisterAsync(); break;
                    case "Login": await LoginAsync(); break;
                    case "Exit": return;
                    case "Add work order": await AddWorkOrderAsync(); break;
                    case "List open": await ListOpenAsync(); break;
                    case "Change stage": await ChangeStageAsync(); break;
                    case "Close work order": await CloseWorkOrderAsync(); break;
                    case "Logout": _currentUserId = null; _currentUsername = null; break;
                }
            }
        }

        // Register a new user by prompting for username, email, and password.
        private async Task RegisterAsync()
        {
            var username = AnsiConsole.Ask<string>("Username:");
            var email = AnsiConsole.Prompt(new TextPrompt<string>("Email (optional):").AllowEmpty());
            var password = AnsiConsole.Prompt(new TextPrompt<string>("Password:").Secret()
                .Validate(p => PasswordPolicy(p) ? ValidationResult.Success() :
                    ValidationResult.Error("[red]Min 6 chars, 1 digit, 1 upper, 1 special[/]")));

            // Attempt to register the user and handle any errors.
            try
            {
                await AnsiConsole.Status().StartAsync("Creating user...", async _ =>
                {
                    await _users.RegisterAsync(username, string.IsNullOrWhiteSpace(email) ? null : email, password);
                });
                AnsiConsole.MarkupLine("[green]User registered.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            }
        }

        // Log in an existing user by prompting for username and password.
        private async Task LoginAsync()
        {
            var username = AnsiConsole.Ask<string>("Username:");
            var password = AnsiConsole.Prompt(new TextPrompt<string>("Password:").Secret());
            var res = await _users.LoginAsync(username, password);
            if (!res.IsSuccess)
            {
                AnsiConsole.MarkupLine($"[red]{res.Error}[/]");
                return;
            }
            _currentUserId = res.User!.Id;
            _currentUsername = res.User.Username;
            AnsiConsole.MarkupLine($"[green]Logged in as[/] [bold]{_currentUsername}[/].");
        }

        // Add a new work order by prompting for details.
        private async Task AddWorkOrderAsync()
        {
            if (_currentUserId is null) { AnsiConsole.MarkupLine("[red]Login first.[/]"); return; }

            var summary = AnsiConsole.Ask<string>("Summary:");
            var details = AnsiConsole.Prompt(new TextPrompt<string>("Details (optional):").AllowEmpty());

            var preset = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                .Title("Quick due date?")
                .AddChoices("No preset", "Today 18:00", "Tomorrow 09:00", "+2h"));

            DateTimeOffset dueUtc;

            if (preset == "Today 18:00")
            {
                dueUtc = DateTimeOffset.Now.Date.AddHours(18).ToUniversalTime();
            }
            else if (preset == "Tomorrow 09:00")
            {
                dueUtc = DateTimeOffset.Now.Date.AddDays(1).AddHours(9).ToUniversalTime();
            }
            else if (preset == "+2h")
            {
                dueUtc = DateTimeOffset.UtcNow.AddHours(2);
            }
            else // No preset, ask manually
            {
                var input = AnsiConsole.Ask<string>($"Enter due date/time (local accepted). {DueParser.Hint}");
                if (!DueParser.TryParseToUtc(input, out dueUtc))
                {
                    AnsiConsole.MarkupLine("[red]Invalid date/time, cancelled.[/]");
                    return;
                }
            }

            var suggested = _work.SuggestPriority(dueUtc);
            var chosen = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"Priority (suggested: [bold]{suggested}[/])")
                    .AddChoices("Use suggested", "Low", "Medium", "High"));

            // Determine the priority based on user choice or suggestion.
            var prio = chosen switch
            {
                "Low" => Priority.Low,
                "Medium" => Priority.Medium,
                "High" => Priority.High,
                _ => suggested
            };

            // Save the new work order and confirm creation.
            await AnsiConsole.Status().StartAsync("Saving...", async _ =>
            {
                await _work.AddAsync(_currentUserId.Value, summary, string.IsNullOrWhiteSpace(details) ? null : details, dueUtc, prio);
            });
            AnsiConsole.MarkupLine("[green]Work order created.[/]");
        }

        private static string FormatRelative(TimeSpan span)
        {
            if (span.TotalDays >= 1)
                return $"{(int)span.TotalDays}d {(int)(span.Hours)}h";
            if (span.TotalHours >= 1)
                return $"{(int)span.TotalHours}h {(int)span.Minutes}m";
            return $"{(int)span.TotalMinutes}m";
        }

        // List all open work orders for the current user.
        private async Task ListOpenAsync()
        {
            // Ensure the user is logged in.
            if (_currentUserId is null) { AnsiConsole.MarkupLine("[red]Login first.[/]"); return; }
            var items = await _work.ListOpenAsync(_currentUserId.Value);

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Id");
            table.AddColumn("Summary");
            table.AddColumn("Due (local)");
            table.AddColumn("Priority");
            table.AddColumn("Stage");

            // Populate the table with work order details.
            foreach (var w in items)
            {
                var now = DateTimeOffset.Now;
                var localDue = w.DueAtUtc.ToLocalTime();
                var remaining = localDue - now;

                string rel;
                if (remaining.TotalMinutes < 0)
                    rel = $"[red]{FormatRelative(-remaining)} ago[/]";
                else
                    rel = $"{FormatRelative(remaining)}";

                var prioText = w.Priority switch
                {
                    Priority.High => "[red]High[/]",
                    Priority.Medium => "[yellow]Medium[/]",
                    _ => "[green]Low[/]"
                };

                var dueDisplay = $"{rel} ({localDue:yyyy-MM-dd HH:mm})";

                table.AddRow(
                    w.Id.ToString(),
                    Escape(w.Summary),
                    dueDisplay,
                    prioText,
                    StageText(w.Stage),
                    "[grey]—[/]"  // placeholder until Stage column logic is added
                );
            }

            // Display the table or a message if there are no open work orders.
            if (items.Count == 0)
                AnsiConsole.MarkupLine("[grey]No open work orders.[/]");
            else
            {
                AnsiConsole.MarkupLine($"[bold underline]Open work orders ({items.Count})[/]");
                AnsiConsole.Write(table);
            }
                
        }

        // Convert Stage enum to colored text for display.
        private static string StageText(Stage s) => s switch
        {
            Stage.Open => "[cyan]Open[/]",
            Stage.InProgress => "[yellow]In Progress[/]",
            Stage.AwaitingParts => "[magenta]Awaiting Parts[/]",
            Stage.Closed => "[grey]Closed[/]",
            _ => s.ToString()
        };

        // Close an existing work order by prompting for its ID.
        private async Task CloseWorkOrderAsync()
        {
            // Ensure the user is logged in.
            if (_currentUserId is null)
            {
                AnsiConsole.MarkupLine("[red]Login first.[/]");
                return;
            }

            // Prompt for the work order ID to close and validate input.
            var id = AnsiConsole.Prompt(
                new TextPrompt<int>("Work order Id to close:")
                    .Validate(v => v > 0 ? ValidationResult.Success() :
                        ValidationResult.Error("[red]Id must be > 0[/]")));

            var confirm = AnsiConsole.Confirm($"Are you sure you want to close work order [yellow]{id}[/]?");
            if (!confirm)
            {
                AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                return;
            }

            // Attempt to close the work order and handle any errors.
            try
            {
                await _work.CloseAsync(id, _currentUserId.Value, CloseReason.Resolved);
                AnsiConsole.MarkupLine("[green]Work order closed.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            }
        }

        // Change the stage of an existing work order.
        private async Task ChangeStageAsync()
        {
            if (_currentUserId is null) { AnsiConsole.MarkupLine("[red]Login first.[/]"); return; }

            var id = AnsiConsole.Prompt(
                new TextPrompt<int>("Work order Id:")
                    .Validate(v => v > 0 ? ValidationResult.Success()
                        : ValidationResult.Error("[red]Id must be > 0[/]")));

            var newStageStr = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select new stage")
                    .AddChoices("Open", "In Progress", "Awaiting Parts", "Closed"));

            var newStage = newStageStr switch
            {
                "Open" => Stage.Open,
                "In Progress" => Stage.InProgress,
                "Awaiting Parts" => Stage.AwaitingParts,
                "Closed" => Stage.Closed,
                _ => Stage.Open
            };

            try
            {
                await _work.ChangeStageAsync(id, _currentUserId.Value, newStage);
                AnsiConsole.MarkupLine("[green]Stage updated.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            }
        }


        // Validate password against policy: min 6 chars, at least 1 digit, 1 uppercase, 1 special char.
        private static bool PasswordPolicy(string p)
        {
            if (p.Length < 6) return false;
            bool hasDigit = p.Any(char.IsDigit);
            bool hasUpper = p.Any(char.IsUpper);
            bool hasSpecial = p.Any(ch => !char.IsLetterOrDigit(ch));
            return hasDigit && hasUpper && hasSpecial;
        }

        // Check if a due date is within the next 24 hours.
        private static bool IsDueSoon(DateTimeOffset due) => (due - DateTimeOffset.UtcNow) <= TimeSpan.FromHours(24);

        // Escape a string for safe markup display.
        private static string Escape(string s) => Markup.Escape(s);
    }
}