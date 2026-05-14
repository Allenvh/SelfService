using System.Runtime.Versioning;
using System.Threading.RateLimiting;
using DirectorySelfService.Middleware;
using DirectorySelfService.Options;
using DirectorySelfService.Services;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DirectoryOptions>(builder.Configuration.GetSection("Directory"));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimit"));
builder.Services.Configure<AuditOptions>(builder.Configuration.GetSection("Audit"));
builder.Services.Configure<AppBrandingOptions>(builder.Configuration.GetSection("Branding"));
builder.Services.Configure<CaptchaOptions>(builder.Configuration.GetSection("Captcha"));
builder.Services.Configure<HostingOptions>(builder.Configuration.GetSection("Hosting"));

var hostingOptions = builder.Configuration.GetSection("Hosting").Get<HostingOptions>() ?? new HostingOptions();
if (hostingOptions.HttpsPort is > 0)
{
    builder.Services.AddHttpsRedirection(options => options.HttpsPort = hostingOptions.HttpsPort);
}

if (!string.IsNullOrWhiteSpace(hostingOptions.DataProtectionKeysPath))
{
    builder.Services.AddDataProtection()
        .SetApplicationName(hostingOptions.DataProtectionApplicationName)
        .PersistKeysToFileSystem(new DirectoryInfo(hostingOptions.DataProtectionKeysPath));
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<UsernameNormalizer>();
builder.Services.AddSingleton<PasswordPolicyErrorMapper>();
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();
builder.Services.AddScoped<IActiveDirectoryPasswordService, ActiveDirectoryPasswordService>();
builder.Services.AddSingleton<IAuditLogger, AuditLogger>();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddHsts(options =>
{
    options.Preload = false;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.AddRateLimiter(options =>
{
    var configured = builder.Configuration.GetSection("RateLimit").Get<RateLimitOptions>() ?? new RateLimitOptions();
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(configured.PermitLimit * 2, configured.PermitLimit),
            Window = TimeSpan.FromMinutes(configured.WindowMinutes),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
var auditOptions = builder.Configuration.GetSection("Audit").Get<AuditOptions>() ?? new AuditOptions();
if (OperatingSystem.IsWindows() && auditOptions.EnableWindowsEventLog)
{
    AddWindowsEventLog(builder.Logging, auditOptions.EventLogSource);
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<SecureHeadersMiddleware>();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.MapRazorPages();

app.Run();

[SupportedOSPlatform("windows")]
static void AddWindowsEventLog(ILoggingBuilder loggingBuilder, string eventLogSource)
{
    loggingBuilder.AddEventLog(settings => settings.SourceName = eventLogSource);
}

public partial class Program;
