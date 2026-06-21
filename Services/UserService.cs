using Services.Repositories;

namespace Services;

public sealed class UserService
{
    private readonly UserRepository _userRepository;

    public UserService(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _userRepository.UserExistsAsync(userId, cancellationToken);
    }
}
