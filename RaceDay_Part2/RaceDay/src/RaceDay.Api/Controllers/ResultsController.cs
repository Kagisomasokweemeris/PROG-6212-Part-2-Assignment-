using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[RequireSession]
public class ResultsController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;
    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Capture the finish time and position for an enrolled participant (Organiser only).</summary>
    [HttpPost("api/events/{eventId:int}/results")]
    [RequireSession(RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Capture(int eventId, ResultRequest req)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return Forbidden("You can only capture results for your own events.");
        if (req.FinishTime <= TimeSpan.Zero)
            return BadRequest(new ProblemDetails { Title = "Invalid finish time", Detail = "FinishTime must be greater than 00:00:00." });

        var enrolment = await _db.Enrolments.FirstOrDefaultAsync(e => e.EnrolmentId == req.EnrolmentId && e.EventId == eventId);
        if (enrolment is null)
            return BadRequest(new ProblemDetails { Title = "Invalid enrolment", Detail = "That enrolment does not belong to this event." });

        if (await _db.Results.AnyAsync(r => r.EnrolmentId == enrolment.EnrolmentId))
            return Conflict(new ProblemDetails { Status = 409, Title = "Result already captured for this enrolment" });
        if (await PositionTaken(eventId, req.FinishPosition, null))
            return Conflict(new ProblemDetails { Status = 409, Title = "Position already taken in this event" });

        var result = new Result { EnrolmentId = enrolment.EnrolmentId, FinishTime = req.FinishTime, FinishPosition = req.FinishPosition };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var dto = await _db.Results.Where(r => r.ResultId == result.ResultId).Select(Projection).FirstAsync();
        return Created($"/api/events/{eventId}/results", dto);
    }

    /// <summary>Correct a captured result (Organiser only).</summary>
    [HttpPut("api/results/{id:int}")]
    [RequireSession(RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, ResultRequest req)
    {
        var result = await _db.Results.Include(r => r.Enrolment).ThenInclude(e => e!.Event).FirstOrDefaultAsync(r => r.ResultId == id);
        if (result is null) return NotFound();
        if (result.Enrolment!.Event!.OrganiserId != CurrentUserId) return Forbidden("You can only change results for your own events.");
        if (req.FinishTime <= TimeSpan.Zero)
            return BadRequest(new ProblemDetails { Title = "Invalid finish time" });
        if (await PositionTaken(result.Enrolment.EventId, req.FinishPosition, id))
            return Conflict(new ProblemDetails { Status = 409, Title = "Position already taken in this event" });

        result.FinishTime = req.FinishTime;
        result.FinishPosition = req.FinishPosition;
        await _db.SaveChangesAsync();
        return Ok(await _db.Results.Where(r => r.ResultId == id).Select(Projection).FirstAsync());
    }

    /// <summary>View all results for an event you own, ordered by position (Organiser only).</summary>
    [HttpGet("api/events/{eventId:int}/results")]
    [RequireSession(RoleNames.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForEvent(int eventId)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return Forbidden("You can only view results for your own events.");
        var list = await _db.Results.Where(r => r.Enrolment!.EventId == eventId).OrderBy(r => r.FinishPosition).Select(Projection).ToListAsync();
        return Ok(list);
    }

    /// <summary>View your own personal results history (Participant only).</summary>
    [HttpGet("api/results/my")]
    [RequireSession(RoleNames.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Mine()
    {
        var list = await _db.Results.Where(r => r.Enrolment!.UserId == CurrentUserId)
            .OrderByDescending(r => r.Enrolment!.Event!.EventDate).Select(Projection).ToListAsync();
        return Ok(list);
    }

    private Task<bool> PositionTaken(int eventId, int position, int? exceptResultId) =>
        _db.Results.AnyAsync(r => r.Enrolment!.EventId == eventId && r.FinishPosition == position && r.ResultId != exceptResultId);

    private ObjectResult Forbidden(string detail) =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Status = 403, Title = "Forbidden", Detail = detail });

    private static readonly System.Linq.Expressions.Expression<Func<Result, ResultDto>> Projection = r => new ResultDto(
        r.ResultId, r.EnrolmentId, r.Enrolment!.EventId, r.Enrolment.Event!.Name, r.Enrolment.Event.EventDate,
        r.Enrolment.User!.FirstName + " " + r.Enrolment.User.LastName, r.Enrolment.Category!.Name,
        r.FinishTime, r.FinishPosition);
}
