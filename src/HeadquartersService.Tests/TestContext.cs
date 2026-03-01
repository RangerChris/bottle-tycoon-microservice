namespace HeadquartersService.Tests;

public static class TestContext
{
    public sealed class TestContextInstance
    {
        public static CancellationToken CancellationToken => CancellationToken.None;
    }
}