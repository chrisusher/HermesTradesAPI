using Microsoft.EntityFrameworkCore;

namespace Services.Database;

public class DatabaseContext : DbContext
{
    public DatabaseContext()
    {
    }

    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MigrationTable>()
            .ToContainer("Migrations")
            .HasPartitionKey(pk => pk.MigrationId)
            .Property(p => p.MigrationId)
            .ToJsonProperty("id");

        modelBuilder.Entity<PortfolioTable>()
            .ToContainer("Portfolio")
            .HasPartitionKey(pk => pk.PartitionKey)
            .HasDiscriminator<string>("Discriminator")
            .HasValue<PortfolioTable>("PortfolioTable");        

        // Individual portfolio holdings (one document per symbol) to prevent large
        // embedded arrays on Portfolio documents exceeding Cosmos 2MB limit.
        modelBuilder.Entity<PortfolioHoldingTable>()
            .ToContainer("PortfolioHoldings")
            .HasPartitionKey(pk => pk.PartitionKey)
            .HasDiscriminator<string>("Discriminator")
            .HasValue<PortfolioHoldingTable>("PortfolioHoldingTable");

        modelBuilder.Entity<SettingsTable>()
            .ToContainer("Settings")
            .HasPartitionKey(pk => pk.PartitionKey)
            .HasDiscriminator<string>("Discriminator")
            .HasValue<SettingsTable>("SettingsTable");

        modelBuilder.Entity<StrategyTable>()
            .ToContainer("Strategies")
            .HasPartitionKey(pk => pk.PartitionKey)
            .HasDiscriminator<string>("Discriminator")
            .HasValue<StrategyTable>("StrategyTable");

        modelBuilder.Entity<StrategyVersionTable>()
            .ToContainer("StrategyVersions")
            .HasPartitionKey(pk => pk.PartitionKey)
            .HasDiscriminator<string>("Discriminator")
            .HasValue<StrategyVersionTable>("StrategyVersionTable");

        modelBuilder.Entity<TransactionsTable>()
            .ToContainer("Transactions")
            .HasPartitionKey(pk => pk.PartitionKey)
            .HasDiscriminator<string>("Discriminator")
            .HasValue<TransactionsTable>("TransactionTable");

        modelBuilder.Entity<UserTable>()
            .ToContainer("Users")
            .HasPartitionKey(pk => pk.PartitionKey)
            .HasDiscriminator<string>("Discriminator")
            .HasValue<UserTable>("UserTable");

        // modelBuilder.Entity<PortfolioHistoryTable>()
        //     .ToContainer("PortfolioHistory")
        //     .HasPartitionKey(pk => pk.StrategyId)
        //     .Property(p => p.StrategyId)
        //     .ToJsonProperty("id");
    }

    public DbSet<MigrationTable> Migrations { get; set; }

    public DbSet<PortfolioTable> Portfolio { get; set; }

    public DbSet<PortfolioHoldingTable> PortfolioHoldings { get; set; }

    public DbSet<PortfolioHistoryTable> PortfolioHistory { get; set; }

    public DbSet<StrategyTable> Strategies { get; set; }

    public DbSet<StrategyVersionTable> StrategyVersions { get; set; }

    public DbSet<TransactionsTable> Transactions { get; set; }

    public DbSet<UserTable> Users { get; set; }
}
