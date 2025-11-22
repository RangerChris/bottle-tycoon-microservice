using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace ApiGateway.Tests;

public class JwtValidationTests
{
    private const string Issuer = "https://localhost:5000";
    private const string Audience = "bottlegame";
    private readonly SymmetricSecurityKey _key = new(Encoding.UTF8.GetBytes("supersecretkeythatislongenough"));

    [Fact]
    public void ValidJwtToken_ShouldBeValidated()
    {
        // Arrange
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim("sub", "user123") }),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = _key
        };

        // Act
        var principal = tokenHandler.ValidateToken(tokenString, validationParameters, out var validatedToken);

        // Assert
        principal.ShouldNotBeNull();
        principal.Identity.ShouldNotBeNull();
        principal.Identity.IsAuthenticated.ShouldBeTrue();
        principal.FindFirst("sub")?.Value.ShouldBe("user123");
        validatedToken.ShouldBeOfType<JwtSecurityToken>();
    }

    [Fact]
    public void ExpiredJwtToken_ShouldFailValidation()
    {
        // Arrange
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim("sub", "user123") }),
            Expires = DateTime.UtcNow.AddHours(-1), // Expired
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = _key
        };

        // Act & Assert
        Should.Throw<SecurityTokenExpiredException>(() =>
            tokenHandler.ValidateToken(tokenString, validationParameters, out _));
    }

    [Fact]
    public void InvalidIssuerJwtToken_ShouldFailValidation()
    {
        // Arrange
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim("sub", "user123") }),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = "invalid-issuer",
            Audience = Audience,
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = _key
        };

        // Act & Assert
        Should.Throw<SecurityTokenInvalidIssuerException>(() =>
            tokenHandler.ValidateToken(tokenString, validationParameters, out _));
    }
}