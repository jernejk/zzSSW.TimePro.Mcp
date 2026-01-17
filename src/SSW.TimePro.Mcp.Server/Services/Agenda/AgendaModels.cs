using System.Text.Json.Serialization;
using SSW.TimePro.Mcp.Server.Models;
using SSW.TimePro.Mcp.Server.Services.Git;

namespace SSW.TimePro.Mcp.Server.Services.Agenda;

/// <summary>
/// A suggested agenda entry for a day.
/// </summary>
public class AgendaItem
{
    [JsonPropertyName("date")]
    public DateOnly Date { get; set; }
    
    [JsonPropertyName("startTime")]
    public TimeOnly StartTime { get; set; }
    
    [JsonPropertyName("endTime")]
    public TimeOnly EndTime { get; set; }
    
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;
    
    [JsonPropertyName("clientName")]
    public string? ClientName { get; set; }
    
    [JsonPropertyName("projectId")]
    public string? ProjectId { get; set; }
    
    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }
    
    [JsonPropertyName("categoryId")]
    public string CategoryId { get; set; } = "DEV";
    
    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }
    
    [JsonPropertyName("locationId")]
    public string LocationId { get; set; } = "Home";
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("source")]
    public AgendaItemSource Source { get; set; }
    
    [JsonPropertyName("confidence")]
    public AgendaConfidence Confidence { get; set; } = AgendaConfidence.Medium;
    
    [JsonPropertyName("hours")]
    public decimal Hours => (decimal)(EndTime - StartTime).TotalHours - (TimeLessMinutes / 60m);
    
    [JsonPropertyName("commits")]
    public List<GitCommit>? RelatedCommits { get; set; }
    
    [JsonPropertyName("crmBookingId")]
    public string? CrmBookingId { get; set; }
    
    [JsonPropertyName("existingTimesheetId")]
    public int? ExistingTimesheetId { get; set; }
    
    [JsonPropertyName("iterationId")]
    public int? IterationId { get; set; }
    
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    /// <summary>
    /// Lunch/break deduction in minutes (e.g., 60 for 1 hour lunch).
    /// Applied to full-day entries from CRM bookings.
    /// </summary>
    [JsonPropertyName("timeLessMinutes")]
    public int TimeLessMinutes { get; set; }

    /// <summary>
    /// Billable ID (e.g., 'BILLABLE', 'B', 'BPP', 'W' for internal).
    /// Used for prioritization: billable client work > internal work > leave.
    /// </summary>
    [JsonPropertyName("billableId")]
    public string? BillableId { get; set; }

    /// <summary>
    /// True if this is billable client work (B, BPP, BILLABLE types).
    /// </summary>
    [JsonPropertyName("isBillable")]
    public bool IsBillable { get; set; }

    /// <summary>
    /// Suggested timesheet ID if this came from a suggested timesheet.
    /// </summary>
    [JsonPropertyName("suggestedTimesheetId")]
    public int? SuggestedTimesheetId { get; set; }

    /// <summary>
    /// Alternative options for this time slot (e.g., internal work vs client work).
    /// The main item is the recommended choice; alternatives are other valid options.
    /// </summary>
    [JsonPropertyName("alternatives")]
    public List<AgendaItem>? Alternatives { get; set; }
}

/// <summary>
/// Source of the agenda suggestion.
/// </summary>
public enum AgendaItemSource
{
    CrmBooking,
    SuggestedTimesheet,
    GitActivity,
    Pattern,
    Manual,
    ExistingTimesheet
}

/// <summary>
/// Confidence level for the suggestion.
/// </summary>
public enum AgendaConfidence
{
    Low,      // Just a guess
    Medium,   // Based on some evidence
    High,     // Strong evidence (CRM booking, suggested timesheet)
    Confirmed // Already exists or manual entry
}

/// <summary>
/// A day's complete agenda.
/// </summary>
public class DayAgenda
{
    [JsonPropertyName("date")]
    public DateOnly Date { get; set; }
    
    [JsonPropertyName("dayOfWeek")]
    public string DayOfWeek => Date.DayOfWeek.ToString();
    
