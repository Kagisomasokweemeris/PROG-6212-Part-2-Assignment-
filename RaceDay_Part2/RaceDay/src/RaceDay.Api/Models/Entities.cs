using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDay.Api.Models;

public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

public static class EventTypes
{
    public const string Run = "Run";
    public const string Walk = "Walk";
    public const string Cycle = "Cycle";
    public static readonly string[] All = { Run, Walk, Cycle };
}

public class Role
{
    public int RoleId { get; set; }
    [Required, MaxLength(30)] public string RoleName { get; set; } = string.Empty;
    public List<User> Users { get; set; } = new();
}

public class User
{
    public int UserId { get; set; }
    [Required, MaxLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string PasswordHash { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    [MaxLength(30)] public string? PhoneNumber { get; set; }
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public User? Organiser { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Description { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    [Required, MaxLength(200)] public string Location { get; set; } = string.Empty;
    [Column(TypeName = "decimal(6,2)")] public decimal DistanceKm { get; set; }
    [Required, MaxLength(10)] public string EventType { get; set; } = EventTypes.Run;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Category> Categories { get; set; } = new();
    public List<Enrolment> Enrolments { get; set; } = new();
}

public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public Event? Event { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    [Column(TypeName = "decimal(6,2)")] public decimal? DistanceKm { get; set; }
    public List<Enrolment> Enrolments { get; set; } = new();
}

public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int EventId { get; set; }
    public Event? Event { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public Result? Result { get; set; }
}

public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public Enrolment? Enrolment { get; set; }
    public TimeSpan FinishTime { get; set; }
    public int FinishPosition { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
