using SpiderHood.Data;

namespace SpiderHood.Tests.Integration;

// Métodos GET_* "por edificio" de BDLayout, cubriendo el resto de los módulos de
// negocio (Unidades, Presupuesto, Gastos, Áreas Comunes/Reservas, Calendario,
// Comunicados, Gobernanza/Reuniones, Planillas, Conciliación). Todas las Stored
// Procedures se verificaron de sólo lectura con sp_helptext antes de usarlas.
//
// El Guid de edificio sale de DatabaseFixture.GetAnyBuildingIdAsync() (el primer
// edificio activo real del backup restaurado, cualquiera que sea) en vez de un
// Guid hardcodeado -- así los tests no dependen de que un backup específico tenga
// siempre el mismo edificio. Donde el modelo devuelto expone IdBuilding, se
// verifica que el SP de verdad filtra por el parámetro que recibe (no que
// devuelva filas de otro edificio); donde no lo expone, sólo se verifica que la
// llamada no tire excepción -- igual detecta que la SP/tabla siga existiendo.
[Collection("Database")]
public class BDLayoutBuildingScopedReadOnlyTests
{
    private readonly DatabaseFixture _fixture;

    public BDLayoutBuildingScopedReadOnlyTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task GetBuildingConfigurationAsync_ReturnsConfigurationForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var configs = await _fixture.CreateBDLayout().GetBuildingConfigurationAsync(buildingId);

