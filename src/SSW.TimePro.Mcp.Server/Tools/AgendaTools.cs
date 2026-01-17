using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using SSW.TimePro.Mcp.Server.Configuration;
using SSW.TimePro.Mcp.Server.Models;
using SSW.TimePro.Mcp.Server.Services;

namespace SSW.TimePro.Mcp.Server.Tools;

/// <summary>
/// MCP Tools for timesheet recommendations.
/// </summary>
[McpServerToolType]
public class RecommendTools
{
    private readonly ITimeProService _timeProService;
    private readonly TimeProSettings _settings;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RecommendTools(
        ITimeProService timeProService,
        IOptions<TimeProSettings> settings)
    {
        _timeProService = timeProService;
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
    /// Get timesheet recommendation for a single day.
    /// </summary>
    [McpServerTool]
    [Description("Get timesheet recommendation for a single day. Returns what's logged, what's suggested, and recent projects to choose from.")]
    public async Task<string> RecommendDay(
        [Description("Date (yyyy-MM-dd). Defaults to today.")] string? date = null,
        [Description("Employee ID (e.g., 'JEK'). Optional if DefaultEmployeeId is configured.")] string? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var empId = GetEmployeeId(employeeId);
            var targetDate = string.IsNullOrEmpty(date)
                ? DateOnly.FromDateTime(DateTime.Today)
                : DateOnly.Parse(date);

            // Skip weekends
            if (targetDate.DayOfWeek == DayOfWeek.Saturday || targetDate.DayOfWeek == DayOfWeek.Sunday)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    date = targetDate.ToString("yyyy-MM-dd"),
                    day = targetDate.DayOfWeek.ToString(),
                    isWeekend = true,
                    message = "Weekend - no timesheet needed."
                }, _jsonOptions);
            }

            // Get existing timesheets for this day
            var existingTimesheets = await _timeProService.GetTimesheetsAsync(
                empId, targetDate, targetDate, cancellationToken);

            var existingHours = existingTimesheets
                .Where(t => !t.IsSuggested)
                .Sum(t => t.TotalTime);

            // Get suggested timesheets (these are templates from TimePro)
            var suggestions = existingTimesheets
                .Where(t => t.IsSuggested)
                .ToList();

            // Get CRM bookings if available
            List<AppointmentItem>? crmBookings = null;
            try
            {
                crmBookings = await _timeProService.GetAppointmentsAsync(
                    empId, targetDate, targetDate, cancellationToken);
            }
            catch
            {
                // CRM not available, ignore
            }

            // Get recent projects
            List<RecentProjectDto>? recentProjects = null;
            try
            {
                recentProjects = await _timeProService.GetRecentProjectsAsync(empId, cancellationToken);
            }
            catch
            {
                // Recent projects not available, continue without
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                employeeId = empId,
                date = targetDate.ToString("yyyy-MM-dd"),
                day = targetDate.DayOfWeek.ToString(),
                existingHours,
                hoursNeeded = Math.Max(0, 8 - existingHours),
                existing = existingTimesheets
                    .Where(t => !t.IsSuggested)
                    .Select(t => new
                    {
                        timesheetId = t.TimeId,
                        clientId = t.ClientId,
                        clientName = t.Client,
                        projectId = t.ProjectId,
                        projectName = t.Project,
                        hours = t.TotalTime,
                        notes = t.Notes
                    }),
                suggested = suggestions.Select(s => new
                {
                    suggestedTimesheetId = s.TimeId,
                    clientId = s.ClientId,
                    clientName = s.Client,
                    projectId = s.ProjectId,
                    projectName = s.Project,
                    hours = s.TotalTime,
                    isBillable = s.IsBillable,
                    notes = s.Notes
                }),
                crmBookings = crmBookings?.Select(b => new
                {
                    id = b.Id,
                    title = b.Title,
                    clientId = b.ClientId,
                    projectId = b.ProjectId,
                    start = b.Start,
                    end = b.End
                }),
                recentProjects = recentProjects?.Take(5).Select(p => new
                {
                    clientId = p.ClientId,
                    clientName = p.ClientName,
                    projectId = p.ProjectId,
                    projectName = p.ProjectName,
                    categoryId = p.CategoryId,
                    billableId = p.BillableId,
                    isBillable = p.IsBillable,
                    totalHours = p.TotalHours,
                    lastUsed = p.LastUsed.ToString("yyyy-MM-dd")
                })
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
    /// Get timesheet recommendations for a week.
    /// </summary>
    [McpServerTool]
    [Description("Get timesheet recommendations for an entire week. Shows status for each day and recent projects to use.")]
    public async Task<string> RecommendWeek(
        [Description("Start date (yyyy-MM-dd). Defaults to Monday of current week.")] string? startDate = null,
        [Description("Employee ID (e.g., 'JEK'). Optional if DefaultEmployeeId is configured.")] string? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var empId = GetEmployeeId(employeeId);
            var today = DateTime.Today;
            var monday = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);

            var start = string.IsNullOrEmpty(startDate)
                ? DateOnly.FromDateTime(monday)
                : DateOnly.Parse(startDate);
            var end = start.AddDays(4); // Monday to Friday

            // Fetch recent projects once
            List<RecentProjectDto>? recentProjects = null;
            try
            {
                recentProjects = await _timeProService.GetRecentProjectsAsync(empId, cancellationToken);
            }
            catch
            {
                // Recent projects not available, continue without
            }

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

                // Get suggested timesheets
                var suggestions = existingTimesheets
                    .Where(t => t.IsSuggested)
                    .ToList();

                // Get CRM bookings if available
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

                days.Add(new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    day = date.DayOfWeek.ToString()[..3],
                    existingHours,
                    hoursNeeded = Math.Max(0, 8 - existingHours),
                    existing = existingTimesheets
                        .Where(t => !t.IsSuggested)
                        .Select(t => new
                        {
                            timesheetId = t.TimeId,
                            clientId = t.ClientId,
                            projectId = t.ProjectId,
                            hours = t.TotalTime
                        }),
                    suggested = suggestions.Select(s => new
                    {
                        suggestedTimesheetId = s.TimeId,
                        clientId = s.ClientId,
                        clientName = s.Client,
                        projectId = s.ProjectId,
                        projectName = s.Project,
                        hours = s.TotalTime,
                        isBillable = s.IsBillable
                    }),
                    crmBookings = crmBookings?.Select(b => new
                    {
                        clientId = b.ClientId,
                        title = b.Title
                    })
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                employeeId = empId,
                weekOf = start.ToString("yyyy-MM-dd"),
                summary = new
                {
                    existingHours = totalExistingHours,
                    expectedHours = totalExpectedHours,
                    hoursNeeded = Math.Max(0, totalExpectedHours - totalExistingHours),
                    complete = totalExistingHours >= totalExpectedHours
                },
                days,
                recentProjects = recentProjects?.Take(10).Select(p => new
                {
                    clientId = p.ClientId,
                    clientName = p.ClientName,
                    projectId = p.ProjectId,
                    projectName = p.ProjectName,
                    categoryId = p.CategoryId,
                    billableId = p.BillableId,
                    isBillable = p.IsBillable,
                    totalHours = p.TotalHours
                })
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
