using RaceDay.Api.Services;

namespace RaceDay.Tests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_DoesNotContainThePlainTextPassword()
    {
        var hash = _hasher.Hash("Password123!");
        Assert.DoesNotContain("Password123!", hash);
        Assert.StartsWith("PBKDF2$", hash);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue() =>
        Assert.True(_hasher.Verify("Password123!", _hasher.Hash("Password123!")));

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse() =>
        Assert.False(_hasher.Verify("WrongPassword1!", _hasher.Hash("Password123!")));

    [Fact]
    public void Hash_SamePasswordTwice_GivesDifferentHashesBecauseOfSalt() =>
        Assert.NotEqual(_hasher.Hash("Password123!"), _hasher.Hash("Password123!"));

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2$abc$def$ghi")]
    [InlineData("PBKDF2$100000$!!!$!!!")]
    public void Verify_MalformedStoredHash_ReturnsFalse(string stored) =>
        Assert.False(_hasher.Verify("Password123!", stored));
}
