using Services.Repositories;

namespace Services.Tests.Repositories;

[TestFixture]
public class UserRepositoryTests
{
    private readonly UserRepository _userRepository;

    public UserRepositoryTests()
    {
        _userRepository = ServiceTestsCommon.Services.GetRequiredService<UserRepository>();
    }

    [Test]
    public async Task GetUserByIdAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var userId = ServiceTestsCommon.Config?.UserId ?? Guid.Empty; // Replace with an actual user ID in your test database

        // Act
        var user = await _userRepository.UserExistsAsync(userId);

        // Assert
        Assert.That(user, Is.True, $"User with ID {userId} should exist in the database.");
    }
}
