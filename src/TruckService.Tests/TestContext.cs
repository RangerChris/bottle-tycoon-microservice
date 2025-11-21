public static class TestContext
{
    public static TestContextInstance Current { get; } = new();
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