using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SSW.TimePro.Mcp.Server.Models;
using SSW.TimePro.Mcp.Server.Services;

namespace SSW.TimePro.Mcp.Server.Tools;

/// <summary>
/// MCP Tools for fetching TimePro timesheets.
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
    /// Unified timesheet fetching - by ID, single date, or date range.
    /// </summary>
    [McpServerTool]
    [Description("Fetch timesheets flexibly: by ID (provide timesheetId+date), by single date, or by date range. Defaults to current week.")]
    public async Task<string> GetTimesheets(
        [Description("The employee ID (e.g., 'JEK')")] string employeeId,
        [Description("Specific timesheet ID to fetch (requires date)")] int? timesheetId = null,
        [Description("Start date or single date (yyyy-MM-dd). Defaults to current week's Monday.")] string? date = null,
        [Description("End date for range queries (yyyy-MM-dd). If omitted with date, fetches single day.")] string? endDate = null,
        [Description("Include full description/notes (default: false)")] bool includeDescription = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateTime.Today;
            var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;

            // Determine date range
            DateOnly start, end;
            if (!string.IsNullOrEmpty(date))
            {
                start = DateOnly.Parse(date);
                end = string.IsNullOrEmpty(endDate) ? start : DateOnly.Parse(endDate);
            }
            else
            {
                // Default to current week
                start = DateOnly.FromDateTime(today.AddDays(-daysSinceMonday));
                end = start.AddDays(6);
            }

            var timesheets = await _timeProService.GetTimesheetsAsync(
                employeeId, start, end, cancellationToken);

            // If specific ID requested, filter to that one
            if (timesheetId.HasValue)
            {
                var timesheet = timesheets.FirstOrDefault(t => t.TimeId == timesheetId.Value);

                if (timesheet == null)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Timesheet with ID {timesheetId} not found between {start:yyyy-MM-dd} and {end:yyyy-MM-dd}"
                    }, _jsonOptions);
                }

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    timesheet
                }, _jsonOptions);
            }

            // Return list of timesheets
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
}
