using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UMLForge.BuildingBlocks.Auth;
using UMLForge.Notification.API;

namespace UMLForge.Notification.Tests;

public sealed class NotificationBootstrapTests
{
    [Fact]
    public void ConfigureNotificationJwt_SharedSchemeWithNoEvents_ResolvesSecureOptions()
    {
        using var provider = Services().ConfigureNotificationJwt().BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Bearer");

        Assert.NotNull(options.Events);
        Assert.Equal(new[] { SecurityAlgorithms.HmacSha256 }, options.TokenValidationParameters.ValidAlgorithms);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.True(options.TokenValidationParameters.ValidateLifetime);
        Assert.True(options.TokenValidationParameters.RequireExpirationTime);
        Assert.True(options.TokenValidationParameters.RequireSignedTokens);
        Assert.False(options.MapInboundClaims);
    }

    [Theory, Trait("BR", "BR-33")]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task ConfigureNotificationJwt_InvalidSubject_RejectsIdentity(string? subject)
    {
        using var provider = Services().ConfigureNotificationJwt().BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Bearer");
        var context = Context(options, subject);

        await options.Events.OnTokenValidated(context);

        Assert.NotNull(context.Result?.Failure);
    }

    [Fact]
    public async Task ConfigureNotificationJwt_ValidSubject_PreservesPreviousCallback()
    {
        var invoked = false;
        var services = Services();
        services.Configure<JwtBearerOptions>("Bearer", options => options.Events = new JwtBearerEvents
        {
            OnTokenValidated = _ => { invoked = true; return Task.CompletedTask; }
        });
        using var provider = services.ConfigureNotificationJwt().BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Bearer");
        var context = Context(options, Guid.NewGuid().ToString());

        await options.Events.OnTokenValidated(context);

        Assert.True(invoked);
        Assert.Null(context.Result?.Failure);
    }

    private static ServiceCollection Services()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
            ["Jwt:Issuer"] = "umlforge", ["Jwt:Audience"] = "umlforge-client"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUmlForgeJwt(configuration);
        return services;
    }

    private static TokenValidatedContext Context(JwtBearerOptions options, string? subject)
        => new(new DefaultHttpContext(), new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)), options)
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity(subject is null ? [] : new[] { new Claim("sub", subject) }, "Bearer"))
        };
}
