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

-- Display initialization status
DO $$
BEGIN
    RAISE NOTICE '✅ Database initialized successfully';
    RAISE NOTICE '📊 Tables created: PacketData, SniData';
    RAISE NOTICE '🔍 View created: PacketSniView';
    RAISE NOTICE '📝 Sample data inserted for testing';
END $$;
