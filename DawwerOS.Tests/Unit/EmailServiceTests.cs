using DawwerOS.Business.Common.Settings;
using DawwerOS.Business.Services.Implementations;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DawwerOS.Tests.Unit;

public class EmailServiceTests
{
    private readonly Mock<IHostEnvironment> _envMock;

    public EmailServiceTests()
    {
        _envMock = new Mock<IHostEnvironment>();
        _envMock.Setup(e => e.EnvironmentName).Returns("Development");
    }

    [Fact]
    public async Task SendEmailAsync_WhenDisabled_SuppressesSendingAndReturnsTrue()
    {
        // Arrange
        var settings = Options.Create(new EmailSettings
        {
            EnableEmailSending = false
        });

        var service = new EmailService(settings, _envMock.Object, NullLogger<EmailService>.Instance);

        // Act
        var result = await service.SendEmailAsync("user@example.com", "Test", "Content");

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SendEmailAsync_WhenRecipientNullOrWhitespace_ReturnsFalse(string? recipient)
    {
        // Arrange
        var settings = Options.Create(new EmailSettings
        {
            EnableEmailSending = true
        });

        var service = new EmailService(settings, _envMock.Object, NullLogger<EmailService>.Instance);

        // Act
        var result = await service.SendEmailAsync(recipient!, "Test", "Content");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendEmailAsync_WhenSmtpNotConfiguredInDev_ReturnsTrueGracefully()
    {
        // Arrange
        var settings = Options.Create(new EmailSettings
        {
            EnableEmailSending = true,
            SmtpHost = "",
            SmtpUsername = ""
        });

        var service = new EmailService(settings, _envMock.Object, NullLogger<EmailService>.Instance);

        // Act
        var result = await service.SendEmailAsync("user@example.com", "Subject", "<p>Hello</p>");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SendVerificationCodeEmailAsync_WhenTriggered_ExecutesGracefully()
    {
        // Arrange
        var settings = Options.Create(new EmailSettings
        {
            EnableEmailSending = true,
            SmtpHost = "",
            SmtpUsername = ""
        });

        var service = new EmailService(settings, _envMock.Object, NullLogger<EmailService>.Instance);

        // Act
        var result = await service.SendVerificationCodeEmailAsync("user@example.com", "أحمد", "123456", 15);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_WhenTriggered_ExecutesGracefully()
    {
        // Arrange
        var settings = Options.Create(new EmailSettings
        {
            EnableEmailSending = true,
            SmtpHost = "",
            SmtpUsername = ""
        });

        var service = new EmailService(settings, _envMock.Object, NullLogger<EmailService>.Instance);

        // Act
        var result = await service.SendPasswordResetEmailAsync("user@example.com", "خالد", "654321", 15);

        // Assert
        result.Should().BeTrue();
    }
}
