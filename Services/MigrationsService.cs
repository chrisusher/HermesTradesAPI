using Microsoft.Extensions.DependencyInjection;
using Services.Repositories;
using Shared.Interfaces;

namespace Services;

public class MigrationsService
{
    private readonly MigrationsRepository _migrationsRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MigrationsService> _logger;

    public MigrationsService(
        MigrationsRepository repository,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory)
    {
        _migrationsRepository = repository;
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MigrationsService>();
    }

    public async Task ApplyMigrationsAsync()
    {
        var migrations = GetMigrations();

        _logger.LogInformation("Found {migrationCount} migrations to apply", migrations.Count);

        foreach (var migration in migrations)
        {
            if (migration is null)
            {
                continue;
            }

            var typeName = migration.GetType().FullName;

            if (string.IsNullOrEmpty(migration.MigrationName))
            {
                _logger.LogWarning("Skipping migration as {typeName} does not have a name", typeName);
                continue;
            }

            try
            {
                if (await IsMigrationAppliedAsync(migration.MigrationName))
                {
                    _logger.LogInformation("Skipping migration '{migration.MigrationName}' as it has already been applied", migration.MigrationName);
                    continue;
                }

                await migration.ApplyAsync();

                await SetMigrationAppliedAsync(migration.MigrationName, typeName!, migration.CreatedOn);

                _logger.LogInformation("Migration '{migration.MigrationName}' applied successfully!", migration.MigrationName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to apply Migration '{migration.MigrationName}' due to errors.", migration.MigrationName);
            }
        }

        _logger.LogInformation("Finished applying migrations");
    }

    public async Task<List<IMigration>> GetPendingMigrationsAsync()
    {
        var migrations = GetMigrations();

        var migrationsInDatabase = await _migrationsRepository.GetMigrationsAsync();

        var migrationsToApply = migrations
            .Where(m => !migrationsInDatabase.Any(db => db.MigrationName == m.MigrationName))
            .ToList();

        return migrationsToApply;
    }

    private async Task SetMigrationAppliedAsync(string migrationName, string typeName, DateTime createdOn)
    {
        await _migrationsRepository.SetMigrationAppliedAsync(migrationName, typeName, createdOn);
    }

    private async Task<bool> IsMigrationAppliedAsync(string migrationName)
    {
        try
        {
            return await _migrationsRepository.GetMigrationAsync(migrationName) != null;
        }
        catch
        {
            return false;
        }
    }

    private List<IMigration> GetMigrations()
    {
        var migrations = new List<IMigration>();

        const string nsPrefix = "Services.";
        
        var migrationTypes = AppDomain.CurrentDomain.GetAssemblies()
                                    .SelectMany(x => x.GetTypes())
                    .Where(x => typeof(IMigration).IsAssignableFrom(x) && x.IsClass && (x.FullName?.StartsWith(nsPrefix) ?? false));

        var migrationConstructors = new List<object>
        {
            _migrationsRepository.DbContext,
            _serviceProvider,
            _loggerFactory
        }.ToArray();

        foreach (var migrationType in migrationTypes)
        {
            if (migrationType is null)
            {
                continue;
            }

            try
            {
                // Prefer DI to construct migrations so they can use injected services if needed
                object? migrationInstanceObj = null;
                try
                {
                    migrationInstanceObj = ActivatorUtilities.CreateInstance(_serviceProvider, migrationType);
                }
                catch
                {
                    // Fall back to legacy ctor pattern (DbContext, ServiceProvider, LoggerFactory)
                    migrationInstanceObj = Activator.CreateInstance(migrationType, migrationConstructors);
                }
                if (migrationInstanceObj is not IMigration migrationInstance)
                {
                    continue;
                }
                migrations.Add(migrationInstance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to create migration {migrationType}", migrationType?.FullName);
            }
        }

        migrations = migrations
            .OrderBy(x => x.CreatedOn)
            .ToList();

        return migrations;
    }
}