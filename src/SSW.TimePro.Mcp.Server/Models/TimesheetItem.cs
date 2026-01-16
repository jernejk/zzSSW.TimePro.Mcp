using System.Text.Json.Serialization;

namespace SSW.TimePro.Mcp.Server.Models;

/// <summary>
/// Represents a timesheet entry from TimePro.
/// </summary>
public class TimesheetItem
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
    
    [JsonPropertyName("iteration")]
    public string? Iteration { get; set; }
    
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;
    
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;
    
    [JsonPropertyName("locationID")]
    public string LocationId { get; set; } = string.Empty;
    
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
    
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
    
    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }
    
    [JsonPropertyName("endTime")]
    public DateTime EndTime { get; set; }
    
    [JsonPropertyName("billableID")]
    public string BillableId { get; set; } = string.Empty;
    
    [JsonPropertyName("isBillable")]
    public bool IsBillable { get; set; }
    
    [JsonPropertyName("less")]
    public decimal Less { get; set; }
    
    [JsonPropertyName("totalTime")]
    public decimal TotalTime { get; set; }
    
    [JsonPropertyName("hasNotes")]
    public bool HasNotes { get; set; }
    
    [JsonPropertyName("isSuggested")]
    public bool IsSuggested { get; set; }
    
    [JsonPropertyName("isLeave")]
    public bool IsLeave { get; set; }
    
    [JsonPropertyName("inputSource")]
    public int InputSource { get; set; }
}

/// <summary>
/// Simplified timesheet for reduced token usage.
/// </summary>
public class TimesheetItemSummary
{
    public int TimeId { get; set; }
    public string Client { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal TotalTime { get; set; }
    public bool HasDescription { get; set; }
    public bool IsSuggested { get; set; }
    public bool IsBillable { get; set; }
    
    public static TimesheetItemSummary FromTimesheet(TimesheetItem item) => new()
    {
        TimeId = item.TimeId,
        Client = item.Client,
        Project = item.Project,
        Date = item.Date,
        StartTime = item.StartTime,
        EndTime = item.EndTime,
        TotalTime = item.TotalTime,
        HasDescription = !string.IsNullOrWhiteSpace(item.Notes),
        IsSuggested = item.IsSuggested,
        IsBillable = item.IsBillable
    };
}
