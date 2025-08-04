using Moq;
using TallaEgg.TelegramBot.Application.Services;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _userService = new UserService(_mockUserRepository.Object);
    }

    [Fact]
    public async Task ValidateInvitationCodeAsync_WithEmptyCode_ReturnsInvalid()
    {
        // Arrange
        var code = "";

        // Act
        var result = await _userService.ValidateInvitationCodeAsync(code);

        // Assert
        Assert.False(result.isValid);
        Assert.Equal("کد دعوت وارد نشده است.", result.message);
        Assert.Null(result.invitation);
    }

    [Fact]
    public async Task ValidateInvitationCodeAsync_WithNullCode_ReturnsInvalid()
    {
        // Arrange
        string? code = null;

        // Act
        var result = await _userService.ValidateInvitationCodeAsync(code);

        // Assert
        Assert.False(result.isValid);
        Assert.Equal("کد دعوت وارد نشده است.", result.message);
        Assert.Null(result.invitation);
    }

    [Fact]
    public async Task ValidateInvitationCodeAsync_WithValidCode_ReturnsValid()
    {
        // Arrange
        var code = "VALID123";
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            Code = code,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            MaxUses = 10,
            UsedCount = 5
        };

        _mockUserRepository.Setup(x => x.GetInvitationByCodeAsync(code))
            .ReturnsAsync(invitation);

        // Act
        var result = await _userService.ValidateInvitationCodeAsync(code);

        // Assert
        Assert.True(result.isValid);
        Assert.Equal("کد دعوت معتبر است.", result.message);
        Assert.NotNull(result.invitation);
        Assert.Equal(code, result.invitation.Code);
    }

    [Fact]
    public async Task ValidateInvitationCodeAsync_WithExpiredCode_ReturnsInvalid()
    {
        // Arrange
        var code = "EXPIRED123";
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            Code = code,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            IsActive = true
        };

        _mockUserRepository.Setup(x => x.GetInvitationByCodeAsync(code))
            .ReturnsAsync(invitation);

        // Act
        var result = await _userService.ValidateInvitationCodeAsync(code);

        // Assert
        Assert.False(result.isValid);
        Assert.Equal("کد دعوت منقضی شده است.", result.message);
    }

    [Fact]
    public async Task ValidateInvitationCodeAsync_WithMaxUsesReached_ReturnsInvalid()
    {
        // Arrange
        var code = "MAXED123";
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            Code = code,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            MaxUses = 5,
            UsedCount = 5
        };

        _mockUserRepository.Setup(x => x.GetInvitationByCodeAsync(code))
            .ReturnsAsync(invitation);

        // Act
        var result = await _userService.ValidateInvitationCodeAsync(code);

        // Assert
        Assert.False(result.isValid);
        Assert.Equal("کد دعوت به حداکثر تعداد استفاده رسیده است.", result.message);
    }

    [Fact]
    public async Task RegisterUserAsync_WithValidData_ReturnsUser()
    {
        // Arrange
        var telegramId = 123456789L;
        var username = "testuser";
        var firstName = "Test";
        var lastName = "User";
        var invitationCode = "VALID123";

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            Code = invitationCode,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var expectedUser = new User
        {
            Id = Guid.NewGuid(),
            TelegramId = telegramId,
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            InvitationCode = invitationCode,
            CreatedAt = DateTime.UtcNow,
            Status = UserStatus.Pending
        };

        _mockUserRepository.Setup(x => x.GetInvitationByCodeAsync(invitationCode))
            .ReturnsAsync(invitation);
        _mockUserRepository.Setup(x => x.UpdateInvitationAsync(It.IsAny<Invitation>()))
            .ReturnsAsync(invitation);
        _mockUserRepository.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(expectedUser);

        // Act
        var result = await _userService.RegisterUserAsync(telegramId, username, firstName, lastName, invitationCode);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(telegramId, result.TelegramId);
        Assert.Equal(username, result.Username);
        Assert.Equal(firstName, result.FirstName);
        Assert.Equal(lastName, result.LastName);
        Assert.Equal(invitationCode, result.InvitationCode);
        Assert.Equal(UserStatus.Pending, result.Status);
    }

    [Fact]
    public async Task RegisterUserAsync_WithInvalidInvitationCode_ThrowsException()
    {
        // Arrange
        var telegramId = 123456789L;
        var username = "testuser";
        var firstName = "Test";
        var lastName = "User";
        var invitationCode = "INVALID123";

        _mockUserRepository.Setup(x => x.GetInvitationByCodeAsync(invitationCode))
            .ReturnsAsync((Invitation?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _userService.RegisterUserAsync(telegramId, username, firstName, lastName, invitationCode));
    }

    [Fact]
    public async Task UpdateUserPhoneAsync_WithValidData_ReturnsUpdatedUser()
    {
        // Arrange
        var telegramId = 123456789L;
        var phoneNumber = "+989123456789";

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            TelegramId = telegramId,
            Username = "testuser",
            Status = UserStatus.Pending
        };

        var expectedUser = new User
        {
            Id = existingUser.Id,
            TelegramId = telegramId,
            Username = "testuser",
            PhoneNumber = phoneNumber,
            Status = UserStatus.Active,
            LastActiveAt = DateTime.UtcNow
        };

        _mockUserRepository.Setup(x => x.GetByTelegramIdAsync(telegramId))
            .ReturnsAsync(existingUser);
        _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(expectedUser);

        // Act
        var result = await _userService.UpdateUserPhoneAsync(telegramId, phoneNumber);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(phoneNumber, result.PhoneNumber);
        Assert.Equal(UserStatus.Active, result.Status);
    }

    [Fact]
    public async Task UpdateUserPhoneAsync_WithNonExistentUser_ThrowsException()
    {
        // Arrange
        var telegramId = 123456789L;
        var phoneNumber = "+989123456789";

        _mockUserRepository.Setup(x => x.GetByTelegramIdAsync(telegramId))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _userService.UpdateUserPhoneAsync(telegramId, phoneNumber));
    }

    [Fact]
    public async Task UserExistsAsync_WithExistingUser_ReturnsTrue()
    {
        // Arrange
        var telegramId = 123456789L;

        _mockUserRepository.Setup(x => x.ExistsByTelegramIdAsync(telegramId))
            .ReturnsAsync(true);

        // Act
        var result = await _userService.UserExistsAsync(telegramId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task UserExistsAsync_WithNonExistentUser_ReturnsFalse()
    {
        // Arrange
        var telegramId = 123456789L;

        _mockUserRepository.Setup(x => x.ExistsByTelegramIdAsync(telegramId))
            .ReturnsAsync(false);

        // Act
        var result = await _userService.UserExistsAsync(telegramId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CreateRootUserAsync_ReturnsRootUser()
    {
        // Arrange
        var expectedUser = new User
        {
            Id = Guid.NewGuid(),
            TelegramId = 123456789,
            Username = "admin",
            FirstName = "مدیر",
            LastName = "سیستم",
            Role = UserRole.Root,
            Status = UserStatus.Active,
            InvitationCode = "ROOT2024"
        };

        _mockUserRepository.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(expectedUser);

        // Act
        var result = await _userService.CreateRootUserAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(UserRole.Root, result.Role);
        Assert.Equal(UserStatus.Active, result.Status);
        Assert.Equal("admin", result.Username);
        Assert.Equal("مدیر", result.FirstName);
        Assert.Equal("سیستم", result.LastName);
        Assert.Equal("ROOT2024", result.InvitationCode);
    }

    [Fact]
    public async Task GenerateInvitationCodeAsync_ReturnsValidCode()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };

        _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);
        _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(user);

        // Act
        var result = await _userService.GenerateInvitationCodeAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("INV", result);
        Assert.Equal(16, result.Length); // INV + 8 digits date + 4 digits random
    }

    [Fact]
    public async Task IsUserAdminAsync_WithAdminUser_ReturnsTrue()
    {
        // Arrange
        var telegramId = 123456789L;
        var user = new User
        {
            TelegramId = telegramId,
            Role = UserRole.Admin
        };

        _mockUserRepository.Setup(x => x.GetByTelegramIdAsync(telegramId))
            .ReturnsAsync(user);

        // Act
        var result = await _userService.IsUserAdminAsync(telegramId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsUserAdminAsync_WithRootUser_ReturnsTrue()
    {
        // Arrange
        var telegramId = 123456789L;
        var user = new User
        {
            TelegramId = telegramId,
            Role = UserRole.Root
        };

        _mockUserRepository.Setup(x => x.GetByTelegramIdAsync(telegramId))
            .ReturnsAsync(user);

        // Act
        var result = await _userService.IsUserAdminAsync(telegramId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsUserAdminAsync_WithRegularUser_ReturnsFalse()
    {
        // Arrange
        var telegramId = 123456789L;
        var user = new User
        {
            TelegramId = telegramId,
            Role = UserRole.User
        };

        _mockUserRepository.Setup(x => x.GetByTelegramIdAsync(telegramId))
            .ReturnsAsync(user);

        // Act
        var result = await _userService.IsUserAdminAsync(telegramId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsUserRootAsync_WithRootUser_ReturnsTrue()
    {
        // Arrange
        var telegramId = 123456789L;
        var user = new User
        {
            TelegramId = telegramId,
            Role = UserRole.Root
        };

        _mockUserRepository.Setup(x => x.GetByTelegramIdAsync(telegramId))
            .ReturnsAsync(user);

        // Act
        var result = await _userService.IsUserRootAsync(telegramId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsUserRootAsync_WithAdminUser_ReturnsFalse()
    {
        // Arrange
        var telegramId = 123456789L;
        var user = new User
        {
            TelegramId = telegramId,
            Role = UserRole.Admin
        };

        _mockUserRepository.Setup(x => x.GetByTelegramIdAsync(telegramId))
            .ReturnsAsync(user);

        // Act
        var result = await _userService.IsUserRootAsync(telegramId);

        // Assert
        Assert.False(result);
    }
} 