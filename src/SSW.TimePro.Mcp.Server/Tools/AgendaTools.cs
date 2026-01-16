using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SSW.TimePro.Mcp.Server.Models;
using SSW.TimePro.Mcp.Server.Services;
using SSW.TimePro.Mcp.Server.Services.Agenda;
using SSW.TimePro.Mcp.Server.Services.Confirmation;
using SSW.TimePro.Mcp.Server.Services.Git;

namespace SSW.TimePro.Mcp.Server.Tools;

/// <summary>
/// MCP Tools for agenda generation and timesheet planning.
/// </summary>
[McpServerToolType]
public class AgendaTools
{
    private readonly IAgendaService _agendaService;
    private readonly ITimeProService _timeProService;
    private readonly IGitScanningService _gitScanningService;
    private readonly IConfirmationService _confirmationService;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AgendaTools(
        IAgendaService agendaService,
        ITimeProService timeProService,
        IGitScanningService gitScanningService,
        IConfirmationService confirmationService)
    {
        _agendaService = agendaService;
        _timeProService = timeProService;
        _gitScanningService = gitScanningService;
        _confirmationService = confirmationService;
    }

    /// <summary>
    /// Generate a weekly agenda based on various data sources.
    /// </summary>
    [McpServerTool]
    [Description("Generate a weekly timesheet agenda by combining CRM bookings, suggested timesheets, existing entries, and git activity. Perfect for Friday timesheet catch-up!")]
    public async Task<string> GenerateWeeklyAgenda(
        [Description("Employee ID (e.g., 'JEK')")] string employeeId,
        [Description("Start date (yyyy-MM-dd). Defaults to Monday of current week.")] string? startDate = null,
        [Description("End date (yyyy-MM-dd). Defaults to Friday of current week.")] string? endDate = null,
        [Description("Comma-separated local git repo paths to scan (optional)")] string? localGitPaths = null,
        [Description("GitHub username for activity (optional)")] string? gitHubUsername = null,
        [Description("Default client ID if not detected")] string? defaultClientId = null,
        [Description("Default project ID if not detected")] string? defaultProjectId = null,
        [Description("Include existing timesheets (default: true)")] bool includeExisting = true,
        [Description("Include CRM bookings (default: true)")] bool includeCrm = true,
        [Description("Include suggested timesheets (default: true)")] bool includeSuggested = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateTime.Today;
            var monday = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            var friday = monday.AddDays(4);
            
            var start = string.IsNullOrEmpty(startDate) 
                ? DateOnly.FromDateTime(monday) 
                : DateOnly.Parse(startDate);
            var end = string.IsNullOrEmpty(endDate) 
                ? DateOnly.FromDateTime(friday) 
                : DateOnly.Parse(endDate);
            
            var options = new AgendaGenerationOptions
            {
                EmployeeId = employeeId,
                StartDate = start,
                EndDate = end,
                IncludeExistingTimesheets = includeExisting,
                IncludeCrmBookings = includeCrm,
                IncludeSuggestedTimesheets = includeSuggested,
                DefaultClientId = defaultClientId,
                DefaultProjectId = defaultProjectId,
                LocalGitPaths = string.IsNullOrEmpty(localGitPaths) 
                    ? null 
                    : localGitPaths.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                GitHubUsername = gitHubUsername
            };
            
            var agenda = await _agendaService.GenerateAgendaAsync(
                options,
                _timeProService,
                !string.IsNullOrEmpty(localGitPaths) ? _gitScanningService : null,
                cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                agendaId = agenda.AgendaId,
                employeeId,
                startDate = start.ToString("yyyy-MM-dd"),
                endDate = end.ToString("yyyy-MM-dd"),
                totalHours = agenda.TotalHours,
                expectedHours = agenda.ExpectedHours,
                completionPercentage = agenda.CompletionPercentage,
                allProjects = agenda.AllProjects,
                daysNeedingAttention = agenda.DaysNeedingAttention.Select(d => d.ToString("yyyy-MM-dd")),
                days = agenda.Days.Select(d => new
                {
                    date = d.Date.ToString("yyyy-MM-dd"),
                    d.DayOfWeek,
                    d.IsWeekend,
                    status = d.Status.ToString(),
                    totalHours = d.TotalHours,
                    itemCount = d.Items.Count,
                    items = d.Items.Select(i => new
                    {
                        startTime = i.StartTime.ToString("HH:mm"),
                        endTime = i.EndTime.ToString("HH:mm"),
                        hours = i.Hours,
                        i.ClientId,
                        i.ProjectId,
                        i.Description,
                        source = i.Source.ToString(),
                        confidence = i.Confidence.ToString(),
                        i.ExistingTimesheetId,
                        i.CrmBookingId
                    })
                }),
                message = $"Agenda generated with {agenda.TotalHours:F1}/{agenda.ExpectedHours:F0} hours ({agenda.CompletionPercentage}% complete). Use ExportAgendaToMarkdown for review."
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _jsonOptions);
        }
    }

    /// <summary>
    /// Export agenda to Markdown format.
    /// </summary>
    [McpServerTool]
    [Description("Export a generated agenda to Markdown format for easy review and modification.")]
    public async Task<string> ExportAgendaToMarkdown(
        [Description("Employee ID (e.g., 'JEK')")] string employeeId,
        [Description("Start date (yyyy-MM-dd). Defaults to Monday of current week.")] string? startDate = null,
        [Description("End date (yyyy-MM-dd). Defaults to Friday of current week.")] string? endDate = null,
        [Description("Comma-separated local git repo paths to scan (optional)")] string? localGitPaths = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateTime.Today;
            var monday = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            var friday = monday.AddDays(4);
            
            var start = string.IsNullOrEmpty(startDate) 
                ? DateOnly.FromDateTime(monday) 
                : DateOnly.Parse(startDate);
            var end = string.IsNullOrEmpty(endDate) 
                ? DateOnly.FromDateTime(friday) 
                : DateOnly.Parse(endDate);
            
            var options = new AgendaGenerationOptions
            {
                EmployeeId = employeeId,
                StartDate = start,
                EndDate = end,
                LocalGitPaths = string.IsNullOrEmpty(localGitPaths) 
                    ? null 
                    : localGitPaths.Split(',').ToList()
            };
            
            var agenda = await _agendaService.GenerateAgendaAsync(
                options,
                _timeProService,
                !string.IsNullOrEmpty(localGitPaths) ? _gitScanningService : null,
                cancellationToken);
            
            var markdown = _agendaService.ExportToMarkdown(agenda);
            
            return markdown;
        }
        catch (Exception ex)
        {
            return $"# Error\n\nFailed to generate agenda: {ex.Message}";
        }
    }

    /// <summary>
    /// Analyze timesheet patterns.
    /// </summary>
    [McpServerTool]
    [Description("Analyze historical timesheet patterns to understand typical work schedules, common projects, and help with predictions.")]
    public async Task<string> AnalyzeWorkPatterns(
        [Description("Employee ID (e.g., 'JEK')")] string employeeId,
        [Description("Number of days to analyze (default: 14)")] int lookbackDays = 14,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var patterns = await _agendaService.AnalyzePatternsAsync(
                employeeId,
                _timeProService,
                lookbackDays,
                cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                employeeId,
                analysisStartDate = patterns.AnalysisStartDate.ToString("yyyy-MM-dd"),
                analysisEndDate = patterns.AnalysisEndDate.ToString("yyyy-MM-dd"),
                patterns.TotalDays,
                patterns.WorkingDays,
                patterns.AverageHoursPerDay,
                typicalStartTime = patterns.TypicalStartTime.ToString("HH:mm"),
                typicalEndTime = patterns.TypicalEndTime.ToString("HH:mm"),
                patterns.FrequentClients,
                patterns.FrequentProjects,
                patterns.HasMultiProjectDays,
                patterns.ConsistentSchedule,
                dayPatterns = patterns.DayProjectMap
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _jsonOptions);
        }
    }

    /// <summary>
    /// Create timesheets from an agenda.
    /// </summary>
    [McpServerTool]
    [Description("Create confirmation requests for timesheets based on an agenda. Only creates for days without existing timesheets.")]
    public async Task<string> CreateTimesheetsFromAgenda(
        [Description("Employee ID (e.g., 'JEK')")] string employeeId,
        [Description("Start date (yyyy-MM-dd)")] string startDate,
        [Description("End date (yyyy-MM-dd)")] string endDate,
        [Description("Client ID")] string clientId,
        [Description("Project ID")] string projectId,
        [Description("Category ID (default: 'DEV')")] string categoryId = "DEV",
        [Description("Location ID (default: 'Home')")] string locationId = "Home",
        [Description("Description for the timesheets")] string? description = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var start = DateOnly.Parse(startDate);
            var end = DateOnly.Parse(endDate);
            
            // Get existing timesheets to avoid duplicates
            var existing = await _timeProService.GetTimesheetsAsync(
                employeeId, start, end, cancellationToken);
            
            var existingDates = existing
                .Select(t => DateOnly.FromDateTime(t.Date))
                .Distinct()
                .ToHashSet();
            
            var requests = new List<TimesheetRequest>();
            
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                // Skip weekends
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    continue;
                
                // Skip days with existing timesheets
                if (existingDates.Contains(date))
                    continue;
                
                var dateTime = date.ToDateTime(TimeOnly.MinValue);
                
                requests.Add(new TimesheetRequest
                {
                    EmpId = employeeId,
                    ClientId = clientId,
                    ProjectId = projectId,
                    DateCreated = dateTime.ToString("yyyy-MM-ddT00:00:00"),
                    TimeStart = dateTime.AddHours(9).ToString("yyyy-MM-ddTHH:mm:00"),
                    TimeEnd = dateTime.AddHours(17).ToString("yyyy-MM-ddTHH:mm:00"),
                    CategoryId = categoryId,
                    LocationId = locationId,
                    Note = description
                });
            }
            
            if (requests.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "No new timesheets needed - all working days already have entries.",
                    existingDates = existingDates.Select(d => d.ToString("yyyy-MM-dd"))
                }, _jsonOptions);
            }
            
            // Create confirmation for bulk creation
            var preview = string.Join("\n", requests.Select(r => 
                $"- {r.DateCreated[..10]}: {r.ClientId}/{r.ProjectId} (09:00-17:00)"));
            
            var confirmation = await _confirmationService.CreateConfirmationAsync(
                ConfirmationOperationType.BulkCreateTimesheets,
                $"Create {requests.Count} timesheets for {employeeId} ({start:MMM dd} to {end:MMM dd})",
                $"Create {requests.Count} timesheets",
                requests,
                cancellationToken: cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                confirmationId = confirmation.Id,
                message = $"Confirmation created for {requests.Count} timesheets. Use ConfirmOperation with ID '{confirmation.Id}' to execute.",
                timesheetCount = requests.Count,
                preview = preview,
                skippedDates = existingDates.Select(d => d.ToString("yyyy-MM-dd")),
                expiresAt = confirmation.ExpiresAt
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _jsonOptions);
        }
    }

    /// <summary>
    /// Generate leave redirect.
    /// </summary>
    [McpServerTool]
    [Description("Get the URL for submitting leave requests. Redirects to Easy Leave system.")]
    public Task<string> GetLeaveUrl(
        [Description("Tenant ID (e.g., 'ssw')")] string tenant = "ssw")
    {
        var url = $"https://{tenant}.sswtimepro.com/b/leave";
        
        return Task.FromResult(JsonSerializer.Serialize(new
        {
            success = true,
            url,
            message = $"To submit leave requests (annual, sick, or public holiday), visit: {url}"
        }, _jsonOptions));
    }
}
