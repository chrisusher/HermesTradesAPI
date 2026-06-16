using Microsoft.EntityFrameworkCore;
using Services.Database;

namespace Services.Repositories;

public class MigrationsRepository
{
    private readonly DatabaseContext _databaseContext;

    public DatabaseContext DbContext => _databaseContext;

    public MigrationsRepository(
        DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task<MigrationTable?> GetMigrationAsync(string migrationName)
    {
        return await _databaseContext.Migrations.FirstOrDefaultAsync(x => x.MigrationName == migrationName);
    }

    public async Task<List<MigrationTable>> GetMigrationsAsync()
    {
        return await _databaseContext.Migrations.ToListAsync();
    }

    public async Task SetMigrationAppliedAsync(string migrationName, string typeName, DateTime migrationDate)
    {
        var migration = new MigrationTable
        {
            MigrationName = migrationName,
            MigrationTypeName = typeName,
            CreatedOn = DateTime.UtcNow,
            MigrationDate = migrationDate
        };
        _databaseContext.Migrations.Add(migration);

        await _databaseContext.SaveChangesAsync();
    }
}
