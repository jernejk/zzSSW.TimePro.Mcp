using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using SSW.TimePro.Mcp.Server.Configuration;
using SSW.TimePro.Mcp.Server.Services;
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
    private readonly ITimeProService _timeProService;
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
        ITimeProService timeProService,
        IOptions<GitHubSettings> gitHubSettings)
    {
        _gitScanningService = gitScanningService;
        _localGitService = localGitService;
        _gitHubService = gitHubService;
        _timeProService = timeProService;
        _gitHubSettings = gitHubSettings.Value;
    }

    /// <summary>
    /// Scan multiple local repositories.
    /// </summary>
    [McpServerTool]
    [Description("Scan multiple local git repositories for the current user's commits. Automatically filters by git user.email.")]
    public async Task<string> ScanLocalRepositories(
        [Description("Comma-separated list of repository paths")] string repositoryPaths,
        [Description("Number of days to scan (default: 7)")] int days = 7,
        [Description("Author name/email to filter (optional, defaults to git user.email)")] string? author = null,
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
    /// Scan remote source for user commits (GitHub or Azure DevOps via TimePro).
    /// </summary>
    [McpServerTool]
    [Description("Scan remote source for user commits. Supports 'github' (default) or 'azuredevops' (via TimePro API). For Azure DevOps, use employeeId instead of username.")]
    public async Task<string> ScanRemoteCommits(
        [Description("Username/employeeId for the remote source (GitHub username or TimePro employee ID for Azure DevOps)")] string username,
        [Description("Number of days to scan (default: 7)")] int days = 7,
        [Description("Source: 'github' (default) or 'azuredevops'")] string source = "github",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var endDate = DateOnly.FromDateTime(DateTime.Today);
            var startDate = endDate.AddDays(-days + 1);

            if (source.Equals("azuredevops", StringComparison.OrdinalIgnoreCase))
            {
                // Azure DevOps via TimePro API
                var azureResults = await _timeProService.GetAzureDevOpsCommitsAsync(
                    username, // employeeId
                    startDate,
                    endDate,
                    cancellationToken);

                // Flatten all commits from all subscriptions and group by day
                var allCommits = azureResults
                    .SelectMany(r => r.Data.Select(c => new
                    {
                        Subscription = r.Subscription?.Name ?? "Unknown",
                        Commit = c
                    }))
                    .ToList();

                var dailyActivity = allCommits
                    .GroupBy(c => DateOnly.FromDateTime(c.Commit.Date.DateTime))
                    .OrderByDescending(g => g.Key)
                    .Select(g => new
                    {
                        date = g.Key.ToString("yyyy-MM-dd"),
                        totalCommits = g.Count(),
                        projects = g.Select(c => c.Commit.Repository).Distinct().ToList(),
                        commits = g.Select(c => new
                        {
                            repository = c.Commit.Repository,
                            description = c.Commit.Description,
                            author = c.Commit.Author,
                            time = c.Commit.Date.ToString("HH:mm"),
                            subscription = c.Subscription
                        })
                    })
                    .ToList();

                var repositories = allCommits
                    .Select(c => c.Commit.Repository)
                    .Distinct()
                    .ToList();

                // Collect any errors
                var errors = azureResults
                    .Where(r => r.Errors.Count > 0)
                    .SelectMany(r => r.Errors.Select(e => new
                    {
                        subscription = r.Subscription?.Name,
                        e.ErrorType,
                        e.Message
                    }))
                    .ToList();

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    source = "azuredevops",
                    employeeId = username,
                    startDate = startDate.ToString("yyyy-MM-dd"),
                    endDate = endDate.ToString("yyyy-MM-dd"),
                    totalCommits = allCommits.Count,
                    repositories,
                    subscriptions = azureResults.Select(r => r.Subscription?.Name).Where(n => n != null).Distinct(),
                    dailyActivity,
                    errors = errors.Count > 0 ? errors : null
                }, _jsonOptions);
            }

            // Default: GitHub
            var result = await _gitScanningService.ScanGitHubAsync(
                username,
                startDate,
                endDate,
                cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                source = "github",
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
}
