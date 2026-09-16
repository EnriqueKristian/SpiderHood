namespace SpiderHood.Tests.Integration;

// Tests de integración contra un SQL Server real (ver DatabaseFixture) -- a
// diferencia de los tests unitarios del resto del proyecto, estos ejercitan
// BDLayout llamando Stored Procedures reales. Sólo se usan SPs de sólo
// lectura (verificadas con sp_helptext) para no depender de -- ni mutar --
// datos de un backup restaurado.
[Collection("Database")]
public class BDLayoutReadOnlyTests
{
    private readonly DatabaseFixture _fixture;

    public BDLayoutReadOnlyTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task GetAllBuildingsPublicAsync_ReturnsOnlyActiveBuildingsOrderedByName()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        var bdLayout = _fixture.CreateBDLayout();

        var buildings = await bdLayout.GetAllBuildingsPublicAsync();

        Assert.NotEmpty(buildings);
        Assert.All(buildings, b => Assert.True(b.IsActive));

        var names = buildings.Select(b => b.Name).ToList();
        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), names);
    }

    [SkippableFact]
    public async Task GetAllRolesAsync_IncludesTheCoreSystemRoles()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        var bdLayout = _fixture.CreateBDLayout();

        var roles = await bdLayout.GetAllRolesAsync();
        var roleNames = roles.Select(r => r.RoleName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Estos 4 roles los siembra la instalación base (no un script incremental
        // de Database/Scripts) -- si faltan, algo restauró mal el esquema.
        Assert.Contains("SysAdmin", roleNames);
        Assert.Contains("Administrador", roleNames);
        Assert.Contains("Residente", roleNames);
        Assert.Contains("Junta", roleNames);
    }

    [SkippableFact]
    public async Task GetAllPermissionsAsync_ReturnsNonEmptyCatalog()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        var bdLayout = _fixture.CreateBDLayout();

        var permissions = await bdLayout.GetAllPermissionsAsync();

        Assert.NotEmpty(permissions);
        Assert.All(permissions, p => Assert.False(string.IsNullOrWhiteSpace(p.PermissionKey)));
    }

    [SkippableFact]
    public async Task GetTemplateBuildingAsync_ReturnsABuildingFlaggedAsTemplate()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        var bdLayout = _fixture.CreateBDLayout();

        var template = await bdLayout.GetTemplateBuildingAsync();

        Assert.NotNull(template);
        Assert.True(template!.IsTemplate);
    }
}
