using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

public class CategoryTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public CategoryTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Organiser_CanAddCategory()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new CategoryRequest { Name = "Under 20", MinAge = 0, MaxAge = 19 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cat = await response.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.Equal("Under 20", cat!.Name);
        Assert.Equal(ev.EventId, cat.EventId);
    }

    [Fact]
    public async Task Participant_CannotAddCategory_Gets403()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var ev = await TestApi.CreateEventAsync(organiser);

        var response = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new CategoryRequest { Name = "21km", DistanceKm = 21 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotAddCategoryToAnotherOrganisersEvent_Gets403()
    {
        var (owner, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var (other, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(owner);

        var response = await other.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new CategoryRequest { Name = "10km", DistanceKm = 10 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_WithNoAgeRangeOrDistance_Returns400()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new CategoryRequest { Name = "Empty" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_MinAgeAboveMaxAge_Returns400()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", new CategoryRequest { Name = "Odd", MinAge = 50, MaxAge = 40 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_UnknownEvent_Returns404()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var response = await organiser.PostAsJsonAsync("/api/events/999999/categories", new CategoryRequest { Name = "10km", DistanceKm = 10 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task BothRoles_CanViewCategories(string role)
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(organiser);
        await TestApi.CreateCategoryAsync(organiser, ev.EventId, "10km", distance: 10);
        await TestApi.CreateCategoryAsync(organiser, ev.EventId, "21km", distance: 21);
        var (viewer, _) = await TestApi.NewUserAsync(_factory, role);

        var list = await (await viewer.GetAsync($"/api/events/{ev.EventId}/categories")).Content.ReadFromJsonAsync<List<CategoryDto>>();
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task ViewCategories_WithoutLogin_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/events/1/categories")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateCategory()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(organiser);
        var cat = await TestApi.CreateCategoryAsync(organiser, ev.EventId, "Senior");

        var response = await organiser.PutAsJsonAsync($"/api/categories/{cat.CategoryId}",
            new CategoryRequest { Name = "Veteran", MinAge = 40, MaxAge = 59 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.Equal("Veteran", updated!.Name);
        Assert.Equal(40, updated.MinAge);
    }

    [Fact]
    public async Task Participant_CannotUpdateCategory_Gets403()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var ev = await TestApi.CreateEventAsync(organiser);
        var cat = await TestApi.CreateCategoryAsync(organiser, ev.EventId);

        var response = await participant.PutAsJsonAsync($"/api/categories/{cat.CategoryId}", new CategoryRequest { Name = "X", DistanceKm = 5 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteUnusedCategory()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(organiser);
        var cat = await TestApi.CreateCategoryAsync(organiser, ev.EventId);

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/categories/{cat.CategoryId}")).StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithEnrolments_Returns409()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var ev = await TestApi.CreateEventAsync(organiser);
        var cat = await TestApi.CreateCategoryAsync(organiser, ev.EventId);
        await TestApi.EnrolOkAsync(participant, ev.EventId, cat.CategoryId);

        Assert.Equal(HttpStatusCode.Conflict, (await organiser.DeleteAsync($"/api/categories/{cat.CategoryId}")).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotDeleteCategory_Gets403()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var ev = await TestApi.CreateEventAsync(organiser);
        var cat = await TestApi.CreateCategoryAsync(organiser, ev.EventId);

        Assert.Equal(HttpStatusCode.Forbidden, (await participant.DeleteAsync($"/api/categories/{cat.CategoryId}")).StatusCode);
    }
}
