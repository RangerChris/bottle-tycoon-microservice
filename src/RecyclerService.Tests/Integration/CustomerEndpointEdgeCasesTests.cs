﻿using System.Net;
using System.Net.Http.Json;
using RecyclerService.Tests.TestFixtures;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Integration;

public class CustomerEndpointEdgeCasesTests : IClassFixture<TestcontainersFixture>
{
    private readonly TestcontainersFixture _fixture;

    public CustomerEndpointEdgeCasesTests(TestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CustomerArrived_WhenRecyclerBecomesFull_ResponseReflectsCapacity()
    {
        var client = _fixture.Client;

        var recyclerId = Guid.NewGuid();
        var createRequest = new CreateRequest(recyclerId, "High Capacity", 50, null);
        var createRes = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);

        var customer1 = new CustomerRequest { Bottles = 40, CustomerType = "Bulk" };
        var res1 = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/customers", customer1, TestContext.Current.CancellationToken);
        res1.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body1 = await res1.Content.ReadFromJsonAsync<CustomerResponse>(TestContext.Current.CancellationToken);
        body1.ShouldNotBeNull();
        body1.CurrentLoad.ShouldBe(40);

        var customer2 = new CustomerRequest { Bottles = 20, CustomerType = "Overflow" };
        var res2 = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/customers", customer2, TestContext.Current.CancellationToken);
        res2.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body2 = await res2.Content.ReadFromJsonAsync<CustomerResponse>(TestContext.Current.CancellationToken);
        body2.ShouldNotBeNull();
        body2.CurrentLoad.ShouldBe(60);
        body2.Capacity.ShouldBe(50);
    }

    [Fact]
    public async Task CustomerArrived_InvalidPayload_ReturnsBadRequest()
    {
        var client = _fixture.Client;

        var recyclerId = Guid.NewGuid();
        var createRequest = new CreateRequest(recyclerId, "Validator", 30, "Aisle 2");
        var createRes = await client.PostAsJsonAsync("/recyclers", createRequest, TestContext.Current.CancellationToken);
        createRes.EnsureSuccessStatusCode();

        var invalidPayload = new { CustomerType = "Invalid" };
        var res = await client.PostAsJsonAsync($"/recyclers/{recyclerId}/customers", invalidPayload, TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var text = await res.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        text.ShouldContain("Bottles");
    }


    private sealed record CreateRequest(Guid Id, string Name, int Capacity, string? Location);

    private sealed record CustomerRequest
    {
        public int Bottles { get; set; }
        public string? CustomerType { get; set; }
    }

    private sealed record CustomerResponse
    {
        public Guid RecyclerId { get; set; }
        public int CurrentLoad { get; set; }
        public int Capacity { get; set; }
    }
}