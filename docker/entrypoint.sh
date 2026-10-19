#!/bin/bash

# Clean and create plugins directory, then copy plugin
rm -rf /config/plugins/PostgreSQL
mkdir -p /config/plugins/PostgreSQL
cp -r /jellyfin-pgsql/plugin/* /config/plugins/PostgreSQL/

# Create database.xml if it doesn't exist
if [ ! -f /config/config/database.xml ]; then
    mkdir -p /config/config
    cp /jellyfin-pgsql/database.xml /config/config/database.xml
fi

# Check database.xml correctly configured
ConfiguredPluginName="$(xmlstarlet select -t -m '//DatabaseConfigurationOptions/CustomProviderOptions/PluginName' -v . -n /config/config/database.xml)"
if [ "${ConfiguredPluginName}" != "PostgreSQL" ]; then
    echo "Plugin name is not set to PostgreSQL. abort."
    exit 2;
fi

# Check env variables set
if [ -z "${POSTGRES_HOST}" ] || [ -z "${POSTGRES_PASSWORD}" ]; then
    echo "PostgreSQL connectionstring variable unset. Please set 'POSTGRES_HOST' 'POSTGRES_PORT' 'POSTGRES_DB' 'POSTGRES_USER' and 'POSTGRES_PASSWORD' then restart"
    exit 3;
fi

# Quote a value for the semicolon-delimited Npgsql connection string. Values
# are wrapped in single quotes and a literal quote is written twice; without
# this a password containing ';' or leading/trailing spaces is misparsed.
quote_conn_value() {
    local value="${1//\'/\'\'}"
    printf "'%s'" "${value}"
}

# Build connection string for migration. Empty optional values are omitted so
# the provider can apply its documented defaults (and POSTGRES_COMMAND_TIMEOUT)
# for them.
ConnectionString="Password=$(quote_conn_value "${POSTGRES_PASSWORD}")"
if [ -n "${POSTGRES_HOST}" ]; then
    ConnectionString="${ConnectionString};Host=$(quote_conn_value "${POSTGRES_HOST}")"
fi

if [ -n "${POSTGRES_PORT}" ]; then
    ConnectionString="${ConnectionString};Port=$(quote_conn_value "${POSTGRES_PORT}")"
fi

if [ -n "${POSTGRES_DB}" ]; then
    ConnectionString="${ConnectionString};Database=$(quote_conn_value "${POSTGRES_DB}")"
fi

if [ -n "${POSTGRES_USER}" ]; then
    ConnectionString="${ConnectionString};User ID=$(quote_conn_value "${POSTGRES_USER}")"
fi

# Add SSL options if provided
if [ -n "${POSTGRES_SSLMODE}" ]; then
    ConnectionString="${ConnectionString};SSL Mode=$(quote_conn_value "${POSTGRES_SSLMODE}")"
fi

if [ -n "${POSTGRES_ROOTCERTIFICATE}" ]; then
    ConnectionString="${ConnectionString};Root Certificate=$(quote_conn_value "${POSTGRES_ROOTCERTIFICATE}")"
fi

if [ -n "${POSTGRES_TRUSTSERVERCERTIFICATE}" ]; then
    ConnectionString="${ConnectionString};Trust Server Certificate=$(quote_conn_value "${POSTGRES_TRUSTSERVERCERTIFICATE}")"
fi

# Update database.xml with connection string
xmlstarlet edit -L -u '//DatabaseConfigurationOptions/CustomProviderOptions/ConnectionString' -v "${ConnectionString}" /config/config/database.xml

# Run original Jellyfin entrypoint
exec /jellyfin/jellyfin "$@"
