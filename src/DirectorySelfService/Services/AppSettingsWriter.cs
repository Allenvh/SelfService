using System.Text.Json;
using System.Text.Json.Nodes;
using DirectorySelfService.Models;
using DirectorySelfService.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace DirectorySelfService.Services;

public sealed class AppSettingsWriter(
    IWebHostEnvironment environment,
    IOptions<AdminPortalOptions> adminOptions,
    ILogger<AppSettingsWriter> logger) : IAppSettingsWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public async Task SaveDirectorySettingsAsync(AdminDirectorySettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var settingsPath = ResolveSettingsPath(adminOptions.Value.WritableSettingsPath);
        logger.LogInformation("Persisting admin directory settings to {SettingsPath}.", settingsPath);

        JsonObject root;
        if (File.Exists(settingsPath))
        {
            var existing = await File.ReadAllTextAsync(settingsPath, cancellationToken);
            root = JsonNode.Parse(existing)?.AsObject() ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        var directorySettings = root["Directory"] as JsonObject;
        if (directorySettings is null)
        {
            directorySettings = new JsonObject();
            root["Directory"] = directorySettings;
        }

        directorySettings["DefaultDomain"] = settings.DefaultDomain.Trim();
        directorySettings["LdapServer"] = settings.LdapServer.Trim();
        directorySettings["LdapPort"] = settings.LdapPort;
        directorySettings["UseSsl"] = settings.UseSsl;
        directorySettings["SearchBaseDn"] = settings.SearchBaseDn.Trim();
        directorySettings["AllowedGroups"] = ToJsonArray(settings.AllowedGroupsText);
        directorySettings["RestrictedGroups"] = ToJsonArray(settings.RestrictedGroupsText);
        directorySettings["LdapTimeoutSeconds"] = settings.LdapTimeoutSeconds;

        var logging = root["Logging"] as JsonObject;
        if (logging is null)
        {
            logging = new JsonObject();
            root["Logging"] = logging;
        }

        var logLevel = logging["LogLevel"] as JsonObject;
        if (logLevel is null)
        {
            logLevel = new JsonObject();
            logging["LogLevel"] = logLevel;
        }

        logLevel["Default"] = settings.VerboseDirectoryLogging ? "Debug" : "Information";
        logLevel["DirectorySelfService.Services.ActiveDirectoryPasswordService"] = settings.VerboseDirectoryLogging ? "Trace" : "Information";
        logLevel["System.DirectoryServices.Protocols"] = settings.VerboseDirectoryLogging ? "Debug" : "Warning";

        var directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{settingsPath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempPath, root.ToJsonString(SerializerOptions), cancellationToken);
        File.Move(tempPath, settingsPath, overwrite: true);
    }

    private string ResolveSettingsPath(string configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath) ? "appsettings.json" : configuredPath;
        return Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path);
    }

    private static JsonArray ToJsonArray(string text)
    {
        var values = SplitLines(text);
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    public static string[] SplitLines(string text) => text
        .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
