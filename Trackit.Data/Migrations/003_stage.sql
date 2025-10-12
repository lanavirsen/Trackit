PRAGMA foreign_keys = ON;

ALTER TABLE WorkOrders ADD COLUMN Stage INTEGER NOT NULL DEFAULT 0; -- 0=Open,1=InProgress,2=AwaitingParts,3=Closed

-- Backfill: if already closed, set Stage=Closed (3)
UPDATE WorkOrders SET Stage = 3 WHERE Closed = 1;
