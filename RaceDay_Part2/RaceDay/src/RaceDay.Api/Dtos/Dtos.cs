using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class RegisterRequest
{
    [Required, MaxLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(200)] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8), MaxLength(100)] public string Password { get; set; } = string.Empty;
    [Required] public DateTime DateOfBirth { get; set; }
    [MaxLength(30)] public string? PhoneNumber { get; set; }
    /// <summary>Organiser or Participant.</summary>
    [Required] public string Role { get; set; } = string.Empty;
    /// <summary>Secret code required when registering as an Organiser.</summary>
    public string? OrganiserCode { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, MaxLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string LastName { get; set; } = string.Empty;
    [Required] public DateTime DateOfBirth { get; set; }
    [MaxLength(30)] public string? PhoneNumber { get; set; }
}

public record ProfileDto(int UserId, string FirstName, string LastName, string Email,
    DateTime DateOfBirth, string? PhoneNumber, string Role);

public class EventRequest
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Description { get; set; } = string.Empty;
    [Required] public DateTime EventDate { get; set; }
    [Required, MaxLength(200)] public string Location { get; set; } = string.Empty;
    [Range(0.1, 1000)] public decimal DistanceKm { get; set; }
    /// <summary>Run, Walk or Cycle.</summary>
    [Required] public string EventType { get; set; } = string.Empty;
}

public record EventDto(int EventId, int OrganiserId, string OrganiserName, string Name, string Description,
    DateTime EventDate, string Location, decimal DistanceKm, string EventType, int CategoryCount, int EnrolmentCount);

public class CategoryRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Range(0, 120)] public int? MinAge { get; set; }
    [Range(0, 120)] public int? MaxAge { get; set; }
    [Range(0.1, 1000)] public decimal? DistanceKm { get; set; }
}

public record CategoryDto(int CategoryId, int EventId, string Name, int? MinAge, int? MaxAge, decimal? DistanceKm);

public class EnrolRequest
{
    [Required] public int CategoryId { get; set; }
}

public record EnrolmentDto(int EnrolmentId, int EventId, string EventName, DateTime EventDate,
    int UserId, string ParticipantName, int CategoryId, string CategoryName, DateTime EnrolledAt);

public class ResultRequest
{
    /// <summary>Required when capturing a result; ignored on update.</summary>
    public int EnrolmentId { get; set; }
    /// <summary>Finish time as hh:mm:ss, e.g. 01:45:30.</summary>
    [Required] public TimeSpan FinishTime { get; set; }
    [Range(1, int.MaxValue)] public int FinishPosition { get; set; }
}

public record ResultDto(int ResultId, int EnrolmentId, int EventId, string EventName, DateTime EventDate,
    string ParticipantName, string CategoryName, TimeSpan FinishTime, int FinishPosition);
