#!/bin/bash
set -e

# FireGuard Microservices Database Initialization
# PostgreSQL container startup'ında çalışır

echo "🔥 FireGuard databases initializing..."

# Fire Detection Service Database
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE DATABASE "FireDetectionDB";
    GRANT ALL PRIVILEGES ON DATABASE "FireDetectionDB" TO $POSTGRES_USER;
EOSQL

# Alert Service Database
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE DATABASE "AlertDB";
    GRANT ALL PRIVILEGES ON DATABASE "AlertDB" TO $POSTGRES_USER;
EOSQL

# User Management Service Database
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE DATABASE "UserDB";
    GRANT ALL PRIVILEGES ON DATABASE "UserDB" TO $POSTGRES_USER;
EOSQL

# Notification Service Database
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE DATABASE "NotificationDB";
    GRANT ALL PRIVILEGES ON DATABASE "NotificationDB" TO $POSTGRES_USER;
EOSQL

# PostGIS extension for Fire Detection DB
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "FireDetectionDB" <<-EOSQL
    CREATE EXTENSION IF NOT EXISTS postgis;
    CREATE EXTENSION IF NOT EXISTS postgis_topology;
EOSQL

echo "✅ FireGuard databases created successfully!"