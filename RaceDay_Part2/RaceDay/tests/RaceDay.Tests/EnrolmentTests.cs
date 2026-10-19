using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

public class EnrolmentTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public EnrolmentTests(RaceDayFactory factory) => _factory = factory;

    private async Task<(HttpClient Organiser, EventDto Event, CategoryDto Category)> SetupEventAsync()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(organiser);
        var cat = await TestApi.CreateCategoryAsync(organiser, ev.EventId, "Senior");
        return (organiser, ev, cat);
    }

    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentLinksUserEventAndCategory()
    {
        var (_, ev, cat) = await SetupEventAsync();
        var (participant, me) = await TestApi.NewUserAsync(_factory, "Participant");

        var response = await TestApi.EnrolAsync(participant, ev.EventId, cat.CategoryId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = await response.Content.ReadFromJsonAsync<EnrolmentDto>();
        Assert.Equal(me.UserId, enrolment!.UserId);
        Assert.Equal(ev.EventId, enrolment.EventId);
        Assert.Equal(cat.CategoryId, enrolment.CategoryId);
    }

    [Fact]
    public async Task Enrolment_AppearsInParticipantsOwnList()
    {
        var (_, ev, cat) = await SetupEventAsync();
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var enrolment = await TestApi.EnrolOkAsync(participant, ev.EventId, cat.CategoryId);

        var mine = await (await participant.GetAsync("/api/enrolments/my")).Content.ReadFromJsonAsync<List<EnrolmentDto>>();
        Assert.Contains(mine!, e => e.EnrolmentId == enrolment.EnrolmentId && e.EventName == ev.Name);
    }

    [Fact]
    public async Task Participant_DoesNotSeeOtherParticipantsEnrolments()
    {
        var (_, ev, cat) = await SetupEventAsync();
        var (alice, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var (bob, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var aliceEnrolment = await TestApi.EnrolOkAsync(alice, ev.EventId, cat.CategoryId);

        var bobList = await (await bob.GetAsync("/api/enrolments/my")).Content.ReadFromJsonAsync<List<EnrolmentDto>>();
        Assert.DoesNotContain(bobList!, e => e.EnrolmentId == aliceEnrolment.EnrolmentId);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_Gets403()
    {
        var (organiser, ev, cat) = await SetupEventAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await TestApi.EnrolAsync(organiser, ev.EventId, cat.CategoryId)).StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotEnrol_Gets401()
    {
        var (_, ev, cat) = await SetupEventAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await TestApi.EnrolAsync(_factory.CreateClient(), ev.EventId, cat.CategoryId)).StatusCode);
    }

    [Fact]
    public async Task Enrol_Twice_InSameEvent_Returns409()
    {
        var (_, ev, cat) = await SetupEventAsync();
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        await TestApi.EnrolOkAsync(participant, ev.EventId, cat.CategoryId);

        Assert.Equal(HttpStatusCode.Conflict, (await TestApi.EnrolAsync(participant, ev.EventId, cat.CategoryId)).StatusCode);
    }

    [Fact]
    public async Task Enrol_UnknownEvent_Returns404()
    {
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        Assert.Equal(HttpStatusCode.NotFound, (await TestApi.EnrolAsync(participant, 999999, 1)).StatusCode);
    }

    [Fact]
    public async Task Enrol_WithCategoryFromAnotherEvent_Returns400()
    {
        var (organiser, ev, _) = await SetupEventAsync();
        var otherEvent = await TestApi.CreateEventAsync(organiser, "Another Race");
        var otherCat = await TestApi.CreateCategoryAsync(organiser, otherEvent.EventId);
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");

        Assert.Equal(HttpStatusCode.BadRequest, (await TestApi.EnrolAsync(participant, ev.EventId, otherCat.CategoryId)).StatusCode);
    }

    [Fact]
    public async Task Enrol_OutsideCategoryAgeRange_Returns400()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(organiser);
        var under20 = await TestApi.CreateCategoryAsync(organiser, ev.EventId, "Under 20", 0, 19, null);
        var (adult, _) = await TestApi.NewUserAsync(_factory, "Participant", new DateTime(1990, 5, 15));

        Assert.Equal(HttpStatusCode.BadRequest, (await TestApi.EnrolAsync(adult, ev.EventId, under20.CategoryId)).StatusCode);
    }

    [Fact]
    public async Task Enrol_InsideCategoryAgeRange_Succeeds()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var ev = await TestApi.CreateEventAsync(organiser);
        var under20 = await TestApi.CreateCategoryAsync(organiser, ev.EventId, "Under 20", 0, 19, null);
        var (teen, _) = await TestApi.NewUserAsync(_factory, "Participant", DateTime.UtcNow.Date.AddYears(-16));

        Assert.Equal(HttpStatusCode.Created, (await TestApi.EnrolAsync(teen, ev.EventId, under20.CategoryId)).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewAllEnrolmentsForOwnEvent()
    {
        var (organiser, ev, cat) = await SetupEventAsync();
        var (alice, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var (bob, _) = await TestApi.NewUserAsync(_factory, "Participant");
        await TestApi.EnrolOkAsync(alice, ev.EventId, cat.CategoryId);
        await TestApi.EnrolOkAsync(bob, ev.EventId, cat.CategoryId);

        var response = await organiser.GetAsync($"/api/events/{ev.EventId}/enrolments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.Content.ReadFromJsonAsync<List<EnrolmentDto>>())!.Count);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolmentList_Gets403()
    {
        var (_, ev, _) = await SetupEventAsync();
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        Assert.Equal(HttpStatusCode.Forbidden, (await participant.GetAsync($"/api/events/{ev.EventId}/enrolments")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsForAnotherOrganisersEvent_Gets403()
    {
        var (_, ev, _) = await SetupEventAsync();
        var (other, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/events/{ev.EventId}/enrolments")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUseParticipantOnlyMyEnrolments_Gets403()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        Assert.Equal(HttpStatusCode.Forbidden, (await organiser.GetAsync("/api/enrolments/my")).StatusCode);
    }

    [Fact]
    public async Task Participant_CanWithdrawOwnEnrolment()
    {
        var (_, ev, cat) = await SetupEventAsync();
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var enrolment = await TestApi.EnrolOkAsync(participant, ev.EventId, cat.CategoryId);

        Assert.Equal(HttpStatusCode.NoContent, (await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}")).StatusCode);
        var mine = await (await participant.GetAsync("/api/enrolments/my")).Content.ReadFromJsonAsync<List<EnrolmentDto>>();
        Assert.Empty(mine!);
    }

    [Fact]
    public async Task Participant_CannotWithdrawSomeoneElsesEnrolment_Gets403()
    {
        var (_, ev, cat) = await SetupEventAsync();
        var (alice, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var (bob, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var enrolment = await TestApi.EnrolOkAsync(alice, ev.EventId, cat.CategoryId);

        Assert.Equal(HttpStatusCode.Forbidden, (await bob.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}")).StatusCode);
    }
}
