using Services.Database;

namespace CLI.Commands.Infrastructure;

/// <summary>
/// Commands for managing the database infrastructure.
/// </summary>
public class DatabaseCommand : CommandBase
{
    public static Command Create()
    {
        var dbCommand = new Command("database", "Commands for managing the database");
        var migrateCommand = new Command("migrate", "Run process to run database schema migrations");

        var pendingOption = new Option<bool>(
            "--pending",
            () => false,
            "Only lists pending migrations without applying them");

        migrateCommand.AddOption(pendingOption);

        migrateCommand.SetHandler(async (bool pending) =>
        {
            var command = new DatabaseCommand();
            await command.RunMigrateAsync(pending);
        }, pendingOption);
        
        dbCommand.AddCommand(migrateCommand);

        return dbCommand;
    }

    private async Task RunMigrateAsync(bool pendingOnly)
    {
        var logger = GetService<ILogger<DatabaseCommand>>();

        try
        {
            logger.LogInformation("=== Migrate operation started ===");
            var dbContext = GetService<DatabaseContext>();
            await dbContext.Database.EnsureCreatedAsync();

            logger.LogInformation("=== Running IMigrations... ===");

            var migrationsService = GetMigrationsService();

            var pendingMigrations = await migrationsService.GetPendingMigrationsAsync();

            logger.LogInformation("Found {PendingCount} pending migrations", pendingMigrations.Count);

            var pendingMigrationNames = pendingMigrations.Select(m => $"| {m.MigrationName} | {m.CreatedOn} |").ToList();

            Console.WriteLine("Pending Migrations:");
            Console.WriteLine("| Migration Name | Created On |");

            foreach (var migrationInfo in pendingMigrationNames)
            {
                Console.WriteLine(migrationInfo);
            }

            if (!pendingOnly)
            {
                await migrationsService.ApplyMigrationsAsync();

                logger.LogInformation("=== Completed IMigrations ===");
            }


            logger.LogInformation("=== Migrate operation completed successfully! ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migrate failed: {Message}", ex.Message);
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }
    }
}
