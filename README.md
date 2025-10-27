# Trackit

Trackit is a terminal-based helpdesk demo for managing work orders with secure login, optional two-factor authentication, and on-demand due-date reminders.

I built it as a study project to simulate a small real-world backend.

## Features

- User registration, login, and optional 2FA (TOTP)
- Work orders with priorities and lifecycle stages
- On-demand due-soon email notifications via Resend email API
- Stage and priority summary reports
- Console UI powered by Spectre.Console

## Design and technical choices

This project is built in **.NET 9 with C#** with **SQLite** for storage and **Dapper** as a lightweight data mapper.

Passwords are hashed with **PBKDF2** (random 32-byte salt, 100 000 iterations), and two-factor authentication follows the **TOTP** standard, compatible with any authenticator app. For due-date reminders I used **Resend**, a minimal REST API for sending emails.

Most operations - database access, email, and TOTP verification - are implemented **asynchronously**. Since Trackit runs on a single console thread, this doesn’t make it visibly faster, but it demonstrates how async workflows are structured in real applications.

The notification workflow logs each reminder to ensure idempotence, preventing the same message from being sent twice.

In-memory repository doubles simulate persistence without touching SQLite, keeping tests fast and isolated.

## Run

```bash
dotnet restore
dotnet run --project Trackit.Cli
```

Set `RESEND_API_KEY` and `RESEND_FROM` environment variables if you want email notifications to work.

The first run automatically creates a local SQLite database under your user data folder.

## Test

```bash
dotnet test
```

