using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[Route("api/events")]
[RequireSession]
public class EventsController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;
    public EventsController(RaceDayDbContext db) => _db = db;

    /// <summary>List all events, optionally filtered by type (Run, Walk, Cycle).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] string? type = null)
    {
        var query = _db.Events.AsQueryable();
        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalised = EventTypes.All.FirstOrDefault(t => t.Equals(type, StringComparison.OrdinalIgnoreCase));
            if (normalised is null)
                return BadRequest(new ProblemDetails { Title = "Invalid event type", Detail = "Use Run, Walk or Cycle." });
            query = query.Where(e => e.EventType == normalised);
        }
        var events = await query.OrderBy(e => e.EventDate).Select(Projection).ToListAsync();
        return Ok(events);
    }

    /// <summary>Get a single event by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var dto = await _db.Events.Where(e => e.EventId == id).Select(Projection).FirstOrDefaultAsync();
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Create a new event (Organiser only).</summary>
    [HttpPost]
    [RequireSession(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(EventRequest req)
    {
        var type = NormaliseType(req.EventType);
        if (type is null)
            return BadRequest(new ProblemDetails { Title = "Invalid event type", Detail = "Use Run, Walk or Cycle." });

        var ev = new Event
        {
            OrganiserId = CurrentUserId,
            Name = req.Name.Trim(),
            Description = req.Description.Trim(),
            EventDate = req.EventDate,
            Location = req.Location.Trim(),
            DistanceKm = req.DistanceKm,
            EventType = type
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        var dto = await _db.Events.Where(e => e.EventId == ev.EventId).Select(Projection).FirstAsync();
        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, dto);
    }

    /// <summary>Update an event you own (Organiser only).</summary>
    [HttpPut("{id:int}")]
    [RequireSession(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, EventRequest req)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return Forbid403("You can only change your own events.");

        var type = NormaliseType(req.EventType);
        if (type is null)
            return BadRequest(new ProblemDetails { Title = "Invalid event type", Detail = "Use Run, Walk or Cycle." });

        ev.Name = req.Name.Trim();
        ev.Description = req.Description.Trim();
        ev.EventDate = req.EventDate;
        ev.Location = req.Location.Trim();
        ev.DistanceKm = req.DistanceKm;
        ev.EventType = type;
        await _db.SaveChangesAsync();
        return Ok(await _db.Events.Where(e => e.EventId == id).Select(Projection).FirstAsync());
    }

    /// <summary>Delete an event you own, including its categories, enrolments and results (Organiser only).</summary>
    [HttpDelete("{id:int}")]
    [RequireSession(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Enrolments).ThenInclude(x => x.Result)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return Forbid403("You can only delete your own events.");

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string? NormaliseType(string type) =>
        EventTypes.All.FirstOrDefault(t => t.Equals(type?.Trim(), StringComparison.OrdinalIgnoreCase));

    private ObjectResult Forbid403(string detail) =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Status = 403, Title = "Forbidden", Detail = detail });

    private static readonly System.Linq.Expressions.Expression<Func<Event, EventDto>> Projection = e => new EventDto(
        e.EventId, e.OrganiserId, e.Organiser!.FirstName + " " + e.Organiser.LastName, e.Name, e.Description,
        e.EventDate, e.Location, e.DistanceKm, e.EventType, e.Categories.Count, e.Enrolments.Count);
}
