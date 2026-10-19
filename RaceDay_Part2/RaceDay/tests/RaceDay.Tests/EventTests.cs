using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

public class EventTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public EventTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Organiser_CanCreateEvent()
    {
        var (organiser, me) = await TestApi.NewUserAsync(_factory, "Organiser");
        var response = await organiser.PostAsJsonAsync("/api/events", TestApi.SampleEvent("Peninsula Dash"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ev = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.Equal("Peninsula Dash", ev!.Name);
        Assert.Equal(me.UserId, ev.OrganiserId);
        Assert.Equal("Run", ev.EventType);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_Gets403()
    {
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var response = await participant.PostAsJsonAsync("/api/events", TestApi.SampleEvent());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotCreateEvent_Gets401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/events", TestApi.SampleEvent());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotListEvents_Gets401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidEventType_Returns400()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var response = await organiser.PostAsJsonAsync("/api/events", TestApi.SampleEvent(type: "Swim"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingName_Returns400()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var req = TestApi.SampleEvent();
        req.Name = "";
        Assert.Equal(HttpStatusCode.BadRequest, (await organiser.PostAsJsonAsync("/api/events", req)).StatusCode);
    }

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task BothRoles_CanViewEvents(string role)
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(organiser, $"Viewable {Guid.NewGuid():N}");
        var (viewer, _) = await TestApi.NewUserAsync(_factory, role);

        var list = await (await viewer.GetAsync("/api/events")).Content.ReadFromJsonAsync<List<EventDto>>();
        Assert.Contains(list!, e => e.EventId == ev.EventId);

        var single = await viewer.GetAsync($"/api/events/{ev.EventId}");
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
    }

    [Fact]
    public async Task GetEvent_UnknownId_Returns404()
    {
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        Assert.Equal(HttpStatusCode.NotFound, (await participant.GetAsync("/api/events/999999")).StatusCode);
    }

    [Fact]
    public async Task ListEvents_CanFilterByType()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var cycle = await TestApi.CreateEventAsync(organiser, "Filter Cycle", "Cycle");
        var walk = await TestApi.CreateEventAsync(organiser, "Filter Walk", "Walk");

        var list = await (await organiser.GetAsync("/api/events?type=cycle")).Content.ReadFromJsonAsync<List<EventDto>>();
        Assert.Contains(list!, e => e.EventId == cycle.EventId);
        Assert.DoesNotContain(list!, e => e.EventId == walk.EventId);
        Assert.All(list!, e => Assert.Equal("Cycle", e.EventType));
    }

    [Fact]
    public async Task ListEvents_InvalidTypeFilter_Returns400()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        Assert.Equal(HttpStatusCode.BadRequest, (await organiser.GetAsync("/api/events?type=swim")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(organiser);

        var update = TestApi.SampleEvent("Renamed Race", "Cycle");
        update.DistanceKm = 40;
        var response = await organiser.PutAsJsonAsync($"/api/events/{ev.EventId}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.Equal("Renamed Race", updated!.Name);
        Assert.Equal("Cycle", updated.EventType);
        Assert.Equal(40, updated.DistanceKm);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Gets403()
    {
        var (owner, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var (other, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(owner);

        var response = await other.PutAsJsonAsync($"/api/events/{ev.EventId}", TestApi.SampleEvent("Hijacked"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateEvent_Gets403()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var ev = await TestApi.CreateEventAsync(organiser);

        var response = await participant.PutAsJsonAsync($"/api/events/{ev.EventId}", TestApi.SampleEvent("Nope"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_UnknownEvent_Returns404()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.PutAsJsonAsync("/api/events/999999", TestApi.SampleEvent())).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(organiser);

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/events/{ev.EventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.GetAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_AlsoRemovesItsCategoriesAndEnrolments()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var ev = await TestApi.CreateEventAsync(organiser);
        var cat = await TestApi.CreateCategoryAsync(organiser, ev.EventId);
        await TestApi.EnrolOkAsync(participant, ev.EventId, cat.CategoryId);

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/events/{ev.EventId}")).StatusCode);

        var mine = await (await participant.GetAsync("/api/enrolments/my")).Content.ReadFromJsonAsync<List<EnrolmentDto>>();
        Assert.DoesNotContain(mine!, e => e.EventId == ev.EventId);
    }

    [Fact]
    public async Task Participant_CannotDeleteEvent_Gets403()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var ev = await TestApi.CreateEventAsync(organiser);

        Assert.Equal(HttpStatusCode.Forbidden, (await participant.DeleteAsync($"/api/events/{ev.EventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await organiser.GetAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_Gets403()
    {
        var (owner, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var (other, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(owner);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.DeleteAsync($"/api/events/{ev.EventId}")).StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownEvent_Returns404()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.DeleteAsync("/api/events/999999")).StatusCode);
    }
}
