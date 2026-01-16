namespace SSW.TimePro.Mcp.Server.Services.Git;

/// <summary>
/// Combined service for scanning multiple git sources.
/// </summary>
public interface IGitScanningService
{
    /// <summary>
    /// Scan all configured sources for git activity.
    /// </summary>
    Task<GitScanResult> ScanAllSourcesAsync(
        DateOnly startDate,
        DateOnly endDate,
        GitScanSettings settings,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Scan local repositories only.
    /// </summary>
    Task<GitScanResult> ScanLocalRepositoriesAsync(
        IEnumerable<string> repositoryPaths,
        DateOnly startDate,
        DateOnly endDate,
        string? author = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Scan GitHub only.
    /// </summary>
    Task<GitScanResult> ScanGitHubAsync(
        string username,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Group commits by day.
    /// </summary>
    List<DayActivity> GroupByDay(List<GitCommit> commits);
}

/// <summary>
/// Implementation of combined git scanning.
/// </summary>
public class GitScanningService : IGitScanningService
{
    private readonly ILocalGitService _localGitService;
    private readonly IGitHubService _gitHubService;
    
    public GitScanningService(
        ILocalGitService localGitService,
        IGitHubService gitHubService)
    {
        _localGitService = localGitService;
        _gitHubService = gitHubService;
    }
    
    public async Task<GitScanResult> ScanAllSourcesAsync(
        DateOnly startDate,
        DateOnly endDate,
        GitScanSettings settings,
        CancellationToken cancellationToken = default)
    {
        var allCommits = new List<GitCommit>();
        
        // Scan local repositories
        if (settings.LocalScan == ScanPreference.Always)
        {
            foreach (var project in settings.Projects.Where(p => !string.IsNullOrEmpty(p.Path)))
            {
                try
                {
                    var commits = await _localGitService.ScanRepositoryAsync(
                        project.Path!,
                        startDate,
                        endDate,
                        cancellationToken: cancellationToken);
                    
                    allCommits.AddRange(commits);
                }
                catch
                {
                    // Skip repositories that can't be scanned
                }
            }
        }
        
        // Scan GitHub
        if (settings.GitHubScan == ScanPreference.Always)
        {
            try
            {
                // We'll use the first email we find in local commits, or default
                var username = allCommits.FirstOrDefault()?.Author ?? "user";
                var githubCommits = await _gitHubService.GetUserActivityAsync(
                    username,
                    startDate,
                    endDate,
                    cancellationToken);
                
                allCommits.AddRange(githubCommits);
            }
            catch
            {
                // Ignore GitHub errors
            }
        }
        
        // Remove duplicates by hash
        allCommits = allCommits
            .DistinctBy(c => c.Hash)
            .OrderByDescending(c => c.Date)
            .ToList();
        
        return new GitScanResult
        {
            StartDate = startDate,
            EndDate = endDate,
            Commits = allCommits,
            DailyActivity = GroupByDay(allCommits)
        };
    }
    
    public async Task<GitScanResult> ScanLocalRepositoriesAsync(
        IEnumerable<string> repositoryPaths,
        DateOnly startDate,
        DateOnly endDate,
        string? author = null,
        CancellationToken cancellationToken = default)
    {
        var allCommits = new List<GitCommit>();
        
        foreach (var path in repositoryPaths)
        {
            try
            {
                var commits = await _localGitService.ScanRepositoryAsync(
                    path,
                    startDate,
                    endDate,
                    author,
                    cancellationToken);
                
                allCommits.AddRange(commits);
            }
            catch
            {
                // Skip repositories that can't be scanned
            }
        }
        
        allCommits = allCommits
            .DistinctBy(c => c.Hash)
            .OrderByDescending(c => c.Date)
            .ToList();
        
        return new GitScanResult
        {
            StartDate = startDate,
            EndDate = endDate,
            Commits = allCommits,
            DailyActivity = GroupByDay(allCommits)
        };
    }
    
    public async Task<GitScanResult> ScanGitHubAsync(
        string username,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var commits = await _gitHubService.GetUserActivityAsync(
            username,
            startDate,
            endDate,
            cancellationToken);
        
        return new GitScanResult
        {
            StartDate = startDate,
            EndDate = endDate,
            Commits = commits,
            DailyActivity = GroupByDay(commits)
        };
    }
    
    public List<DayActivity> GroupByDay(List<GitCommit> commits)
    {
        return commits
            .GroupBy(c => DateOnly.FromDateTime(c.Date))
            .Select(g => new DayActivity
            {
                Date = g.Key,
                Commits = g.OrderBy(c => c.Date).ToList()
            })
            .OrderBy(d => d.Date)
            .ToList();
    }
}
