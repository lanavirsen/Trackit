using Spectre.Console;
using Trackit.Core.Domain;
using Trackit.Core.Services;
using Trackit.Core.Ports;

namespace Trackit.Cli.Ui
{
    // Command-line user interface shell for the Trackit application.
    public sealed class UiShell
    {
        private readonly UserService _users;
        private readonly WorkOrderService _work;
        private readonly INotificationService _notifications;
        private int? _currentUserId;
        private string? _currentUsername;
        private string? _currentUserEmail;

        // Constructor accepting user and work order services.
        public UiShell(UserService users, WorkOrderService work, INotificationService notifications)
        { _users = users; _work = work; _notifications = notifications; }

        // Main loop to run the CLI application.
        public async Task RunAsync()
        {
            while (true)
            {
                RenderHeader();

                var choices = _currentUserId is null
                    ? new[] { "Register", "Login", "Exit" }
                    : new[] { "Workspace", "Logout" };

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title(_currentUserId is null ? "[bold]Choose an option[/]" : $"[bold]Hello, {_currentUsername}[/]")
                        .AddChoices(choices));

                switch (choice)
                {
                    case "Register": await RegisterAsync(); break;
                    case "Login":
                        if (await LoginAsync())
                            await WorkspaceLoopAsync(); // auto-list + actions
                        break;
                    case "Workspace": await WorkspaceLoopAsync(); break;
                    case "Logout": _currentUserId = null; _currentUsername = null; break;
                    case "Exit": return;
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
        private async Task<bool> LoginAsync()
        {
            var username = AnsiConsole.Ask<string>("Username:");
            var password = AnsiConsole.Prompt(new TextPrompt<string>("Password:").Secret());
            var res = await _users.LoginAsync(username, password);
            if (!res.IsSuccess)
            {
                AnsiConsole.MarkupLine($"[red]{res.Error}[/]");
                return false;
            }
            _currentUserId = res.User!.Id;
            _currentUsername = res.User.Username;
            _currentUserEmail = res.User.Email;
            AnsiConsole.MarkupLine($"[green]Logged in as[/] [bold]{_currentUsername}[/].");
            return true;
        }

        private async Task WorkspaceLoopAsync()
        {
            while (_currentUserId is not null)
            {
                RenderHeader();

                // Render current open items
                await ListOpenAsync(renderOnly: true);

                // Action menu displayed under the list
                var action = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .AddChoices("Add work order", "Change stage", "Report", "Refresh", "Check notifications", "Logout"));

                switch (action)
                {
                    case "Add work order":
                        await AddWorkOrderAsync();
                        break;
                    case "Change stage":
                        await ChangeStageAsync();
                        break;
                    case "Report":
                        await ShowReportAsync();
                        break;
                    case "Refresh":
                        // no-op; next loop iteration re-renders
                        break;
                    case "Check notifications":
                        await CheckNotificationsAsync();
                        break;
                    case "Logout":
                        _currentUserId = null;
                        _currentUsername = null;
                        _currentUserEmail = null;
                        return; // exit workspace back to main menu
                }
                // loop continues; screen will clear and re-render list + actions
            }
        }

        // Add a new work order by prompting for details.
        private async Task AddWorkOrderAsync()
        {
            if (!RequireLogin()) return;

            PrintCancelHint();
            var summary = AnsiConsole.Prompt(
                new TextPrompt<string>("Summary:").AllowEmpty());
            if (string.IsNullOrWhiteSpace(summary)) { AnsiConsole.MarkupLine("[grey]Cancelled.[/]"); return; }

            var details = AnsiConsole.Prompt(new TextPrompt<string>("Details (optional):").AllowEmpty());

            var preset = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Quick due date?")
                    .AddChoices("No preset", "Today 18:00", "Tomorrow 09:00", "+2h", "[red]Cancel[/]"));
            if (preset.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                return;
            }

            DateTimeOffset dueUtc;
            if (preset == "Today 18:00")
                dueUtc = DateTimeOffset.Now.Date.AddHours(18).ToUniversalTime();
            else if (preset == "Tomorrow 09:00")
                dueUtc = DateTimeOffset.Now.Date.AddDays(1).AddHours(9).ToUniversalTime();
            else if (preset == "+2h")
                dueUtc = DateTimeOffset.UtcNow.AddHours(2);
            else
            {
                PrintCancelHint();
                var input = AnsiConsole.Prompt(
                    new TextPrompt<string>($"Enter due date/time (local accepted). {DueParser.Hint}")
                        .AllowEmpty());
                if (string.IsNullOrWhiteSpace(input)) { AnsiConsole.MarkupLine("[grey]Cancelled.[/]"); return; }
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
                    .AddChoices("Use suggested", "Low", "Medium", "High", "[red]Cancel[/]"));
            if (chosen.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                return;
            }

            var prio = chosen switch
            {
                "Low" => Priority.Low,
                "Medium" => Priority.Medium,
                "High" => Priority.High,
                _ => suggested
            };

            await AnsiConsole.Status().StartAsync("Saving...", async _ =>
            {
                await _work.AddAsync(_currentUserId.Value, summary,
                    string.IsNullOrWhiteSpace(details) ? null : details, dueUtc, prio);
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
        private async Task ListOpenAsync(bool renderOnly = false)
        {
            if (!RequireLogin()) return;
            var items = await _work.ListOpenAsync(_currentUserId.Value);

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Id");
            table.AddColumn("Summary");
            table.AddColumn("Due (local)");
            table.AddColumn("Priority");
            table.AddColumn("Stage");

            foreach (var w in items)
            {
                var now = DateTimeOffset.Now;
                var localDue = w.DueAtUtc.ToLocalTime();
                var remaining = localDue - now;

                string rel = remaining.TotalMinutes < 0
                    ? $"[red]{FormatRelative(-remaining)} ago[/]"
                    : $"{FormatRelative(remaining)}";

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
                    StageText(w.Stage)
                );
            }

            AnsiConsole.MarkupLine($"[bold underline]Open work orders ({items.Count})[/]");
            AnsiConsole.Write(table);

            if (!renderOnly)
            {
                AnsiConsole.MarkupLine("[grey]Press any key to return...[/]");
                Console.ReadKey(intercept: true);
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

        // Change the stage of an existing work order.
        private async Task ChangeStageAsync()
        {
            if (!RequireLogin()) return;

            PrintCancelHint();
            var idStr = AnsiConsole.Prompt(
                new TextPrompt<string>("Work order Id:").AllowEmpty());
            if (string.IsNullOrWhiteSpace(idStr))
            {
                AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                return;
            }
            if (!int.TryParse(idStr, out var id) || id <= 0)
            {
                AnsiConsole.MarkupLine("[red]Invalid Id.[/]");
                return;
            }

            // --- clear screen, show header, and re-render the current list ---
            RenderHeader();

            // show the open items so user has context while choosing the new stage
            await ListOpenAsync(renderOnly: true);

            AnsiConsole.MarkupLine($"[bold]Change stage for work order [yellow]{id}[/][/]");
            AnsiConsole.WriteLine();

            var newStageStr = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select new stage")
                    .AddChoices("Open", "In Progress", "Awaiting Parts", "Closed", "[red]Cancel[/]"));
            if (newStageStr.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                return;
            }

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
                if (newStage == Stage.Closed)
                {
                    var confirm = AnsiConsole.Confirm("Are you sure you want to close this work order?");
                    if (!confirm)
                    {
                        AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                        return;
                    }

                    await _work.CloseAsync(id, _currentUserId.Value, CloseReason.Resolved);
                    AnsiConsole.MarkupLine("[green]Work order closed.[/]");
                }
                else
                {
                    await _work.ChangeStageAsync(id, _currentUserId.Value, newStage);
                    AnsiConsole.MarkupLine("[green]Stage updated.[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            }
        }

        private async Task CheckNotificationsAsync()
        {
            if (!RequireLogin()) return;

            AnsiConsole.MarkupLine("[bold]Checking for due work orders...[/]");
            
            try
            {
                // Get user email from login.
                if (string.IsNullOrWhiteSpace(_currentUserEmail))
                {
                    AnsiConsole.MarkupLine("[red]No email address found for user. Please update your profile.[/]");
                    return;
                }
                
                // Check for work orders due in the next 24 hours.
                var timeWindow = TimeSpan.FromHours(24);
                await _work.SendDueNotificationsAsync(_currentUserId!.Value, _currentUserEmail!, timeWindow);

                AnsiConsole.MarkupLine("[green]Notification check completed![/]");
                AnsiConsole.MarkupLine("[grey]If you have work orders due soon, you should have received email notifications.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error checking notifications:[/] {ex.Message}");
            }
            
            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey();
        }

        private async Task ShowReportAsync()
        {
            // temporary placeholder until report feature is added
            AnsiConsole.MarkupLine("[grey italic]Report feature not implemented yet.[/]");
            await Task.Delay(500); // just to keep async signature
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

        // Header renderer
        private void RenderHeader()
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new FigletText("Trackit").Centered().Color(Color.Aqua));
            if (!string.IsNullOrWhiteSpace(_currentUsername))
                AnsiConsole.MarkupLine($"[grey]Logged as [/][bold]{Markup.Escape(_currentUsername!)}[/]");
            AnsiConsole.WriteLine();
        }

        // Check if a due date is within the next 24 hours.
        private static bool IsDueSoon(DateTimeOffset due) => (due - DateTimeOffset.UtcNow) <= TimeSpan.FromHours(24);

        // Escape a string for safe markup display.
        private static string Escape(string s) => Markup.Escape(s);

        // �Press Enter to cancel� helper.
        private static void PrintCancelHint() =>
            AnsiConsole.MarkupLine("[grey]Press Enter to cancel[/]");

        // Require-login guard.
        private bool RequireLogin()
        {
            if (_currentUserId is null)
            {
                AnsiConsole.MarkupLine("[red]Login first.[/]");
                return false;
            }
            return true;
        }
    }
}