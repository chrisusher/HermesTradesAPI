using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Services.Database;
using Services.Repositories;

namespace CLI.Commands;

public abstract class CommandBase
{
    public static IServiceProvider? ServiceProvider { get; set; }

    protected T GetService<T>() where T : class
    {
        if (ServiceProvider == null)
        {
            throw new InvalidOperationException("Service provider not initialised");
        }

        return ServiceProvider?.GetRequiredService<T>() ??
               throw new InvalidOperationException("Service provider not initialised");
    }

    protected MigrationsService GetMigrationsService()
    {
        if (ServiceProvider == null)
        {
            throw new InvalidOperationException("Service provider not initialised");
        }

        var logger = GetService<ILoggerFactory>();
        var migrationRepo = new MigrationsRepository(new DatabaseContext(GetService<DbContextOptions<DatabaseContext>>()));

        return new MigrationsService(migrationRepo, ServiceProvider, logger);
    }
}
