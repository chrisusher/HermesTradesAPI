using System.ComponentModel.DataAnnotations;

namespace Services.Database;

public class MigrationTable
{
    [Key]
    public string MigrationId { get; set; } = Guid.NewGuid().ToString(); 

    [Required]
    public string MigrationName { get; set; } = string.Empty;

    [Required]
    public string MigrationTypeName { get; set; } = string.Empty;

    [Required]
    public DateTime CreatedOn { get; set; }

    [Required]
    public DateTime MigrationDate { get; set; }
}