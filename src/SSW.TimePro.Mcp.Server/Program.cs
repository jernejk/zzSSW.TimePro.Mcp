using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using SSW.TimePro.Mcp.Server.Configuration;
using SSW.TimePro.Mcp.Server.Services;
using SSW.TimePro.Mcp.Server.Services.Agenda;
using SSW.TimePro.Mcp.Server.Services.Confirmation;
using SSW.TimePro.Mcp.Server.Services.Git;
using SSW.TimePro.Mcp.Server.Tools;

// Build the host with MCP server configuration
var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ApplicationName = "SSW.TimePro.Mcp.Server",
    EnvironmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"
});

// Configure configuration sources
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true);

// Bind configuration with support for single underscore env vars (e.g., TIMEPRO_API_KEY)
builder.Services.Configure<TimeProSettings>(options =>
{
    builder.Configuration.GetSection(TimeProSettings.SectionName).Bind(options);

    // Support single-underscore env vars as fallback (more common convention)
    options.BaseUrl = GetEnvOrDefault("TIMEPRO_BASE_URL", options.BaseUrl);
    options.TenantId = GetEnvOrDefault("TIMEPRO_TENANT_ID", options.TenantId);
    options.ApiKey = GetEnvOrDefault("TIMEPRO_API_KEY", options.ApiKey);
    options.ConfirmPhrase = GetEnvOrDefault("TIMEPRO_CONFIRM_PHRASE", options.ConfirmPhrase);
    options.DefaultEmployeeId = GetEnvOrDefault("TIMEPRO_DEFAULT_EMPLOYEE_ID", options.DefaultEmployeeId);
});

builder.Services.Configure<GitHubSettings>(options =>
{
    builder.Configuration.GetSection(GitHubSettings.SectionName).Bind(options);

    // Support single-underscore and standard env vars as fallback
    options.Token = GetEnvOrDefault("GITHUB_TOKEN", options.Token);
    options.Username = GetEnvOrDefault("GITHUB_USERNAME", options.Username);
});

// Helper to get env var with fallback
static string GetEnvOrDefault(string name, string defaultValue)
{
    var value = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrEmpty(value) ? defaultValue : value;
}

// Register HTTP clients with SSL bypass for local development
builder.Services.AddHttpClient<ITimeProService, TimeProService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            // Trust local development certificates
            if (message.RequestUri?.Host.Contains("local-") == true)
                return true;
            // Use default validation for all other hosts
            return errors == System.Net.Security.SslPolicyErrors.None;
        }
    });
builder.Services.AddHttpClient<IGitHubService, GitHubService>();

// Register confirmation service (file-based persistence)
builder.Services.AddSingleton<IConfirmationService>(_ => 
    new FileConfirmationService(Directory.GetCurrentDirectory()));

// Register git services
builder.Services.AddSingleton<ILocalGitService, LocalGitService>();
builder.Services.AddSingleton<IGitScanningService, GitScanningService>();

// Register agenda services
builder.Services.AddScoped<IAgendaService, AgendaService>();

// Register MCP tools as scoped services
builder.Services.AddScoped<TimesheetTools>();
builder.Services.AddScoped<AppointmentTools>();
builder.Services.AddScoped<TimesheetManagementTools>();
builder.Services.AddScoped<ConfirmationTools>();
builder.Services.AddScoped<GitScanTools>();
builder.Services.AddScoped<RecommendTools>();

// Configure MCP server with stdio transport
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Build and run the host
var app = builder.Build();

await app.RunAsync();

// Make Program class accessible for testing
public partial class Program { }
