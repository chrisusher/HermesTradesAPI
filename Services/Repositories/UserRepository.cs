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
        return await _dbContext.Users.AnyAsync(x => x.PartitionKey == userId.ToString(), cancellationToken);
    }
}
