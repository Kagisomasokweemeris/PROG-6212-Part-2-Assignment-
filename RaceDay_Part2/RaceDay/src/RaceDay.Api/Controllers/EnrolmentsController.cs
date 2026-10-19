using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[RequireSession]
public class EnrolmentsController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;
    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    /// <summary>Enter an event by choosing one of its categories (Participant only).</summary>
    [HttpPost("api/events/{eventId:int}/enrolments")]
    [RequireSession(RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int eventId, EnrolRequest req)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev is null) return NotFound();
        if (ev.EventDate < DateTime.UtcNow)
            return BadRequest(new ProblemDetails { Title = "Event has already taken place" });

        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == req.CategoryId && c.EventId == eventId);
        if (cat is null)
            return BadRequest(new ProblemDetails { Title = "Invalid category", Detail = "That category does not belong to this event." });

        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user is null) return NotFound();

        var age = AgeOn(user.DateOfBirth, ev.EventDate);
        if ((cat.MinAge is not null && age < cat.MinAge) || (cat.MaxAge is not null && age > cat.MaxAge))
            return BadRequest(new ProblemDetails { Title = "Age not eligible", Detail = $"You will be {age} on race day; this category is for ages {cat.MinAge?.ToString() ?? "any"}-{cat.MaxAge?.ToString() ?? "any"}." });

        if (await _db.Enrolments.AnyAsync(e => e.EventId == eventId && e.UserId == user.UserId))
            return Conflict(new ProblemDetails { Status = 409, Title = "Already enrolled", Detail = "You are already enrolled in this event." });

        var enrolment = new Enrolment { UserId = user.UserId, EventId = eventId, CategoryId = cat.CategoryId };
        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        var dto = await _db.Enrolments.Where(e => e.EnrolmentId == enrolment.EnrolmentId).Select(Projection).FirstAsync();
        return Created("/api/enrolments/my", dto);
    }

    /// <summary>View all of your own enrolments (Participant only).</summary>
    [HttpGet("api/enrolments/my")]
    [RequireSession(RoleNames.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Mine()
    {
        var list = await _db.Enrolments.Where(e => e.UserId == CurrentUserId)
            .OrderBy(e => e.Event!.EventDate).Select(Projection).ToListAsync();
        return Ok(list);
    }

    /// <summary>Withdraw from an event you enrolled in (Participant only). Not allowed once a result exists.</summary>
    [HttpDelete("api/enrolments/{id:int}")]
    [RequireSession(RoleNames.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Withdraw(int id)
    {
        var enrolment = await _db.Enrolments.Include(e => e.Result).FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFound();
        if (enrolment.UserId != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Status = 403, Title = "Forbidden", Detail = "That is not your enrolment." });
        if (enrolment.Result is not null)
            return Conflict(new ProblemDetails { Status = 409, Title = "Result already captured" });

        _db.Enrolments.Remove(enrolment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>View every enrolment for an event you own (Organiser only).</summary>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [RequireSession(RoleNames.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForEvent(int eventId)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Status = 403, Title = "Forbidden", Detail = "You can only view enrolments for your own events." });

        var list = await _db.Enrolments.Where(e => e.EventId == eventId).OrderBy(e => e.EnrolledAt).Select(Projection).ToListAsync();
        return Ok(list);
    }

    private static int AgeOn(DateTime dob, DateTime on)
    {
        var age = on.Year - dob.Year;
        if (dob.Date > on.AddYears(-age).Date) age--;
        return age;
    }

    private static readonly System.Linq.Expressions.Expression<Func<Enrolment, EnrolmentDto>> Projection = e => new EnrolmentDto(
        e.EnrolmentId, e.EventId, e.Event!.Name, e.Event.EventDate, e.UserId,
        e.User!.FirstName + " " + e.User.LastName, e.CategoryId, e.Category!.Name, e.EnrolledAt);
}
