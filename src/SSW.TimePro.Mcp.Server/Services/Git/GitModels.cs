using System.Text.Json.Serialization;

namespace SSW.TimePro.Mcp.Server.Services.Git;

/// <summary>
/// Represents a git commit with activity information.
/// </summary>
public class GitCommit
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;
    
    [JsonPropertyName("shortHash")]
    public string ShortHash => Hash.Length >= 7 ? Hash[..7] : Hash;
    
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;
    
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
    
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("subject")]
    public string Subject => Message.Split('\n').FirstOrDefault() ?? string.Empty;
    
    [JsonPropertyName("repository")]
    public string Repository { get; set; } = string.Empty;
    
    [JsonPropertyName("branch")]
    public string? Branch { get; set; }
    
    [JsonPropertyName("source")]
    public GitSource Source { get; set; }
}

/// <summary>
/// Source of git activity.
/// </summary>
public enum GitSource
{
    Local,
    GitHub,
    AzureDevOps
}

/// <summary>
/// Configuration for a project to scan.
/// </summary>
public class ProjectConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("path")]
    public string? Path { get; set; }
    
    [JsonPropertyName("projectId")]
    public string? ProjectId { get; set; }
    
    [JsonPropertyName("clientId")]
    public string? ClientId { get; set; }
    
    [JsonPropertyName("categoryId")]
    public string? CategoryId { get; set; }
}

/// <summary>
/// User preferences for git scanning.
/// </summary>
public class GitScanSettings
{
    [JsonPropertyName("localScan")]
    public ScanPreference LocalScan { get; set; } = ScanPreference.Ask;
    
    [JsonPropertyName("githubScan")]
    public ScanPreference GitHubScan { get; set; } = ScanPreference.Always;
    
    [JsonPropertyName("azureDevOpsScan")]
    public ScanPreference AzureDevOpsScan { get; set; } = ScanPreference.Always;
    
    [JsonPropertyName("projects")]
    public List<ProjectConfig> Projects { get; set; } = [];
}

/// <summary>
/// Scan preference for different sources.
/// </summary>
public enum ScanPreference
{
    Ask,
    Always,
    Never
}

/// <summary>
/// Summary of work activity for a day.
/// </summary>
public class DayActivity
{
    [JsonPropertyName("date")]
    public DateOnly Date { get; set; }
    
    [JsonPropertyName("commits")]
    public List<GitCommit> Commits { get; set; } = [];
    
    [JsonPropertyName("projects")]
    public List<string> Projects => Commits
        .Select(c => c.Repository)
        .Distinct()
        .ToList();
    
    [JsonPropertyName("totalCommits")]
    public int TotalCommits => Commits.Count;
    
    [JsonPropertyName("firstActivity")]
    public DateTime? FirstActivity => Commits.MinBy(c => c.Date)?.Date;
    
    [JsonPropertyName("lastActivity")]
    public DateTime? LastActivity => Commits.MaxBy(c => c.Date)?.Date;
}

/// <summary>
/// Result of scanning git activity.
/// </summary>
public class GitScanResult
{
    [JsonPropertyName("startDate")]
    public DateOnly StartDate { get; set; }
    
    [JsonPropertyName("endDate")]
    public DateOnly EndDate { get; set; }
    
    [JsonPropertyName("commits")]
    public List<GitCommit> Commits { get; set; } = [];
    
    [JsonPropertyName("dailyActivity")]
    public List<DayActivity> DailyActivity { get; set; } = [];
    
    [JsonPropertyName("totalCommits")]
    public int TotalCommits => Commits.Count;
    
    [JsonPropertyName("repositories")]
    public List<string> Repositories => Commits
        .Select(c => c.Repository)
        .Distinct()
        .ToList();
    
    [JsonPropertyName("sources")]
    public List<GitSource> Sources => Commits
        .Select(c => c.Source)
        .Distinct()
        .ToList();
}
