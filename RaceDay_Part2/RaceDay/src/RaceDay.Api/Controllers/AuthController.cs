using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/auth")]
public class AuthController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IConfiguration _config;

    public AuthController(RaceDayDbContext db, IPasswordHasher hasher, IConfiguration config)
    {
        _db = db; _hasher = hasher; _config = config;
    }

    /// <summary>Register a new Organiser or Participant account.</summary>
    /// <remarks>Organisers must supply the secret OrganiserCode so that nobody can self-promote to Organiser.</remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var roleName = RoleNames.Participant.Equals(req.Role, StringComparison.OrdinalIgnoreCase) ? RoleNames.Participant
            : RoleNames.Organiser.Equals(req.Role, StringComparison.OrdinalIgnoreCase) ? RoleNames.Organiser
            : null;
        if (roleName is null)
            return BadRequest(new ProblemDetails { Title = "Invalid role", Detail = "Role must be Organiser or Participant." });

        if (req.DateOfBirth.Date >= DateTime.UtcNow.Date)
            return BadRequest(new ProblemDetails { Title = "Invalid date of birth", Detail = "Date of birth must be in the past." });

        if (roleName == RoleNames.Organiser)
        {
            var expected = _config["Registration:OrganiserCode"];
            if (string.IsNullOrEmpty(expected) || req.OrganiserCode != expected)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new ProblemDetails { Status = 403, Title = "Forbidden", Detail = "A valid OrganiserCode is required to register as an Organiser." });
        }

        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new ProblemDetails { Status = 409, Title = "Email already registered" });

        var role = await _db.Roles.FirstAsync(r => r.RoleName == roleName);
        var user = new User
        {
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(req.Password),
            DateOfBirth = req.DateOfBirth.Date,
            PhoneNumber = req.PhoneNumber,
            RoleId = role.RoleId
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Created($"/api/profile", ToDto(user, roleName));
    }

    /// <summary>Log in with email and password. Starts a session that stores the user id and role.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !_hasher.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid email or password" });

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role!.RoleName);
        return Ok(ToDto(user, user.Role.RoleName));
    }

    /// <summary>Log out and end the session.</summary>
    [HttpPost("logout")]
    [RequireSession]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }

    internal static ProfileDto ToDto(User u, string role) =>
        new(u.UserId, u.FirstName, u.LastName, u.Email, u.DateOfBirth, u.PhoneNumber, role);
}
