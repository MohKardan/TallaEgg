using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Tests.Models;

public class InvitationTests
{
    [Fact]
    public void Invitation_DefaultValues_AreCorrect()
    {
        // Act
        var invitation = new Invitation();

        // Assert
        Assert.Equal(Guid.Empty, invitation.Id);
        Assert.Equal("", invitation.Code);
        Assert.Equal(Guid.Empty, invitation.CreatedByUserId);
        Assert.Equal(DateTime.MinValue, invitation.CreatedAt);
        Assert.Null(invitation.ExpiresAt);
        Assert.Equal(-1, invitation.MaxUses);
        Assert.Equal(0, invitation.UsedCount);
        Assert.True(invitation.IsActive);
    }

    [Fact]
    public void Invitation_WithValidData_PropertiesAreSet()
    {
        // Arrange
        var id = Guid.NewGuid();
        var code = "INV202412011234";
        var createdByUserId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var maxUses = 10;
        var usedCount = 5;
        var isActive = true;

        // Act
        var invitation = new Invitation
        {
            Id = id,
            Code = code,
            CreatedByUserId = createdByUserId,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            MaxUses = maxUses,
            UsedCount = usedCount,
            IsActive = isActive
        };

        // Assert
        Assert.Equal(id, invitation.Id);
        Assert.Equal(code, invitation.Code);
        Assert.Equal(createdByUserId, invitation.CreatedByUserId);
        Assert.Equal(createdAt, invitation.CreatedAt);
        Assert.Equal(expiresAt, invitation.ExpiresAt);
        Assert.Equal(maxUses, invitation.MaxUses);
        Assert.Equal(usedCount, invitation.UsedCount);
        Assert.Equal(isActive, invitation.IsActive);
    }

    [Fact]
    public void Invitation_WithUnlimitedUses_HasMaxUsesMinusOne()
    {
        // Arrange
        var invitation = new Invitation
        {
            MaxUses = -1
        };

        // Assert
        Assert.Equal(-1, invitation.MaxUses);
        Assert.True(invitation.MaxUses < 0);
    }

    [Fact]
    public void Invitation_WithLimitedUses_HasPositiveMaxUses()
    {
        // Arrange
        var maxUses = 5;
        var invitation = new Invitation
        {
            MaxUses = maxUses
        };

        // Assert
        Assert.Equal(maxUses, invitation.MaxUses);
        Assert.True(invitation.MaxUses > 0);
    }

    [Fact]
    public void Invitation_IsExpired_WhenExpiresAtIsInPast()
    {
        // Arrange
        var invitation = new Invitation
        {
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        var isExpired = invitation.ExpiresAt < DateTime.UtcNow;

        // Assert
        Assert.True(isExpired);
    }

    [Fact]
    public void Invitation_IsNotExpired_WhenExpiresAtIsInFuture()
    {
        // Arrange
        var invitation = new Invitation
        {
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var isExpired = invitation.ExpiresAt < DateTime.UtcNow;

        // Assert
        Assert.False(isExpired);
    }

    [Fact]
    public void Invitation_IsNotExpired_WhenExpiresAtIsNull()
    {
        // Arrange
        var invitation = new Invitation
        {
            ExpiresAt = null
        };

        // Assert
        Assert.Null(invitation.ExpiresAt);
    }

    [Fact]
    public void Invitation_IsAtMaxUses_WhenUsedCountEqualsMaxUses()
    {
        // Arrange
        var maxUses = 5;
        var usedCount = 5;
        var invitation = new Invitation
        {
            MaxUses = maxUses,
            UsedCount = usedCount
        };

        // Act
        var isAtMaxUses = invitation.UsedCount >= invitation.MaxUses;

        // Assert
        Assert.True(isAtMaxUses);
    }

    [Fact]
    public void Invitation_IsNotAtMaxUses_WhenUsedCountLessThanMaxUses()
    {
        // Arrange
        var maxUses = 5;
        var usedCount = 3;
        var invitation = new Invitation
        {
            MaxUses = maxUses,
            UsedCount = usedCount
        };

        // Act
        var isAtMaxUses = invitation.UsedCount >= invitation.MaxUses;

        // Assert
        Assert.False(isAtMaxUses);
    }

    [Fact]
    public void Invitation_WithUnlimitedUses_NeverReachesMaxUses()
    {
        // Arrange
        var invitation = new Invitation
        {
            MaxUses = -1,
            UsedCount = 1000
        };

        // Act
        var isAtMaxUses = invitation.UsedCount >= invitation.MaxUses;

        // Assert
        Assert.False(isAtMaxUses);
    }

    [Theory]
    [InlineData("INV202412011234")]
    [InlineData("ROOT2024")]
    [InlineData("ADMIN123")]
    public void Invitation_CodeFormats_AreValid(string code)
    {
        // Arrange
        var invitation = new Invitation { Code = code };

        // Act & Assert
        Assert.Equal(code, invitation.Code);
        Assert.NotNull(invitation.Code);
        Assert.NotEmpty(invitation.Code);
    }
} 