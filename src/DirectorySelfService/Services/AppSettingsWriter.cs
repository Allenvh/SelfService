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

        root["Directory"] = new JsonObject
        {
            ["DefaultDomain"] = settings.DefaultDomain.Trim(),
            ["LdapServer"] = settings.LdapServer.Trim(),
            ["LdapPort"] = settings.LdapPort,
            ["UseSsl"] = settings.UseSsl,
            ["UseSigning"] = settings.UseSigning,
            ["UseSealing"] = settings.UseSealing,
            ["SearchBaseDn"] = settings.SearchBaseDn.Trim(),
            ["AllowedGroups"] = ToJsonArray(settings.AllowedGroupsText),
            ["RestrictedGroups"] = ToJsonArray(settings.RestrictedGroupsText),
            ["LdapTimeoutSeconds"] = settings.LdapTimeoutSeconds
        };

        var logging = root["Logging"] as JsonObject ?? new JsonObject();
        var logLevel = logging["LogLevel"] as JsonObject ?? new JsonObject();
        logLevel["Default"] = settings.VerboseDirectoryLogging ? "Debug" : "Information";
        logLevel["DirectorySelfService.Services.ActiveDirectoryPasswordService"] = settings.VerboseDirectoryLogging ? "Trace" : "Information";
        logLevel["System.DirectoryServices.Protocols"] = settings.VerboseDirectoryLogging ? "Debug" : "Warning";
        logging["LogLevel"] = logLevel;
        root["Logging"] = logging;

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
