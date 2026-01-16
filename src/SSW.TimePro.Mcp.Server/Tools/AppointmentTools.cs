using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SSW.TimePro.Mcp.Server.Services;

namespace SSW.TimePro.Mcp.Server.Tools;

/// <summary>
/// MCP Tools for CRM appointments/bookings.
/// </summary>
[McpServerToolType]
public class AppointmentTools
{
    private readonly ITimeProService _timeProService;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppointmentTools(ITimeProService timeProService)
    {
        _timeProService = timeProService;
    }

    /// <summary>
    /// Fetch CRM bookings/appointments for a date range.
    /// </summary>
    [McpServerTool]
    [Description("Fetch CRM bookings/appointments for a date range. Works only on SSW production. Defaults to current week.")]
    public async Task<string> GetCrmBookings(
        [Description("The employee ID (e.g., 'JEK')")] string employeeId,
        [Description("Start date (yyyy-MM-dd). Defaults to current week's Monday.")] string? startDate = null,
        [Description("End date (yyyy-MM-dd). Defaults to current week's Sunday.")] string? endDate = null,
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
            
            var appointments = await _timeProService.GetAppointmentsAsync(
                employeeId, start, end, cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                startDate = start.ToString("yyyy-MM-dd"),
                endDate = end.ToString("yyyy-MM-dd"),
                count = appointments.Count,
                note = "CRM bookings work only on SSW production environment.",
                appointments
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                note = "CRM bookings may not be available in non-production environments."
            }, _jsonOptions);
        }
    }
}