    [JsonPropertyName("isWeekend")]
    public bool IsWeekend => Date.DayOfWeek == System.DayOfWeek.Saturday || 
                             Date.DayOfWeek == System.DayOfWeek.Sunday;
    
    [JsonPropertyName("items")]
    public List<AgendaItem> Items { get; set; } = [];
    
    [JsonPropertyName("totalHours")]
    public decimal TotalHours => Items.Sum(i => i.Hours);
    
    [JsonPropertyName("hasGaps")]
    public bool HasGaps => TotalHours < 8 && !IsWeekend && Items.Count > 0;
    
    [JsonPropertyName("hasExistingTimesheets")]
    public bool HasExistingTimesheets => Items.Any(i => i.ExistingTimesheetId.HasValue);
    
    [JsonPropertyName("uniqueProjects")]
    public List<string> UniqueProjects => Items
        .Where(i => !string.IsNullOrEmpty(i.ProjectId))
        .Select(i => i.ProjectId!)
        .Distinct()
        .ToList();
    
    [JsonPropertyName("status")]
    public DayAgendaStatus Status { get; set; } = DayAgendaStatus.Empty;

    /// <summary>
    /// The primary/selected item for this day (billable client work prioritized).
    /// </summary>
    [JsonPropertyName("selectedItem")]
    public AgendaItem? SelectedItem => Items
        .Where(i => i.Source != AgendaItemSource.ExistingTimesheet || i.ExistingTimesheetId.HasValue)
        .OrderByDescending(i => i.IsBillable)
        .ThenByDescending(i => i.Confidence)
        .ThenBy(i => i.Source == AgendaItemSource.SuggestedTimesheet ? 0 : 1)
        .FirstOrDefault();

    /// <summary>
    /// Alternative options that were not selected as primary.
    /// </summary>
    [JsonPropertyName("alternativeItems")]
    public List<AgendaItem> AlternativeItems => Items
        .Where(i => i != SelectedItem && i.Source != AgendaItemSource.ExistingTimesheet)
        .ToList();
}

/// <summary>
/// Status of a day's agenda.
/// </summary>
public enum DayAgendaStatus
{
    Empty,           // No data
    Partial,         // Some timesheets but not 8 hours
    Complete,        // 8 hours logged
    Weekend,         // Weekend day
    Leave,           // Leave detected
    NeedsAttention   // Multiple projects, gaps, etc.
}

/// <summary>
/// Weekly agenda for timesheet planning.
/// </summary>
public class WeeklyAgenda
{
    [JsonPropertyName("weekStartDate")]
    public DateOnly WeekStartDate { get; set; }
    
    [JsonPropertyName("weekEndDate")]
    public DateOnly WeekEndDate { get; set; }
    
    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; } = string.Empty;
    
    [JsonPropertyName("days")]
    public List<DayAgenda> Days { get; set; } = [];
    
    [JsonPropertyName("totalHours")]
    public decimal TotalHours => Days.Sum(d => d.TotalHours);
    
    /// <summary>
    /// Work hours per day (set from employee settings, default 8).
    /// </summary>
    [JsonPropertyName("workHoursPerDay")]
    public decimal WorkHoursPerDay { get; set; } = 8;

    [JsonPropertyName("expectedHours")]
    public decimal ExpectedHours => Days.Count(d => !d.IsWeekend) * WorkHoursPerDay;
    
    [JsonPropertyName("completionPercentage")]
    public decimal CompletionPercentage => ExpectedHours > 0 
        ? Math.Round(TotalHours / ExpectedHours * 100, 1) 
        : 0;
    
    [JsonPropertyName("allProjects")]
    public List<string> AllProjects => Days
        .SelectMany(d => d.UniqueProjects)
        .Distinct()
        .ToList();
    
    [JsonPropertyName("daysNeedingAttention")]
    public List<DateOnly> DaysNeedingAttention => Days
        .Where(d => d.Status == DayAgendaStatus.NeedsAttention || 
                    d.Status == DayAgendaStatus.Empty ||
                    d.Status == DayAgendaStatus.Partial)
        .Select(d => d.Date)
        .ToList();
    
    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("agendaId")]
    public string AgendaId { get; set; } = string.Empty;

    /// <summary>
    /// Recent projects from the last 14 days, sorted by usage frequency.
    /// Use these for fallback when no suggested timesheets exist.
    /// </summary>
    [JsonPropertyName("recentProjects")]
    public List<RecentProject> RecentProjects { get; set; } = [];
}

