using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SSW.TimePro.Mcp.Server.Models;
using SSW.TimePro.Mcp.Server.Services;

namespace SSW.TimePro.Mcp.Server.Tools;

/// <summary>
/// MCP Tools for fetching and managing TimePro timesheets.
/// </summary>
[McpServerToolType]
public class TimesheetTools
{
    private readonly ITimeProService _timeProService;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TimesheetTools(ITimeProService timeProService)
    {
        _timeProService = timeProService;
    }

    /// <summary>
    /// Fetch timesheets for X days from today.
    /// </summary>
    /// <param name="employeeId">The employee ID to fetch timesheets for.</param>
    /// <param name="takeDays">Number of days to fetch (default: 7). Each day can have multiple timesheets.</param>
    /// <param name="skipDays">Number of days to skip from today (default: 0). 0 means today is included.</param>
    /// <param name="includeDescription">Include full description/notes in the response (default: false to reduce tokens).</param>
    [McpServerTool]
    [Description("Fetch timesheets for X days from today. Each day can have multiple timesheet entries. Use takeDays=7 for a week, skipDays=0 to include today.")]
    public async Task<string> GetTimesheetsByDays(
        [Description("The employee ID (e.g., 'JEK')")] string employeeId,
        [Description("Number of days to fetch (default: 7)")] int takeDays = 7,
        [Description("Number of days to skip from today (default: 0)")] int skipDays = 0,
        [Description("Include full description/notes (default: false)")] bool includeDescription = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var timesheets = await _timeProService.GetTimesheetsByDaysAsync(
                employeeId, takeDays, skipDays, cancellationToken);
            
            if (includeDescription)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    count = timesheets.Count,
                    timesheets
                }, _jsonOptions);
            }
            
            var summaries = timesheets.Select(TimesheetItemSummary.FromTimesheet).ToList();
            return JsonSerializer.Serialize(new
            {
                success = true,
                count = summaries.Count,
                timesheets = summaries
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
    /// Fetch timesheets for a specific date range.
    /// </summary>
    [McpServerTool]
    [Description("Fetch timesheets for a specific date range. Defaults to current week (Monday to Sunday).")]
    public async Task<string> GetTimesheetsByDateRange(
        [Description("The employee ID (e.g., 'JEK')")] string employeeId,
        [Description("Start date (yyyy-MM-dd). Defaults to current week's Monday.")] string? startDate = null,
        [Description("End date (yyyy-MM-dd). Defaults to current week's Sunday.")] string? endDate = null,
        [Description("Include full description/notes (default: false)")] bool includeDescription = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateTime.Today;
            var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            
            var start = string.IsNullOrEmpty(startDate) 
                ? DateOnly.FromDateTime(today.AddDays(-daysSinceMonday))
                : DateOnly.Parse(startDate);
                
            var end = string.IsNullOrEmpty(endDate)
                ? start.AddDays(6)
                : DateOnly.Parse(endDate);
            
            var timesheets = await _timeProService.GetTimesheetsAsync(
                employeeId, start, end, cancellationToken);
            
            if (includeDescription)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    startDate = start.ToString("yyyy-MM-dd"),
                    endDate = end.ToString("yyyy-MM-dd"),
                    count = timesheets.Count,
                    timesheets
                }, _jsonOptions);
            }
            
            var summaries = timesheets.Select(TimesheetItemSummary.FromTimesheet).ToList();
            return JsonSerializer.Serialize(new
            {
                success = true,
                startDate = start.ToString("yyyy-MM-dd"),
                endDate = end.ToString("yyyy-MM-dd"),
                count = summaries.Count,
                timesheets = summaries
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
    /// Fetch suggested timesheets for a specific date.
    /// </summary>
    [McpServerTool]
    [Description("Fetch suggested/automatic timesheets for a specific date. These are auto-generated suggestions that can be accepted or modified.")]
    public async Task<string> GetSuggestedTimesheets(
        [Description("The employee ID (e.g., 'JEK')")] string employeeId,
        [Description("Date to fetch suggestions for (yyyy-MM-dd). Defaults to today.")] string? date = null,
        [Description("Refresh suggestions before fetching (default: false)")] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDate = string.IsNullOrEmpty(date)
                ? DateOnly.FromDateTime(DateTime.Today)
                : DateOnly.Parse(date);
            
            if (refresh)
            {
                await _timeProService.RefreshSuggestedTimesheetsAsync(
                    employeeId, targetDate, cancellationToken);
            }
            
            var suggestions = await _timeProService.GetSuggestedTimesheetsAsync(
                employeeId, targetDate, cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                date = targetDate.ToString("yyyy-MM-dd"),
                count = suggestions.Count,
                suggestions
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
    /// Get a specific timesheet by ID.
    /// </summary>
    [McpServerTool]
    [Description("Get details of a specific timesheet by its ID.")]
    public async Task<string> GetTimesheetById(
        [Description("The employee ID (e.g., 'JEK')")] string employeeId,
        [Description("The timesheet ID")] int timesheetId,
        [Description("Date of the timesheet (yyyy-MM-dd)")] string date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDate = DateOnly.Parse(date);
            var timesheets = await _timeProService.GetTimesheetsAsync(
                employeeId, targetDate, targetDate, cancellationToken);
            
            var timesheet = timesheets.FirstOrDefault(t => t.TimeId == timesheetId);
            
            if (timesheet == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Timesheet with ID {timesheetId} not found on {date}"
                }, _jsonOptions);
            }
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                timesheet
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
}
