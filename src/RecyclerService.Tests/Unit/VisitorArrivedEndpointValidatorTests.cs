using RecyclerService.Endpoints;
using Shouldly;
using Xunit;

namespace RecyclerService.Tests.Unit;

public class VisitorArrivedEndpointValidatorTests
{
    private readonly VisitorArrivedEndpoint.RequestValidator _validator;

    public VisitorArrivedEndpointValidatorTests()
    {
        _validator = new VisitorArrivedEndpoint.RequestValidator();
    }

    [Fact]
    public void Validate_WithPositiveBottles_IsValid()
    {
        var request = new VisitorArrivedEndpoint.Request { Bottles = 10 };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithPositiveGlass_IsValid()
    {
        var request = new VisitorArrivedEndpoint.Request { Glass = 5 };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithPositiveMetal_IsValid()
    {
        var request = new VisitorArrivedEndpoint.Request { Metal = 8 };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithPositivePlastic_IsValid()
    {
        var request = new VisitorArrivedEndpoint.Request { Plastic = 12 };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithBottleCountsDictionary_IsValid()
    {
        var request = new VisitorArrivedEndpoint.Request
        {
            BottleCounts = new Dictionary<string, int>
            {
                { "glass", 10 },
                { "metal", 5 }
            }
        };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithMultiplePositiveFields_IsValid()
    {
        var request = new VisitorArrivedEndpoint.Request
        {
            Glass = 5,
            Metal = 3,
            Plastic = 7
        };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithZeroBottles_AndNoOtherCounts_IsInvalid()
    {
        var request = new VisitorArrivedEndpoint.Request { Bottles = 0 };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void Validate_WithNegativeBottles_AndNoOtherCounts_IsInvalid()
    {
        var request = new VisitorArrivedEndpoint.Request { Bottles = -5 };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WithEmptyBottleCounts_AndZeroBottles_IsInvalid()
    {
        var request = new VisitorArrivedEndpoint.Request
        {
            Bottles = 0,
            BottleCounts = new Dictionary<string, int>()
        };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WithZeroValueBottleCounts_IsInvalid()
    {
        var request = new VisitorArrivedEndpoint.Request
        {
            BottleCounts = new Dictionary<string, int>
            {
                { "glass", 0 },
                { "metal", 0 }
            }
        };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WithNullBottleCounts_AndNoOtherValues_IsInvalid()
    {
        var request = new VisitorArrivedEndpoint.Request
        {
            BottleCounts = null,
            Bottles = 0
        };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WithZeroGlassMetalPlastic_AndZeroBottles_IsInvalid()
    {
        var request = new VisitorArrivedEndpoint.Request
        {
            Bottles = 0,
            Glass = 0,
            Metal = 0,
            Plastic = 0
        };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WithNullGlassMetalPlastic_AndZeroBottles_IsInvalid()
    {
        var request = new VisitorArrivedEndpoint.Request
        {
            Bottles = 0,
            Glass = null,
            Metal = null,
            Plastic = null
        };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WithMixedBottleCountsIncludingZero_IsValid()
    {
        var request = new VisitorArrivedEndpoint.Request
        {
            BottleCounts = new Dictionary<string, int>
            {
                { "glass", 10 },
                { "metal", 0 },
                { "plastic", 5 }
            }
        };

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}