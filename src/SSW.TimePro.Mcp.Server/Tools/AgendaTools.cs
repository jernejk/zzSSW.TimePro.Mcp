using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using SSW.TimePro.Mcp.Server.Configuration;
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
    private readonly TimeProSettings _settings;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AgendaTools(
        IAgendaService agendaService,
        ITimeProService timeProService,
        IGitScanningService gitScanningService,
        IConfirmationService confirmationService,
        IOptions<TimeProSettings> settings)
    {
        _agendaService = agendaService;
        _timeProService = timeProService;
        _gitScanningService = gitScanningService;
        _confirmationService = confirmationService;
        _settings = settings.Value;
    }

    private string GetEmployeeId(string? providedId)
    {
        if (!string.IsNullOrEmpty(providedId))
            return providedId;

        if (!string.IsNullOrEmpty(_settings.DefaultEmployeeId))
            return _settings.DefaultEmployeeId;

        throw new InvalidOperationException(
            "Employee ID is required. Either provide it as a parameter or set TimePro__DefaultEmployeeId in configuration.");
    }

    /// <summary>
    /// Suggest timesheets for a date range - the main entry point for timesheet automation.
    /// </summary>
    [McpServerTool]
    [Description("Get timesheet suggestions for a date range. Shows what's already logged vs what needs to be created. This is the recommended starting point for timesheet automation.")]
    public async Task<string> SuggestTimesheets(
        [Description("Employee ID (e.g., 'JEK'). Optional if DefaultEmployeeId is configured.")] string? employeeId = null,
        [Description("Start date (yyyy-MM-dd). Defaults to Monday of current week.")] string? startDate = null,
        [Description("End date (yyyy-MM-dd). Defaults to Friday of current week.")] string? endDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var empId = GetEmployeeId(employeeId);
            var today = DateTime.Today;
            var monday = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            var friday = monday.AddDays(4);

            var start = string.IsNullOrEmpty(startDate)
                ? DateOnly.FromDateTime(monday)
                : DateOnly.Parse(startDate);
            var end = string.IsNullOrEmpty(endDate)
                ? DateOnly.FromDateTime(friday)
                : DateOnly.Parse(endDate);

            var days = new List<object>();
            decimal totalExistingHours = 0;
            decimal totalExpectedHours = 0;

            // Process each day
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                totalExpectedHours += 8;

                // Get existing timesheets for this day
                var existingTimesheets = await _timeProService.GetTimesheetsAsync(
                    empId, date, date, cancellationToken);

                var existingHours = existingTimesheets
                    .Where(t => !t.IsSuggested)
                    .Sum(t => t.TotalTime);

                totalExistingHours += existingHours;

                // If day is complete (8+ hours), just report it
                if (existingHours >= 8)
                {
                    days.Add(new
                    {
                        date = date.ToString("yyyy-MM-dd"),
                        day = date.DayOfWeek.ToString()[..3],
                        status = "complete",
                        existingHours,
                        action = "none",
                        existing = existingTimesheets
                            .Where(t => !t.IsSuggested)
                            .Select(t => new
                            {
                                timesheetId = t.TimeId,
                                client = t.ClientId,
                                project = t.ProjectId,
                                hours = t.TotalTime
                            })
                    });
                    continue;
                }

                // Day needs work - get suggestions
                var suggestions = existingTimesheets
                    .Where(t => t.IsSuggested)
                    .ToList();

                // Also get CRM bookings if available
                List<AppointmentItem>? crmBookings = null;
                try
                {
                    crmBookings = await _timeProService.GetAppointmentsAsync(
                        empId, date, date, cancellationToken);
                }
                catch
                {
                    // CRM not available, ignore
                }

                // Build recommendations
                var recommendations = new List<object>();

                // Priority 1: Suggested timesheets from TimePro
                foreach (var suggestion in suggestions)
                {
                    recommendations.Add(new
                    {
                        priority = 1,
                        source = "suggested",
                        action = "AcceptSuggestedTimesheet",
                        suggestedTimesheetId = suggestion.TimeId,
                        client = suggestion.ClientId,
                        clientName = suggestion.Client,
                        project = suggestion.ProjectId,
                        projectName = suggestion.Project,
                        hours = suggestion.TotalTime,
                        isBillable = suggestion.IsBillable,
                        notes = suggestion.Notes
                    });
                }

                // Priority 2: CRM bookings
                if (crmBookings != null)
                {
                    foreach (var booking in crmBookings)
                    {
                        // Skip if already covered by suggestion
                        if (suggestions.Any(s => s.ClientId == booking.ClientId))
                            continue;

                        recommendations.Add(new
                        {
                            priority = 2,
                            source = "crm",
                            action = "CreateTimesheet",
                            client = booking.ClientId,
                            title = booking.Title,
                            start = booking.Start,
                            end = booking.End
                        });
                    }
                }

                days.Add(new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    day = date.DayOfWeek.ToString()[..3],
                    status = existingHours > 0 ? "partial" : "empty",
                    existingHours,
                    hoursNeeded = 8 - existingHours,
                    action = recommendations.Count > 0 ? "choose" : "manual",
                    recommendations = recommendations.Count > 0 ? recommendations : null,
                    existing = existingTimesheets
                        .Where(t => !t.IsSuggested)
                        .Select(t => new
                        {
                            timesheetId = t.TimeId,
                            client = t.ClientId,
                            project = t.ProjectId,
                            hours = t.TotalTime
                        })
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                employeeId = empId,
                summary = new
                {
                    existingHours = totalExistingHours,
                    expectedHours = totalExpectedHours,
                    hoursNeeded = Math.Max(0, totalExpectedHours - totalExistingHours),
                    complete = totalExistingHours >= totalExpectedHours
                },
                days,
                help = totalExistingHours >= totalExpectedHours
                    ? "All timesheets complete for this period."
                    : "Use AcceptSuggestedTimesheet(suggestedTimesheetId, notes?, location?) for suggestions, or CreateTimesheet for manual entry."
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
    /// Analyze timesheet patterns.
    /// </summary>
    [McpServerTool]
    [Description("Analyze historical timesheet patterns to understand typical work schedules, common projects, and help with predictions.")]
    public async Task<string> AnalyzeWorkPatterns(
        [Description("Employee ID (e.g., 'JEK'). Optional if DefaultEmployeeId is configured.")] string? employeeId = null,
        [Description("Number of days to analyze (default: 14)")] int lookbackDays = 14,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var empId = GetEmployeeId(employeeId);
            var patterns = await _agendaService.AnalyzePatternsAsync(
                empId,
                _timeProService,
                lookbackDays,
                cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                employeeId = empId,
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
        [Description("Start date (yyyy-MM-dd)")] string startDate,
        [Description("End date (yyyy-MM-dd)")] string endDate,
        [Description("Client ID")] string clientId,
        [Description("Project ID")] string projectId,
        [Description("Employee ID (e.g., 'JEK'). Optional if DefaultEmployeeId is configured.")] string? employeeId = null,
        [Description("Category ID (default: 'DEV')")] string categoryId = "DEV",
        [Description("Location ID (default: 'Home')")] string locationId = "Home",
        [Description("Description for the timesheets")] string? description = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var empId = GetEmployeeId(employeeId);
            var start = DateOnly.Parse(startDate);
            var end = DateOnly.Parse(endDate);

            // Get existing timesheets to avoid duplicates
            var existing = await _timeProService.GetTimesheetsAsync(
                empId, start, end, cancellationToken);
            
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
                    EmpId = empId,
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
                $"Create {requests.Count} timesheets for {empId} ({start:MMM dd} to {end:MMM dd})",
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
