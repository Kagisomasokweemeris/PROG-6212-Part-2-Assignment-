using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

public class AuthTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public AuthTests(RaceDayFactory factory) => _factory = factory;

    private static RegisterRequest NewRegistration(string role = "Participant", string? code = null) => new()
    {
        FirstName = "Thandi", LastName = "Nkosi", Email = $"{Guid.NewGuid():N}@raceday.test",
        Password = TestApi.Password, DateOfBirth = new DateTime(1992, 3, 1), Role = role, OrganiserCode = code
    };

    [Fact]
    public async Task Register_Participant_Returns201WithProfile()
    {
        var req = NewRegistration();
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", req);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.Equal("Participant", profile!.Role);
        Assert.Equal(req.Email, profile.Email);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var req = NewRegistration();
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", req);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var user = db.Users.Single(u => u.Email == req.Email);
        Assert.NotEqual(req.Password, user.PasswordHash);
        Assert.DoesNotContain(req.Password, user.PasswordHash);
    }

    [Fact]
    public async Task Register_OrganiserWithValidCode_Returns201()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            NewRegistration("Organiser", TestApi.OrganiserCode));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Organiser", (await response.Content.ReadFromJsonAsync<ProfileDto>())!.Role);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("WRONG-CODE")]
    public async Task Register_OrganiserWithoutValidCode_Returns403(string? code)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", NewRegistration("Organiser", code));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var req = NewRegistration();
        await client.PostAsJsonAsync("/api/auth/register", req);

        var second = await client.PostAsJsonAsync("/api/auth/register", req);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_Returns400()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", NewRegistration("Spectator"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_PasswordTooShort_Returns400()
    {
        var req = NewRegistration();
        req.Password = "short";
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var req = NewRegistration();
        req.Email = "not-an-email";
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200AndRole()
    {
        var req = NewRegistration();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", req);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = req.Email, Password = req.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Participant", (await response.Content.ReadFromJsonAsync<ProfileDto>())!.Role);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        var req = NewRegistration();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", req);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = req.Email.ToUpperInvariant(), Password = req.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var req = NewRegistration();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", req);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = req.Email, Password = "WrongPassword1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "nobody@raceday.test", Password = TestApi.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutLogin_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_AfterLogin_ReturnsOwnProfile()
    {
        var (client, me) = await TestApi.NewUserAsync(_factory, "Participant");
        var response = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(me.Email, (await response.Content.ReadFromJsonAsync<ProfileDto>())!.Email);
    }

    [Fact]
    public async Task Profile_Update_PersistsChanges()
    {
        var (client, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var update = await client.PutAsJsonAsync("/api/profile", new UpdateProfileRequest
        {
            FirstName = "Sipho", LastName = "Dlamini", DateOfBirth = new DateTime(1985, 1, 20), PhoneNumber = "0821234567"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var profile = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<ProfileDto>();
        Assert.Equal("Sipho", profile!.FirstName);
        Assert.Equal("0821234567", profile.PhoneNumber);
    }

    [Fact]
    public async Task Profile_Update_WithoutLogin_Returns401()
    {
        var response = await _factory.CreateClient().PutAsJsonAsync("/api/profile", new UpdateProfileRequest
        {
            FirstName = "A", LastName = "B", DateOfBirth = new DateTime(1990, 1, 1)
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var (client, _) = await TestApi.NewUserAsync(_factory, "Participant");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
    }

    [Fact]
    public async Task Sessions_AreSeparatePerClient()
    {
        var (_, alice) = await TestApi.NewUserAsync(_factory, "Participant");
        var (bobClient, _) = await TestApi.NewUserAsync(_factory, "Participant");

        var profile = await (await bobClient.GetAsync("/api/profile")).Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotEqual(alice.UserId, profile!.UserId);
    }
}
