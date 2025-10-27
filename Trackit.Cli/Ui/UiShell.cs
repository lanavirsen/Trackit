using System;
using Spectre.Console;
using Trackit.Core.Domain;
using Trackit.Core.Services;

// CLI stands for Command-Line Interface.
namespace Trackit.Cli.Ui
{
    // Command-line user interface shell for the Trackit application.
    public sealed class UiShell
    {
        private readonly UserService _users;
        private readonly WorkOrderService _work;
        private int? _currentUserId;
        private string? _currentUsername;
        private string? _currentUserEmail;
        private readonly UserTwoFactorManager _tfa;
        private readonly TotpService _totp;
        private bool _twoFactorEnabled;

        // Constructor accepting user and work order services.
        // It exists so the UiShell class can receive the components it depends on —
        // it’s how dependency injection (manual in this case) is done.
        public UiShell(UserService users, WorkOrderService work, UserTwoFactorManager tfa, TotpService totp)
        {
            _users = users;
            _work = work;
            _tfa = tfa;
            _totp = totp;
        }

        // Main loop to run the CLI application.
        public async Task RunAsync()
        {
            // while (true) creates an infinite loop — it keeps running over and over
            // until something inside the loop explicitly stops it.
            while (true)
            {
                RenderHeader();

                // Build menu dynamically depending on login state.
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
                            await WorkspaceLoopAsync(); // enter workspace if login successful.
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
            try
            {
                var username = AnsiConsole.Ask<string>("Username:");

                if (await _users.UsernameExistsAsync(username))
                {
                    AnsiConsole.MarkupLine("[red]That username is already taken. Try another.[/]");
                    await Task.Delay(1500);
                    return;
                }

                var email = AnsiConsole.Prompt(new TextPrompt<string>("Email (optional):").AllowEmpty());
                var password = AnsiConsole.Prompt(new TextPrompt<string>("Password:").Secret()
                    .Validate(p => PasswordPolicy(p) ? ValidationResult.Success() :
                        ValidationResult.Error("[red]Min 6 chars, 1 digit, 1 upper, 1 special[/]")));

                // Attempt to register the user and handle any errors.
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
                await Task.Delay(1500);
                return false;
            }

            var user = res.User!;
            // Require TOTP only if enabled and a secret exists.
            if (user.TwoFactorEnabled && !string.IsNullOrWhiteSpace(user.TotpSecret))
            {
                var code = AnsiConsole.Prompt(new TextPrompt<string>("Enter 6-digit TOTP code:").Secret());
                var ok = _totp.VerifyCode(user.TotpSecret, code, allowedDriftSteps: 1);
                if (!ok)
                {
                    AnsiConsole.MarkupLine("[red]Invalid or expired TOTP code.[/]");
                    await Task.Delay(1500);
                    return false;
                }
            }

            // Set session only after all checks pass.
            _currentUserId = user.Id;
            _currentUsername = user.Username;
            _currentUserEmail = user.Email;
            _twoFactorEnabled = user.TwoFactorEnabled && !string.IsNullOrWhiteSpace(user.TotpSecret);

            AnsiConsole.MarkupLine($"[green]Logged in as[/] [bold]{_currentUsername}[/].");
            await Task.Delay(1500);
            return true;
        }

        // Main workspace loop after login.
        private async Task WorkspaceLoopAsync()
        {
            while (_currentUserId is not null)
            {
                RenderHeader();

                // Render current open items.
                await ListOpenAsync(renderOnly: true);

                // Build menu dynamically depending on 2FA state.
                var choices = new List<string>
                {
                    "Add work order",
                    "Change stage",
                    "Report",
                    "Refresh",
                    "Due check (24h)"
                };

                if (_twoFactorEnabled)
                    choices.Add("Disable TOTP (2FA)");
                else
                    choices.Add("Enable TOTP (2FA)");

                choices.Add("Logout");

                // Prompt for action.
                var action = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[bold cyan]Choose an action[/]:")
                        .AddChoices(choices));

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
                        // No action needed; loop will re-render.
                        break;

                    case "Due check (24h)":
                        await RunDueCheckAsync();
                        break;

                    case "Enable TOTP (2FA)":
                        if (await EnableTotpAsync())
                            _twoFactorEnabled = true;
                        break;

                    case "Disable TOTP (2FA)":
                        await DisableTotpAsync();
                        _twoFactorEnabled = false;
                        break;

                    case "Logout":
                        _currentUserId = null;
                        _currentUsername = null;
                        _currentUserEmail = null;
                        return; // exit workspace back to main menu.
                }
            }
        }

