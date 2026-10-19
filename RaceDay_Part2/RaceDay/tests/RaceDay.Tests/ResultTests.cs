using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

public class ResultTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;
    public ResultTests(RaceDayFactory factory) => _factory = factory;

    private record Setup(HttpClient Organiser, HttpClient Participant, EventDto Event, EnrolmentDto Enrolment);

    private async Task<Setup> SetupAsync()
    {
        var (organiser, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var (participant, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var ev = await TestApi.CreateEventAsync(organiser);
        var cat = await TestApi.CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await TestApi.EnrolOkAsync(participant, ev.EventId, cat.CategoryId);
        return new Setup(organiser, participant, ev, enrolment);
    }

    private static ResultRequest Req(int enrolmentId, string time = "00:45:30", int position = 1) =>
        new() { EnrolmentId = enrolmentId, FinishTime = TimeSpan.Parse(time), FinishPosition = position };

    [Fact]
    public async Task Organiser_CanCaptureResult()
    {
        var s = await SetupAsync();
        var response = await s.Organiser.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(s.Enrolment.EnrolmentId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ResultDto>();
        Assert.Equal(new TimeSpan(0, 45, 30), result!.FinishTime);
        Assert.Equal(1, result.FinishPosition);
    }

    [Fact]
    public async Task Participant_CannotCaptureResult_Gets403()
    {
        var s = await SetupAsync();
        var response = await s.Participant.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(s.Enrolment.EnrolmentId));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCaptureResultForAnotherOrganisersEvent_Gets403()
    {
        var s = await SetupAsync();
        var (other, _) = await TestApi.NewUserAsync(_factory, "Organiser");
        var response = await other.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(s.Enrolment.EnrolmentId));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Capture_DuplicateForSameEnrolment_Returns409()
    {
        var s = await SetupAsync();
        await s.Organiser.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(s.Enrolment.EnrolmentId));
        var second = await s.Organiser.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(s.Enrolment.EnrolmentId, position: 2));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Capture_PositionAlreadyTaken_Returns409()
    {
        var s = await SetupAsync();
        var (second, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var cats = await (await s.Organiser.GetAsync($"/api/events/{s.Event.EventId}/categories")).Content.ReadFromJsonAsync<List<CategoryDto>>();
        var secondEnrolment = await TestApi.EnrolOkAsync(second, s.Event.EventId, cats![0].CategoryId);

        await s.Organiser.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(s.Enrolment.EnrolmentId, position: 1));
        var clash = await s.Organiser.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(secondEnrolment.EnrolmentId, "00:50:00", 1));
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
    }

    [Fact]
    public async Task Capture_ZeroFinishTime_Returns400()
    {
        var s = await SetupAsync();
        var response = await s.Organiser.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(s.Enrolment.EnrolmentId, "00:00:00"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Capture_PositionBelowOne_Returns400()
    {
        var s = await SetupAsync();
        var response = await s.Organiser.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(s.Enrolment.EnrolmentId, position: 0));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Capture_EnrolmentFromAnotherEvent_Returns400()
    {
        var s = await SetupAsync();
        var other = await TestApi.CreateEventAsync(s.Organiser, "Second Race");
        var response = await s.Organiser.PostAsJsonAsync($"/api/events/{other.EventId}/results", Req(s.Enrolment.EnrolmentId));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewOwnResults()
    {
        var s = await SetupAsync();
        await s.Organiser.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(s.Enrolment.EnrolmentId, "01:02:03", 5));

        var response = await s.Participant.GetAsync("/api/results/my");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<ResultDto>>();
        var mine = Assert.Single(list!);
        Assert.Equal(s.Event.Name, mine.EventName);
        Assert.Equal(new TimeSpan(1, 2, 3), mine.FinishTime);
        Assert.Equal(5, mine.FinishPosition);
    }

    [Fact]
    public async Task Participant_DoesNotSeeAnotherParticipantsResults()
    {
        var s = await SetupAsync();
        await s.Organiser.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(s.Enrolment.EnrolmentId));
        var (stranger, _) = await TestApi.NewUserAsync(_factory, "Participant");

        var list = await (await stranger.GetAsync("/api/results/my")).Content.ReadFromJsonAsync<List<ResultDto>>();
        Assert.Empty(list!);
    }

    [Fact]
    public async Task Organiser_CannotUseParticipantOnlyMyResults_Gets403()
    {
        var s = await SetupAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Organiser.GetAsync("/api/results/my")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotViewResults_Gets401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/results/my")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewEventResults_OrderedByPosition()
    {
        var s = await SetupAsync();
        var (second, _) = await TestApi.NewUserAsync(_factory, "Participant");
        var cats = await (await s.Organiser.GetAsync($"/api/events/{s.Event.EventId}/categories")).Content.ReadFromJsonAsync<List<CategoryDto>>();
        var secondEnrolment = await TestApi.EnrolOkAsync(second, s.Event.EventId, cats![0].CategoryId);

        await s.Organiser.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(s.Enrolment.EnrolmentId, "00:55:00", 2));
        await s.Organiser.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(secondEnrolment.EnrolmentId, "00:40:00", 1));

        var list = await (await s.Organiser.GetAsync($"/api/events/{s.Event.EventId}/results")).Content.ReadFromJsonAsync<List<ResultDto>>();
        Assert.Equal(new[] { 1, 2 }, list!.Select(r => r.FinishPosition).ToArray());
    }

    [Fact]
    public async Task Participant_CannotViewEventResultList_Gets403()
    {
        var s = await SetupAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Participant.GetAsync($"/api/events/{s.Event.EventId}/results")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCorrectAResult()
    {
        var s = await SetupAsync();
        var created = await (await s.Organiser.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(s.Enrolment.EnrolmentId)))
            .Content.ReadFromJsonAsync<ResultDto>();

        var response = await s.Organiser.PutAsJsonAsync($"/api/results/{created!.ResultId}", Req(s.Enrolment.EnrolmentId, "00:44:10", 3));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ResultDto>();
        Assert.Equal(new TimeSpan(0, 44, 10), updated!.FinishTime);
        Assert.Equal(3, updated.FinishPosition);
    }

    [Fact]
    public async Task Participant_CannotCorrectAResult_Gets403()
    {
        var s = await SetupAsync();
        var created = await (await s.Organiser.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(s.Enrolment.EnrolmentId)))
            .Content.ReadFromJsonAsync<ResultDto>();

        var response = await s.Participant.PutAsJsonAsync($"/api/results/{created!.ResultId}", Req(s.Enrolment.EnrolmentId, "00:10:00", 1));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Withdraw_AfterResultCaptured_Returns409()
    {
        var s = await SetupAsync();
        await s.Organiser.PostAsJsonAsync($"/api/events/{s.Event.EventId}/results", Req(s.Enrolment.EnrolmentId));
        Assert.Equal(HttpStatusCode.Conflict, (await s.Participant.DeleteAsync($"/api/enrolments/{s.Enrolment.EnrolmentId}")).StatusCode);
    }
}
