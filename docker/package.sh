#!/bin/bash

# Package Jellyfin PostgreSQL Plugin for Docker build
# This script creates a tar.gz with all files needed to build the Docker image.
# Usage: run from the docker/ directory; extract the tar.gz elsewhere and run
#        `docker build -f docker/Dockerfile .` from the extracted directory.

set -e

PACKAGE_NAME="jellyfin-pgsql-plugin.tar.gz"

echo "Creating package: $PACKAGE_NAME"

# Remove existing package if it exists
if [ -f "$PACKAGE_NAME" ]; then
    rm "$PACKAGE_NAME"
    echo "Removed existing $PACKAGE_NAME"
fi

# Copy files into a staging directory that mirrors the repository layout the
# Dockerfile expects (docker/* at the top level, plugin sources at root).
STAGING="$(mktemp -d)"
trap 'rm -rf "$STAGING"' EXIT

mkdir -p "$STAGING/docker"
mkdir -p "$STAGING/Jellyfin.Plugin.Pgsql/Configuration"
mkdir -p "$STAGING/Jellyfin.Plugin.Pgsql/Database"
mkdir -p "$STAGING/Jellyfin.Plugin.Pgsql/Migrations"

cp ../jellyfin.ruleset "$STAGING/"
cp ../Jellyfin.Plugin.Pgsql.sln "$STAGING/"
cp ../build.yaml "$STAGING/"
cp Dockerfile "$STAGING/docker/"
cp entrypoint.sh "$STAGING/docker/"
cp database.xml "$STAGING/docker/"

cp ../Jellyfin.Plugin.Pgsql/Jellyfin.Plugin.Pgsql.csproj "$STAGING/Jellyfin.Plugin.Pgsql/"
cp ../Jellyfin.Plugin.Pgsql/Plugin.cs "$STAGING/Jellyfin.Plugin.Pgsql/"
cp ../Jellyfin.Plugin.Pgsql/Configuration/PluginConfiguration.cs "$STAGING/Jellyfin.Plugin.Pgsql/Configuration/"
cp ../Jellyfin.Plugin.Pgsql/Configuration/configPage.html "$STAGING/Jellyfin.Plugin.Pgsql/Configuration/"
cp ../Jellyfin.Plugin.Pgsql/Database/PgSqlDatabaseProvider.cs "$STAGING/Jellyfin.Plugin.Pgsql/Database/"
cp ../Jellyfin.Plugin.Pgsql/Database/PgSqlDesignTimeJellyfinDbFactory.cs "$STAGING/Jellyfin.Plugin.Pgsql/Database/"
# Copy every migration (including Designer files) so the packaged plugin ships
# the complete schema history.
cp ../Jellyfin.Plugin.Pgsql/Migrations/*.cs "$STAGING/Jellyfin.Plugin.Pgsql/Migrations/"

tar -czf "$PACKAGE_NAME" \
    --uid=0 --gid=0 \
    -C "$STAGING" \
    .

# Show package size
SIZE=$(ls -lh "$PACKAGE_NAME" | awk '{print $5}')
echo "Package created: $PACKAGE_NAME ($SIZE)"

echo ""
echo "To use on another system:"
echo "1. Copy $PACKAGE_NAME to the target system"
echo "2. Extract: tar -xzf $PACKAGE_NAME"
echo "3. Build: docker build -f docker/Dockerfile ."
echo "4. Run with PostgreSQL environment variables"