        // Add a new work order by prompting for details.
        private async Task AddWorkOrderAsync()
        {
            // Guard: must be logged in.
            if (!RequireLogin()) return;

            PrintCancelHint();

            // Prompt for summary.
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
                await Task.Delay(2000);
                return;
            }

            // Determine due date/time.
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

            // Suggest priority based on due date.
            var suggested = _work.SuggestPriority(dueUtc);
            var chosen = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"Priority (suggested: [bold]{suggested}[/])")
                    .AddChoices("Use suggested", "Low", "Medium", "High", "[red]Cancel[/]"));
            if (chosen.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                await Task.Delay(2000);
                return;
            }

            var prio = chosen switch
            {
                "Low" => Priority.Low,
                "Medium" => Priority.Medium,
                "High" => Priority.High,
                _ => suggested
            };

            // Attempt to save the new work order.
            await AnsiConsole.Status().StartAsync("Saving...", async _ =>
            {
                // Try to add the work order, handling past-due cases.
                try
                {
                    await _work.AddAsync(
                        _currentUserId!.Value,
                        summary,
                        string.IsNullOrWhiteSpace(details) ? null : details,
                        dueUtc,
                        prio,
                        allowPastDue: false);
                    AnsiConsole.MarkupLine("[green]Work order created.[/]");
                    await Task.Delay(2000);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Due cannot be in the past."))
                {
                    var overridePast = AnsiConsole.Confirm("[yellow]Due is in the past. Create anyway?[/]");
                    if (!overridePast)
                    {
                        AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                        await Task.Delay(1500);
                        return;
                    }

                    await _work.AddAsync(
                        _currentUserId!.Value,
                        summary,
                        string.IsNullOrWhiteSpace(details) ? null : details,
                        dueUtc,
                        prio,
                        allowPastDue: true);
                    AnsiConsole.MarkupLine("[green]Work order created (past due allowed).[/]");
                    await Task.Delay(2000);
                }
                catch (ArgumentException ex)
                {
                    AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
                    await Task.Delay(2000);
                }
            });
        }

        // Format a TimeSpan into a relative string (e.g., "2d 3h", "5h 30m", "45m").
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
            var items = await _work.ListOpenAsync(_currentUserId!.Value);

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

            // Prompt for work order ID.
            var idStr = AnsiConsole.Prompt(
                new TextPrompt<string>("Work order Id:").AllowEmpty());
            if (string.IsNullOrWhiteSpace(idStr))
            {
                AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                await Task.Delay(1500);
                return;
            }
            if (!int.TryParse(idStr, out var id) || id <= 0)
            {
                AnsiConsole.MarkupLine("[red]Invalid Id.[/]");
                await Task.Delay(1500);
                return;
            }

            // Fetch existing work order.
            var existing = await _work.GetByIdAsync(id);
                if (existing is null)
                {
                    AnsiConsole.MarkupLine($"[red]No work order found with Id {id}.[/]");
                    await Task.Delay(1500);
                    return;
                }

            // Clear screen, show header, and re-render the current list.
            RenderHeader();

            // Show the open items so user has context while choosing the new stage.
            await ListOpenAsync(renderOnly: true);

            AnsiConsole.MarkupLine($"[bold]Change stage for work order [yellow]{id}[/][/]");
            AnsiConsole.WriteLine();

            // Prompt for new stage.
            var newStageStr = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select new stage")
                    .AddChoices("Open", "In Progress", "Awaiting Parts", "Closed", "[red]Cancel[/]"));
            if (newStageStr.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                await Task.Delay(1500);
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
                    // Confirm closure.
                    var confirm = AnsiConsole.Confirm("Are you sure you want to close this work order?");
                    if (!confirm)
                    {
                        AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                        await Task.Delay(1500);
                        return;
                    }

                    await _work.CloseAsync(id, _currentUserId!.Value, CloseReason.Resolved);
                    AnsiConsole.MarkupLine("[green]Work order closed.[/]");
                    await Task.Delay(1500);
                }
                else
                {
                    await _work.ChangeStageAsync(id, _currentUserId!.Value, newStage);
                    AnsiConsole.MarkupLine("[green]Stage updated.[/]");
                    await Task.Delay(1500);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            }
        }

        // Run due check and send email notifications for items due within 24 hours.
        private async Task RunDueCheckAsync()
        {
            if (!RequireLogin()) return;
            if (string.IsNullOrWhiteSpace(_currentUserEmail))
            {
                AnsiConsole.MarkupLine("[red]Your account has no email. Cannot send notifications.[/]");
                return;
            }

            try
            {
                // Run due check and send notifications.
                IReadOnlyList<DueSoonItem> sentItems = Array.Empty<DueSoonItem>();
                await AnsiConsole.Status()
                    .StartAsync("Checking due items and sending emails...", async _ =>
                    {
                        sentItems = await _work.SendDueNotificationsAsync(_currentUserId.Value, _currentUserEmail!, TimeSpan.FromHours(24));
                    });

                RenderHeader();

                // Display results.
                if (sentItems.Count == 0)
                {
                    var ts = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm");
                    var panel = new Panel(new Markup("No notifications to send.\n[dim]" + ts + "[/]"))
                    {
                        Header = new PanelHeader("Due check (24h)", Justify.Center),
                        Border = BoxBorder.Rounded
                    };
                    AnsiConsole.Write(panel);
                }
                else
                {
                    AnsiConsole.MarkupLine("[bold underline]Due notifications sent[/]");
                    var table = new Table().Border(TableBorder.Rounded);
                    table.AddColumn("Id");
                    table.AddColumn("Summary");
                    table.AddColumn("Due (local)");
                    table.AddColumn("Priority");

                    foreach (var item in sentItems)
                    {
                        var local = item.DueAtUtc.ToLocalTime();
                        var prio = item.Priority switch
                        {
                            Priority.High => "[red]High[/]",
                            Priority.Medium => "[yellow]Medium[/]",
                            _ => "[green]Low[/]"
                        };
                        table.AddRow(
                            item.Id.ToString(),
                            Escape(item.Summary),
                            $"{local:yyyy-MM-dd HH:mm}",
                            prio);
                    }

                    AnsiConsole.Write(table);
                }

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[grey]Press any key to return...[/]");
                Console.ReadKey(intercept: true);
            }

            // Handle email sending errors.
            catch (HttpRequestException ex)
            {
                AnsiConsole.MarkupLine($"[red]Email provider error:[/] {Markup.Escape(ex.Message)}");
            }
        }

        // Enable TOTP-based two-factor authentication.
        private async Task<bool> EnableTotpAsync()
        {
            if (!RequireLogin()) return false;

            // Generate TOTP secret and URI.
            var secret = _totp.GenerateSecret();
            var uri = _totp.BuildUri("Trackit", _currentUsername ?? $"user{_currentUserId}", secret);

            AnsiConsole.MarkupLine($"[yellow]TOTP secret:[/] {secret}");
            AnsiConsole.MarkupLine($"[yellow]URI:[/] {uri}");
            AnsiConsole.MarkupLine("[grey]Scan in your authenticator. Enter code to confirm, or press Enter to cancel.[/]");

            // Prompt for TOTP code and verify.
            while (true)
            {
                var code = AnsiConsole.Prompt(
                    new TextPrompt<string>("Code (6 digits):")
                        .AllowEmpty()
                        .Secret());

                if (string.IsNullOrWhiteSpace(code))
                {
                    AnsiConsole.MarkupLine("[grey]Cancelled. 2FA not enabled.[/]");
                    await Task.Delay(1500);
                    return false;
                }

                if (_totp.VerifyCode(secret, code, allowedDriftSteps: 1))
                {
                    await _tfa.EnableAsync(_currentUserId.Value, secret);
                    AnsiConsole.MarkupLine("[green]2FA enabled.[/]");
                    await Task.Delay(1500);
                    return true;
                }

                var retry = AnsiConsole.Confirm("[red]Invalid/expired code.[/] Try again?");
                if (!retry)
                {
                    AnsiConsole.MarkupLine("[grey]Aborted. 2FA not enabled.[/]");
                    await Task.Delay(1500);
                    return false;
                }
            }
        }

        // Disable TOTP-based two-factor authentication.
        private async Task DisableTotpAsync()
        {
            if (!RequireLogin()) return;

            // Confirm disabling 2FA.
            var confirm = AnsiConsole.Confirm("Are you sure you want to disable two-factor authentication?");
            if (!confirm)
            {
                AnsiConsole.MarkupLine("[grey]Canceled.[/]");
                await Task.Delay(2000);
                return;
            }

            // Attempt to disable 2FA.
            try
            {
                await _tfa.DisableAsync(_currentUserId.Value);
                AnsiConsole.MarkupLine("[green]Two-factor authentication disabled.[/]");
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to disable 2FA:[/] {Markup.Escape(ex.Message)}");
            }

            // Update session state.
            _twoFactorEnabled = false;
        }

        // Show a report of work order statistics.
        private async Task ShowReportAsync()
        {
            if (!RequireLogin()) return;

            RenderHeader();

            // Fetch statistics.
            var userId = _currentUserId!.Value;

            var stageCounts = await _work.GetStageCountsAsync(userId);
            var priorityCounts = await _work.GetPriorityCountsAsync(userId);
            var openItems = await _work.ListOpenAsync(userId);

            AnsiConsole.MarkupLine("[bold underline]Workspace Snapshot[/]");
            AnsiConsole.WriteLine();

            // Unpack tuples for easier access.
            var (total, open, inProgress, awaitingParts, closed) = stageCounts;
            var (high, medium, low) = priorityCounts;

            // Build and display tables.
            var stageTable = new Table()
                .Border(TableBorder.Rounded)
                .Title("Stage Breakdown");
            stageTable.AddColumn(new TableColumn("Stage").LeftAligned());
            stageTable.AddColumn(new TableColumn("Count").Centered());
            stageTable.AddRow("[silver]Total[/]", total.ToString());
            stageTable.AddRow("[cyan]Open[/]", open.ToString());
            stageTable.AddRow("[yellow]In Progress[/]", inProgress.ToString());
            stageTable.AddRow("[magenta]Awaiting Parts[/]", awaitingParts.ToString());
            stageTable.AddRow("[grey]Closed[/]", closed.ToString());

            var priorityTable = new Table()
                .Border(TableBorder.Rounded)
                .Title("Priority Mix");
            priorityTable.AddColumn(new TableColumn("Priority").LeftAligned());
            priorityTable.AddColumn(new TableColumn("Count").Centered());
            priorityTable.AddRow("[red]High[/]", high.ToString());
            priorityTable.AddRow("[yellow]Medium[/]", medium.ToString());
            priorityTable.AddRow("[green]Low[/]", low.ToString());

            AnsiConsole.Write(new Columns(stageTable, priorityTable).Expand());
            AnsiConsole.WriteLine();

            // Due status summary.
            var now = DateTimeOffset.UtcNow;
            var overdue = openItems.Count(w => w.DueAtUtc < now);
            var dueSoon = openItems.Count(w => w.DueAtUtc >= now && w.DueAtUtc <= now.AddHours(24));
            AnsiConsole.MarkupLine($"[red]Overdue:[/] {overdue}    [yellow]Due <=24h:[/] {dueSoon}    [green]Open backlog:[/] {open}");
            AnsiConsole.WriteLine();

            // List open work orders or show a message if none exist.
            if (openItems.Count == 0)
                AnsiConsole.MarkupLine("[grey]No open work orders yet. Add one from the workspace menu.[/]");

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Press any key to return...[/]");
            Console.ReadKey(intercept: true);
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

        // Header renderer.
        private void RenderHeader()
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new FigletText("Trackit").Centered().Color(Color.Aqua));
            if (!string.IsNullOrWhiteSpace(_currentUsername))
                AnsiConsole.MarkupLine($"[grey]Logged as [/][bold]{Markup.Escape(_currentUsername!)}[/]");
            AnsiConsole.WriteLine();
        }

        // Escape a string for safe markup display.
        private static string Escape(string s) => Markup.Escape(s);

        // "Press Enter to cancel" helper.
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
