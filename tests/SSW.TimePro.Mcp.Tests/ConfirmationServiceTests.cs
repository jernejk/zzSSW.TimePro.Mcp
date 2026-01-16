using FluentAssertions;
using SSW.TimePro.Mcp.Server.Services.Confirmation;

namespace SSW.TimePro.Mcp.Tests;

public class ConfirmationServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileConfirmationService _service;

    public ConfirmationServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"confirmation-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _service = new FileConfirmationService(_testDirectory);
    }

    [Fact]
    public async Task CreateConfirmationAsync_ShouldCreatePendingConfirmation()
    {
        // Arrange
        var payload = new { Test = "value" };
        
        // Act
        var confirmation = await _service.CreateConfirmationAsync(
            ConfirmationOperationType.CreateTimesheet,
            "Test description",
            "Test preview",
            payload);
        
        // Assert
        confirmation.Id.Should().NotBeNullOrEmpty();
        confirmation.OperationType.Should().Be(ConfirmationOperationType.CreateTimesheet);
        confirmation.Description.Should().Be("Test description");
        confirmation.Preview.Should().Be("Test preview");
        confirmation.Status.Should().Be(ConfirmationStatus.Pending);
        confirmation.CanExecute.Should().BeTrue();
        confirmation.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        
        // Verify file was created
        var filePath = Path.Combine(_testDirectory, "dry-run", $"{confirmation.Id}.json");
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task GetConfirmationAsync_ShouldReturnSavedConfirmation()
    {
        // Arrange
        var original = await _service.CreateConfirmationAsync(
            ConfirmationOperationType.DeleteTimesheet,
            "Delete test",
            "Preview",
            new { Id = 123 });
        
        // Act
        var retrieved = await _service.GetConfirmationAsync(original.Id);
        
        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(original.Id);
        retrieved.OperationType.Should().Be(original.OperationType);
        retrieved.Description.Should().Be(original.Description);
    }

    [Fact]
    public async Task GetConfirmationAsync_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var result = await _service.GetConfirmationAsync("non-existent-id");
        
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListConfirmationsAsync_ShouldReturnAllConfirmations()
    {
        // Arrange
        await _service.CreateConfirmationAsync(ConfirmationOperationType.CreateTimesheet, "Test 1", "Preview 1", new { });
        await _service.CreateConfirmationAsync(ConfirmationOperationType.DeleteTimesheet, "Test 2", "Preview 2", new { });
        
        // Act
        var confirmations = await _service.ListConfirmationsAsync();
        
        // Assert
        confirmations.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListConfirmationsAsync_WithStatusFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        await _service.CreateConfirmationAsync(ConfirmationOperationType.CreateTimesheet, "Test 1", "Preview 1", new { });
        var toCancel = await _service.CreateConfirmationAsync(ConfirmationOperationType.DeleteTimesheet, "Test 2", "Preview 2", new { });
        await _service.CancelConfirmationAsync(toCancel.Id);
        
        // Act
        var pending = await _service.ListConfirmationsAsync(ConfirmationStatus.Pending);
        var cancelled = await _service.ListConfirmationsAsync(ConfirmationStatus.Cancelled);
        
        // Assert
        pending.Should().HaveCount(1);
        cancelled.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteConfirmationAsync_ShouldExecuteAndUpdateStatus()
    {
        // Arrange
        var confirmation = await _service.CreateConfirmationAsync(
            ConfirmationOperationType.CreateTimesheet,
            "Test",
            "Preview",
            new { Value = 42 });
        
        var executed = false;
        
        // Act
        var result = await _service.ExecuteConfirmationAsync(
            confirmation.Id,
            async (payload, ct) =>
            {
                executed = true;
                return new { Success = true };
            });
        
        // Assert
        result.Success.Should().BeTrue();
        executed.Should().BeTrue();
        
        var updated = await _service.GetConfirmationAsync(confirmation.Id);
        updated!.Status.Should().Be(ConfirmationStatus.Confirmed);
        updated.ExecutedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteConfirmationAsync_WhenAlreadyExecuted_ShouldFail()
    {
        // Arrange
        var confirmation = await _service.CreateConfirmationAsync(
            ConfirmationOperationType.CreateTimesheet, "Test", "Preview", new { });
        
        await _service.ExecuteConfirmationAsync(confirmation.Id, async (_, _) => new { });
        
        // Act
        var result = await _service.ExecuteConfirmationAsync(
            confirmation.Id,
            async (_, _) => new { });
        
        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already been executed");
    }

    [Fact]
    public async Task ExecuteConfirmationAsync_WhenExpired_ShouldFail()
    {
        // Arrange
        var confirmation = await _service.CreateConfirmationAsync(
            ConfirmationOperationType.CreateTimesheet,
            "Test",
            "Preview",
            new { },
            expiration: TimeSpan.FromMilliseconds(1));
        
        await Task.Delay(10); // Wait for expiration
        
        // Act
        var result = await _service.ExecuteConfirmationAsync(
            confirmation.Id,
            async (_, _) => new { });
        
        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task CancelConfirmationAsync_ShouldCancelPendingConfirmation()
    {
        // Arrange
        var confirmation = await _service.CreateConfirmationAsync(
            ConfirmationOperationType.CreateTimesheet, "Test", "Preview", new { });
        
        // Act
        var result = await _service.CancelConfirmationAsync(confirmation.Id);
        
        // Assert
        result.Should().BeTrue();
        
        var updated = await _service.GetConfirmationAsync(confirmation.Id);
        updated!.Status.Should().Be(ConfirmationStatus.Cancelled);
        updated.CanExecute.Should().BeFalse();
    }

    [Fact]
    public async Task CancelConfirmationAsync_WhenNotPending_ShouldReturnFalse()
    {
        // Arrange
        var confirmation = await _service.CreateConfirmationAsync(
            ConfirmationOperationType.CreateTimesheet, "Test", "Preview", new { });
        await _service.ExecuteConfirmationAsync(confirmation.Id, async (_, _) => new { });
        
        // Act
        var result = await _service.CancelConfirmationAsync(confirmation.Id);
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void PendingConfirmation_IsExpired_ShouldReturnCorrectValue()
    {
        // Arrange
        var notExpired = new PendingConfirmation
        {
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        
        var expired = new PendingConfirmation
        {
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };
        
        // Assert
        notExpired.IsExpired.Should().BeFalse();
        expired.IsExpired.Should().BeTrue();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }
}
