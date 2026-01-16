using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using SSW.TimePro.Mcp.Server.Configuration;
using SSW.TimePro.Mcp.Server.Services.Git;

namespace SSW.TimePro.Mcp.Server.Tools;

/// <summary>
/// MCP Tools for scanning git activity.
/// </summary>
[McpServerToolType]
public class GitScanTools
{
    private readonly IGitScanningService _gitScanningService;
    private readonly ILocalGitService _localGitService;
    private readonly IGitHubService _gitHubService;
    private readonly GitHubSettings _gitHubSettings;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GitScanTools(
        IGitScanningService gitScanningService,
        ILocalGitService localGitService,
        IGitHubService gitHubService,
        IOptions<GitHubSettings> gitHubSettings)
    {
        _gitScanningService = gitScanningService;
        _localGitService = localGitService;
        _gitHubService = gitHubService;
        _gitHubSettings = gitHubSettings.Value;
    }

    /// <summary>
    /// Scan a local git repository for commits.
    /// </summary>
    [McpServerTool]
    [Description("Scan a local git repository for commits. Useful for finding work activity that hasn't been pushed yet.")]
    public async Task<string> ScanLocalRepository(
        [Description("Path to the git repository")] string repositoryPath,
        [Description("Number of days to scan (default: 7)")] int days = 7,
        [Description("Author name/email to filter (optional)")] string? author = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_localGitService.IsGitRepository(repositoryPath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"'{repositoryPath}' is not a git repository."
                }, _jsonOptions);
            }
            
            var endDate = DateOnly.FromDateTime(DateTime.Today);
            var startDate = endDate.AddDays(-days + 1);
            
            var commits = await _localGitService.ScanRepositoryAsync(
                repositoryPath,
                startDate,
                endDate,
                author,
                cancellationToken);
            
            var repoName = await _localGitService.GetRepositoryNameAsync(repositoryPath, cancellationToken);
            var hasUncommitted = await _localGitService.HasUncommittedChangesAsync(repositoryPath, cancellationToken);
            var currentBranch = await _localGitService.GetCurrentBranchAsync(repositoryPath, cancellationToken);
            
            var dailyActivity = _gitScanningService.GroupByDay(commits);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                repository = repoName,
                path = repositoryPath,
                currentBranch,
                hasUncommittedChanges = hasUncommitted,
                startDate = startDate.ToString("yyyy-MM-dd"),
                endDate = endDate.ToString("yyyy-MM-dd"),
                totalCommits = commits.Count,
                dailyActivity = dailyActivity.Select(d => new
                {
                    date = d.Date.ToString("yyyy-MM-dd"),
                    d.TotalCommits,
                    commits = d.Commits.Select(c => new
                    {
                        c.ShortHash,
                        time = c.Date.ToString("HH:mm"),
                        c.Subject
                    })
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _jsonOptions);
        }
    }

    /// <summary>
    /// Scan multiple local repositories.
    /// </summary>
    [McpServerTool]
    [Description("Scan multiple local git repositories for commits. Provide a list of paths.")]
    public async Task<string> ScanLocalRepositories(
        [Description("Comma-separated list of repository paths")] string repositoryPaths,
        [Description("Number of days to scan (default: 7)")] int days = 7,
        [Description("Author name/email to filter (optional)")] string? author = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var paths = repositoryPaths.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            if (paths.Length == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "No repository paths provided."
                }, _jsonOptions);
            }
            
            var endDate = DateOnly.FromDateTime(DateTime.Today);
            var startDate = endDate.AddDays(-days + 1);
            
            var result = await _gitScanningService.ScanLocalRepositoriesAsync(
                paths,
                startDate,
                endDate,
                author,
                cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                startDate = result.StartDate.ToString("yyyy-MM-dd"),
                endDate = result.EndDate.ToString("yyyy-MM-dd"),
                totalCommits = result.TotalCommits,
                repositories = result.Repositories,
                dailyActivity = result.DailyActivity.Select(d => new
                {
                    date = d.Date.ToString("yyyy-MM-dd"),
                    d.TotalCommits,
                    d.Projects,
                    firstActivity = d.FirstActivity?.ToString("HH:mm"),
                    lastActivity = d.LastActivity?.ToString("HH:mm")
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _jsonOptions);
        }
    }

    /// <summary>
    /// Scan GitHub for user activity.
    /// </summary>
    [McpServerTool]
    [Description("Scan GitHub for a user's commit activity. Uses GitHub API to fetch recent push events.")]
    public async Task<string> ScanGitHub(
        [Description("GitHub username")] string username,
        [Description("Number of days to scan (default: 7)")] int days = 7,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endDate = DateOnly.FromDateTime(DateTime.Today);
            var startDate = endDate.AddDays(-days + 1);
            
            var result = await _gitScanningService.ScanGitHubAsync(
                username,
                startDate,
                endDate,
                cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                username,
                startDate = result.StartDate.ToString("yyyy-MM-dd"),
                endDate = result.EndDate.ToString("yyyy-MM-dd"),
                totalCommits = result.TotalCommits,
                repositories = result.Repositories,
                dailyActivity = result.DailyActivity.Select(d => new
                {
                    date = d.Date.ToString("yyyy-MM-dd"),
                    d.TotalCommits,
                    d.Projects
                })
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _jsonOptions);
        }
    }

    /// <summary>
    /// Discover git repositories in a directory.
    /// </summary>
    [McpServerTool]
    [Description("Find git repositories in a directory (searches up to 2 levels deep).")]
    public async Task<string> DiscoverRepositories(
        [Description("Base directory to search")] string baseDirectory,
        [Description("Maximum search depth (default: 2)")] int maxDepth = 2,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(baseDirectory))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Directory '{baseDirectory}' does not exist."
                }, _jsonOptions);
            }
            
            var repositories = new List<object>();
            
            await ScanForRepositoriesAsync(baseDirectory, 0, maxDepth, repositories, cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                baseDirectory,
                count = repositories.Count,
                repositories
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, _jsonOptions);
        }
    }
    
    private async Task ScanForRepositoriesAsync(
        string directory,
        int currentDepth,
        int maxDepth,
        List<object> repositories,
        CancellationToken cancellationToken)
    {
        if (currentDepth > maxDepth || cancellationToken.IsCancellationRequested)
            return;
        
        if (_localGitService.IsGitRepository(directory))
        {
            var name = await _localGitService.GetRepositoryNameAsync(directory, cancellationToken);
            var branch = await _localGitService.GetCurrentBranchAsync(directory, cancellationToken);
            var hasChanges = await _localGitService.HasUncommittedChangesAsync(directory, cancellationToken);
            
            repositories.Add(new
            {
                name,
                path = directory,
                branch,
                hasUncommittedChanges = hasChanges
            });
            
            return; // Don't scan subdirectories of a git repo
        }
        
        try
        {
            foreach (var subDir in Directory.EnumerateDirectories(directory))
            {
                var dirName = Path.GetFileName(subDir);
                
                // Skip common non-project directories
                if (dirName.StartsWith('.') || dirName == "node_modules" || 
                    dirName == "bin" || dirName == "obj" || dirName == "vendor")
                    continue;
                
                await ScanForRepositoriesAsync(subDir, currentDepth + 1, maxDepth, repositories, cancellationToken);
            }
        }
        catch
        {
            // Skip directories we can't access
        }
    }
}
