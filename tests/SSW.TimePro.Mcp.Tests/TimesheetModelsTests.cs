using SSW.TimePro.Mcp.Server.Models;
using FluentAssertions;

namespace SSW.TimePro.Mcp.Tests;

public class TimesheetModelsTests
{
    [Fact]
    public void TimesheetItemSummary_FromTimesheet_ShouldMapCorrectly()
    {
        // Arrange
        var item = new TimesheetItem
        {
            TimeId = 123,
            Client = "Test Client",
            Project = "Test Project",
            Date = new DateTime(2026, 1, 16),
            StartTime = new DateTime(2026, 1, 16, 9, 0, 0),
            EndTime = new DateTime(2026, 1, 16, 17, 0, 0),
            TotalTime = 8,
            Notes = "Test notes",
            IsSuggested = true,
            IsBillable = true
        };
        
        // Act
        var summary = TimesheetItemSummary.FromTimesheet(item);
        
        // Assert
        summary.TimeId.Should().Be(123);
        summary.Client.Should().Be("Test Client");
        summary.Project.Should().Be("Test Project");
        summary.Date.Should().Be(new DateTime(2026, 1, 16));
        summary.StartTime.Should().Be(new DateTime(2026, 1, 16, 9, 0, 0));
        summary.EndTime.Should().Be(new DateTime(2026, 1, 16, 17, 0, 0));
        summary.TotalTime.Should().Be(8);
        summary.HasDescription.Should().BeTrue();
        summary.IsSuggested.Should().BeTrue();
        summary.IsBillable.Should().BeTrue();
    }
    
    [Fact]
    public void TimesheetItemSummary_FromTimesheet_WithNullNotes_ShouldSetHasDescriptionFalse()
    {
        // Arrange
        var item = new TimesheetItem
        {
            TimeId = 123,
            Client = "Test Client",
            Project = "Test Project",
            Notes = null
        };
        
        // Act
        var summary = TimesheetItemSummary.FromTimesheet(item);
        
        // Assert
        summary.HasDescription.Should().BeFalse();
    }
    
    [Fact]
    public void TimesheetItemSummary_FromTimesheet_WithEmptyNotes_ShouldSetHasDescriptionFalse()
    {
        // Arrange
        var item = new TimesheetItem
        {
            TimeId = 123,
            Client = "Test Client",
            Project = "Test Project",
            Notes = "   "
        };
        
        // Act
        var summary = TimesheetItemSummary.FromTimesheet(item);
        
        // Assert
        summary.HasDescription.Should().BeFalse();
    }
}
