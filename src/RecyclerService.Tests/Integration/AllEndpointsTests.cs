using System.Net;
using System.Net.Http.Json;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Integration;

public class AllEndpointsTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public AllEndpointsTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateRecycler_ShouldReturnCreatedWithBody()
    {
        var client = _fixture.Client;

        var createRequest = new CreateRequest(Guid.NewGuid(), "Endpoint Test Recycler", 150, "Zone A");
        var response = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        var body = await response.Content.ReadFromJsonAsync<CreateResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Id.ShouldBe(createRequest.Id);
        body.Name.ShouldBe(createRequest.Name);
        body.Capacity.ShouldBe(createRequest.Capacity);
        body.CurrentLoad.ShouldBe(0);
        body.Location.ShouldBe(createRequest.Location);
    }

    [Fact]
    public async Task GetRecycler_ShouldReturnPersistedEntity()
    {
        var client = _fixture.Client;

        var createRequest = new CreateRequest(Guid.NewGuid(), "Fetcher", 80, "Zone B");
        var createRes = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createRes.EnsureSuccessStatusCode();

        var getRes = await client.GetAsync($"/recyclers/{createRequest.Id}", TestContext.Current.CancellationToken);
        getRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await getRes.Content.ReadFromJsonAsync<GetResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Id.ShouldBe(createRequest.Id);
        body.Name.ShouldBe(createRequest.Name);
        body.Capacity.ShouldBe(createRequest.Capacity);
    }

    [Fact]
    public async Task GetRecycler_NotFound_ShouldReturn404()
    {
        var client = _fixture.Client;

        var res = await client.GetAsync($"/recyclers/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var text = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        text.ShouldContain("Recycler not found");
    }

    [Fact]
    public async Task VisitorArrived_ShouldIncreaseCurrentLoad()
    {
        var client = _fixture.Client;

        var recyclerId = Guid.NewGuid();
        var createRequest = new CreateRequest(recyclerId, "Visitor Target", 60, "Zone C");
        var createRes = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createRes.EnsureSuccessStatusCode();

        var visitorReq = new VisitorRequest { Bottles = 25, VisitorType = "Regular" };
        var visitorRes = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/visitors", visitorReq, TestContext.Current.CancellationToken);
        visitorRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var visitorBody = await visitorRes.Content.ReadFromJsonAsync<VisitorResponse>(TestContext.Current.CancellationToken);
        visitorBody.ShouldNotBeNull();
        visitorBody.RecyclerId.ShouldBe(recyclerId);
        visitorBody.CurrentLoad.ShouldBe(25);
        visitorBody.Capacity.ShouldBe(60);
    }

    [Fact]
    public async Task VisitorArrived_InvalidRecycler_ShouldReturn404()
    {
        var client = _fixture.Client;

        var visitorReq = new VisitorRequest { Bottles = 10, VisitorType = "WalkIn" };
        var res = await client.PostAsJsonAsync($"/recyclers/{Guid.NewGuid()}/visitors", visitorReq, TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var text = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        text.ShouldContain("Recycler not found");
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthy()
    {
        var client = _fixture.Client;

        var res = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<HealthResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Status.ShouldNotBeNullOrWhiteSpace();
        body.Checks.ShouldNotBeNull();
    }

    private sealed record CreateRequest(Guid Id, string Name, int Capacity, string? Location);

    private sealed record CreateResponse(Guid Id, string Name, int Capacity, int CurrentLoad, string? Location);

    private sealed record GetResponse(Guid Id, string Name, int CurrentLoad, int Capacity);

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

    private sealed record HealthResponse(string Status, Dictionary<string, string> Checks);
}