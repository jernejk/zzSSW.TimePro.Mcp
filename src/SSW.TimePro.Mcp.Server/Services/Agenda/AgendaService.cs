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
            if (empSettings.TimeLessMinutes > 0)
            {
                options.TimeLessMinutes = empSettings.TimeLessMinutes;
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
            AgendaId = $"agenda-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}",
            WorkHoursPerDay = options.WorkHoursPerDay  // e.g., 9h (09:00-18:00) - 1h lunch = 8h
        };
        
        // Initialize days
        for (var date = options.StartDate; date <= options.EndDate; date = date.AddDays(1))
        {
            agenda.Days.Add(new DayAgenda { Date = date });
        }
        
        // Fetch existing timesheets first (these are confirmed, highest priority)
        if (options.IncludeExistingTimesheets)
        {
            await AddExistingTimesheetsAsync(agenda, options, timeProService, cancellationToken);
        }

        // Fetch recent projects (for fallback when no suggestions exist)
        var recentProjects = await GetRecentProjectsAsync(options.EmployeeId, timeProService, cancellationToken);
        agenda.RecentProjects = recentProjects;

        // Fetch suggested timesheets (prioritized source for days without existing timesheets)
        // This populates options including billable vs internal alternatives
        if (options.IncludeSuggestedTimesheets)
        {
            await AddSuggestedTimesheetsAsync(agenda, options, timeProService, cancellationToken);
        }

        // Fetch CRM bookings (may supplement or provide alternatives - but local CRM may be broken)
        if (options.IncludeCrmBookings)
        {
            await AddCrmBookingsAsync(agenda, options, timeProService, cancellationToken);
        }

        // For days without any suggestions, use recent projects as fallback
        await AddRecentProjectFallbackAsync(agenda, options, recentProjects, cancellationToken);

        // Add git activity (supplementary info for descriptions)
        if (gitScanningService != null && options.LocalGitPaths?.Count > 0)
        {
            await AddGitActivityAsync(agenda, options, gitScanningService, cancellationToken);
        }

        // Update day statuses
        foreach (var day in agenda.Days)
        {
            UpdateDayStatus(day, options.WorkHoursPerDay);
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
                        ExistingTimesheetId = ts.TimeId,
                        BillableId = ts.BillableId,
                        IsBillable = ts.IsBillable
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

            // Skip if day already has existing timesheets covering full hours
            if (day.Items.Any(i => i.ExistingTimesheetId.HasValue) && day.TotalHours >= 8)
                continue;

            try
            {
                var suggestions = await timeProService.GetSuggestedTimesheetsAsync(
                    options.EmployeeId,
                    day.Date,
                    cancellationToken);

                if (suggestions.Count == 0) continue;

                // Convert suggestions to agenda items with billable info
                var suggestedItems = suggestions.Select(suggestion =>
                {
                    var start = TimeOnly.FromDateTime(suggestion.StartTime);
                    var end = TimeOnly.FromDateTime(suggestion.EndTime);

                    return new AgendaItem
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
                        LocationId = options.DefaultLocationId,
                        IsBillable = suggestion.IsBillable,
                        SuggestedTimesheetId = suggestion.TimeId
                    };
                }).ToList();

                // Skip items that overlap with existing timesheets
                suggestedItems = suggestedItems
                    .Where(item => !day.Items.Any(i => i.ExistingTimesheetId.HasValue &&
                        OverlapsTime(i.StartTime, i.EndTime, item.StartTime, item.EndTime)))
                    .ToList();

                if (suggestedItems.Count == 0) continue;

                // Prioritize: billable client work > internal work > leave
                // Sort by: IsBillable (desc), then by hours (desc)
                var sortedItems = suggestedItems
                    .OrderByDescending(i => i.IsBillable)
                    .ThenByDescending(i => i.Hours)
                    .ToList();

                // The first (highest priority) becomes the main item
                var primaryItem = sortedItems[0];

                // Others become alternatives on the primary item
                if (sortedItems.Count > 1)
                {
                    primaryItem.Alternatives = sortedItems.Skip(1).ToList();
                }

                day.Items.Add(primaryItem);
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
    
    private void UpdateDayStatus(DayAgenda day, decimal workHoursPerDay = 8)
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

        if (day.TotalHours >= workHoursPerDay)
        {
            day.Status = DayAgendaStatus.Complete;
        }
        else if (day.UniqueProjects.Count > 1 || day.TotalHours < workHoursPerDay)
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

    /// <summary>
    /// Get recent projects from the API endpoint.
    /// Falls back to calculating from timesheets if API fails.
    /// </summary>
    private async Task<List<RecentProject>> GetRecentProjectsAsync(
        string employeeId,
        ITimeProService timeProService,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use the dedicated API endpoint for recent projects
            var apiProjects = await timeProService.GetRecentProjectsAsync(employeeId, cancellationToken);

            if (apiProjects.Count > 0)
            {
                // Convert API response to our RecentProject model
                return apiProjects
                    .Select(p => new RecentProject
                    {
                        ClientId = p.ClientId,
                        ClientName = p.ClientName,
                        ProjectId = p.ProjectId,
                        ProjectName = p.ProjectName,
                        CategoryId = p.CategoryId,
                        BillableId = p.BillableId,
                        IsBillable = p.IsBillable,
                        UsageCount = p.TimesheetCount,
                        LastUsedDate = DateOnly.FromDateTime(p.LastUsed),
                        AverageHours = p.TimesheetCount > 0 ? p.TotalHours / p.TimesheetCount : 0
                    })
                    // Prioritize: billable client work > internal > by usage
                    .OrderByDescending(p => p.IsBillable)
                    .ThenByDescending(p => p.UsageCount)
                    .ThenByDescending(p => p.LastUsedDate)
                    .Take(10)
                    .ToList();
            }
        }
        catch
        {
            // Fall through to fallback calculation
        }

        // Fallback: calculate from recent timesheets if API fails
        return await GetRecentProjectsFromTimesheetsAsync(employeeId, timeProService, cancellationToken);
    }

    /// <summary>
    /// Fallback: calculate recent projects from timesheets.
    /// </summary>
    private async Task<List<RecentProject>> GetRecentProjectsFromTimesheetsAsync(
        string employeeId,
        ITimeProService timeProService,
        CancellationToken cancellationToken)
    {
        var recentProjects = new List<RecentProject>();

        try
        {
            var endDate = DateOnly.FromDateTime(DateTime.Today);
            var startDate = endDate.AddDays(-30);

            var timesheets = await timeProService.GetTimesheetsAsync(
                employeeId, startDate, endDate, cancellationToken);

            if (timesheets.Count == 0)
                return recentProjects;

            // Group by client+project and calculate stats
            var grouped = timesheets
                .Where(t => !string.IsNullOrEmpty(t.ClientId) && !string.IsNullOrEmpty(t.ProjectId))
                .GroupBy(t => new { t.ClientId, t.ProjectId })
                .Select(g =>
                {
                    var first = g.First();
                    var lastUsed = g.Max(t => DateOnly.FromDateTime(t.Date));
                    var totalHours = g.Sum(t => t.TotalTime);
                    var dayCount = g.Select(t => t.Date.Date).Distinct().Count();

                    return new RecentProject
                    {
                        ClientId = first.ClientId!,
                        ClientName = first.Client,
                        ProjectId = first.ProjectId!,
                        ProjectName = first.Project,
                        CategoryId = first.Category ?? "DEV",
                        BillableId = first.BillableId,
                        IsBillable = first.IsBillable,
                        UsageCount = g.Count(),
                        LastUsedDate = lastUsed,
                        AverageHours = dayCount > 0 ? totalHours / dayCount : 0
                    };
                })
                // Prioritize: billable client work > internal > by usage
                .OrderByDescending(p => p.IsBillable)
                .ThenByDescending(p => p.UsageCount)
                .ThenByDescending(p => p.LastUsedDate)
                .Take(10)
                .ToList();

            return grouped;
        }
        catch
        {
            return recentProjects;
        }
    }

    /// <summary>
    /// For days without any items, use recent projects as fallback.
    /// </summary>
    private Task AddRecentProjectFallbackAsync(
        WeeklyAgenda agenda,
        AgendaGenerationOptions options,
        List<RecentProject> recentProjects,
        CancellationToken cancellationToken)
    {
        if (recentProjects.Count == 0) return Task.CompletedTask;

        foreach (var day in agenda.Days)
        {
            if (day.IsWeekend) continue;

            // Skip if day already has items (from existing timesheets or suggestions)
            if (day.Items.Count > 0) continue;

            // Use the most frequently used billable project as primary
            var primaryProject = recentProjects.FirstOrDefault(p => p.IsBillable)
                                 ?? recentProjects.FirstOrDefault();

            if (primaryProject == null) continue;

            var primaryItem = new AgendaItem
            {
                Date = day.Date,
                StartTime = options.DefaultStartTime,
                EndTime = options.DefaultEndTime,
                ClientId = primaryProject.ClientId,
                ClientName = primaryProject.ClientName,
                ProjectId = primaryProject.ProjectId,
                ProjectName = primaryProject.ProjectName,
                CategoryId = primaryProject.CategoryId,
                LocationId = options.DefaultLocationId,
                Description = $"Based on recent activity ({primaryProject.UsageCount} timesheets, last: {primaryProject.LastUsedDate:MMM dd})",
                Source = AgendaItemSource.Pattern,
                Confidence = AgendaConfidence.Medium,
                BillableId = primaryProject.BillableId,
                IsBillable = primaryProject.IsBillable
            };

            // Add other recent projects as alternatives
            var alternatives = recentProjects
                .Where(p => p != primaryProject)
                .Take(3)
                .Select(p => new AgendaItem
                {
                    Date = day.Date,
                    StartTime = options.DefaultStartTime,
                    EndTime = options.DefaultEndTime,
                    ClientId = p.ClientId,
                    ClientName = p.ClientName,
                    ProjectId = p.ProjectId,
                    ProjectName = p.ProjectName,
                    CategoryId = p.CategoryId,
                    LocationId = options.DefaultLocationId,
                    Description = $"Alternative: {p.UsageCount} recent timesheets",
                    Source = AgendaItemSource.Pattern,
                    Confidence = AgendaConfidence.Low,
                    BillableId = p.BillableId,
                    IsBillable = p.IsBillable
                })
                .ToList();

            if (alternatives.Count > 0)
            {
                primaryItem.Alternatives = alternatives;
            }

            day.Items.Add(primaryItem);
        }

        return Task.CompletedTask;
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

                var billableLabel = item.IsBillable ? "💰 Billable" : "🏠 Internal";

                sb.AppendLine($"### {item.StartTime:HH:mm} - {item.EndTime:HH:mm} ({item.Hours:F1}h) {sourceLabel} {billableLabel}");
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

                // Show alternatives if available
                if (item.Alternatives?.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("**Alternatives:**");
                    foreach (var alt in item.Alternatives)
                    {
                        var altBillable = alt.IsBillable ? "💰" : "🏠";
                        sb.AppendLine($"- {altBillable} {alt.ClientId}/{alt.ProjectId} ({alt.ClientName ?? alt.ProjectName ?? "Unknown"})");
                    }
                }

                sb.AppendLine();
            }
        }
        
        // Show recent projects for reference
        if (agenda.RecentProjects.Count > 0)
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Recent Projects (Last 14 Days)");
            sb.AppendLine();
            sb.AppendLine("| Client | Project | Billable | Usage | Last Used |");
            sb.AppendLine("|--------|---------|----------|-------|-----------|");
            foreach (var proj in agenda.RecentProjects.Take(5))
            {
                var billable = proj.IsBillable ? "💰 Yes" : "🏠 No";
                sb.AppendLine($"| {proj.ClientId} | {proj.ProjectId} | {billable} | {proj.UsageCount}x | {proj.LastUsedDate:MMM dd} |");
            }
            sb.AppendLine();
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
