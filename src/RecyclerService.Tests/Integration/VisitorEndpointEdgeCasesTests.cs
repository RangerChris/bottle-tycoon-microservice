using System.Net;
using System.Net.Http.Json;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Integration;

public class VisitorEndpointEdgeCasesTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public VisitorEndpointEdgeCasesTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task VisitorArrived_WhenRecyclerBecomesFull_ResponseReflectsCapacity()
    {
        var client = _fixture.Client;

        var recyclerId = Guid.NewGuid();
        var createRequest = new CreateRequest(recyclerId, "High Capacity", 50, null);
        var createRes = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);

        // First visitor fills most of the capacity
        var visitor1 = new VisitorRequest { Bottles = 40, VisitorType = "Bulk" };
        var res1 = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/visitors", visitor1, TestContext.Current.CancellationToken);
        res1.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body1 = await res1.Content.ReadFromJsonAsync<VisitorResponse>(TestContext.Current.CancellationToken);
        body1.ShouldNotBeNull();
        body1.CurrentLoad.ShouldBe(40);

        // Second visitor pushes beyond capacity
        var visitor2 = new VisitorRequest { Bottles = 20, VisitorType = "Overflow" };
        var res2 = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/visitors", visitor2, TestContext.Current.CancellationToken);
        res2.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body2 = await res2.Content.ReadFromJsonAsync<VisitorResponse>(TestContext.Current.CancellationToken);
        body2.ShouldNotBeNull();
        body2.CurrentLoad.ShouldBe(60);
        body2.Capacity.ShouldBe(50);
    }

    [Fact]
    public async Task VisitorArrived_InvalidPayload_ReturnsBadRequest()
    {
        var client = _fixture.Client;

        var recyclerId = Guid.NewGuid();
        // Create recycler so endpoint exists
        var createRequest = new CreateRequest(recyclerId, "Validator", 30, "Aisle 2");
        var createRes = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createRes.EnsureSuccessStatusCode();

        // Send invalid payload (missing bottles)
        var invalidPayload = new { VisitorType = "Invalid" };
        var res = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/visitors", invalidPayload, TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var text = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        text.ShouldContain("Bottles");
    }


    private sealed record CreateRequest(Guid Id, string Name, int Capacity, string? Location);

    private sealed record VisitorRequest
    {
        public int Bottles { get; set; }
        public string? VisitorType { get; set; }
    }

    private sealed record VisitorResponse
    {
        public Guid RecyclerId { get; set; }
        public int CurrentLoad { get; set; }
        public int Capacity { get; set; }
    }
}