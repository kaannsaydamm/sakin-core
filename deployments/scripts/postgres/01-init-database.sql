-- Sakin Security Platform - PostgreSQL Database Initialization
-- This script creates the required database schema for the network sensor service

-- Set default schema
SET search_path TO public;

-- Create PacketData table
-- Stores network packet metadata captured by the network sensor
CREATE TABLE IF NOT EXISTS "PacketData" (
    "id" SERIAL PRIMARY KEY,
    "srcIp" VARCHAR(45) NOT NULL,
    "dstIp" VARCHAR(45) NOT NULL,
    "protocol" VARCHAR(20) NOT NULL,
    "timestamp" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "PacketData_srcIp_check" CHECK (LENGTH("srcIp") >= 7),
    CONSTRAINT "PacketData_dstIp_check" CHECK (LENGTH("dstIp") >= 7)
);

-- Create index for common queries
CREATE INDEX IF NOT EXISTS "idx_packetdata_timestamp" ON "PacketData"("timestamp" DESC);
CREATE INDEX IF NOT EXISTS "idx_packetdata_srcip" ON "PacketData"("srcIp");
CREATE INDEX IF NOT EXISTS "idx_packetdata_dstip" ON "PacketData"("dstIp");
CREATE INDEX IF NOT EXISTS "idx_packetdata_protocol" ON "PacketData"("protocol");

-- Create SniData table
-- Stores TLS Server Name Indication (SNI) data extracted from HTTPS traffic
CREATE TABLE IF NOT EXISTS "SniData" (
    "id" SERIAL PRIMARY KEY,
    "sni" VARCHAR(255) NOT NULL,
    "srcIp" VARCHAR(45) NOT NULL,
    "dstIp" VARCHAR(45) NOT NULL,
    "protocol" VARCHAR(20) NOT NULL,
    "timestamp" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "SniData_sni_check" CHECK (LENGTH("sni") >= 3)
);

-- Create index for common queries
CREATE INDEX IF NOT EXISTS "idx_snidata_timestamp" ON "SniData"("timestamp" DESC);
CREATE INDEX IF NOT EXISTS "idx_snidata_sni" ON "SniData"("sni");
CREATE INDEX IF NOT EXISTS "idx_snidata_srcip" ON "SniData"("srcIp");
CREATE INDEX IF NOT EXISTS "idx_snidata_dstip" ON "SniData"("dstIp");

-- Create a view for combined packet and SNI analysis
CREATE OR REPLACE VIEW "PacketSniView" AS
SELECT 
    p."id" as "packetId",
    p."srcIp",
    p."dstIp",
    p."protocol",
    p."timestamp" as "packetTimestamp",
    s."sni",
    s."timestamp" as "sniTimestamp"
FROM "PacketData" p
LEFT JOIN "SniData" s 
    ON p."srcIp" = s."srcIp" 
    AND p."dstIp" = s."dstIp"
    AND ABS(EXTRACT(EPOCH FROM (p."timestamp" - s."timestamp"))) < 5;

-- Grant permissions
GRANT ALL PRIVILEGES ON TABLE "PacketData" TO postgres;
GRANT ALL PRIVILEGES ON TABLE "SniData" TO postgres;
GRANT ALL PRIVILEGES ON SEQUENCE "PacketData_id_seq" TO postgres;
GRANT ALL PRIVILEGES ON SEQUENCE "SniData_id_seq" TO postgres;

-- Insert sample data for testing
INSERT INTO "PacketData" ("srcIp", "dstIp", "protocol", "timestamp") VALUES
    ('192.168.1.100', '8.8.8.8', 'UDP', CURRENT_TIMESTAMP - INTERVAL '5 minutes'),
    ('192.168.1.100', '1.1.1.1', 'UDP', CURRENT_TIMESTAMP - INTERVAL '4 minutes'),
    ('192.168.1.101', '172.217.14.206', 'TCP', CURRENT_TIMESTAMP - INTERVAL '3 minutes'),
    ('192.168.1.101', '142.250.185.46', 'TCP', CURRENT_TIMESTAMP - INTERVAL '2 minutes'),
    ('10.0.0.50', '93.184.216.34', 'TCP', CURRENT_TIMESTAMP - INTERVAL '1 minute');

