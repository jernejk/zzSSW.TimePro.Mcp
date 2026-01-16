using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SSW.TimePro.Mcp.Server.Configuration;
using SSW.TimePro.Mcp.Server.Services;
using SSW.TimePro.Mcp.Server.Services.Agenda;
using SSW.TimePro.Mcp.Server.Services.Git;
using Xunit;
using Xunit.Abstractions;

namespace SSW.TimePro.Mcp.Tests.Integration;

[Trait("Category", "Integration")]
public class ProductionReplayTests
{
    private readonly ITestOutputHelper _output;

    public ProductionReplayTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [SkippableFact]
    public async Task GenerateAgenda_ShouldMatchProduction()
    {
        Skip.IfNot(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS") == "true", "Integration tests are disabled");

        // 1. Setup Services
        var settings = Options.Create(new TimeProSettings
        {
            BaseUrl = Environment.GetEnvironmentVariable("TIMEPRO_BASE_URL") ?? "https://ssw.sswtimepro.com/",
            TenantId = Environment.GetEnvironmentVariable("TIMEPRO_TENANT_ID") ?? "ssw",
            ApiKey = Environment.GetEnvironmentVariable("TIMEPRO_API_KEY") ?? throw new InvalidOperationException("TIMEPRO_API_KEY environment variable is not set") // AppName is read-only
        });

        var httpClient = new HttpClient();
        var timeProService = new TimeProService(httpClient, settings);
        var localGitService = new LocalGitService();
        var gitHubService = new GitHubService(httpClient, Options.Create(new GitHubSettings()));
        var gitScanningService = new GitScanningService(localGitService, gitHubService);
        var agendaService = new AgendaService();

        // 2. Define Date Range (12 Jan 2026 - 16 Jan 2026)
        var start = new DateOnly(2026, 1, 12);
        var end = new DateOnly(2026, 1, 16);
        string employeeId = "JEK";

        // 3. Fetch Production Timesheets
        _output.WriteLine($"Fetching real timesheets for {start} to {end}...");
        
        // Use loop inside service as range endpoint might be missing
        var timesheets = await timeProService.GetTimesheetsAsync(employeeId, start, end);
        var timesheetJson = JsonSerializer.Serialize(timesheets, new JsonSerializerOptions { WriteIndented = true });
        
        var dumpPath = Path.Combine(Directory.GetCurrentDirectory(), "production-timesheets.json");
        await File.WriteAllTextAsync(dumpPath, timesheetJson);
        _output.WriteLine($"Saved production timesheets to {dumpPath}");

        // 4. Generate Agenda
        var repoPaths = new List<string> 
        { 
            "/Users/jk/Developer/git/SSW.Rewards.Mobile",
            "/Users/jk/Developer/git/ASF/HubX", 
            "/Users/jk/Developer/git/ASF/AI-Auditor" 
        };

        _output.WriteLine($"Generating agenda using Git paths: {string.Join(", ", repoPaths)}");
        
        // Ensure directories exist or warn
        foreach (var path in repoPaths)
        {
            if (!Directory.Exists(path))
            {
                _output.WriteLine($"WARNING: Git path not found: {path}. Test results may be empty.");
            }
        }
        
        var options = new AgendaGenerationOptions
        {
            EmployeeId = employeeId,
            StartDate = start,
            EndDate = end,
            IncludeExistingTimesheets = false, // Mocking "fresh" generation
            IncludeCrmBookings = true,
            IncludeSuggestedTimesheets = true,
            LocalGitPaths = repoPaths,
            DefaultClientId = "SSW",
            DefaultProjectId = "TimePro", // Default fallback
            DefaultCategoryId = "DEV"
        };


        var agenda = await agendaService.GenerateAgendaAsync(options, timeProService, gitScanningService);
        
        // 5. Export Agenda
        var markdown = agendaService.ExportToMarkdown(agenda);
        var agendaPath = Path.Combine(Directory.GetCurrentDirectory(), "generated-agenda.md");
        await File.WriteAllTextAsync(agendaPath, markdown);
        _output.WriteLine($"Saved generated agenda to {agendaPath}");
        
        _output.WriteLine("\n--- Generated Agenda ---\n");
        _output.WriteLine(markdown);

        // 6. Basic Assertions behavior check
        agenda.Days.Should().HaveCount(5); // 5 days requested
    }
}
