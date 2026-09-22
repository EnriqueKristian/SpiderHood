using SpiderHood.Models;

namespace SpiderHood.Tests.Integration;

// Pedido del usuario (2026-09-22): probar concurrencia en ediciones simultáneas
// de Edificio/Unidad/Propietario. UPD_Building (y UPD_Unit/UPD_Owner, mismo
// patrón) reescribe la fila COMPLETA con lo que tenga en memoria el que
// guarda -- no hay ninguna columna de versión/timestamp ni chequeo de "nadie
// más lo tocó mientras tanto". BuildingPage.razor carga el Building COMPLETO
// al abrir el modal de editar (Building.Clone() de la fila entera) y
// UpdateBuildingAsync manda TODOS los campos de vuelta, no sólo los del tab
// que se estaba editando -- así que si dos administradores editan el mismo
// edificio a la vez (uno el Tab 1 "Nombre", otro el Tab 3 "Amenidades"), el
// que guarda SEGUNDO pisa en silencio el cambio del primero con los datos
// viejos que tenía cargados desde antes de que el primero guardara
// ("lost update" clásico).
[Collection("Database")]
public class BuildingConcurrencyTests
{
    private readonly DatabaseFixture _fixture;

    public BuildingConcurrencyTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task TwoConcurrentEditors_DifferentFields_SecondSaveSilentlyOverwritesTheFirst()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        var ec = _fixture.CreateBDLayout();

        // Edificio de prueba propio (no uno real) -- así el test no corre ningún
        // riesgo sobre datos reales, ni siquiera temporalmente.
        var original = new Building
        {
            IdBuilding = Guid.NewGuid(),
            Name = "QA Concurrency Test",
            Location = "Test",
            Type = 1,
            Floors = 5,
            Basements = 0,
            Apartments = 10,
            Parkings = 5,
            Deposits = 2,
            Others = 0,
            TotalArea = 100m,
            IsActive = true,
            IsTemplate = false,
            Elevators = 1,
            HasPool = false,
        };
        await ec.AddNewRecordAsync(original);

        try
        {
            // Los dos "usuarios" abren el modal de editar al mismo tiempo -- cada
            // uno se lleva su propia copia completa del estado ORIGINAL.
            var comoLoVeUsuarioA = original.Clone();
            var comoLoVeUsuarioB = original.Clone();

            // Usuario A edita el Nombre (Tab 1) y guarda primero.
            comoLoVeUsuarioA.Name = "Renombrado por Usuario A";
            await ec.UpdateRecordAsync(comoLoVeUsuarioA);

            // Usuario B edita Elevators (Tab 2, un campo DISTINTO) y guarda
            // segundo -- pero su copia en memoria sigue teniendo el Name
            // ORIGINAL, porque cargó su copia ANTES de que A guardara.
            comoLoVeUsuarioB.Elevators = 3;
            await ec.UpdateRecordAsync(comoLoVeUsuarioB);

            var estadoFinal = await ec.GetBuildingByIdAsync(original.IdBuilding);

            Assert.True(estadoFinal.Name == "Renombrado por Usuario A" && estadoFinal.Elevators == 3,
                $"Se esperaba que sobrevivieran los cambios de AMBOS usuarios (Name='Renombrado por Usuario A', " +
                $"Elevators=3), pero quedó Name='{estadoFinal.Name}', Elevators={estadoFinal.Elevators}. " +
                "Esto confirma un 'lost update': como UPD_Building reescribe la fila completa sin ningún chequeo " +
                "de versión, el segundo guardado (Usuario B) pisó en silencio el cambio de Nombre del primero " +
                "(Usuario A) con el valor viejo que tenía cargado en memoria desde antes.");
        }
        finally
        {
            await using var conn = new Microsoft.Data.SqlClient.SqlConnection(TryReadConnectionString());
            await conn.OpenAsync();
            await using var cmd = new Microsoft.Data.SqlClient.SqlCommand("DELETE FROM Building WHERE IdBuilding = @id", conn);
            cmd.Parameters.AddWithValue("@id", original.IdBuilding);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // Mismo helper duplicado que ReservationConcurrencyTests -- DatabaseFixture
    // no expone su connection string.
    private static string? TryReadConnectionString()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SpiderHood.slnx")))
            {
                var path = Path.Combine(dir.FullName, "SpiderHood", "appsettings.Development.json");
                if (!File.Exists(path)) return null;

                using var stream = File.OpenRead(path);
                using var doc = System.Text.Json.JsonDocument.Parse(stream);
                return doc.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings) &&
                       connectionStrings.TryGetProperty("SpiderHoodContext", out var connectionString)
                    ? connectionString.GetString()
                    : null;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