INSERT INTO "SniData" ("sni", "srcIp", "dstIp", "protocol", "timestamp") VALUES
    ('www.google.com', '192.168.1.101', '172.217.14.206', 'TCP', CURRENT_TIMESTAMP - INTERVAL '3 minutes'),
    ('www.youtube.com', '192.168.1.101', '142.250.185.46', 'TCP', CURRENT_TIMESTAMP - INTERVAL '2 minutes'),
    ('www.example.com', '10.0.0.50', '93.184.216.34', 'TCP', CURRENT_TIMESTAMP - INTERVAL '1 minute');

-- Create Assets table
-- Stores asset inventory information for tracking hosts, services, and criticality
CREATE TABLE IF NOT EXISTS "Assets" (
    "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "name" TEXT NOT NULL,
    "ip_address" INET UNIQUE,
    "hostname" TEXT,
    "asset_type" TEXT NOT NULL CHECK ("asset_type" IN ('host', 'service', 'database', 'firewall', 'network_device', 'iot', 'other')),
    "criticality" TEXT NOT NULL CHECK ("criticality" IN ('low', 'medium', 'high', 'critical')),
    "owner" TEXT,
    "tags" TEXT[] DEFAULT '{}',
    "description" TEXT,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes for common queries
CREATE INDEX IF NOT EXISTS "idx_assets_ip_address" ON "Assets"("ip_address");
CREATE INDEX IF NOT EXISTS "idx_assets_hostname" ON "Assets"("hostname");
CREATE INDEX IF NOT EXISTS "idx_assets_type" ON "Assets"("asset_type");
CREATE INDEX IF NOT EXISTS "idx_assets_criticality" ON "Assets"("criticality");
CREATE INDEX IF NOT EXISTS "idx_assets_owner" ON "Assets"("owner");

-- Create trigger to update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $
BEGIN
    NEW."updated_at" = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$ language 'plpgsql';

CREATE TRIGGER update_assets_updated_at 
    BEFORE UPDATE ON "Assets" 
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Insert sample assets for testing
INSERT INTO "Assets" ("name", "ip_address", "hostname", "asset_type", "criticality", "owner", "tags", "description") VALUES
    ('DC01', '192.168.1.10', 'DC01.corp.local', 'host', 'critical', 'IT-Core', ARRAY['domain-controller', 'windows', 'active-directory'], 'Primary Domain Controller'),
    ('DB-PROD', '192.168.1.20', 'db-prod.corp.local', 'database', 'critical', 'Data-Team', ARRAY['mysql', 'production', 'critical-data'], 'Production MySQL Database Server'),
    ('WEB-SRV-01', '192.168.1.30', 'web-srv-01.corp.local', 'host', 'high', 'Web-Team', ARRAY['web-server', 'nginx', 'production'], 'Production Web Server'),
    ('LAB-01', '10.0.1.1', 'lab-01.test.local', 'host', 'low', 'Dev-Team', ARRAY['testing', 'development', 'windows'], 'Development Testing Machine'),
    ('FW-EDGE', '192.168.1.1', NULL, 'firewall', 'critical', 'Security-Team', ARRAY['perimeter', 'cisco', 'asa'], 'Edge Firewall'),
    ('SW-CORE-01', '192.168.1.5', 'sw-core-01.mgmt.local', 'network_device', 'high', 'Network-Team', ARRAY['switch', 'cisco', 'core'], 'Core Network Switch');

-- Display initialization status
DO $
BEGIN
    RAISE NOTICE '✅ Database initialized successfully';
    RAISE NOTICE '📊 Tables created: PacketData, SniData, Assets';
    RAISE NOTICE '🔍 View created: PacketSniView';
    RAISE NOTICE '📝 Sample data inserted for testing';
END $;
