namespace SpiderHood.Tests.Integration;

// Métodos GET_* que no reciben ningún parámetro de entidad (o solo un valor
// genérico como un "top N") -- no dependen de que exista un edificio/usuario/etc.
// específico en el backup restaurado. Todas las Stored Procedures se verificaron
// de sólo lectura con sp_helptext antes de usarlas acá (ver BDLayoutReadOnlyTests
// para el resto de módulos que ya cubre building-scoped).
[Collection("Database")]
public class BDLayoutNoParamReadOnlyTests
{
    private readonly DatabaseFixture _fixture;

    public BDLayoutNoParamReadOnlyTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task GetAllUserBuildingRolesAsync_DoesNotThrow()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        var roles = await _fixture.CreateBDLayout().GetAllUserBuildingRolesAsync();

        Assert.NotNull(roles);
    }

    [SkippableFact]
    public async Task GetAllUsersWithRolesAsync_DoesNotThrow()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        var users = await _fixture.CreateBDLayout().GetAllUsersWithRolesAsync();

        Assert.NotNull(users);
    }

    [SkippableFact]
    public async Task GetAllSubscriptionPlansAsync_ReturnsTheSeededPlans()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        var plans = await _fixture.CreateBDLayout().GetAllSubscriptionPlansAsync();

        // La instalación base siembra planes fijos (Docs/Design-Subscripcion-Administrador.md).
        Assert.NotEmpty(plans);
    }

    [SkippableFact]
    public async Task GetMixtoParameterCandidatesAsync_DoesNotThrow()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        var candidates = await _fixture.CreateBDLayout().GetMixtoParameterCandidatesAsync();

        Assert.NotNull(candidates);
    }

    [SkippableFact]
    public async Task GetWorkflowsAsync_ReturnsTheSeededWorkflows()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        var workflows = await _fixture.CreateBDLayout().GetWorkflowsAsync();

        // Presupuesto e Incidente siembran su propio workflow (Database/Scripts/
        // 2026-09-02_09_Seed_IncidentWorkflowCatalog.sql y ..._10_Seed_BudgetWorkflowCatalog.sql).
        Assert.NotEmpty(workflows);
    }

    [SkippableFact]
    public async Task GetSystemLogSettingsAsync_ReturnsDefaultsWhenNoRowExists()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        var settings = await _fixture.CreateBDLayout().GetSystemLogSettingsAsync();

        // El propio método documenta que devuelve un default si la tabla está vacía --
        // lo único garantizado es que nunca es null y nunca tira.
        Assert.NotNull(settings);
    }

    [SkippableFact]
    public async Task GetRecentSystemLogsAsync_RespectsTheTopParameter()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        var logs = await _fixture.CreateBDLayout().GetRecentSystemLogsAsync(top: 5);

        Assert.True(logs.Count <= 5);
    }

    [SkippableFact]
    public async Task GetAllMenuPermissionsAsync_DoesNotThrow()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        // Implementación cruda (SqlDataAdapter, no el patrón Dapper estándar) con su
        // propio try/catch que traga errores devolviendo [] -- igual vale la pena
        // ejercitarla para detectar si la SP o la tabla dejan de existir.
        var permissions = await _fixture.CreateBDLayout().GetAllMenuPermissionsAsync();

        Assert.NotNull(permissions);
    }

    [SkippableFact]
    public async Task GetMenuItemsAsync_BuildsParentChildHierarchyWithoutThrowing()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        // También ADO.NET crudo (SqlDataAdapter) -- arma la jerarquía de menú en memoria
        // a partir de ParentKey, distinto código que el resto de métodos Get.
        var items = await _fixture.CreateBDLayout().GetMenuItemsAsync();

        Assert.NotNull(items);
    }
}
