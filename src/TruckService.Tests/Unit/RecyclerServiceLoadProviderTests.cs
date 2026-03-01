using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Shouldly;
using TruckService.Services;
using Xunit;

namespace TruckService.Tests.Unit;

public class RecyclerServiceLoadProviderTests
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<RecyclerServiceLoadProvider>> _loggerMock;
    private readonly RecyclerServiceLoadProvider _provider;

    public RecyclerServiceLoadProviderTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<RecyclerServiceLoadProvider>>();
        _configurationMock = new Mock<IConfiguration>();

        _configurationMock.Setup(c => c["RecyclerService:Url"]).Returns("http://localhost:5002");

        _provider = new RecyclerServiceLoadProvider(
            _httpClientFactoryMock.Object,
            _loggerMock.Object,
            _configurationMock.Object);
    }

    [Fact]
    public async Task GetLoadForRecyclerAsync_WithSuccessfulResponse_ReturnsBottles()
    {
        var recyclerId = Guid.NewGuid();
        var responseContent = @"{
            ""bottlesPickedUp"": {
                ""glass"": 10,
                ""metal"": 5,
                ""plastic"": 8
            },
            ""totalPickedUp"": 23,
            ""remainingBottles"": {}
        }";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await _provider.GetLoadForRecyclerAsync(recyclerId, 50, TestContext.Current.CancellationToken);

        result.glass.ShouldBe(10);
        result.metal.ShouldBe(5);
        result.plastic.ShouldBe(8);
    }

    [Fact]
    public async Task GetLoadForRecyclerAsync_WithFailedResponse_ReturnsZeros()
    {
        var recyclerId = Guid.NewGuid();

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await _provider.GetLoadForRecyclerAsync(recyclerId, 50, TestContext.Current.CancellationToken);

        result.glass.ShouldBe(0);
        result.metal.ShouldBe(0);
        result.plastic.ShouldBe(0);
    }

    [Fact]
    public async Task GetLoadForRecyclerAsync_WithEmptyBottles_ReturnsZeros()
    {
        var recyclerId = Guid.NewGuid();
        var responseContent = @"{
            ""bottlesPickedUp"": {},
            ""totalPickedUp"": 0,
            ""remainingBottles"": {}
        }";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await _provider.GetLoadForRecyclerAsync(recyclerId, 50, TestContext.Current.CancellationToken);

        result.glass.ShouldBe(0);
        result.metal.ShouldBe(0);
        result.plastic.ShouldBe(0);
    }

    [Fact]
    public async Task GetLoadForRecyclerAsync_WithException_ReturnsZeros()
    {
        var recyclerId = Guid.NewGuid();

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await _provider.GetLoadForRecyclerAsync(recyclerId, 50, TestContext.Current.CancellationToken);

        result.glass.ShouldBe(0);
        result.metal.ShouldBe(0);
        result.plastic.ShouldBe(0);
    }

    [Fact]
    public async Task GetLoadForRecyclerAsync_WithNullBottlesPickedUp_ReturnsZeros()
    {
        var recyclerId = Guid.NewGuid();
        var responseContent = @"{
            ""bottlesPickedUp"": null,
            ""totalPickedUp"": 0
        }";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await _provider.GetLoadForRecyclerAsync(recyclerId, 50, TestContext.Current.CancellationToken);

        result.glass.ShouldBe(0);
        result.metal.ShouldBe(0);
        result.plastic.ShouldBe(0);
    }

    [Fact]
    public async Task GetLoadForRecyclerAsync_WithPartialBottles_ReturnsCorrectValues()
    {
        var recyclerId = Guid.NewGuid();
        var responseContent = @"{
            ""bottlesPickedUp"": {
                ""glass"": 15
            },
            ""totalPickedUp"": 15
        }";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await _provider.GetLoadForRecyclerAsync(recyclerId, 50, TestContext.Current.CancellationToken);

        result.glass.ShouldBe(15);
        result.metal.ShouldBe(0);
        result.plastic.ShouldBe(0);
    }

    [Fact]
    public async Task GetLoadForRecyclerAsync_WithMaxCapacity_SendsCorrectRequest()
    {
        var recyclerId = Guid.NewGuid();
        var maxCapacity = 100;
        HttpRequestMessage? capturedRequest = null;

        var responseContent = @"{
            ""bottlesPickedUp"": {
                ""glass"": 10
            },
            ""totalPickedUp"": 10
        }";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        await _provider.GetLoadForRecyclerAsync(recyclerId, maxCapacity, TestContext.Current.CancellationToken);

        capturedRequest.ShouldNotBeNull();
        capturedRequest.RequestUri.ShouldNotBeNull();
        capturedRequest.RequestUri.ToString().ShouldContain(recyclerId.ToString());
        capturedRequest.RequestUri.ToString().ShouldContain("pickup");
    }

    [Fact]
    public async Task GetLoadForRecyclerAsync_UsesConfiguredUrl()
    {
        var customUrl = "http://custom-recycler-service:5555";
        var recyclerId = Guid.NewGuid();
        HttpRequestMessage? capturedRequest = null;

        _configurationMock.Setup(c => c["RecyclerService:Url"]).Returns(customUrl);
        var customProvider = new RecyclerServiceLoadProvider(
            _httpClientFactoryMock.Object,
            _loggerMock.Object,
            _configurationMock.Object);

        var responseContent = @"{
            ""bottlesPickedUp"": {},
            ""totalPickedUp"": 0
        }";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        await customProvider.GetLoadForRecyclerAsync(recyclerId, 50, TestContext.Current.CancellationToken);

        capturedRequest.ShouldNotBeNull();
        capturedRequest.RequestUri.ShouldNotBeNull();
        capturedRequest.RequestUri.ToString().ShouldStartWith(customUrl);
    }
}