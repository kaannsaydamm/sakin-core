-- Audit Events Table for Compliance & Security
CREATE TABLE IF NOT EXISTS audit_events (
    id VARCHAR(255) PRIMARY KEY,
    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    user_name VARCHAR(255) NOT NULL,
    action VARCHAR(255) NOT NULL,
    resource_type VARCHAR(100) NOT NULL,
    resource_id VARCHAR(255),
    old_state JSONB,
    new_state JSONB,
    ip_address VARCHAR(45),
    user_agent TEXT,
    status VARCHAR(50) NOT NULL DEFAULT 'success',
    error_code VARCHAR(100),
    error_message TEXT,
    metadata JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes for efficient querying
CREATE INDEX IF NOT EXISTS idx_audit_events_timestamp ON audit_events(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_audit_events_user ON audit_events(user_name);
CREATE INDEX IF NOT EXISTS idx_audit_events_action ON audit_events(action);
CREATE INDEX IF NOT EXISTS idx_audit_events_resource ON audit_events(resource_type, resource_id);
CREATE INDEX IF NOT EXISTS idx_audit_events_status ON audit_events(status);

-- Composite index for common queries
CREATE INDEX IF NOT EXISTS idx_audit_events_user_timestamp ON audit_events(user_name, timestamp DESC);

-- Retention policy: auto-delete after 2 years (configurable)
-- Note: Implement with pg_cron or application-level cleanup
CREATE OR REPLACE FUNCTION cleanup_old_audit_events() RETURNS void AS $$
BEGIN
    DELETE FROM audit_events WHERE timestamp < NOW() - INTERVAL '2 years';
END;
$$ LANGUAGE plpgsql;

-- Optional: Create materialized view for audit analytics
CREATE MATERIALIZED VIEW IF NOT EXISTS audit_summary AS
SELECT 
    user_name,
    action,
    resource_type,
    status,
    DATE(timestamp) as date,
    COUNT(*) as count
FROM audit_events
GROUP BY user_name, action, resource_type, status, DATE(timestamp);

CREATE INDEX IF NOT EXISTS idx_audit_summary_user_date ON audit_summary(user_name, date DESC);
CREATE INDEX IF NOT EXISTS idx_audit_summary_action_date ON audit_summary(action, date DESC);

COMMENT ON TABLE audit_events IS 'Audit log for all user actions and system events';
COMMENT ON COLUMN audit_events.old_state IS 'Resource state before the action (for update operations)';
COMMENT ON COLUMN audit_events.new_state IS 'Resource state after the action';
COMMENT ON COLUMN audit_events.metadata IS 'Additional context-specific information';
