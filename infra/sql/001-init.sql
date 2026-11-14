-- Idempotent; Hub runs every infra/sql/*.sql at startup.
CREATE TABLE IF NOT EXISTS sensor_reading (
  id bigserial PRIMARY KEY, ts timestamptz NOT NULL, node text NOT NULL,
  temp_c double precision NOT NULL, humidity double precision NOT NULL, lux double precision NOT NULL);
CREATE INDEX IF NOT EXISTS sensor_reading_ts ON sensor_reading (ts);
CREATE TABLE IF NOT EXISTS ai_event (
  id bigserial PRIMARY KEY, ts timestamptz NOT NULL, kind text NOT NULL, payload jsonb NOT NULL);
CREATE INDEX IF NOT EXISTS ai_event_ts ON ai_event (ts);
CREATE TABLE IF NOT EXISTS door_event (
  id bigserial PRIMARY KEY, ts timestamptz NOT NULL, node text NOT NULL, open boolean NOT NULL);
CREATE INDEX IF NOT EXISTS door_event_ts ON door_event (ts);
CREATE TABLE IF NOT EXISTS device_command_log (
  id bigserial PRIMARY KEY, ts timestamptz NOT NULL, device text NOT NULL, "on" boolean NOT NULL, source text NOT NULL);
CREATE INDEX IF NOT EXISTS device_command_log_ts ON device_command_log (ts);
CREATE TABLE IF NOT EXISTS occupancy_log (
  id bigserial PRIMARY KEY, ts timestamptz NOT NULL, state text NOT NULL);
CREATE INDEX IF NOT EXISTS occupancy_log_ts ON occupancy_log (ts);
