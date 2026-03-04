-- Postgres init script — EF migrations handle schema.
-- This file exists for any one-time DB setup that EF cannot handle.

-- Ensure uuid extension is available (postgres 13+ has it built-in)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
