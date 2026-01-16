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
        var page = 1;
        const int perPage = 100;
        
        while (page <= 3) // GitHub only returns last 300 events max
        {
            var url = $"/users/{username}/events?per_page={perPage}&page={page}";
            
            try
            {
                var events = await _httpClient.GetFromJsonAsync<List<GitHubEvent>>(
                    url, cancellationToken);
                
                if (events == null || events.Count == 0)
                    break;
                
                foreach (var evt in events)
                {
                    if (evt.Type != "PushEvent" || evt.Payload?.Commits == null)
                        continue;
                    
                    var eventDate = DateOnly.FromDateTime(evt.CreatedAt);
                    
                    // Skip if outside date range
                    if (eventDate < startDate || eventDate > endDate)
                        continue;
                    
                    foreach (var commit in evt.Payload.Commits)
                    {
                        commits.Add(new GitCommit
                        {
                            Hash = commit.Sha,
                            Author = commit.Author?.Name ?? username,
                            Email = commit.Author?.Email ?? "",
                            Date = evt.CreatedAt,
                            Message = commit.Message,
                            Repository = evt.Repo?.Name ?? "unknown",
                            Source = GitSource.GitHub
                        });
                    }
                }
                
                // Check if we've gone past the start date
                var oldestEvent = events.MinBy(e => e.CreatedAt);
                if (oldestEvent != null && DateOnly.FromDateTime(oldestEvent.CreatedAt) < startDate)
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
