namespace SSW.TimePro.Mcp.Server.Configuration;

/// <summary>
/// Configuration settings for GitHub integration.
/// </summary>
public class GitHubSettings
{
    public const string SectionName = "GitHub";
    
    /// <summary>
    /// GitHub personal access token for fetching commit data.
    /// </summary>
    public string Token { get; set; } = string.Empty;
    
    /// <summary>
    /// GitHub username to fetch commits for.
    /// </summary>
    public string Username { get; set; } = string.Empty;
}
