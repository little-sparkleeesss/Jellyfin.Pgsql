using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Plugin.Pgsql.Migrations;

/// <inheritdoc />
/// <summary>
/// Applies the "C" collation to every text column so that equality and
/// ordering behave byte-wise like SQLite BINARY, independent of the
/// PostgreSQL cluster's default locale.
/// </summary>
public partial class ApplyCCollation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
migrationBuilder.Sql("""
        ALTER TABLE "ActivityLogs" ALTER COLUMN "ItemId" TYPE character varying(256) COLLATE "C";
        ALTER TABLE "ActivityLogs" ALTER COLUMN "Name" TYPE character varying(512) COLLATE "C";
        ALTER TABLE "ActivityLogs" ALTER COLUMN "Overview" TYPE character varying(512) COLLATE "C";
        ALTER TABLE "ActivityLogs" ALTER COLUMN "ShortOverview" TYPE character varying(512) COLLATE "C";
        ALTER TABLE "ActivityLogs" ALTER COLUMN "Type" TYPE character varying(256) COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "AttachmentStreamInfos" ALTER COLUMN "Codec" TYPE text COLLATE "C";
        ALTER TABLE "AttachmentStreamInfos" ALTER COLUMN "CodecTag" TYPE text COLLATE "C";
        ALTER TABLE "AttachmentStreamInfos" ALTER COLUMN "Comment" TYPE text COLLATE "C";
        ALTER TABLE "AttachmentStreamInfos" ALTER COLUMN "Filename" TYPE text COLLATE "C";
        ALTER TABLE "AttachmentStreamInfos" ALTER COLUMN "MimeType" TYPE text COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "BaseItems" ALTER COLUMN "Album" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "AlbumArtists" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "Artists" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "CleanName" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "CustomRating" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "Data" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "EpisodeTitle" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "ExternalId" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "ExternalSeriesId" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "ExternalServiceId" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "ExtraIds" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "ForcedSortName" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "Genres" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "MediaType" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "Name" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "OfficialRating" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "OriginalTitle" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "Overview" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "OwnerId" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "Path" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "PreferredMetadataCountryCode" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "PreferredMetadataLanguage" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "PresentationUniqueKey" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "PrimaryVersionId" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "ProductionLocations" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "SeasonName" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "SeriesName" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "SeriesPresentationUniqueKey" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "ShowId" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "SortName" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "Studios" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "Tagline" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "Tags" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "Type" TYPE text COLLATE "C";
        ALTER TABLE "BaseItems" ALTER COLUMN "UnratedType" TYPE text COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "BaseItemImageInfos" ALTER COLUMN "Path" TYPE text COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "BaseItemProviders" ALTER COLUMN "ProviderId" TYPE text COLLATE "C";
        ALTER TABLE "BaseItemProviders" ALTER COLUMN "ProviderValue" TYPE text COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "Chapters" ALTER COLUMN "ImagePath" TYPE text COLLATE "C";
        ALTER TABLE "Chapters" ALTER COLUMN "Name" TYPE text COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "CustomItemDisplayPreferences" ALTER COLUMN "Client" TYPE character varying(32) COLLATE "C";
        ALTER TABLE "CustomItemDisplayPreferences" ALTER COLUMN "Key" TYPE text COLLATE "C";
        ALTER TABLE "CustomItemDisplayPreferences" ALTER COLUMN "Value" TYPE text COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "DisplayPreferences" ALTER COLUMN "Client" TYPE character varying(32) COLLATE "C";
        ALTER TABLE "DisplayPreferences" ALTER COLUMN "DashboardTheme" TYPE character varying(32) COLLATE "C";
        ALTER TABLE "DisplayPreferences" ALTER COLUMN "TvHome" TYPE character varying(32) COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "ImageInfos" ALTER COLUMN "Path" TYPE character varying(512) COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "ItemDisplayPreferences" ALTER COLUMN "Client" TYPE character varying(32) COLLATE "C";
        ALTER TABLE "ItemDisplayPreferences" ALTER COLUMN "SortBy" TYPE character varying(64) COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "ItemValues" ALTER COLUMN "CleanValue" TYPE text COLLATE "C";
        ALTER TABLE "ItemValues" ALTER COLUMN "Value" TYPE text COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "MediaSegments" ALTER COLUMN "SegmentProviderId" TYPE text COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "AspectRatio" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "ChannelLayout" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "Codec" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "CodecTag" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "CodecTimeBase" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "ColorPrimaries" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "ColorSpace" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "ColorTransfer" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "Comment" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "KeyFrames" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "Language" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "NalLengthSize" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "Path" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "PixelFormat" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "Profile" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "TimeBase" TYPE text COLLATE "C";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "Title" TYPE text COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "Peoples" ALTER COLUMN "Name" TYPE text COLLATE "C";
        ALTER TABLE "Peoples" ALTER COLUMN "PersonType" TYPE text COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "PeopleBaseItemMap" ALTER COLUMN "Role" TYPE text COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "Preferences" ALTER COLUMN "Value" TYPE character varying(65535) COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "ApiKeys" ALTER COLUMN "AccessToken" TYPE text COLLATE "C";
        ALTER TABLE "ApiKeys" ALTER COLUMN "Name" TYPE character varying(64) COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "Devices" ALTER COLUMN "AccessToken" TYPE text COLLATE "C";
        ALTER TABLE "Devices" ALTER COLUMN "AppName" TYPE character varying(64) COLLATE "C";
        ALTER TABLE "Devices" ALTER COLUMN "AppVersion" TYPE character varying(32) COLLATE "C";
        ALTER TABLE "Devices" ALTER COLUMN "DeviceId" TYPE character varying(256) COLLATE "C";
        ALTER TABLE "Devices" ALTER COLUMN "DeviceName" TYPE character varying(64) COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "DeviceOptions" ALTER COLUMN "CustomName" TYPE text COLLATE "C";
        ALTER TABLE "DeviceOptions" ALTER COLUMN "DeviceId" TYPE text COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "Users" ALTER COLUMN "AudioLanguagePreference" TYPE character varying(255) COLLATE "C";
        ALTER TABLE "Users" ALTER COLUMN "AuthenticationProviderId" TYPE character varying(255) COLLATE "C";
        ALTER TABLE "Users" ALTER COLUMN "CastReceiverId" TYPE character varying(32) COLLATE "C";
        ALTER TABLE "Users" ALTER COLUMN "NormalizedUsername" TYPE character varying(255) COLLATE "C";
        ALTER TABLE "Users" ALTER COLUMN "Password" TYPE character varying(65535) COLLATE "C";
        ALTER TABLE "Users" ALTER COLUMN "PasswordResetProviderId" TYPE character varying(255) COLLATE "C";
        ALTER TABLE "Users" ALTER COLUMN "SubtitleLanguagePreference" TYPE character varying(255) COLLATE "C";
        ALTER TABLE "Users" ALTER COLUMN "Username" TYPE character varying(255) COLLATE "C";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "UserData" ALTER COLUMN "CustomDataKey" TYPE text COLLATE "C";
        """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
migrationBuilder.Sql("""
        ALTER TABLE "ActivityLogs" ALTER COLUMN "ItemId" TYPE character varying(256) COLLATE "default";
        ALTER TABLE "ActivityLogs" ALTER COLUMN "Name" TYPE character varying(512) COLLATE "default";
        ALTER TABLE "ActivityLogs" ALTER COLUMN "Overview" TYPE character varying(512) COLLATE "default";
        ALTER TABLE "ActivityLogs" ALTER COLUMN "ShortOverview" TYPE character varying(512) COLLATE "default";
        ALTER TABLE "ActivityLogs" ALTER COLUMN "Type" TYPE character varying(256) COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "AttachmentStreamInfos" ALTER COLUMN "Codec" TYPE text COLLATE "default";
        ALTER TABLE "AttachmentStreamInfos" ALTER COLUMN "CodecTag" TYPE text COLLATE "default";
        ALTER TABLE "AttachmentStreamInfos" ALTER COLUMN "Comment" TYPE text COLLATE "default";
        ALTER TABLE "AttachmentStreamInfos" ALTER COLUMN "Filename" TYPE text COLLATE "default";
        ALTER TABLE "AttachmentStreamInfos" ALTER COLUMN "MimeType" TYPE text COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "BaseItems" ALTER COLUMN "Album" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "AlbumArtists" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "Artists" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "CleanName" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "CustomRating" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "Data" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "EpisodeTitle" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "ExternalId" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "ExternalSeriesId" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "ExternalServiceId" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "ExtraIds" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "ForcedSortName" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "Genres" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "MediaType" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "Name" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "OfficialRating" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "OriginalTitle" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "Overview" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "OwnerId" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "Path" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "PreferredMetadataCountryCode" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "PreferredMetadataLanguage" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "PresentationUniqueKey" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "PrimaryVersionId" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "ProductionLocations" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "SeasonName" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "SeriesName" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "SeriesPresentationUniqueKey" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "ShowId" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "SortName" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "Studios" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "Tagline" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "Tags" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "Type" TYPE text COLLATE "default";
        ALTER TABLE "BaseItems" ALTER COLUMN "UnratedType" TYPE text COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "BaseItemImageInfos" ALTER COLUMN "Path" TYPE text COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "BaseItemProviders" ALTER COLUMN "ProviderId" TYPE text COLLATE "default";
        ALTER TABLE "BaseItemProviders" ALTER COLUMN "ProviderValue" TYPE text COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "Chapters" ALTER COLUMN "ImagePath" TYPE text COLLATE "default";
        ALTER TABLE "Chapters" ALTER COLUMN "Name" TYPE text COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "CustomItemDisplayPreferences" ALTER COLUMN "Client" TYPE character varying(32) COLLATE "default";
        ALTER TABLE "CustomItemDisplayPreferences" ALTER COLUMN "Key" TYPE text COLLATE "default";
        ALTER TABLE "CustomItemDisplayPreferences" ALTER COLUMN "Value" TYPE text COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "DisplayPreferences" ALTER COLUMN "Client" TYPE character varying(32) COLLATE "default";
        ALTER TABLE "DisplayPreferences" ALTER COLUMN "DashboardTheme" TYPE character varying(32) COLLATE "default";
        ALTER TABLE "DisplayPreferences" ALTER COLUMN "TvHome" TYPE character varying(32) COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "ImageInfos" ALTER COLUMN "Path" TYPE character varying(512) COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "ItemDisplayPreferences" ALTER COLUMN "Client" TYPE character varying(32) COLLATE "default";
        ALTER TABLE "ItemDisplayPreferences" ALTER COLUMN "SortBy" TYPE character varying(64) COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "ItemValues" ALTER COLUMN "CleanValue" TYPE text COLLATE "default";
        ALTER TABLE "ItemValues" ALTER COLUMN "Value" TYPE text COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "MediaSegments" ALTER COLUMN "SegmentProviderId" TYPE text COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "AspectRatio" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "ChannelLayout" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "Codec" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "CodecTag" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "CodecTimeBase" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "ColorPrimaries" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "ColorSpace" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "ColorTransfer" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "Comment" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "KeyFrames" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "Language" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "NalLengthSize" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "Path" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "PixelFormat" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "Profile" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "TimeBase" TYPE text COLLATE "default";
        ALTER TABLE "MediaStreamInfos" ALTER COLUMN "Title" TYPE text COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "Peoples" ALTER COLUMN "Name" TYPE text COLLATE "default";
        ALTER TABLE "Peoples" ALTER COLUMN "PersonType" TYPE text COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "PeopleBaseItemMap" ALTER COLUMN "Role" TYPE text COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "Preferences" ALTER COLUMN "Value" TYPE character varying(65535) COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "ApiKeys" ALTER COLUMN "AccessToken" TYPE text COLLATE "default";
        ALTER TABLE "ApiKeys" ALTER COLUMN "Name" TYPE character varying(64) COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "Devices" ALTER COLUMN "AccessToken" TYPE text COLLATE "default";
        ALTER TABLE "Devices" ALTER COLUMN "AppName" TYPE character varying(64) COLLATE "default";
        ALTER TABLE "Devices" ALTER COLUMN "AppVersion" TYPE character varying(32) COLLATE "default";
        ALTER TABLE "Devices" ALTER COLUMN "DeviceId" TYPE character varying(256) COLLATE "default";
        ALTER TABLE "Devices" ALTER COLUMN "DeviceName" TYPE character varying(64) COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "DeviceOptions" ALTER COLUMN "CustomName" TYPE text COLLATE "default";
        ALTER TABLE "DeviceOptions" ALTER COLUMN "DeviceId" TYPE text COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "Users" ALTER COLUMN "AudioLanguagePreference" TYPE character varying(255) COLLATE "default";
        ALTER TABLE "Users" ALTER COLUMN "AuthenticationProviderId" TYPE character varying(255) COLLATE "default";
        ALTER TABLE "Users" ALTER COLUMN "CastReceiverId" TYPE character varying(32) COLLATE "default";
        ALTER TABLE "Users" ALTER COLUMN "NormalizedUsername" TYPE character varying(255) COLLATE "default";
        ALTER TABLE "Users" ALTER COLUMN "Password" TYPE character varying(65535) COLLATE "default";
        ALTER TABLE "Users" ALTER COLUMN "PasswordResetProviderId" TYPE character varying(255) COLLATE "default";
        ALTER TABLE "Users" ALTER COLUMN "SubtitleLanguagePreference" TYPE character varying(255) COLLATE "default";
        ALTER TABLE "Users" ALTER COLUMN "Username" TYPE character varying(255) COLLATE "default";
        """);

migrationBuilder.Sql("""
        ALTER TABLE "UserData" ALTER COLUMN "CustomDataKey" TYPE text COLLATE "default";
        """);
    }
}
