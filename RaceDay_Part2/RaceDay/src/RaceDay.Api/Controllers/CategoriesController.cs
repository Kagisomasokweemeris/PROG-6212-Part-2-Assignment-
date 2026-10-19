using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[RequireSession]
public class CategoriesController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;
    public CategoriesController(RaceDayDbContext db) => _db = db;

    /// <summary>List the categories (age or distance) defined for an event.</summary>
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId)) return NotFound();
        var list = await _db.Categories.Where(c => c.EventId == eventId).OrderBy(c => c.CategoryId)
            .Select(c => new CategoryDto(c.CategoryId, c.EventId, c.Name, c.MinAge, c.MaxAge, c.DistanceKm))
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>Add a category to an event you own (Organiser only).</summary>
    [HttpPost("api/events/{eventId:int}/categories")]
    [RequireSession(RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int eventId, CategoryRequest req)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return Forbidden("You can only add categories to your own events.");
        var error = Validate(req);
        if (error is not null) return BadRequest(new ProblemDetails { Title = "Invalid category", Detail = error });

        var cat = new Category { EventId = eventId, Name = req.Name.Trim(), MinAge = req.MinAge, MaxAge = req.MaxAge, DistanceKm = req.DistanceKm };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return Created($"/api/events/{eventId}/categories", ToDto(cat));
    }

    /// <summary>Update a category on an event you own (Organiser only).</summary>
    [HttpPut("api/categories/{id:int}")]
    [RequireSession(RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, CategoryRequest req)
    {
        var cat = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat is null) return NotFound();
        if (cat.Event!.OrganiserId != CurrentUserId) return Forbidden("You can only change categories on your own events.");
        var error = Validate(req);
        if (error is not null) return BadRequest(new ProblemDetails { Title = "Invalid category", Detail = error });

        cat.Name = req.Name.Trim(); cat.MinAge = req.MinAge; cat.MaxAge = req.MaxAge; cat.DistanceKm = req.DistanceKm;
        await _db.SaveChangesAsync();
        return Ok(ToDto(cat));
    }

    /// <summary>Delete a category that nobody has enrolled in yet (Organiser only).</summary>
    [HttpDelete("api/categories/{id:int}")]
    [RequireSession(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat is null) return NotFound();
        if (cat.Event!.OrganiserId != CurrentUserId) return Forbidden("You can only delete categories on your own events.");
        if (await _db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return Conflict(new ProblemDetails { Status = 409, Title = "Category in use", Detail = "Participants are already enrolled in this category." });

        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Validate(CategoryRequest r)
    {
        if (r.MinAge is null && r.MaxAge is null && r.DistanceKm is null)
            return "Provide an age range (MinAge/MaxAge) or a DistanceKm.";
        if (r.MinAge is not null && r.MaxAge is not null && r.MinAge > r.MaxAge)
            return "MinAge cannot be greater than MaxAge.";
        return null;
    }

    private static CategoryDto ToDto(Category c) => new(c.CategoryId, c.EventId, c.Name, c.MinAge, c.MaxAge, c.DistanceKm);

    private ObjectResult Forbidden(string detail) =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Status = 403, Title = "Forbidden", Detail = detail });
}
