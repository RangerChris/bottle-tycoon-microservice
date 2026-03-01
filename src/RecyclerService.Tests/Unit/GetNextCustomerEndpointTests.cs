using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Xunit;
using RecyclerService.Tests.TestFixtures;
using RecyclerService.Data;
using RecyclerService.Models;
using Microsoft.Extensions.DependencyInjection;

namespace RecyclerService.Tests.Unit;

public class GetNextCustomerEndpointTests(TestcontainersFixture fixture) : IClassFixture<TestcontainersFixture>
{
    [Fact]
    public async Task GetNextCustomer_WhenNoCustomer_ReturnsEmptyResponse()
    {
        if (!fixture.Started)
        {
            return;
        }

        var client = fixture.Client;
        var recyclerRes = await client.PostAsJsonAsync("/recyclers", new { Name = "R1", Capacity = 100 }, TestContext.Current.CancellationToken);
        recyclerRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var location = recyclerRes.Headers.Location!.ToString();

        // extract recycler id
        var id = Guid.Parse(location.Split('/').Last());

        var res = await client.GetAsync($"/recyclers/{id}/next-customer", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await res.Content.ReadFromJsonAsync<NextCustomerResponse>(TestContext.Current.CancellationToken);
        payload.ShouldNotBeNull();
        payload.CustomerId.ShouldBe(Guid.Empty);
        payload.Total.ShouldBe(0);
    }

    [Fact]
    public async Task GetNextCustomer_WhenCustomerExists_ReturnsPopulatedResponse()
    {
        if (!fixture.Started)
        {
            return;
        }

        var client = fixture.Client;

        var recyclerRes = await client.PostAsJsonAsync("/recyclers", new { Name = "R2", Capacity = 100 }, TestContext.Current.CancellationToken);
        recyclerRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var location = recyclerRes.Headers.Location!.ToString();
        var id = Guid.Parse(location.Split('/').Last());

        // Insert a real Customer row into the Postgres DB via the test host's scope
        var counts = new Dictionary<string,int> { { "glass", 3 }, { "metal", 2 }, { "plastic", 1 } };
        using (var scope = fixture.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
            var ct = TestContext.Current.CancellationToken;

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                RecyclerId = id,
                BottleCountsJson = System.Text.Json.JsonSerializer.Serialize(counts),
                ArrivedAt = DateTimeOffset.UtcNow,
                Status = CustomerStatus.Waiting
            };

            await db.Customers.AddAsync(customer, ct);
            await db.SaveChangesAsync(ct);
        }

        var res = await client.GetAsync($"/recyclers/{id}/next-customer", TestContext.Current.CancellationToken);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await res.Content.ReadFromJsonAsync<NextCustomerResponse>(TestContext.Current.CancellationToken);
        payload.ShouldNotBeNull();
        payload.CustomerId.ShouldNotBe(Guid.Empty);
        payload.Total.ShouldBe(6);
        payload.Status.ShouldNotBeNull();
    }

    private class NextCustomerResponse
    {
        public Guid CustomerId { get; init; }
        public Guid RecyclerId { get; init; }
        public int Glass { get; init; }
        public int Metal { get; init; }
        public int Plastic { get; init; }
        public int Total { get; init; }
        public string? Status { get; init; }
    }
}