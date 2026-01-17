using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SSW.TimePro.Mcp.Server.Configuration;
using SSW.TimePro.Mcp.Server.Models;
using SSW.TimePro.Mcp.Server.Services;

namespace SSW.TimePro.Mcp.Tests.Integration;

/// <summary>
/// Integration tests for TimeProService against a live local TimePro instance.
/// Note: These tests require a running local TimePro server.
/// Set the environment variable RUN_INTEGRATION_TESTS=true to run these tests.
/// </summary>
[Trait("Category", "Integration")]
public class TimeProServiceIntegrationTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly TimeProService _service;
    private readonly string _testEmployeeId = "JEK";
    
    // Skip integration tests if not explicitly enabled
    private static bool ShouldRunIntegrationTests => 
        Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS") == "true";

    public TimeProServiceIntegrationTests()
    {
        // Use production for integration tests (local might not be running)
        var settings = Options.Create(new TimeProSettings
        {
            BaseUrl = Environment.GetEnvironmentVariable("TIMEPRO_BASE_URL") 
                      ?? "https://ssw.local-sswtimepro.com:7107/",
            TenantId = Environment.GetEnvironmentVariable("TIMEPRO_TENANT_ID") 
                       ?? "ssw",
            ApiKey = Environment.GetEnvironmentVariable("TIMEPRO_API_KEY")
                     ?? throw new InvalidOperationException("TIMEPRO_API_KEY environment variable is not set")
        });
        
        _httpClient = new HttpClient();
        _service = new TimeProService(_httpClient, settings);
    }

    [SkippableFact]
    public async Task GetTimesheetsAsync_ShouldReturnTimesheets()
    {
        Skip.IfNot(ShouldRunIntegrationTests, "Integration tests are disabled");
        
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
        var endDate = DateOnly.FromDateTime(DateTime.Today);
        
        // Act
        var result = await _service.GetTimesheetsAsync(_testEmployeeId, startDate, endDate);
        
        // Assert
        result.Should().NotBeNull();
        // May or may not have timesheets depending on the user's data
    }

    [SkippableFact]
    public async Task GetTimesheetsByDaysAsync_ShouldReturnTimesheets()
    {
        Skip.IfNot(ShouldRunIntegrationTests, "Integration tests are disabled");
        
        // Act
        var result = await _service.GetTimesheetsByDaysAsync(_testEmployeeId, takeDays: 7);
        
        // Assert
        result.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task GetTimesheetCategoriesAsync_ShouldReturnCategories()
    {
        Skip.IfNot(ShouldRunIntegrationTests, "Integration tests are disabled");
        
        // Act
        var result = await _service.GetTimesheetCategoriesAsync();
        
        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(c => !string.IsNullOrEmpty(c.CategoryId));
    }

    [SkippableFact]
    public async Task GetTimesheetLocationsAsync_ShouldReturnLocations()
    {
        Skip.IfNot(ShouldRunIntegrationTests, "Integration tests are disabled");
        
        // Act
        var result = await _service.GetTimesheetLocationsAsync();
        
        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(l => l.LocationName == "At Home" || l.LocationId == "Home");
    }

    [SkippableFact]
    public async Task GetTimesheetBillableTypesAsync_ShouldReturnBillableTypes()
    {
        Skip.IfNot(ShouldRunIntegrationTests, "Integration tests are disabled");
        
        // Act
        var result = await _service.GetTimesheetBillableTypesAsync();
        
        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(b => b.Text.Contains("Billable"));
    }

    [SkippableFact]
    public async Task SearchClientsAsync_ShouldReturnClients()
    {
        Skip.IfNot(ShouldRunIntegrationTests, "Integration tests are disabled");
        
        // Act
        var result = await _service.SearchClientsAsync(_testEmployeeId, "SSW");
        
        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(c => c.Text.Contains("SSW"));
    }

    [SkippableFact]
    public async Task GetAppointmentsAsync_ShouldReturnAppointments()
    {
        Skip.IfNot(ShouldRunIntegrationTests, "Integration tests are disabled");
        
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
        var endDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
        
        // Act
        var result = await _service.GetAppointmentsAsync(_testEmployeeId, startDate, endDate);
        
        // Assert
        result.Should().NotBeNull();
        // CRM appointments may or may not exist
    }

    [SkippableFact]
    public async Task GetAddTimesheetViewAsync_ShouldReturnDefaults()
    {
        Skip.IfNot(ShouldRunIntegrationTests, "Integration tests are disabled");
        
        // Arrange
        var date = DateOnly.FromDateTime(DateTime.Today);
        
        // Act
        var result = await _service.GetAddTimesheetViewAsync(_testEmployeeId, date);
        
        // Assert
        result.Should().NotBeNull();
        result!.EmpId.Should().Be(_testEmployeeId);
    }

    [SkippableFact]
    public async Task GetSuggestedTimesheetsAsync_ShouldReturnSuggestions()
    {
        Skip.IfNot(ShouldRunIntegrationTests, "Integration tests are disabled");
        
        // Arrange
        var date = DateOnly.FromDateTime(DateTime.Today);
        
        // Act
        var result = await _service.GetSuggestedTimesheetsAsync(_testEmployeeId, date);
        
        // Assert
        result.Should().NotBeNull();
        // Suggested timesheets may or may not exist
        result.Should().OnlyContain(t => t.IsSuggested);
    }

    [Fact]
    public async Task CreateTimesheetAsync_WithDryRun_ShouldNotCreateTimesheet()
    {
        // This test doesn't need the server since dry run returns immediately
        
        // Arrange
        var request = new TimesheetRequest
        {
            EmpId = _testEmployeeId,
            ClientId = "SSW",
            ProjectId = "TEST",
            DateCreated = DateTime.Today.ToString("yyyy-MM-ddT00:00:00"),
            TimeStart = DateTime.Today.AddHours(9).ToString("yyyy-MM-ddTHH:mm:00"),
            TimeEnd = DateTime.Today.AddHours(17).ToString("yyyy-MM-ddTHH:mm:00"),
            CategoryId = "DEV",
            LocationId = "Home"
        };
        
        // Act
        var result = await _service.CreateTimesheetAsync(request, dryRun: true);
        
        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("[DRY RUN]");
        result.TimesheetId.Should().BeNull();
    }

    [Fact]
    public async Task DeleteTimesheetAsync_WithDryRun_ShouldNotDelete()
    {
        // Act
        var result = await _service.DeleteTimesheetAsync(123456, dryRun: true);
        
        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("[DRY RUN]");
        result.TimesheetId.Should().Be(123456);
    }

    [Fact]
    public async Task DeleteSuggestedTimesheetAsync_WithDryRun_ShouldNotDelete()
    {
        // Act
        var result = await _service.DeleteSuggestedTimesheetAsync(123456, dryRun: true);
        
        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("[DRY RUN]");
        result.TimesheetId.Should().Be(123456);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
