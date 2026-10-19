using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[Route("api/profile")]
[RequireSession]
public class ProfileController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;
    public ProfileController(RaceDayDbContext db) => _db = db;

    /// <summary>View the logged-in user's own profile.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get()
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        return user is null ? NotFound() : Ok(AuthController.ToDto(user, user.Role!.RoleName));
    }

    /// <summary>Update the logged-in user's own profile (name, phone number, date of birth).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(UpdateProfileRequest req)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return NotFound();
        if (req.DateOfBirth.Date >= DateTime.UtcNow.Date)
            return BadRequest(new ProblemDetails { Title = "Invalid date of birth" });

        user.FirstName = req.FirstName.Trim();
        user.LastName = req.LastName.Trim();
        user.DateOfBirth = req.DateOfBirth.Date;
        user.PhoneNumber = req.PhoneNumber;
        await _db.SaveChangesAsync();
        return Ok(AuthController.ToDto(user, user.Role!.RoleName));
    }
}
