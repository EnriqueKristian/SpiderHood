using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Docs/Pendientes-Negocio-Consolidado.md #21, "Gobernanza -- Reuniones,
    // Votación y Actas como un solo flujo" -- diseño cerrado 2026-09-11,
    // alcance de la primera entrega decidido 2026-09-15 (Reuniones+Votación+
    // Actas juntas, Encuestas después). Esta es la Fase 1: Convocatoria +
    // Agenda + Asistencia + Quórum + Segunda Convocatoria. La Votación real
    // (Fase 2) y las Actas (Fase 3) se construyen sobre esta base.
    //
    // Flujo: Convocada -> (Administrador/Junta registra Asistencia) ->
    // IniciarReunionAsync calcula el quórum -> si alcanza: EnCurso -> luego
    // FinalizarAsync; si no alcanza: QuorumNoAlcanzado -> se puede crear una
    // Segunda Convocatoria (nueva Reunion con el mismo agenda, quórum
    // reducido según el Reglamento Interno de cada edificio -- el sistema no
    // asume un número fijo, lo ingresa el Administrador).
    public interface IReunionService
    {
        Task<List<Reunion>> GetReunionesAsync(Guid idBuilding);

        // Compone la Reunion con su Agenda y sus Asistentes (con NombreUnidad
        // resuelto) -- GET_ReunionById solo trae la fila de Reunion.
        Task<Reunion?> GetReunionByIdAsync(Guid idReunion);

        // Roster de unidades del edificio con su alícuota ya calculada
        // (OwnerUnitView.TotalArea / Building.TotalArea) y, si se pasa
        // idReunion, marcadas Presente según la Asistencia ya registrada.
        Task<List<UnidadConAlicuota>> GetRosterAlicuotasAsync(Guid idBuilding, Guid? idReunion = null);

        // Crea la Reunion + su Agenda + el CalendarItem asociado (inserción
        // directa, sin correo masivo -- mismo patrón que Reserva).
        Task<ConvocarReunionResultado> ConvocarAsync(Reunion reunion, List<AgendaItem> agenda);

        // Sólo válido mientras Estado=Convocada -- el service lo valida.
        Task ActualizarConvocatoriaAsync(Reunion reunion);

        Task<AgendaItem> AgregarAgendaItemAsync(AgendaItem item);

        Task EditarAgendaItemAsync(AgendaItem item);

        Task EliminarAgendaItemAsync(Guid idAgendaItem);

        Task RegistrarAsistenciaAsync(Guid idReunion, Guid idGroupUnit, Guid registradoPor);

        Task QuitarAsistenciaAsync(Guid idReunion, Guid idGroupUnit);

        // Suma las alícuotas de los asistentes registrados y decide si se
        // alcanza QuorumRequerido -- Estado pasa a EnCurso o
        // QuorumNoAlcanzado según el resultado.
        Task<IniciarReunionResultado> IniciarReunionAsync(Guid idReunion);

        // Sólo válido sobre una Reunion en QuorumNoAlcanzado. Clona la Agenda
        // completa (la reunión original nunca llegó a tratarla) en una
        // Reunion nueva con IdReunionOrigen apuntando a la original.
        Task<ConvocarReunionResultado> CrearSegundaConvocatoriaAsync(Guid idReunionOrigen, DateTime nuevaFechaReunion, decimal nuevoQuorumRequerido, string? nuevoLugarOVinculo);

        // Marca todos los puntos Informativos como Informado y cierra la
        // Reunion. Los puntos Sujetos a Votación quedan como estén (Fase 2
        // les da su mecánica real) -- no se fuerza ningún resultado acá.
        Task FinalizarReunionAsync(Guid idReunion);

        Task CancelarReunionAsync(Guid idReunion);

        // Override manual de un punto de agenda -- en Fase 1 sólo tiene
        // sentido para puntos Informativos (marcar "Informado" durante la
        // reunión, antes de Finalizar). Los Sujetos a Votación se gatean
        // desde el service (ver implementación) hasta que exista Votación.
        Task MarcarAgendaItemEstadoAsync(Guid idAgendaItem, EstadoAgendaItem nuevoEstado);
    }

    public class ReunionService : IReunionService
    {
        private readonly ICalendarService _calendarService;
        private readonly ILogger<ReunionService> _logger;
        private BDLayout ec { get; set; }

        public ReunionService(
            IDbContextFactory<SpiderHoodContext> contextFactory,
            ICalendarService calendarService,
            ILogger<ReunionService> logger)
        {
            _calendarService = calendarService;
            _logger = logger;
            ec = new BDLayout(contextFactory);
        }

        public async Task<List<Reunion>> GetReunionesAsync(Guid idBuilding)
            => await ec.GetReunionesByBuildingAsync(idBuilding);

        public async Task<Reunion?> GetReunionByIdAsync(Guid idReunion)
        {
            Reunion reunion;
            try
            {
                reunion = await ec.GetReunionByIdAsync(idReunion);
            }
            catch (EntityNotFoundException)
            {
                return null;
            }

            reunion.Agenda = await ec.GetAgendaItemsByReunionAsync(idReunion);

            var asistentes = await ec.GetAsistenciasByReunionAsync(idReunion);
            if (asistentes.Count > 0)
            {
                var unidades = await ec.GetOwnersByBuildingAsync(reunion.IdBuilding);
                var nombresPorGrupo = ConstruirNombresPorGrupo(unidades);
                foreach (var asistente in asistentes)
                    asistente.NombreUnidad = nombresPorGrupo.TryGetValue(asistente.IdGroupUnit, out var nombre) ? nombre : "Unidad";
            }
            reunion.Asistentes = asistentes;

            return reunion;
        }

        public async Task<List<UnidadConAlicuota>> GetRosterAlicuotasAsync(Guid idBuilding, Guid? idReunion = null)
        {
            var building = await ec.GetBuildingByIdAsync(idBuilding);
            var unidades = await ec.GetOwnersByBuildingAsync(idBuilding);

            var presentes = idReunion.HasValue
                ? (await ec.GetAsistenciasByReunionAsync(idReunion.Value)).Select(a => a.IdGroupUnit).ToHashSet()
                : new HashSet<Guid>();

            return unidades
                .GroupBy(u => u.IdGroupUnit)
                .Select(g =>
                {
                    var primero = g.First();
                    return new UnidadConAlicuota
                    {
                        IdGroupUnit = g.Key,
                        NombreUnidad = $"DPTO {primero.GroupNumber} - {primero.FirstName} {primero.LastName}",
                        // Alícuota -- decisión cerrada 2026-09-11: se deriva, no es un campo
                        // nuevo (OwnerUnitView.TotalArea / Building.TotalArea).
                        Alicuota = building.TotalArea > 0 ? Math.Round(primero.TotalArea / building.TotalArea * 100m, 6) : 0m,
                        Presente = presentes.Contains(g.Key)
                    };
                })
                .OrderBy(u => u.NombreUnidad)
                .ToList();
        }

        private static Dictionary<Guid, string> ConstruirNombresPorGrupo(List<OwnerUnitView> unidades)
            => unidades
                .GroupBy(u => u.IdGroupUnit)
                .ToDictionary(g => g.Key, g => $"DPTO {g.First().GroupNumber} - {g.First().FirstName} {g.First().LastName}");

        public async Task<ConvocarReunionResultado> ConvocarAsync(Reunion reunion, List<AgendaItem> agenda)
        {
            if (agenda.Count == 0)
                return new ConvocarReunionResultado { Exito = false, Mensaje = "La agenda debe tener al menos un punto." };

            reunion.IdReunion = Guid.NewGuid();
            reunion.Estado = EstadoReunion.Convocada;

            var calendarItem = await CrearCalendarItemDeReunionAsync(reunion);
            reunion.IdCalendarItem = calendarItem.IdCalendarItem;

            await ec.AddNewRecordAsync(reunion);

            var orden = 1;
            foreach (var item in agenda)
            {
                item.IdAgendaItem = Guid.NewGuid();
                item.IdReunion = reunion.IdReunion;
                item.Orden = orden++;
                item.Estado = EstadoAgendaItem.Pendiente;
                await ec.AddNewRecordAsync(item);
            }

            return new ConvocarReunionResultado { Exito = true, Mensaje = "Reunión convocada.", IdReunion = reunion.IdReunion };
        }

        public async Task ActualizarConvocatoriaAsync(Reunion reunion)
        {
            var existente = await ec.GetReunionByIdAsync(reunion.IdReunion);
            if (existente.Estado != EstadoReunion.Convocada)
                throw new InvalidOperationException("Sólo se puede editar la convocatoria mientras está en estado Convocada.");

            await ec.UpdateReunionAsync(reunion);

            var calendarItem = await _calendarService.GetByIdAsync(existente.IdCalendarItem!.Value);
            calendarItem.Title = $"Asamblea: {reunion.Titulo}";
            calendarItem.StartDate = reunion.FechaReunion;
            calendarItem.Location = reunion.LugarOVinculo ?? string.Empty;
            calendarItem.ModifiedOn = DateTime.UtcNow;
            await _calendarService.UpdateAsync(calendarItem);
        }

        public async Task<AgendaItem> AgregarAgendaItemAsync(AgendaItem item)
        {
            var reunion = await ec.GetReunionByIdAsync(item.IdReunion);
            if (reunion.Estado != EstadoReunion.Convocada)
                throw new InvalidOperationException("Sólo se pueden agregar puntos de agenda mientras la reunión está Convocada.");

            var agendaActual = await ec.GetAgendaItemsByReunionAsync(item.IdReunion);
            item.IdAgendaItem = Guid.NewGuid();
            item.Orden = agendaActual.Count + 1;
            item.Estado = EstadoAgendaItem.Pendiente;
            await ec.AddNewRecordAsync(item);
            return item;
        }

        public async Task EditarAgendaItemAsync(AgendaItem item)
            => await ec.UpdateAgendaItemAsync(item);

        public async Task EliminarAgendaItemAsync(Guid idAgendaItem)
            => await ec.DeleteAgendaItemAsync(idAgendaItem);

        public async Task RegistrarAsistenciaAsync(Guid idReunion, Guid idGroupUnit, Guid registradoPor)
        {
            var reunion = await ec.GetReunionByIdAsync(idReunion);
            var roster = await GetRosterAlicuotasAsync(reunion.IdBuilding);
            var unidad = roster.FirstOrDefault(u => u.IdGroupUnit == idGroupUnit)
                ?? throw new InvalidOperationException("La unidad no pertenece a este edificio.");

            await ec.AddNewRecordAsync(new Asistencia
            {
                IdAsistencia = Guid.NewGuid(),
                IdReunion = idReunion,
                IdGroupUnit = idGroupUnit,
                Alicuota = unidad.Alicuota,
                RegistradoPor = registradoPor
            });
        }

        public async Task QuitarAsistenciaAsync(Guid idReunion, Guid idGroupUnit)
            => await ec.DeleteAsistenciaAsync(idReunion, idGroupUnit);

        public async Task<IniciarReunionResultado> IniciarReunionAsync(Guid idReunion)
        {
            var reunion = await ec.GetReunionByIdAsync(idReunion);
            if (reunion.Estado != EstadoReunion.Convocada)
                return new IniciarReunionResultado { Exito = false, Mensaje = "La reunión ya fue iniciada, finalizada o cancelada." };

            var asistentes = await ec.GetAsistenciasByReunionAsync(idReunion);
            var porcentajeAlcanzado = asistentes.Sum(a => a.Alicuota);
            var alcanzaQuorum = porcentajeAlcanzado >= reunion.QuorumRequerido;

            await ec.UpdateReunionEstadoAsync(
                idReunion,
                alcanzaQuorum ? EstadoReunion.EnCurso : EstadoReunion.QuorumNoAlcanzado,
                quorumAlcanzado: porcentajeAlcanzado);

            return new IniciarReunionResultado
            {
                Exito = true,
                Mensaje = alcanzaQuorum ? "Quórum alcanzado -- la reunión está en curso." : "Quórum no alcanzado.",
                QuorumAlcanzado = alcanzaQuorum,
                PorcentajeAlcanzado = porcentajeAlcanzado
            };
        }

        public async Task<ConvocarReunionResultado> CrearSegundaConvocatoriaAsync(Guid idReunionOrigen, DateTime nuevaFechaReunion, decimal nuevoQuorumRequerido, string? nuevoLugarOVinculo)
        {
            var original = await ec.GetReunionByIdAsync(idReunionOrigen);
            if (original.Estado != EstadoReunion.QuorumNoAlcanzado)
                return new ConvocarReunionResultado { Exito = false, Mensaje = "Sólo se puede convocar una segunda convocatoria cuando la original no alcanzó quórum." };

            var agendaOriginal = await ec.GetAgendaItemsByReunionAsync(idReunionOrigen);

            var nueva = new Reunion
            {
                IdBuilding = original.IdBuilding,
                Tipo = original.Tipo,
                Titulo = $"{original.Titulo} (Segunda Convocatoria)",
                FechaConvocatoria = DateTime.UtcNow,
                FechaReunion = nuevaFechaReunion,
                Modalidad = original.Modalidad,
                LugarOVinculo = nuevoLugarOVinculo ?? original.LugarOVinculo,
                QuorumRequerido = nuevoQuorumRequerido,
                IdReunionOrigen = idReunionOrigen,
                CreatedBy = original.CreatedBy
            };

            var agendaClonada = agendaOriginal.Select(a => new AgendaItem
            {
                Titulo = a.Titulo,
                Descripcion = a.Descripcion,
                Tipo = a.Tipo,
                TipoVotacion = a.TipoVotacion,
                TipoMayoria = a.TipoMayoria,
                PorcentajeMayoriaCalificada = a.PorcentajeMayoriaCalificada,
                PermiteRevotacion = a.PermiteRevotacion
            }).ToList();

            return await ConvocarAsync(nueva, agendaClonada);
        }

        public async Task FinalizarReunionAsync(Guid idReunion)
        {
            var reunion = await ec.GetReunionByIdAsync(idReunion);
            if (reunion.Estado != EstadoReunion.EnCurso)
                throw new InvalidOperationException("Sólo se puede finalizar una reunión que está En Curso.");

            var agenda = await ec.GetAgendaItemsByReunionAsync(idReunion);
            foreach (var item in agenda.Where(a => a.Tipo == TipoAgendaItem.Informativo && a.Estado == EstadoAgendaItem.Pendiente))
                await ec.UpdateAgendaItemEstadoAsync(item.IdAgendaItem, EstadoAgendaItem.Informado);

            await ec.UpdateReunionEstadoAsync(idReunion, EstadoReunion.Finalizada);

            // UPD_CalendarItem (usado por ICalendarService.UpdateAsync) no toca la
            // columna Status -- tiene su propio SP dedicado (mismo motivo que
            // UPD_ReservaEstado está separado de un UPDATE genérico). Setear
            // calendarItem.Status y llamar UpdateAsync no hace nada en silencio.
            if (reunion.IdCalendarItem.HasValue)
                await _calendarService.ChangeStatusAsync(reunion.IdCalendarItem.Value, CalendarItemStatus.Completed, reunion.CreatedBy.ToString());
        }

        public async Task CancelarReunionAsync(Guid idReunion)
        {
            var reunion = await ec.GetReunionByIdAsync(idReunion);
            if (reunion.Estado == EstadoReunion.Finalizada || reunion.Estado == EstadoReunion.Cancelada)
                throw new InvalidOperationException("La reunión ya está finalizada o cancelada.");

            await ec.UpdateReunionEstadoAsync(idReunion, EstadoReunion.Cancelada);

            if (reunion.IdCalendarItem.HasValue)
                await _calendarService.DeleteAsync(reunion.IdCalendarItem.Value, deleteSeries: false, reunion.CreatedBy.ToString());
        }

        public async Task MarcarAgendaItemEstadoAsync(Guid idAgendaItem, EstadoAgendaItem nuevoEstado)
            => await ec.UpdateAgendaItemEstadoAsync(idAgendaItem, nuevoEstado);

        // Se inserta directo por BDLayout, sin pasar por ICalendarService.CreateAsync --
        // mismo motivo que Reserva (Docs/Pendientes-Negocio-Consolidado.md #21): ese
        // método manda un correo a TODOS los residentes por cada CalendarItem nuevo, y acá
        // ya existe el propio flujo de convocatoria (con acuse de recibo, cuando el módulo
        // de Comunicaciones esté listo) para notificar la Reunión -- un correo genérico de
        // "Nuevo evento programado" sería redundante y confuso.
        private async Task<CalendarItem> CrearCalendarItemDeReunionAsync(Reunion reunion)
        {
            var calendarItem = new CalendarItem
            {
                IdCalendarItem = Guid.NewGuid(),
                IdBuilding = reunion.IdBuilding,
                Title = $"Asamblea: {reunion.Titulo}",
                Description = $"{(reunion.Tipo == TipoReunion.Ordinaria ? "Reunión Ordinaria" : "Reunión Extraordinaria")} -- {reunion.Modalidad}",
                Type = CalendarItemType.Event,
                StartDate = reunion.FechaReunion,
                Location = reunion.LugarOVinculo ?? string.Empty,
                Status = CalendarItemStatus.Scheduled,
                CreatedBy = reunion.CreatedBy.ToString(),
                CreatedOn = DateTime.UtcNow
            };
            await ec.AddNewRecordAsync(calendarItem);
            return calendarItem;
        }
    }
}