        Assert.NotEmpty(configs);
        Assert.All(configs, c => Assert.Equal(buildingId, c.IdBuilding));
    }

    [SkippableFact]
    public async Task GetOwnersByBuildingAsync_ReturnsOwnersOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var owners = await _fixture.CreateBDLayout().GetOwnersByBuildingAsync(buildingId);

        Assert.All(owners, o => Assert.Equal(buildingId, o.IdBuilding));
    }

    [SkippableFact]
    public async Task GetUnitsByBuildingAsync1_ReturnsUnitsOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var units = await _fixture.CreateBDLayout().GetUnitsByBuildingAsync1(buildingId);

        Assert.All(units, u => Assert.Equal(buildingId, u.IdBuilding));
    }

    [SkippableFact]
    public async Task GetCategoriesAsync_ReturnsCategoriesOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var categories = await _fixture.CreateBDLayout().GetCategoriesAsync(buildingId);

        Assert.All(categories, c => Assert.Equal(buildingId, c.IdBuilding));
    }

    [SkippableFact]
    public async Task GetParametersByBuildingAsync_DoesNotThrow()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var parameters = await _fixture.CreateBDLayout().GetParametersByBuildingAsync(buildingId);

        Assert.NotNull(parameters);
    }

    [SkippableFact]
    public async Task GetBankAccountsByBuildingAsync_ReturnsAccountsOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var accounts = await _fixture.CreateBDLayout().GetBankAccountsByBuildingAsync(buildingId);

        Assert.All(accounts, a => Assert.Equal(buildingId, a.IdBuilding));
    }

    [SkippableFact]
    public async Task GetIncidentsByBuildingAsync_ReturnsIncidentsOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var incidents = await _fixture.CreateBDLayout().GetIncidentsByBuildingAsync(buildingId);

        Assert.All(incidents, i => Assert.Equal(buildingId, i.IdBuilding));
    }

    [SkippableFact]
    public async Task GetCommonAreasByBuildingAsync_ReturnsAreasOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var areas = await _fixture.CreateBDLayout().GetCommonAreasByBuildingAsync(buildingId);

        Assert.All(areas, a => Assert.Equal(buildingId, a.IdBuilding));
    }

    [SkippableFact]
    public async Task GetReservationsByBuildingAsync_ReturnsReservationsOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var reservations = await _fixture.CreateBDLayout().GetReservationsByBuildingAsync(buildingId);

        Assert.All(reservations, r => Assert.Equal(buildingId, r.IdBuilding));
    }

    [SkippableFact]
    public async Task GetCalendarItemsByBuildingAsync_WithoutDateRange_DoesNotThrow()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var items = await _fixture.CreateBDLayout().GetCalendarItemsByBuildingAsync(buildingId, from: null, to: null);

        Assert.All(items, i => Assert.Equal(buildingId, i.IdBuilding));
    }

    [SkippableFact]
    public async Task GetAnnouncementsByBuildingAsync_ReturnsAnnouncementsOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var announcements = await _fixture.CreateBDLayout().GetAnnouncementsByBuildingAsync(buildingId);

        Assert.All(announcements, a => Assert.Equal(buildingId, a.IdBuilding));
    }

    [SkippableFact]
    public async Task GetMeetingsByBuildingAsync_ReturnsMeetingsOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var meetings = await _fixture.CreateBDLayout().GetMeetingsByBuildingAsync(buildingId);

        Assert.All(meetings, m => Assert.Equal(buildingId, m.IdBuilding));
    }

    [SkippableFact]
    public async Task GetPeriodsByBuildingAsync_ReturnsPeriodsOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var periods = await _fixture.CreateBDLayout().GetPeriodsByBuildingAsync(buildingId);

        Assert.All(periods, p => Assert.Equal(buildingId, p.IdBuilding));
    }

    // BUG REAL encontrado al escribir este test (no arreglado acá a propósito -- el
    // pedido era ampliar cobertura de tests, no tocar Stored Procedures de producción):
    // GET_ExpensesByBuilding no selecciona la columna RequiresExpenseCreation que
    // ViewExpense sí mapea (a diferencia de GET_PendingConciliationExpenses, que sí la
    // trae desde Database/Scripts/2026-09-14_101_Fix_GET_PendingConciliationExpenses_
    // RequiresExpenseCreation.sql -- ver el comentario en ViewExpense.cs). Como
    // ExpensePage.razor llama exactamente este método para cargar /expense
    // (ExpenseService.GetExpensesByBuildingAsync, línea ~434), HOY esa página tira
    // excepción para cualquier edificio con este esquema. Este test documenta el bug
    // en vez de esconderlo: si alguien arregla el SP (agregando la columna, ej.
    // `CAST(0 AS BIT) AS RequiresExpenseCreation`), este test se pondrá en rojo -- esa
    // es la señal de que hay que actualizarlo para volver a esperar éxito.
    [SkippableFact]
    public async Task GetExpensesByBuildingAsync_CurrentlyFailsBecauseTheStoredProcedureIsMissingAColumn()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var ex = await Assert.ThrowsAsync<RepositoryException>(
            () => _fixture.CreateBDLayout().GetExpensesByBuildingAsync(buildingId));

        Assert.Contains("RequiresExpenseCreation", ex.InnerException?.Message ?? ex.Message);
    }

    [SkippableFact]
    public async Task GetExpenseTemplatesByBuildingAsync_ReturnsTemplatesOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var templates = await _fixture.CreateBDLayout().GetExpenseTemplatesByBuildingAsync(buildingId);

        Assert.All(templates, t => Assert.Equal(buildingId, t.IdBuilding));
    }

    [SkippableFact]
    public async Task GetServiceReadingListAsync_ReturnsReadingsOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var readings = await _fixture.CreateBDLayout().GetServiceReadingListAsync(buildingId);

        Assert.All(readings, r => Assert.Equal(buildingId, r.IdBuilding));
    }

    [SkippableFact]
    public async Task GetBudgetsAsync_ReturnsBudgetsOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var budgets = await _fixture.CreateBDLayout().GetBudgetsAsync(buildingId);

        Assert.All(budgets, b => Assert.Equal(buildingId, b.IdBuilding));
    }

    [SkippableFact]
    public async Task GetPendingInstallmentsAsync_DoesNotThrow()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var installments = await _fixture.CreateBDLayout().GetPendingInstallmentsAsync(buildingId);

        Assert.NotNull(installments);
    }

    [SkippableFact]
    public async Task GetExonerationsByBuildingAsync_ReturnsExonerationsOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var exonerations = await _fixture.CreateBDLayout().GetExonerationsByBuildingAsync(buildingId);

        Assert.All(exonerations, e => Assert.Equal(buildingId, e.IdBuilding));
    }

    [SkippableFact]
    public async Task GetEmployeeBuildingAssignmentsByBuildingAsync_ReturnsAssignmentsOnlyForThatBuilding()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var buildingId = await _fixture.GetAnyBuildingIdAsync();

        var assignments = await _fixture.CreateBDLayout().GetEmployeeBuildingAssignmentsByBuildingAsync(buildingId);

        Assert.All(assignments, a => Assert.Equal(buildingId, a.IdBuilding));
    }
}