/// <summary>
/// Options for agenda generation.
/// </summary>
public class AgendaGenerationOptions
{
    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; } = string.Empty;
    
    [JsonPropertyName("startDate")]
    public DateOnly StartDate { get; set; }
    
    [JsonPropertyName("endDate")]
    public DateOnly EndDate { get; set; }
    
    [JsonPropertyName("includeExistingTimesheets")]
    public bool IncludeExistingTimesheets { get; set; } = true;
    
    [JsonPropertyName("includeCrmBookings")]
    public bool IncludeCrmBookings { get; set; } = true;
    
    [JsonPropertyName("includeSuggestedTimesheets")]
    public bool IncludeSuggestedTimesheets { get; set; } = true;
    
    [JsonPropertyName("localGitPaths")]
    public List<string>? LocalGitPaths { get; set; }
    
    [JsonPropertyName("gitHubUsername")]
    public string? GitHubUsername { get; set; }
    
    [JsonPropertyName("defaultClientId")]
    public string? DefaultClientId { get; set; }
    
    [JsonPropertyName("defaultProjectId")]
    public string? DefaultProjectId { get; set; }
    
    [JsonPropertyName("defaultCategoryId")]
    public string DefaultCategoryId { get; set; } = "DEV";
    
    [JsonPropertyName("defaultLocationId")] 
    public string DefaultLocationId { get; set; } = "Home";
    
    [JsonPropertyName("defaultStartTime")]
    public TimeOnly DefaultStartTime { get; set; } = new(9, 0);

    [JsonPropertyName("defaultEndTime")]
    public TimeOnly DefaultEndTime { get; set; } = new(18, 0);

    /// <summary>
    /// Lunch break duration in minutes (typically 60).
    /// Subtracted from total time span to get actual work hours.
    /// </summary>
    [JsonPropertyName("timeLessMinutes")]
    public int TimeLessMinutes { get; set; } = 60;

    /// <summary>
    /// Calculated work hours per day (end - start - lunch).
    /// </summary>
    [JsonIgnore]
    public decimal WorkHoursPerDay => (decimal)(DefaultEndTime - DefaultStartTime).TotalHours - (TimeLessMinutes / 60m);
}

/// <summary>
/// Work patterns detected from historical data.
/// </summary>
public class WorkPatterns
{
    public string EmployeeId { get; set; } = string.Empty;
    public DateOnly AnalysisStartDate { get; set; }
    public DateOnly AnalysisEndDate { get; set; }
    public int TotalDays { get; set; }
    public int WorkingDays { get; set; }
    public decimal AverageHoursPerDay { get; set; }
    public TimeOnly TypicalStartTime { get; set; }
    public TimeOnly TypicalEndTime { get; set; }
    public List<string> FrequentClients { get; set; } = [];
    public List<string> FrequentProjects { get; set; } = [];
    public Dictionary<DayOfWeek, List<string>> DayProjectMap { get; set; } = [];
    public bool HasMultiProjectDays { get; set; }
    public bool ConsistentSchedule { get; set; }
}

/// <summary>
/// A recent project from historical timesheets.
/// </summary>
public class RecentProject
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("clientName")]
    public string? ClientName { get; set; }

    [JsonPropertyName("projectId")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }

    [JsonPropertyName("categoryId")]
    public string CategoryId { get; set; } = "DEV";

    [JsonPropertyName("billableId")]
    public string? BillableId { get; set; }

    [JsonPropertyName("isBillable")]
    public bool IsBillable { get; set; }

    /// <summary>
    /// Number of times this project was used in recent timesheets.
    /// </summary>
    [JsonPropertyName("usageCount")]
    public int UsageCount { get; set; }

    /// <summary>
    /// Most recent date this project was used.
    /// </summary>
    [JsonPropertyName("lastUsedDate")]
    public DateOnly LastUsedDate { get; set; }

    /// <summary>
    /// Average hours per day on this project.
    /// </summary>
    [JsonPropertyName("averageHours")]
    public decimal AverageHours { get; set; }
}
