namespace HeadquartersService.Tests;

public static class TestContext
{
    public static TestContextInstance Current { get; } = new();

    // Convenience helpers used by tests
    public static Random Rng { get; } = new();

    public static Guid RandomGuid()
    {
        return Guid.NewGuid();
    }

    public sealed class TestContextInstance
    {
        public CancellationToken CancellationToken => CancellationToken.None;

        public Guid RandomGuid()
        {
            return Guid.NewGuid();
        }
    }
}