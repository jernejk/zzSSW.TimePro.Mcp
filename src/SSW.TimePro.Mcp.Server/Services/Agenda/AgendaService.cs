using System.Text;
using SSW.TimePro.Mcp.Server.Models;
using SSW.TimePro.Mcp.Server.Services.Git;

namespace SSW.TimePro.Mcp.Server.Services.Agenda;

/// <summary>
/// Service for generating timesheet agendas based on various sources.
/// </summary>
public interface IAgendaService
{
    /// <summary>
    /// Generate a weekly agenda based on available data sources.
    /// </summary>
    Task<WeeklyAgenda> GenerateAgendaAsync(
        AgendaGenerationOptions options,
        ITimeProService timeProService,
        IGitScanningService? gitScanningService = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Export agenda to Markdown format for review/editing.
    /// </summary>
    string ExportToMarkdown(WeeklyAgenda agenda);
    
    /// <summary>
    /// Analyze patterns from historical timesheets.
    /// </summary>
    Task<WorkPatterns> AnalyzePatternsAsync(
        string employeeId,
        ITimeProService timeProService,
        int lookbackDays = 14,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of agenda generation service.
/// </summary>
public class AgendaService : IAgendaService
{
    public async Task<WeeklyAgenda> GenerateAgendaAsync(
        AgendaGenerationOptions options,
        ITimeProService timeProService,
        IGitScanningService? gitScanningService = null,
        CancellationToken cancellationToken = default)
    {
        // Try to fetch employee settings to set defaults
        try
        {
            var empSettings = await timeProService.GetEmployeeSettingsAsync(cancellationToken);
            if (!string.IsNullOrEmpty(empSettings.StartTime) && TimeOnly.TryParse(empSettings.StartTime, out var start))
            {
                options.DefaultStartTime = start;
            }
            if (!string.IsNullOrEmpty(empSettings.EndTime) && TimeOnly.TryParse(empSettings.EndTime, out var end))
            {
                options.DefaultEndTime = end;
            }
        }
        catch (Exception)
        {
            // Ignore settings fetch failure
        }

        var agenda = new WeeklyAgenda
        {
            WeekStartDate = options.StartDate,
            WeekEndDate = options.EndDate,
            EmployeeId = options.EmployeeId,
            AgendaId = $"agenda-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}"
        };
        
        // Initialize days
        for (var date = options.StartDate; date <= options.EndDate; date = date.AddDays(1))
        {
            agenda.Days.Add(new DayAgenda { Date = date });
        }
        
        // Fetch existing timesheets
        if (options.IncludeExistingTimesheets)
        {
            await AddExistingTimesheetsAsync(agenda, options, timeProService, cancellationToken);
        }
        
        // Fetch CRM bookings
        if (options.IncludeCrmBookings)
        {
            await AddCrmBookingsAsync(agenda, options, timeProService, cancellationToken);
        }
        
        // Fetch suggested timesheets
        if (options.IncludeSuggestedTimesheets)
        {
            await AddSuggestedTimesheetsAsync(agenda, options, timeProService, cancellationToken);
        }
        
        // Add git activity
        if (gitScanningService != null && options.LocalGitPaths?.Count > 0)
        {
            await AddGitActivityAsync(agenda, options, gitScanningService, cancellationToken);
        }
        
        // Update day statuses
        foreach (var day in agenda.Days)
        {
            UpdateDayStatus(day);
        }
        
        return agenda;
    }
    
    private async Task AddExistingTimesheetsAsync(
        WeeklyAgenda agenda,
        AgendaGenerationOptions options,
        ITimeProService timeProService,
        CancellationToken cancellationToken)
    {
        try
        {
            var timesheets = await timeProService.GetTimesheetsAsync(
                options.EmployeeId,
                options.StartDate,
                options.EndDate,
                cancellationToken);
            
            foreach (var ts in timesheets)
            {
                var date = DateOnly.FromDateTime(ts.Date);
                var day = agenda.Days.FirstOrDefault(d => d.Date == date);
                
                if (day != null)
                {
                    day.Items.Add(new AgendaItem
                    {
                        Date = date,
                        StartTime = TimeOnly.FromDateTime(ts.StartTime),
                        EndTime = TimeOnly.FromDateTime(ts.EndTime),
                        ClientId = ts.ClientId ?? "",
                        ClientName = ts.Client,
                        ProjectId = ts.ProjectId,
                        ProjectName = ts.Project,
                        CategoryId = ts.Category ?? "DEV",
                        Description = ts.Notes,
                        Source = AgendaItemSource.ExistingTimesheet,
                        Confidence = AgendaConfidence.Confirmed,
                        ExistingTimesheetId = ts.TimeId
                    });
                }
            }
        }
        catch
        {
            // Ignore errors - continue with other sources
        }
    }
    
    private async Task AddCrmBookingsAsync(
        WeeklyAgenda agenda,
        AgendaGenerationOptions options,
        ITimeProService timeProService,
        CancellationToken cancellationToken)
    {
        try
        {
            var bookings = await timeProService.GetAppointmentsAsync(
                options.EmployeeId,
                options.StartDate,
                options.EndDate,
                cancellationToken);
            
            foreach (var booking in bookings)
            {
                var bookingStart = DateTime.Parse(booking.Start);
                var bookingEnd = DateTime.Parse(booking.End);
                var date = DateOnly.FromDateTime(bookingStart);
                var day = agenda.Days.FirstOrDefault(d => d.Date == date);
                
                if (day != null && !day.Items.Any(i => i.CrmBookingId == booking.Id))
                {
                    // Check if there's already a timesheet for this time
                    var start = TimeOnly.FromDateTime(bookingStart);
                    var end = TimeOnly.FromDateTime(bookingEnd);
                    
                    if (!day.Items.Any(i => i.ExistingTimesheetId.HasValue && 
                        OverlapsTime(i.StartTime, i.EndTime, start, end)))
                    {
                        day.Items.Add(new AgendaItem
                        {
                            Date = date,
                            StartTime = start,
                            EndTime = end,
                            ClientId = booking.ClientId ?? options.DefaultClientId ?? "",
                            ProjectId = booking.ProjectId ?? options.DefaultProjectId,
                            Description = booking.Title,
                            Source = AgendaItemSource.CrmBooking,
                            Confidence = AgendaConfidence.High,
                            CrmBookingId = booking.Id,
                            CategoryId = options.DefaultCategoryId,
                            LocationId = options.DefaultLocationId
                        });
                    }
                }
            }
        }
        catch
        {
            // Ignore errors - continue with other sources
        }
    }
    
    private async Task AddSuggestedTimesheetsAsync(
        WeeklyAgenda agenda,
        AgendaGenerationOptions options,
        ITimeProService timeProService,
        CancellationToken cancellationToken)
    {
        foreach (var day in agenda.Days)
        {
            if (day.IsWeekend) continue;
            
            try
            {
                var suggestions = await timeProService.GetSuggestedTimesheetsAsync(
                    options.EmployeeId,
                    day.Date,
                    cancellationToken);
                
                foreach (var suggestion in suggestions)
                {
                    var start = TimeOnly.FromDateTime(suggestion.StartTime);
                    var end = TimeOnly.FromDateTime(suggestion.EndTime);
                    
                    // Skip if already covered by existing timesheet
                    if (day.Items.Any(i => i.ExistingTimesheetId.HasValue && 
                        OverlapsTime(i.StartTime, i.EndTime, start, end)))
                    {
                        continue;
                    }
                    
                    day.Items.Add(new AgendaItem
                    {
                        Date = day.Date,
                        StartTime = start,
                        EndTime = end,
                        ClientId = suggestion.ClientId ?? options.DefaultClientId ?? "",
                        ClientName = suggestion.Client,
                        ProjectId = suggestion.ProjectId,
                        ProjectName = suggestion.Project,
                        Description = suggestion.Notes,
                        Source = AgendaItemSource.SuggestedTimesheet,
                        Confidence = AgendaConfidence.High,
                        CategoryId = suggestion.Category ?? options.DefaultCategoryId,
                        LocationId = options.DefaultLocationId
                    });
                }
            }
            catch
            {
                // Ignore errors - continue with other sources
            }
        }
    }
    
    private async Task AddGitActivityAsync(
        WeeklyAgenda agenda,
        AgendaGenerationOptions options,
        IGitScanningService gitScanningService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await gitScanningService.ScanLocalRepositoriesAsync(
                options.LocalGitPaths!,
                options.StartDate,
                options.EndDate,
                cancellationToken: cancellationToken);
            
            foreach (var dayActivity in result.DailyActivity)
            {
                var day = agenda.Days.FirstOrDefault(d => d.Date == dayActivity.Date);
                if (day == null) continue;
                
                // Skip if day already has entries
                if (day.Items.Count > 0) continue;
                
                // Group commits by repository
                var commitsByRepo = dayActivity.Commits
                    .GroupBy(c => c.Repository)
                    .ToList();
                
                // If there's just one project, create a single entry
                if (commitsByRepo.Count == 1)
                {
                    var repoGroup = commitsByRepo[0];
                    day.Items.Add(CreateGitAgendaItem(day.Date, repoGroup.ToList(), options));
                }
                else
                {
                    // Multiple projects - add notes about which repos were worked on
                    var notes = string.Join("\n", commitsByRepo.Select(g => 
                        $"- {g.Key}: {g.Count()} commits"));
                    
                    day.Items.Add(new AgendaItem
                    {
                        Date = day.Date,
                        StartTime = options.DefaultStartTime,
                        EndTime = options.DefaultEndTime,
                        ClientId = options.DefaultClientId ?? "",
                        ProjectId = options.DefaultProjectId,
                        CategoryId = options.DefaultCategoryId,
                        LocationId = options.DefaultLocationId,
                        Description = $"Git activity detected in multiple repositories",
                        Notes = notes,
                        Source = AgendaItemSource.GitActivity,
                        Confidence = AgendaConfidence.Low,
                        RelatedCommits = dayActivity.Commits
                    });
                }
            }
        }
        catch
        {
            // Ignore errors
        }
    }
    
    private AgendaItem CreateGitAgendaItem(DateOnly date, List<GitCommit> commits, AgendaGenerationOptions options)
    {
        var first = commits.MinBy(c => c.Date);
        var last = commits.MaxBy(c => c.Date);
        
        // Try to match repository to known project
        var repoName = commits.FirstOrDefault()?.Repository ?? "Unknown";
        
        // Use standard hours (aim for 8h) instead of commit timestamps to avoid overtime
        // StartTime and EndTime are derived from options (which may be set by EmployeeSettings)
        
        return new AgendaItem
        {
            Date = date,
            StartTime = options.DefaultStartTime,
            EndTime = options.DefaultEndTime,
            ClientId = options.DefaultClientId ?? "",
            ProjectId = options.DefaultProjectId,
            CategoryId = options.DefaultCategoryId,
            LocationId = options.DefaultLocationId,
            Description = $"Development on {repoName}",
            Notes = string.Join("\n", commits.Take(10).Select(c => $"- {c.Subject}")),
            Source = AgendaItemSource.GitActivity,
            Confidence = AgendaConfidence.Medium,
            RelatedCommits = commits
        };
    }
    
    private void UpdateDayStatus(DayAgenda day)
    {
        if (day.IsWeekend)
        {
            day.Status = DayAgendaStatus.Weekend;
            return;
        }
        
        if (day.Items.Count == 0)
        {
            day.Status = DayAgendaStatus.Empty;
            return;
        }
        
        // Check for leave
        if (day.Items.Any(i => i.CategoryId?.StartsWith("L-") == true || 
                              i.CategoryId == "LSICK" || 
                              i.CategoryId == "LNWD"))
        {
            day.Status = DayAgendaStatus.Leave;
            return;
        }
        
        if (day.TotalHours >= 8)
        {
            day.Status = DayAgendaStatus.Complete;
        }
        else if (day.UniqueProjects.Count > 1 || day.HasGaps)
        {
            day.Status = DayAgendaStatus.NeedsAttention;
        }
        else
        {
            day.Status = DayAgendaStatus.Partial;
        }
    }
    
    private static bool OverlapsTime(TimeOnly start1, TimeOnly end1, TimeOnly start2, TimeOnly end2)
    {
        return start1 < end2 && start2 < end1;
    }
    
    public string ExportToMarkdown(WeeklyAgenda agenda)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"# Weekly Agenda: {agenda.WeekStartDate:yyyy-MM-dd} to {agenda.WeekEndDate:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine($"**Employee:** {agenda.EmployeeId}");
        sb.AppendLine($"**Agenda ID:** `{agenda.AgendaId}`");
        sb.AppendLine($"**Generated:** {agenda.GeneratedAt:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine($"## Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Total Hours:** {agenda.TotalHours:F1} / {agenda.ExpectedHours:F0} ({agenda.CompletionPercentage}%)");
        sb.AppendLine($"- **Projects:** {string.Join(", ", agenda.AllProjects)}");
        
        if (agenda.DaysNeedingAttention.Count > 0)
        {
            sb.AppendLine($"- **Days Needing Attention:** {string.Join(", ", agenda.DaysNeedingAttention.Select(d => d.ToString("ddd MMM dd")))}");
        }
        
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        
        foreach (var day in agenda.Days)
        {
            var statusEmoji = day.Status switch
            {
                DayAgendaStatus.Complete => "✅",
                DayAgendaStatus.Weekend => "🏖️",
                DayAgendaStatus.Leave => "🏠",
                DayAgendaStatus.Empty => "❌",
                DayAgendaStatus.Partial => "⚠️",
                DayAgendaStatus.NeedsAttention => "🔍",
                _ => ""
            };
            
            sb.AppendLine($"## {statusEmoji} {day.Date:ddd, MMM dd yyyy}");
            sb.AppendLine();
            
            if (day.IsWeekend)
            {
                sb.AppendLine("*Weekend*");
                sb.AppendLine();
                continue;
            }
            
            if (day.Items.Count == 0)
            {
                sb.AppendLine("*No timesheets or suggestions for this day.*");
                sb.AppendLine();
                continue;
            }
            
            sb.AppendLine($"**Total: {day.TotalHours:F1} hours**");
            sb.AppendLine();
            
            foreach (var item in day.Items.OrderBy(i => i.StartTime))
            {
                var sourceLabel = item.Source switch
                {
                    AgendaItemSource.ExistingTimesheet => "📝 Existing",
                    AgendaItemSource.CrmBooking => "📅 CRM",
                    AgendaItemSource.SuggestedTimesheet => "💡 Suggested",
                    AgendaItemSource.GitActivity => "🔧 Git",
                    AgendaItemSource.Pattern => "📊 Pattern",
                    _ => ""
                };
                
                sb.AppendLine($"### {item.StartTime:HH:mm} - {item.EndTime:HH:mm} ({item.Hours:F1}h) {sourceLabel}");
                sb.AppendLine();
                sb.AppendLine($"- **Client:** {item.ClientId} {(item.ClientName != null ? $"({item.ClientName})" : "")}");
                
                if (!string.IsNullOrEmpty(item.ProjectId))
                    sb.AppendLine($"- **Project:** {item.ProjectId} {(item.ProjectName != null ? $"({item.ProjectName})" : "")}");
                
                sb.AppendLine($"- **Category:** {item.CategoryId}");
                sb.AppendLine($"- **Location:** {item.LocationId}");
                
                if (!string.IsNullOrEmpty(item.Description))
                    sb.AppendLine($"- **Description:** {item.Description}");
                
                if (!string.IsNullOrEmpty(item.Notes))
                {
                    sb.AppendLine();
                    sb.AppendLine("**Notes:**");
                    sb.AppendLine(item.Notes);
                }
                
                sb.AppendLine();
            }
        }
        
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*To create timesheets from this agenda, use the `ConfirmOperation` tool with the agenda ID.*");
        
        return sb.ToString();
    }
    
    public async Task<WorkPatterns> AnalyzePatternsAsync(
        string employeeId,
        ITimeProService timeProService,
        int lookbackDays = 14,
        CancellationToken cancellationToken = default)
    {
        var endDate = DateOnly.FromDateTime(DateTime.Today);
        var startDate = endDate.AddDays(-lookbackDays);
        
        var patterns = new WorkPatterns
        {
            EmployeeId = employeeId,
            AnalysisStartDate = startDate,
            AnalysisEndDate = endDate
        };
        
        try
        {
            var timesheets = await timeProService.GetTimesheetsAsync(
                employeeId, startDate, endDate, cancellationToken);
            
            if (timesheets.Count == 0)
                return patterns;
            
            // Calculate patterns
            var byDay = timesheets
                .GroupBy(t => DateOnly.FromDateTime(t.Date))
                .ToList();
            
            patterns.TotalDays = lookbackDays;
            patterns.WorkingDays = byDay.Count;
            patterns.AverageHoursPerDay = patterns.WorkingDays > 0 
                ? timesheets.Sum(t => t.TotalTime) / patterns.WorkingDays 
                : 0;
            
            // Typical start/end times
            var startTimes = timesheets
                .Select(t => t.StartTime.Hour)
                .GroupBy(h => h)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            
            var endTimes = timesheets
                .Select(t => t.EndTime.Hour)
                .GroupBy(h => h)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
            
            patterns.TypicalStartTime = new TimeOnly(startTimes?.Key ?? 9, 0);
            patterns.TypicalEndTime = new TimeOnly(endTimes?.Key ?? 17, 0);
            
            // Frequent clients/projects
            patterns.FrequentClients = timesheets
                .Where(t => !string.IsNullOrEmpty(t.ClientId))
                .GroupBy(t => t.ClientId!)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToList();
            
            patterns.FrequentProjects = timesheets
                .Where(t => !string.IsNullOrEmpty(t.ProjectId))
                .GroupBy(t => t.ProjectId!)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToList();
            
            // Day-project mapping
            patterns.DayProjectMap = byDay
                .GroupBy(d => d.Key.DayOfWeek)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(d => d)
                          .Where(t => !string.IsNullOrEmpty(t.ProjectId))
                          .GroupBy(t => t.ProjectId!)
                          .OrderByDescending(pg => pg.Count())
                          .Take(3)
                          .Select(pg => pg.Key)
                          .ToList());
            
            // Multi-project detection
            patterns.HasMultiProjectDays = byDay
                .Any(d => d.Select(t => t.ProjectId)
                          .Where(p => !string.IsNullOrEmpty(p))
                          .Distinct()
                          .Count() > 1);
            
            // Consistent schedule
            var startTimeVariance = timesheets.Select(t => t.StartTime.Hour).Distinct().Count();
            patterns.ConsistentSchedule = startTimeVariance <= 2;
        }
        catch
        {
            // Return default patterns on error
        }
        
        return patterns;
    }
}
