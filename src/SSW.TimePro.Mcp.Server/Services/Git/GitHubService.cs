using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SSW.TimePro.Mcp.Server.Configuration;

namespace SSW.TimePro.Mcp.Server.Services.Git;

/// <summary>
/// Service for scanning GitHub for user activity.
/// </summary>
public interface IGitHubService
{
    /// <summary>
    /// Get commits from GitHub events for a user.
    /// </summary>
    Task<List<GitCommit>> GetUserActivityAsync(
        string username,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if the GitHub token is valid.
    /// </summary>
    Task<bool> ValidateTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// GitHub service implementation using the GitHub API.
/// </summary>
public class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;
    private readonly GitHubSettings _settings;
    private const string GitHubApiUrl = "https://api.github.com";
    
    public GitHubService(HttpClient httpClient, IOptions<GitHubSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        
        ConfigureHttpClient();
    }
    
    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(GitHubApiUrl);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("SSW-TimePro-MCP", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        
        if (!string.IsNullOrEmpty(_settings.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _settings.Token);
        }
    }
    
    public async Task<List<GitCommit>> GetUserActivityAsync(
        string username,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var commits = new List<GitCommit>();

        // Use Search Commits API for full history (not limited to 300 events)
        // Format: author:username author-date:YYYY-MM-DD..YYYY-MM-DD
        var query = $"author:{username} author-date:{startDate:yyyy-MM-dd}..{endDate:yyyy-MM-dd}";
        var page = 1;
        const int perPage = 100;

        while (page <= 10) // Search API allows up to 1000 results (10 pages x 100)
        {
            var url = $"/search/commits?q={Uri.EscapeDataString(query)}&per_page={perPage}&page={page}&sort=author-date&order=desc";

            try
            {
                var response = await _httpClient.GetFromJsonAsync<GitHubSearchResponse>(
                    url, cancellationToken);

                if (response?.Items == null || response.Items.Count == 0)
                    break;

                foreach (var item in response.Items)
                {
                    commits.Add(new GitCommit
                    {
                        Hash = item.Sha,
                        Author = item.Commit?.Author?.Name ?? username,
                        Email = item.Commit?.Author?.Email ?? "",
                        Date = item.Commit?.Author?.Date ?? DateTime.MinValue,
                        Message = item.Commit?.Message ?? "",
                        Repository = item.Repository?.FullName ?? "unknown",
                        Source = GitSource.GitHub
                    });
                }

                // Check if we've fetched all results
                if (response.Items.Count < perPage || commits.Count >= response.TotalCount)
                    break;

                page++;
            }
            catch (HttpRequestException)
            {
                break;
            }
        }

        return commits
            .DistinctBy(c => c.Hash)
            .Where(c => DateOnly.FromDateTime(c.Date) >= startDate && DateOnly.FromDateTime(c.Date) <= endDate)
            .ToList();
    }
    
    public async Task<bool> ValidateTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/user", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

// GitHub API models
file class GitHubEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [JsonPropertyName("repo")]
    public GitHubRepo? Repo { get; set; }
    
    [JsonPropertyName("payload")]
    public GitHubPayload? Payload { get; set; }
}

file class GitHubRepo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

file class GitHubPayload
{
    [JsonPropertyName("commits")]
    public List<GitHubCommit>? Commits { get; set; }
}

file class GitHubCommit
{
    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("author")]
    public GitHubAuthor? Author { get; set; }
}

file class GitHubAuthor
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

// Search Commits API models
file class GitHubSearchResponse
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("incomplete_results")]
    public bool IncompleteResults { get; set; }

    [JsonPropertyName("items")]
    public List<GitHubSearchCommitItem> Items { get; set; } = [];
}

file class GitHubSearchCommitItem
{
    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;

    [JsonPropertyName("commit")]
    public GitHubCommitDetail? Commit { get; set; }

    [JsonPropertyName("repository")]
    public GitHubSearchRepository? Repository { get; set; }
}

file class GitHubCommitDetail
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public GitHubCommitAuthor? Author { get; set; }
}

file class GitHubCommitAuthor
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}

file class GitHubSearchRepository
{
    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;
}
