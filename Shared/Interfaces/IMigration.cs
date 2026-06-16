namespace Shared.Interfaces;

public interface IMigration
{
    string MigrationName { get; }

    DateTime CreatedOn { get; }

    Task ApplyAsync();
}