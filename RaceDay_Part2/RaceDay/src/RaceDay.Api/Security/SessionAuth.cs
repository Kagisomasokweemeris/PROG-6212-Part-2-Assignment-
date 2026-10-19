using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Security;

public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
}

/// <summary>
/// Session-based access control. With no roles the caller only has to be logged in;
/// with roles the caller must also hold one of them. 401 = not logged in, 403 = wrong role.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireSessionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _roles;

    public RequireSessionAttribute(params string[] roles) => _roles = roles;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);

        if (userId is null || role is null)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Not logged in",
                Detail = "Log in via POST /api/auth/login first."
            })
            { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(role))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = $"This endpoint requires the role: {string.Join(" or ", _roles)}."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}

[ApiController]
public abstract class RaceDayControllerBase : ControllerBase
{
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;
    protected string? CurrentRole => HttpContext.Session.GetString(SessionKeys.Role);
}
