PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS WorkOrders (
  Id             INTEGER PRIMARY KEY AUTOINCREMENT,
  CreatorUserId  INTEGER NOT NULL,
  Summary        TEXT NOT NULL,
  Details        TEXT,
  DueAtUtc       TEXT NOT NULL,
  Priority       INTEGER NOT NULL,   -- 0=Low,1=Medium,2=High
  Closed         INTEGER NOT NULL DEFAULT 0,
  ClosedAtUtc    TEXT,
  ClosedReason   INTEGER,            -- CloseReason enum
  CreatedAtUtc   TEXT NOT NULL,
  UpdatedAtUtc   TEXT NOT NULL,
  FOREIGN KEY (CreatorUserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_WorkOrders_User_Open_Due
  ON WorkOrders(CreatorUserId, Closed, DueAtUtc);
