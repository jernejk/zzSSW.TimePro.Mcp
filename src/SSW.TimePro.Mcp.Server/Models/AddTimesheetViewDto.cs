using System.Text.Json.Serialization;

namespace SSW.TimePro.Mcp.Server.Models;

/// <summary>
/// Add timesheet view data from TimePro.
/// </summary>
public class AddTimesheetViewDto
{
    [JsonPropertyName("empID")]
    public string EmpId { get; set; } = string.Empty;
    
    [JsonPropertyName("empName")]
    public string EmpName { get; set; } = string.Empty;
    
    [JsonPropertyName("clientID")]
    public string ClientId { get; set; } = string.Empty;
    
    [JsonPropertyName("clientName")]
    public string? ClientName { get; set; }
    
    [JsonPropertyName("projectID")]
    public string ProjectId { get; set; } = string.Empty;
    
    [JsonPropertyName("projectType")]
    public string? ProjectType { get; set; }
    
    [JsonPropertyName("iterationID")]
    public int? IterationId { get; set; }
    
    [JsonPropertyName("categoryID")]
    public string CategoryId { get; set; } = string.Empty;
    
    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }
    
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;
    
    [JsonPropertyName("locationID")]
    public string LocationId { get; set; } = string.Empty;
    
    [JsonPropertyName("isNonWorkingCategory")]
    public bool IsNonWorkingCategory { get; set; }
    
    [JsonPropertyName("dateCreated")]
    public string? DateCreated { get; set; }
    
    [JsonPropertyName("startTime")]
    public string? StartTime { get; set; }
    
    [JsonPropertyName("endTime")]
    public string? EndTime { get; set; }
    
    [JsonPropertyName("timeLess")]
    public decimal TimeLess { get; set; }
    
    [JsonPropertyName("sellPrice")]
    public decimal SellPrice { get; set; }
    
    [JsonPropertyName("salesTaxPct")]
    public decimal SalesTaxPct { get; set; }
    
    [JsonPropertyName("salesTaxAmt")]
    public decimal SalesTaxAmt { get; set; }
    
    [JsonPropertyName("sellTotal")]
    public decimal SellTotal { get; set; }
    
    [JsonPropertyName("hasAzureDevOpsSettings")]
    public bool HasAzureDevOpsSettings { get; set; }
    
    [JsonPropertyName("billableID")]
    public string BillableId { get; set; } = string.Empty;
    
    [JsonPropertyName("prepaidRate")]
    public decimal? PrepaidRate { get; set; }
    
    [JsonPropertyName("regularRate")]
    public decimal? RegularRate { get; set; }
}

/// <summary>
/// Category for timesheets.
/// </summary>
public class TimesheetCategory
{
    [JsonPropertyName("categoryID")]
    public string CategoryId { get; set; } = string.Empty;
    
    [JsonPropertyName("categoryName")]
    public string CategoryName { get; set; } = string.Empty;
    
    [JsonPropertyName("isNonWorking")]
    public bool IsNonWorking { get; set; }
}

/// <summary>
/// Location for timesheets.
/// </summary>
public class TimesheetLocation
{
    [JsonPropertyName("locationID")]
    public string LocationId { get; set; } = string.Empty;
    
    [JsonPropertyName("locationName")]
    public string LocationName { get; set; } = string.Empty;
}

/// <summary>
/// Billable type for timesheets.
/// </summary>
public class TimesheetBillableType
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Client search result.
/// </summary>
public class ClientSearchResult
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Project for selection.
/// </summary>
public class ProjectForSelect
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("displayText")]
    public string DisplayText { get; set; } = string.Empty;

    [JsonPropertyName("useIteration")]
    public bool UseIteration { get; set; }

    [JsonPropertyName("isGeneral")]
    public bool IsGeneral { get; set; }

    [JsonPropertyName("isLeave")]
    public bool IsLeave { get; set; }
}

/// <summary>
/// Recent project from the GetRecentProjects API endpoint.
/// </summary>
public class RecentProjectDto
{
    [JsonPropertyName("clientID")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientName")]
    public string? ClientName { get; set; }

    [JsonPropertyName("projectID")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }

    [JsonPropertyName("categoryID")]
    public string CategoryId { get; set; } = string.Empty;

    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("billableID")]
    public string? BillableId { get; set; }

    [JsonPropertyName("isBillable")]
    public bool IsBillable { get; set; }

    [JsonPropertyName("totalHours")]
    public decimal TotalHours { get; set; }

    [JsonPropertyName("timesheetCount")]
    public int TimesheetCount { get; set; }

    [JsonPropertyName("lastUsed")]
    public DateTime LastUsed { get; set; }
}
