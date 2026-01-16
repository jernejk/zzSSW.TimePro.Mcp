using System.Text.Json.Serialization;

namespace SSW.TimePro.Mcp.Server.Models;

/// <summary>
/// Request model for creating/updating a timesheet.
/// </summary>
public class TimesheetRequest
{
    [JsonPropertyName("empID")]
    public string EmpId { get; set; } = string.Empty;
    
    [JsonPropertyName("clientID")]
    public string ClientId { get; set; } = string.Empty;
    
    [JsonPropertyName("projectID")]
    public string ProjectId { get; set; } = string.Empty;
    
    [JsonPropertyName("iterationID")]
    public int? IterationId { get; set; }
    
    [JsonPropertyName("categoryID")]
    public string CategoryId { get; set; } = string.Empty;
    
    [JsonPropertyName("locationID")]
    public string LocationId { get; set; } = string.Empty;
    
    [JsonPropertyName("dateCreated")]
    public string DateCreated { get; set; } = string.Empty;
    
    [JsonPropertyName("timeStart")]
    public string TimeStart { get; set; } = string.Empty;
    
    [JsonPropertyName("timeEnd")]
    public string TimeEnd { get; set; } = string.Empty;
    
    [JsonPropertyName("timeLess")]
    public decimal TimeLess { get; set; }
    
    [JsonPropertyName("note")]
    public string? Note { get; set; }
    
    [JsonPropertyName("billableID")]
    public string BillableId { get; set; } = "BILLABLE";
    
    [JsonPropertyName("sellPrice")]
    public decimal? SellPrice { get; set; }
    
    [JsonPropertyName("isOverridden")]
    public bool IsOverridden { get; set; }
    
    [JsonPropertyName("isOverwriteRate")]
    public bool IsOverwriteRate { get; set; }
}

/// <summary>
/// Response from create/update timesheet operation.
/// </summary>
public class TimesheetResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    [JsonPropertyName("timesheetId")]
    public int? TimesheetId { get; set; }
}

/// <summary>
/// DTO for editing a timesheet (extends request with ID).
/// </summary>
public class EditTimesheetRequest : TimesheetRequest
{
    [JsonPropertyName("timeID")]
    public int TimeId { get; set; }
}
