public static class TestContext
{
    public static TestContextInstance Current { get; } = new();

    public sealed class TestContextInstance
    {
        public CancellationToken CancellationToken => CancellationToken.None;
    }
}