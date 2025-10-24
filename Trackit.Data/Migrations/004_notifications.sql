PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS NotificationLog (
  Id            INTEGER PRIMARY KEY AUTOINCREMENT,
  WorkOrderId   INTEGER NOT NULL,
  WindowTag     TEXT NOT NULL,         -- e.g. '24h'
  SentAtUtc     TEXT NOT NULL,
  UNIQUE (WorkOrderId, WindowTag),
  FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id) ON DELETE CASCADE
);
