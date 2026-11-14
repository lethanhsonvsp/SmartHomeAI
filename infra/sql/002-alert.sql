-- Idempotent. One row per alert episode; reminders do not add rows. feedback: none|correct|wrong.
CREATE TABLE IF NOT EXISTS alert_log (
  pk bigserial PRIMARY KEY, id text NOT NULL, raised_at timestamptz NOT NULL,
  cancelled_at timestamptz, reason text, feedback text NOT NULL DEFAULT 'none');
CREATE INDEX IF NOT EXISTS alert_log_raised ON alert_log (raised_at);
