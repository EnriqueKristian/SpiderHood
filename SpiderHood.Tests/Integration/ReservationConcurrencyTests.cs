using Microsoft.Data.SqlClient;
using SpiderHood.Models;

namespace SpiderHood.Tests.Integration;

// Pedido del usuario (2026-09-22): probar stress y concurrencia en un flujo de
// negocio real con datos/dinero en juego. IReservationService.SolicitarAsync
// (Classes/Reservations -- Docs/Pendientes-Negocio-Consolidado.md #21) hace
// "leer conflictos de horario, si no hay ninguno insertar la reserva" sin
// ninguna transacción ni lock a nivel BD (ni SELECT ... WITH (UPDLOCK,
// HOLDLOCK), ni un índice único que impida el solapamiento) -- un TOCTOU
// clásico: dos solicitudes para el mismo Área Común y mismo horario pueden
// pasar el chequeo de conflicto ANTES de que cualquiera de las dos haya
// insertado todavía, y las dos terminan reservando el mismo horario.
//
// Este test reproduce el patrón exacto de SolicitarAsync directamente contra
// BDLayout (sin pasar por ReservationService/CalendarService/EmailService,
// que sólo agregan side-effects no relacionados a la carrera que se está
// midiendo) disparando N solicitudes concurrentes para la misma Área Común y
// el mismo horario, y confirma cuántas terminan insertadas de verdad.
[Collection("Database")]
public class ReservationConcurrencyTests
{
    private readonly DatabaseFixture _fixture;

    public ReservationConcurrencyTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task ConcurrentSolicitudes_MismaAreaMismoHorario_SoloUnaDeberiaGanarLaReserva()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);

        var connectionString = TryReadConnectionString();
        Skip.If(connectionString is null, "No se pudo leer la connection string para el cleanup de este test.");

        var idBuilding = await _fixture.GetAnyBuildingIdAsync();
        var ec = _fixture.CreateBDLayout();

        // GET_ReservationsConflicto hace LEFT JOIN Users por CreatedBy para armar
        // CreatedByName (concatenado NOT NULL en el modelo C#) -- un CreatedBy que
        // no matchee ningún usuario real deja ese JOIN en NULL y revienta con
        // SqlNullValueException al leer la fila (mismo bug ya documentado varias
        // veces en este backlog para otras vistas). Se usa un IdUser real para no
        // toparse con ESE bug (harina de otro costal) mientras se mide la
        // condición de carrera real de este test.
        Guid idUserReal;
        await using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT TOP 1 IdUser FROM Users", conn);
            idUserReal = (Guid?)await cmd.ExecuteScalarAsync() ?? Guid.Empty;
        }
        Skip.If(idUserReal == Guid.Empty, "La BD restaurada no tiene ningún usuario real para usar como CreatedBy.");

        // Área Común de prueba, sin tope por unidad (null) a propósito -- ese
        // chequeo es una regla de negocio distinta (cuántas reservas activas
        // puede tener una MISMA unidad), no la condición de carrera que este
        // test mide (dos solicitudes cualquiera para el mismo horario).
        var areaComun = new CommonArea
        {
            IdCommonArea = Guid.NewGuid(),
            IdBuilding = idBuilding,
            Nombre = "QA Stress Test -- borrar si aparece",
            BufferMinutos = 0,
            AnticipacionMinHoras = 0,
            TopeReservationsActivasPorUnidad = null,
            GarantiaInternos = 0,
            GarantiaExternos = 0,
            AlquilerInternos = 0,
            AlquilerExternos = 0,
            Limpieza = 0,
            Activo = true,
            CreatedBy = idUserReal,
            CreatedOn = DateTime.Now,
        };
        await ec.AddNewRecordAsync(areaComun);

        var fechaInicio = DateTime.Now.AddDays(3);
        var fechaFin = fechaInicio.AddHours(1);
        const int concurrencia = 20;
        var idsReservasCreadas = new System.Collections.Concurrent.ConcurrentBag<Guid>();

        try
        {
            // Todas las tareas arrancan a la vez (startGate) y hay un delay
            // artificial entre el chequeo y el insert -- sin esto, en un SQL
            // Server local (loopback, sub-milisegundo) las N lecturas y escrituras
            // pueden terminar tan rápido que la carrera no llega a manifestarse
            // "por casualidad" en esta corrida puntual (no porque el código esté
            // protegido, sino porque nadie llegó a pisarse). Ensanchar la ventana
            // deliberadamente es la única forma de probar un TOCTOU de forma
            // confiable en un test, en vez de depender del timing real del
            // entorno donde corra.
            var startGate = new TaskCompletionSource();

            var tareas = Enumerable.Range(0, concurrencia).Select(async _ =>
            {
                await startGate.Task;

                // Réplica exacta del patrón de IReservationService.SolicitarAsync:
                // leer conflictos, y si no hay ninguno, insertar -- sin transacción.
                var conflictos = await ec.GetReservationsConflictoAsync(areaComun.IdCommonArea, fechaInicio, fechaFin);
                if (conflictos.Count > 0)
                    return false;

                await Task.Delay(150);

                var idReservation = Guid.NewGuid();
                await ec.AddNewRecordAsync(new Reservation
                {
                    IdReservation = idReservation,
                    IdBuilding = idBuilding,
                    IdCommonArea = areaComun.IdCommonArea,
                    IdGroupUnit = Guid.NewGuid(),
                    FechaInicio = fechaInicio,
                    FechaFin = fechaFin,
                    EsExterno = false,
                    Estado = ReservationStatus.PendienteDeAprobacion,
                    CreatedBy = idUserReal,
                    CreatedOn = DateTime.Now,
                });
                idsReservasCreadas.Add(idReservation);
                return true;
            }).ToList();

            startGate.SetResult();
            var resultados = await Task.WhenAll(tareas);
            var exitosas = resultados.Count(r => r);

            // Confirmación independiente contra la BD real, no sólo los booleanos
            // en memoria de cada Task.
            var reservasReales = await ec.GetReservationsConflictoAsync(areaComun.IdCommonArea, fechaInicio, fechaFin);

            Assert.True(exitosas <= 1,
                $"Se esperaba que sólo 1 de {concurrencia} solicitudes concurrentes ganara la reserva del mismo " +
                $"horario, pero {exitosas} pasaron el chequeo de conflicto e insertaron -- quedaron " +
                $"{reservasReales.Count} reservas activas para la misma Área Común en el mismo horario " +
                "(double-booking real). Confirma que IReservationService.SolicitarAsync (mismo patrón acá) tiene " +
                "una condición de carrera real: lee conflictos y recién después inserta, sin transacción ni lock.");
        }
        finally
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            foreach (var idReservation in idsReservasCreadas)
            {
                await using var delReserva = new SqlCommand("DELETE FROM Reservation WHERE IdReservation = @id", conn);
                delReserva.Parameters.AddWithValue("@id", idReservation);
                await delReserva.ExecuteNonQueryAsync();
            }

            await using var delArea = new SqlCommand("DELETE FROM CommonArea WHERE IdCommonArea = @id", conn);
            delArea.Parameters.AddWithValue("@id", areaComun.IdCommonArea);
            await delArea.ExecuteNonQueryAsync();
        }
    }

    // Duplica el helper privado de DatabaseFixture -- sólo se necesita acá para
    // el cleanup vía SqlConnection directa (DatabaseFixture no expone su
    // connection string ni su IDbContextFactory).
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
