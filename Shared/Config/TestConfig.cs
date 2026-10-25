namespace Shared.Config;

public class TestConfig : ChrisUsher.Core.Shared.Tests.TestConfig
{
    public Guid UserId { get; set; } = Guid.Empty;
}
