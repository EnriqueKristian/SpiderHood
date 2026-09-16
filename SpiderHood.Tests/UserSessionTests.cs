using SpiderHood.Models;

namespace SpiderHood.Tests;

public class UserSessionTests
{
    [Fact]
    public void IsAuthenticated_TrueWhenSessionExpiryIsInTheFuture()
    {
        var session = new UserSession { SessionExpiry = DateTime.UtcNow.AddMinutes(30) };
        Assert.True(session.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_FalseWhenSessionExpiryIsInThePast()
    {
        var session = new UserSession { SessionExpiry = DateTime.UtcNow.AddMinutes(-1) };
        Assert.False(session.IsAuthenticated);
    }

    // Estos 4 GUIDs son los mismos IDs fijos sembrados por la instalación base para los
    // roles del sistema (tabla roles) -- si alguno cambia acá sin cambiar en la base, el
    // mapeo de sesión queda desincronizado del catálogo real de roles.
    [Theory]
    [InlineData("Administrador", "46198F07-F865-49A6-8057-571B867C5D1B")]
    [InlineData("Residente", "E507B520-E2F5-4A47-99FB-D71B5515A575")]
    [InlineData("Junta", "46461AA2-5A7B-4083-88CF-D9FD4704DF80")]
    [InlineData("SysAdmin", "E6A7FC24-75C2-44CE-88BF-7FC5B2A0EED4")]
    public void IdRole_MapsKnownRoleNamesToTheirFixedGuid(string role, string expectedGuid)
    {
        var session = new UserSession { Role = role };
        Assert.Equal(Guid.Parse(expectedGuid), session.IdRole);
    }

    [Fact]
    public void IdRole_WithUnknownRole_ReturnsEmptyGuid()
    {
        var session = new UserSession { Role = "RolInexistente" };
        Assert.Equal(Guid.Empty, session.IdRole);
    }
}
