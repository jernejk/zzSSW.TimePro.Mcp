using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SSW.TimePro.Mcp.Server.Services.Git;

/// <summary>
/// Service for scanning local git repositories.
/// </summary>
public interface ILocalGitService
{
    /// <summary>
    /// Scan a local git repository for commits.
    /// </summary>
    Task<List<GitCommit>> ScanRepositoryAsync(
        string repositoryPath,
        DateOnly startDate,
        DateOnly endDate,
        string? author = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a directory is a git repository.
    /// </summary>
    bool IsGitRepository(string path);
    
    /// <summary>
    /// Get the current branch name.
    /// </summary>
    Task<string?> GetCurrentBranchAsync(string repositoryPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get uncommitted changes (for work in progress).
    /// </summary>
    Task<bool> HasUncommittedChangesAsync(string repositoryPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the repository name from the path or remote.
    /// </summary>
    Task<string> GetRepositoryNameAsync(string repositoryPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of local git scanning using git CLI.
/// </summary>
public partial class LocalGitService : ILocalGitService
{
    // Format: hash|author|email|date|message
    private const string GitLogFormat = "%H|%an|%ae|%aI|%s";
    
    public async Task<List<GitCommit>> ScanRepositoryAsync(
        string repositoryPath,
        DateOnly startDate,
        DateOnly endDate,
        string? author = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsGitRepository(repositoryPath))
        {
            throw new InvalidOperationException($"'{repositoryPath}' is not a git repository.");
        }

        var repoName = await GetRepositoryNameAsync(repositoryPath, cancellationToken);
        var branch = await GetCurrentBranchAsync(repositoryPath, cancellationToken);

        // Default to current git user if no author specified
        var effectiveAuthor = author;
        if (string.IsNullOrEmpty(effectiveAuthor))
        {
            effectiveAuthor = await GetCurrentUserEmailAsync(repositoryPath, cancellationToken);
        }

        var args = $"log --all --format=\"{GitLogFormat}\" --since=\"{startDate:yyyy-MM-dd}\" --until=\"{endDate.AddDays(1):yyyy-MM-dd}\"";

        if (!string.IsNullOrEmpty(effectiveAuthor))
        {
            args += $" --author=\"{effectiveAuthor}\"";
        }
        
        var output = await RunGitCommandAsync(repositoryPath, args, cancellationToken);
        
        var commits = new List<GitCommit>();
        
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|', 5);
            if (parts.Length >= 5)
            {
                commits.Add(new GitCommit
                {
                    Hash = parts[0].Trim(),
                    Author = parts[1].Trim(),
                    Email = parts[2].Trim(),
                    Date = DateTime.TryParse(parts[3].Trim(), out var date) ? date : DateTime.MinValue,
                    Message = parts[4].Trim(),
                    Repository = repoName,
                    Branch = branch,
                    Source = GitSource.Local
                });
            }
        }
        
        return commits.Where(c => 
            DateOnly.FromDateTime(c.Date) >= startDate && 
            DateOnly.FromDateTime(c.Date) <= endDate
        ).ToList();
    }
    
    public bool IsGitRepository(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;
        
        var gitDir = Path.Combine(path, ".git");
        return Directory.Exists(gitDir) || File.Exists(gitDir); // .git can be a file for worktrees
    }
    
    public async Task<string?> GetCurrentBranchAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var output = await RunGitCommandAsync(repositoryPath, "rev-parse --abbrev-ref HEAD", cancellationToken);
            return output.Trim();
        }
        catch
        {
            return null;
        }
    }
    
    public async Task<bool> HasUncommittedChangesAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var output = await RunGitCommandAsync(repositoryPath, "status --porcelain", cancellationToken);
            return !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> GetCurrentUserEmailAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var output = await RunGitCommandAsync(repositoryPath, "config user.email", cancellationToken);
            return output.Trim();
        }
        catch
        {
            return null;
        }
    }
    
    public async Task<string> GetRepositoryNameAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get from remote URL first
            var remoteUrl = await RunGitCommandAsync(repositoryPath, "config --get remote.origin.url", cancellationToken);
            remoteUrl = remoteUrl.Trim();
            
            if (!string.IsNullOrEmpty(remoteUrl))
            {
                return ExtractRepoNameFromUrl(remoteUrl);
            }
        }
        catch
        {
            // Ignore
        }
        
        // Fall back to directory name
        return new DirectoryInfo(repositoryPath).Name;
    }
    
    private static string ExtractRepoNameFromUrl(string url)
    {
        // Handle SSH URLs like git@github.com:user/repo.git
        var sshMatch = SshUrlRegex().Match(url);
        if (sshMatch.Success)
        {
            return sshMatch.Groups[2].Value.TrimSuffix(".git");
        }
        
        // Handle HTTPS URLs
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath.TrimStart('/').TrimSuffix(".git");
            return path;
        }
        catch
        {
            // If parsing fails, just return the last part
            return url.Split('/').Last().TrimSuffix(".git");
        }
    }
    
    private static async Task<string> RunGitCommandAsync(
        string workingDirectory,
        string arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        
        return output;
    }

    [GeneratedRegex(@"git@[\w.-]+:([\w.-]+)/([\w.-]+)")]
    private static partial Regex SshUrlRegex();
}

// Extension method for string trimming
internal static class StringExtensions
{
    public static string TrimSuffix(this string str, string suffix)
    {
        if (str.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return str[..^suffix.Length];
        }
        return str;
    }
}
