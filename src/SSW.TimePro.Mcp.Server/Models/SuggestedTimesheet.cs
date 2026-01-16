using System.Text.Json.Serialization;

namespace SSW.TimePro.Mcp.Server.Models;

/// <summary>
/// Represents suggested timesheet data from automatic timesheet generation.
/// </summary>
public class SuggestedTimesheet
{
    [JsonPropertyName("timeID")]
    public int TimeId { get; set; }
    
    [JsonPropertyName("empID")]
    public string EmpId { get; set; } = string.Empty;
    
    [JsonPropertyName("empName")]
    public string EmpName { get; set; } = string.Empty;
    
    [JsonPropertyName("client")]
    public string Client { get; set; } = string.Empty;
    
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;
    
    [JsonPropertyName("project")]
    public string Project { get; set; } = string.Empty;
    
    [JsonPropertyName("projectID")]
    public string ProjectId { get; set; } = string.Empty;
    
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;
    
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;
    
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
    
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
    
    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }
    
    [JsonPropertyName("endTime")]
    public DateTime EndTime { get; set; }
    
    [JsonPropertyName("totalTime")]
    public decimal TotalTime { get; set; }
    
    [JsonPropertyName("isBillable")]
    public bool IsBillable { get; set; }
    
    [JsonPropertyName("inputSource")]
    public int InputSource { get; set; }
    
    [JsonPropertyName("sourceDescription")]
    public string? SourceDescription { get; set; }
}

/// <summary>
/// Response from the suggested timesheets API.
/// </summary>
public class SuggestedTimesheetResponse
{
    [JsonPropertyName("timesheets")]
    public List<SuggestedTimesheet> Timesheets { get; set; } = [];
    
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}
