using System.Text.Json.Serialization;

namespace SSW.TimePro.Mcp.Server.Models;

/// <summary>
/// User settings details from TimePro.
/// </summary>
public class EmployeeSettings
{
    [JsonPropertyName("EmpID")]
    public string EmployeeId { get; set; } = string.Empty;

    [JsonPropertyName("StartTime")]
    public string StartTime { get; set; } = "09:00:00"; // Format: HH:mm:ss

    [JsonPropertyName("EndTime")]
    public string EndTime { get; set; } = "18:00:00";

    [JsonPropertyName("LunchBreakStart")]
    public string LunchBreakStart { get; set; } = "13:00:00";

    [JsonPropertyName("LunchBreakEnd")]
    public string LunchBreakEnd { get; set; } = "14:00:00";

    [JsonPropertyName("TimeLessMinutes")]
    public int TimeLessMinutes { get; set; }

    [JsonPropertyName("TimezoneId")]
    public string TimezoneId { get; set; } = string.Empty;
}
