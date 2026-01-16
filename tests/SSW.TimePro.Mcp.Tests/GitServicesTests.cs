using FluentAssertions;
using SSW.TimePro.Mcp.Server.Services.Git;

namespace SSW.TimePro.Mcp.Tests;

public class LocalGitServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly LocalGitService _service;

    public LocalGitServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"git-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _service = new LocalGitService();
    }

    [Fact]
    public void IsGitRepository_WithGitDir_ShouldReturnTrue()
    {
        // Arrange
        var gitDir = Path.Combine(_testDir, ".git");
        Directory.CreateDirectory(gitDir);
        
        // Act
        var result = _service.IsGitRepository(_testDir);
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsGitRepository_WithoutGitDir_ShouldReturnFalse()
    {
        // Act
        var result = _service.IsGitRepository(_testDir);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsGitRepository_WithNullPath_ShouldReturnFalse()
    {
        // Act
        var result = _service.IsGitRepository(null!);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsGitRepository_WithNonExistentPath_ShouldReturnFalse()
    {
        // Act
        var result = _service.IsGitRepository("/non/existent/path");
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ScanRepositoryAsync_WithNonGitDir_ShouldThrow()
    {
        // Act
        var act = () => _service.ScanRepositoryAsync(
            _testDir, 
            DateOnly.FromDateTime(DateTime.Today), 
            DateOnly.FromDateTime(DateTime.Today));
        
        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*is not a git repository*");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }
}

public class GitModelsTests
{
    [Fact]
    public void GitCommit_ShortHash_ShouldReturnFirst7Characters()
    {
        // Arrange
        var commit = new GitCommit
        {
            Hash = "abc123456789def"
        };
        
        // Assert
        commit.ShortHash.Should().Be("abc1234");
    }

    [Fact]
    public void GitCommit_ShortHash_WithShortHash_ShouldReturnFullHash()
    {
        // Arrange
        var commit = new GitCommit
        {
            Hash = "abc"
        };
        
        // Assert
        commit.ShortHash.Should().Be("abc");
    }

    [Fact]
    public void GitCommit_Subject_ShouldReturnFirstLine()
    {
        // Arrange
        var commit = new GitCommit
        {
            Message = "First line\nSecond line\nThird line"
        };
        
        // Assert
        commit.Subject.Should().Be("First line");
    }

    [Fact]
    public void DayActivity_TotalCommits_ShouldReturnCount()
    {
        // Arrange
        var activity = new DayActivity
        {
            Commits =
            [
                new GitCommit { Hash = "a" },
                new GitCommit { Hash = "b" },
                new GitCommit { Hash = "c" }
            ]
        };
        
        // Assert
        activity.TotalCommits.Should().Be(3);
    }

    [Fact]
    public void DayActivity_Projects_ShouldReturnDistinctRepositories()
    {
        // Arrange
        var activity = new DayActivity
        {
            Commits =
            [
                new GitCommit { Hash = "a", Repository = "repo1" },
                new GitCommit { Hash = "b", Repository = "repo1" },
                new GitCommit { Hash = "c", Repository = "repo2" }
            ]
        };
        
        // Assert
        activity.Projects.Should().HaveCount(2);
        activity.Projects.Should().Contain("repo1");
        activity.Projects.Should().Contain("repo2");
    }

    [Fact]
    public void DayActivity_FirstActivity_ShouldReturnEarliestDate()
    {
        // Arrange
        var early = DateTime.Today.AddHours(9);
        var late = DateTime.Today.AddHours(17);
        
        var activity = new DayActivity
        {
            Commits =
            [
                new GitCommit { Hash = "a", Date = late },
                new GitCommit { Hash = "b", Date = early }
            ]
        };
        
        // Assert
        activity.FirstActivity.Should().Be(early);
        activity.LastActivity.Should().Be(late);
    }

    [Fact]
    public void GitScanResult_Repositories_ShouldReturnDistinctList()
    {
        // Arrange
        var result = new GitScanResult
        {
            Commits =
            [
                new GitCommit { Hash = "a", Repository = "repo1", Source = GitSource.Local },
                new GitCommit { Hash = "b", Repository = "repo1", Source = GitSource.GitHub },
                new GitCommit { Hash = "c", Repository = "repo2", Source = GitSource.Local }
            ]
        };
        
        // Assert
        result.Repositories.Should().HaveCount(2);
        result.Sources.Should().HaveCount(2);
    }
}
