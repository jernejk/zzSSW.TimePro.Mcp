using SSW.TimePro.Mcp.Server.Configuration;
using FluentAssertions;

namespace SSW.TimePro.Mcp.Tests;

public class TimeProSettingsTests
{
    [Theory]
    [InlineData("https://api.local-sswtimepro.com:7107/", "https://api.local-sswtimepro.com:7107/")]
    [InlineData("https://ssw.local-sswtimepro.com:7107/", "https://api.local-sswtimepro.com:7107/")]
    [InlineData("https://api.staging-sswtimepro.com/", "https://api.staging-sswtimepro.com/")]
    [InlineData("https://jk.staging-sswtimepro.com/", "https://api.staging-sswtimepro.com/")]
    [InlineData("https://api.sswtimepro.com/", "https://api.sswtimepro.com/")]
    [InlineData("https://ssw.sswtimepro.com/", "https://api.sswtimepro.com/")]
    [InlineData("https://api.sswtimepro.com", "https://api.sswtimepro.com/")]
    public void NormalizeApiUrl_ShouldReplaceSubdomainWithApi(string input, string expected)
    {
        // Act
        var result = TimeProSettings.NormalizeApiUrl(input);
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void NormalizedApiUrl_ShouldUseNormalizeApiUrl()
    {
        // Arrange
        var settings = new TimeProSettings
        {
            BaseUrl = "https://ssw.sswtimepro.com/"
        };
        
        // Act
        var result = settings.NormalizedApiUrl;
        
        // Assert
        result.Should().Be("https://api.sswtimepro.com/");
    }
    
    [Fact]
    public void AppName_ShouldBeFixed()
    {
        // Arrange
        var settings = new TimeProSettings();

        // Assert
        settings.AppName.Should().Be("SSW-TimePro-MCP");
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeApiUrl_ShouldHandleNullOrEmptyInput(string? input)
    {
        // Act
        var result = TimeProSettings.NormalizeApiUrl(input!);
        
        // Assert
        result.Should().Be(input);
    }
}
