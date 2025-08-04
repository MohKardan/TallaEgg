using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Tests.Models;

public class UserTests
{
    [Fact]
    public void User_DefaultValues_AreCorrect()
    {
        // Act
        var user = new User();

        // Assert
        Assert.Equal(Guid.Empty, user.Id);
        Assert.Equal(0, user.TelegramId);
        Assert.Null(user.PhoneNumber);
        Assert.Null(user.Username);
        Assert.Null(user.FirstName);
        Assert.Null(user.LastName);
        Assert.Null(user.InvitedByUserId);
        Assert.Null(user.InvitationCode);
        Assert.Equal(DateTime.MinValue, user.CreatedAt);
        Assert.Null(user.LastActiveAt);
        Assert.True(user.IsActive);
        Assert.Equal(UserStatus.Pending, user.Status);
        Assert.Equal(UserRole.User, user.Role);
        Assert.Null(user.InvitationCodeGenerated);
    }

    [Fact]
    public void User_WithValidData_PropertiesAreSet()
    {
        // Arrange
        var id = Guid.NewGuid();
        var telegramId = 123456789L;
        var phoneNumber = "+989123456789";
        var username = "testuser";
        var firstName = "Test";
        var lastName = "User";
        var invitedByUserId = Guid.NewGuid();
        var invitationCode = "INV123";
        var createdAt = DateTime.UtcNow;
        var lastActiveAt = DateTime.UtcNow;
        var isActive = true;
        var status = UserStatus.Active;
        var role = UserRole.Admin;
        var invitationCodeGenerated = "INV202412011234";

        // Act
        var user = new User
        {
            Id = id,
            TelegramId = telegramId,
            PhoneNumber = phoneNumber,
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            InvitedByUserId = invitedByUserId,
            InvitationCode = invitationCode,
            CreatedAt = createdAt,
            LastActiveAt = lastActiveAt,
            IsActive = isActive,
            Status = status,
            Role = role,
            InvitationCodeGenerated = invitationCodeGenerated
        };

        // Assert
        Assert.Equal(id, user.Id);
        Assert.Equal(telegramId, user.TelegramId);
        Assert.Equal(phoneNumber, user.PhoneNumber);
        Assert.Equal(username, user.Username);
        Assert.Equal(firstName, user.FirstName);
        Assert.Equal(lastName, user.LastName);
        Assert.Equal(invitedByUserId, user.InvitedByUserId);
        Assert.Equal(invitationCode, user.InvitationCode);
        Assert.Equal(createdAt, user.CreatedAt);
        Assert.Equal(lastActiveAt, user.LastActiveAt);
        Assert.Equal(isActive, user.IsActive);
        Assert.Equal(status, user.Status);
        Assert.Equal(role, user.Role);
        Assert.Equal(invitationCodeGenerated, user.InvitationCodeGenerated);
    }

    [Fact]
    public void UserStatus_EnumValues_AreCorrect()
    {
        // Assert
        Assert.Equal(0, (int)UserStatus.Pending);
        Assert.Equal(1, (int)UserStatus.Active);
        Assert.Equal(2, (int)UserStatus.Blocked);
    }

    [Fact]
    public void UserRole_EnumValues_AreCorrect()
    {
        // Assert
        Assert.Equal(0, (int)UserRole.User);
        Assert.Equal(1, (int)UserRole.Admin);
        Assert.Equal(2, (int)UserRole.Root);
    }
} 