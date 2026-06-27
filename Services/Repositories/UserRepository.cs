using Microsoft.EntityFrameworkCore;
using Services.Database;

namespace Services.Repositories;

public class UserRepository
{
    private readonly DatabaseContext _dbContext;

    public UserRepository(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userIdString = userId.ToString();

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.PartitionKey == userIdString, cancellationToken);

        return user != null;
    }
}
