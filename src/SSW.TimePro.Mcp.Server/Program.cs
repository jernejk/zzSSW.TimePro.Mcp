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

// Bind configuration
builder.Services.Configure<TimeProSettings>(
    builder.Configuration.GetSection(TimeProSettings.SectionName));
builder.Services.Configure<GitHubSettings>(
    builder.Configuration.GetSection(GitHubSettings.SectionName));

// Register HTTP clients
builder.Services.AddHttpClient<ITimeProService, TimeProService>();
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
builder.Services.AddScoped<AgendaTools>();

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
