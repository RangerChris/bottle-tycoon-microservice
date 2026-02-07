﻿using System.Net;
using System.Net.Http.Json;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Integration;

public class RecyclerEndpointResponseTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public RecyclerEndpointResponseTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateRecycler_ResponseContainsLocationHeader()
    {
        var client = _fixture.Client;

        var createRequest = new CreateRequest(Guid.NewGuid(), "Recycler Alpha", 120, "Sector 7");
        var response = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldBe($"/recyclers/{createRequest.Id}");

        var body = await response.Content.ReadFromJsonAsync<CreateResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Id.ShouldBe(createRequest.Id);
        body.Name.ShouldBe(createRequest.Name);
        body.Capacity.ShouldBe(createRequest.Capacity);
        body.Location.ShouldBe(createRequest.Location);
        body.CurrentLoad.ShouldBe(0);
    }

    [Fact]
    public async Task GetRecycler_NotFound_ReturnsErrorPayload()
    {
        var client = _fixture.Client;

        var res = await client.GetAsync($"/recyclers/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("Recycler not found", Case.Sensitive);
    }

    [Fact]
    public async Task CustomerArrived_NonexistentRecycler_ReturnsErrorPayload()
    {
        var client = _fixture.Client;

        var customerRequest = new CustomerRequest { Bottles = 15, CustomerType = "WalkIn" };
        var res = await client.PostAsJsonAsync($"/recyclers/{Guid.NewGuid()}/customers", customerRequest, TestContext.Current.CancellationToken);

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("Recycler not found", Case.Sensitive);
    }


    private sealed record CreateRequest(Guid Id, string Name, int Capacity, string? Location);

    private sealed record CreateResponse(Guid Id, string Name, int Capacity, int CurrentLoad, string? Location);

    private sealed record CustomerRequest
    {
        public int Bottles { get; set; }
        public string? CustomerType { get; set; }
    }
}