using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Small helpers so each test reads as a story about the API.</summary>
public static class TestApi
{
    public const string OrganiserCode = "RACEDAY-ORG-2026";
    public const string Password = "Password123!";

    public static DateTime FutureDate => DateTime.UtcNow.Date.AddMonths(3);

    /// <summary>Registers and logs in a brand-new user. Each user gets their own client, so their own session cookie.</summary>
    public static async Task<(HttpClient Client, ProfileDto Profile)> NewUserAsync(
        RaceDayFactory factory, string role, DateTime? dateOfBirth = null)
    {
        var client = factory.CreateClient();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@raceday.test";

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = role, LastName = "Tester", Email = email, Password = Password,
            DateOfBirth = dateOfBirth ?? new DateTime(1990, 5, 15), Role = role,
            OrganiserCode = role == "Organiser" ? OrganiserCode : null
        });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = Password });
        login.EnsureSuccessStatusCode();
        return (client, (await login.Content.ReadFromJsonAsync<ProfileDto>())!);
    }

    public static EventRequest SampleEvent(string name = "Test 10km Fun Run", string type = "Run") => new()
    {
        Name = name, Description = "A friendly road race.", EventDate = FutureDate,
        Location = "Green Point, Cape Town", DistanceKm = 10, EventType = type
    };

    public static async Task<EventDto> CreateEventAsync(HttpClient organiser, string name = "Test 10km Fun Run", string type = "Run")
    {
        var response = await organiser.PostAsJsonAsync("/api/events", SampleEvent(name, type));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventDto>())!;
    }

    public static async Task<CategoryDto> CreateCategoryAsync(HttpClient organiser, int eventId,
        string name = "Senior", int? minAge = null, int? maxAge = null, decimal? distance = 10)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new CategoryRequest { Name = name, MinAge = minAge, MaxAge = maxAge, DistanceKm = distance });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    public static Task<HttpResponseMessage> EnrolAsync(HttpClient participant, int eventId, int categoryId) =>
        participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new EnrolRequest { CategoryId = categoryId });

    public static async Task<EnrolmentDto> EnrolOkAsync(HttpClient participant, int eventId, int categoryId)
    {
        var response = await EnrolAsync(participant, eventId, categoryId);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EnrolmentDto>())!;
    }
}
