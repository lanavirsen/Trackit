PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Users (
  Id            INTEGER PRIMARY KEY AUTOINCREMENT,
  Username      TEXT NOT NULL UNIQUE, -- normalized (lowercase, trimmed)
  Email         TEXT,
  PasswordHash  BLOB NOT NULL,
  PasswordSalt  BLOB NOT NULL,
  CreatedAtUtc  TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_Users_Username ON Users(Username);
