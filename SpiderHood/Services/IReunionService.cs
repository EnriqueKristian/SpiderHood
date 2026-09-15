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

        // Override manual de un punto de agenda -- para un punto Informativo,
        // marcar "Informado" durante la reunión (antes de Finalizar); para un
        // punto Sujeto a Votación, permite al operador cerrar el punto como
        // Rechazado a mano cuando decide no abrir más rondas (ver
        // CerrarVotacionAsync).
        Task MarcarAgendaItemEstadoAsync(Guid idAgendaItem, EstadoAgendaItem nuevoEstado);

        // ===================== Votación (Fase 2) =====================

        // Todas las rondas de votación de un punto de agenda, con sus Votos
        // (NombreUnidad resuelto sólo si TipoVotacion=Nominal -- una votación
        // Secreta no expone en la UI quién votó qué, aunque el voto se
        // guarda con su IdGroupUnit real para poder validar quórum e impedir
        // doble voto; una secrecía criptográfica real queda fuera de alcance
        // de esta primera versión).
        Task<List<Votacion>> GetVotacionesAsync(Guid idAgendaItem);

        // Abre una nueva ronda sobre un AgendaItem.Tipo=SujetoAVotacion --
        // sólo válido con la Reunion En Curso, el punto todavía Pendiente, y
        // sin ninguna ronda ya abierta para ese mismo punto.
        Task<Votacion> IniciarVotacionAsync(Guid idAgendaItem, Guid iniciadoPor);

        // Sólo pueden votar unidades ya registradas como Asistencia de la
        // Reunion (usa esa misma Alicuota). Permite corregir un voto ya
        // cargado mientras la Votación sigue Abierta (upsert).
        Task RegistrarVotoAsync(Guid idVotacion, Guid idGroupUnit, OpcionVoto opcion, Guid registradoPor);

        // Suma las alícuotas por opción y evalúa la mayoría requerida por el
        // AgendaItem (Simple/Calificada/75% legal). Si alcanza: el punto
        // queda Aprobado. Si no alcanza y PermiteRevotacion está apagado: el
        // punto queda Rechazado automáticamente. Si no alcanza y
        // PermiteRevotacion está prendido: el punto queda Pendiente -- el
        // operador decide si abre una nueva ronda (IniciarVotacionAsync) o
        // lo rechaza a mano (MarcarAgendaItemEstadoAsync).
        Task<CerrarVotacionResultado> CerrarVotacionAsync(Guid idVotacion);

        // ===================== Actas (Fase 3) =====================

        Task<Acta?> GetActaAsync(Guid idReunion);

        // Compone el contenido desde Reunion+Agenda+Asistencia+Votacion y
        // crea el Acta si no existe, o la regenera si sigue en Borrador --
        // sólo válido con la Reunion Finalizada o QuorumNoAlcanzado (son los
        // dos estados con un desenlace real que registrar), y rechaza
        // regenerar un Acta ya Firmada (inmutable).
        Task<Acta> GenerarBorradorActaAsync(Guid idReunion, Guid generadoPor);

        // Marca el Acta Firmada -- desde ahí en adelante es inmutable
        // (GenerarBorradorActaAsync la rechaza).
        Task FirmarActaAsync(Guid idActa, string nombrePresidente, string nombreSecretario);
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

        // ===================== Votación (Fase 2) =====================

        public async Task<List<Votacion>> GetVotacionesAsync(Guid idAgendaItem)
        {
            var votaciones = await ec.GetVotacionesByAgendaItemAsync(idAgendaItem);
            if (votaciones.Count == 0) return votaciones;

            var agendaItem = await ec.GetAgendaItemByIdAsync(idAgendaItem);
            var reunion = await ec.GetReunionByIdAsync(agendaItem.IdReunion);
            var unidades = await ec.GetOwnersByBuildingAsync(reunion.IdBuilding);
            var nombresPorGrupo = ConstruirNombresPorGrupo(unidades);

            foreach (var votacion in votaciones)
            {
                var votos = await ec.GetVotosByVotacionAsync(votacion.IdVotacion);
                if (agendaItem.TipoVotacion == TipoVotacionAgenda.Nominal)
                {
                    foreach (var voto in votos)
                        voto.NombreUnidad = nombresPorGrupo.TryGetValue(voto.IdGroupUnit, out var nombre) ? nombre : "Unidad";
                }
                votacion.Votos = votos;
            }

            return votaciones;
        }

        public async Task<Votacion> IniciarVotacionAsync(Guid idAgendaItem, Guid iniciadoPor)
        {
            var agendaItem = await ec.GetAgendaItemByIdAsync(idAgendaItem);
            if (agendaItem.Tipo != TipoAgendaItem.SujetoAVotacion)
                throw new InvalidOperationException("Este punto de agenda no está sujeto a votación.");
            if (agendaItem.Estado != EstadoAgendaItem.Pendiente)
                throw new InvalidOperationException("Este punto ya fue resuelto.");

            var reunion = await ec.GetReunionByIdAsync(agendaItem.IdReunion);
            if (reunion.Estado != EstadoReunion.EnCurso)
                throw new InvalidOperationException("Sólo se puede votar mientras la reunión está En Curso.");

            var rondasPrevias = await ec.GetVotacionesByAgendaItemAsync(idAgendaItem);
            if (rondasPrevias.Any(v => v.Estado == EstadoVotacion.Abierta))
                throw new InvalidOperationException("Ya hay una votación abierta para este punto.");

            var votacion = new Votacion
            {
                IdVotacion = Guid.NewGuid(),
                IdAgendaItem = idAgendaItem,
                NroRonda = rondasPrevias.Count + 1,
                Estado = EstadoVotacion.Abierta,
                CreatedBy = iniciadoPor
            };
            await ec.AddNewRecordAsync(votacion);
            return votacion;
        }

        public async Task RegistrarVotoAsync(Guid idVotacion, Guid idGroupUnit, OpcionVoto opcion, Guid registradoPor)
        {
            var votacion = await ec.GetVotacionByIdAsync(idVotacion);
            if (votacion.Estado != EstadoVotacion.Abierta)
                throw new InvalidOperationException("Esta votación ya está cerrada.");

            var agendaItem = await ec.GetAgendaItemByIdAsync(votacion.IdAgendaItem);
            var asistentes = await ec.GetAsistenciasByReunionAsync(agendaItem.IdReunion);
            var asistente = asistentes.FirstOrDefault(a => a.IdGroupUnit == idGroupUnit)
                ?? throw new InvalidOperationException("Sólo pueden votar las unidades registradas como asistentes de la reunión.");

            // Permite corregir un voto ya cargado mientras la votación sigue abierta.
            await ec.DeleteVotoAsync(idVotacion, idGroupUnit);
            await ec.AddNewRecordAsync(new Voto
            {
                IdVoto = Guid.NewGuid(),
                IdVotacion = idVotacion,
                IdGroupUnit = idGroupUnit,
                Opcion = opcion,
                Alicuota = asistente.Alicuota,
                RegistradoPor = registradoPor
            });
        }

        public async Task<CerrarVotacionResultado> CerrarVotacionAsync(Guid idVotacion)
        {
            var votacion = await ec.GetVotacionByIdAsync(idVotacion);
            if (votacion.Estado != EstadoVotacion.Abierta)
                return new CerrarVotacionResultado { Exito = false, Mensaje = "Esta votación ya está cerrada." };

            var votos = await ec.GetVotosByVotacionAsync(idVotacion);
            var aFavor = votos.Where(v => v.Opcion == OpcionVoto.AFavor).Sum(v => v.Alicuota);
            var enContra = votos.Where(v => v.Opcion == OpcionVoto.EnContra).Sum(v => v.Alicuota);
            var abstencion = votos.Where(v => v.Opcion == OpcionVoto.Abstencion).Sum(v => v.Alicuota);

            var agendaItem = await ec.GetAgendaItemByIdAsync(votacion.IdAgendaItem);
            var alcanzada = EvaluarMayoria(agendaItem, aFavor, enContra, abstencion);

            await ec.UpdateVotacionCierreAsync(idVotacion, aFavor, enContra, abstencion, alcanzada);

            string mensaje;
            if (alcanzada)
            {
                await ec.UpdateAgendaItemEstadoAsync(agendaItem.IdAgendaItem, EstadoAgendaItem.Aprobado);
                mensaje = "Mayoría alcanzada -- punto aprobado.";
            }
            else if (!agendaItem.PermiteRevotacion)
            {
                await ec.UpdateAgendaItemEstadoAsync(agendaItem.IdAgendaItem, EstadoAgendaItem.Rechazado);
                mensaje = "Mayoría no alcanzada -- punto rechazado.";
            }
            else
            {
                // El punto permite revotación y no la agotó -- queda Pendiente para
                // que el operador abra una nueva ronda o lo rechace a mano.
                mensaje = "Mayoría no alcanzada -- se puede abrir una nueva ronda de votación.";
            }

            return new CerrarVotacionResultado
            {
                Exito = true,
                Mensaje = mensaje,
                MayoriaAlcanzada = alcanzada,
                AlicuotaAFavor = aFavor,
                AlicuotaEnContra = enContra,
                AlicuotaAbstencion = abstencion
            };
        }

        // Mayoría -- simplificación deliberada (documentada en
        // 2026-09-15_114_Gobernanza_Fase2_Votacion.sql): Simple compara
        // alícuota a favor vs. en contra; Calificada es un % sobre los votos
        // EMITIDOS (a favor + en contra + abstención), no sobre el edificio
        // completo; Legal75 (Art. 14.1 D.L. 1568) sí es sobre el edificio
        // completo, tal como lo fija la ley.
        private static bool EvaluarMayoria(AgendaItem item, decimal aFavor, decimal enContra, decimal abstencion)
        {
            var totalEmitido = aFavor + enContra + abstencion;
            return item.TipoMayoria switch
            {
                TipoMayoria.Legal75 => aFavor >= 75m,
                TipoMayoria.Calificada => totalEmitido > 0 && (aFavor / totalEmitido * 100m) >= (item.PorcentajeMayoriaCalificada ?? 100m),
                _ => aFavor > enContra // Simple
            };
        }

        // ===================== Actas (Fase 3) =====================

        public async Task<Acta?> GetActaAsync(Guid idReunion)
            => await ec.GetActaByReunionAsync(idReunion);

        public async Task<Acta> GenerarBorradorActaAsync(Guid idReunion, Guid generadoPor)
        {
            var reunion = await ec.GetReunionByIdAsync(idReunion);
            if (reunion.Estado != EstadoReunion.Finalizada && reunion.Estado != EstadoReunion.QuorumNoAlcanzado)
                throw new InvalidOperationException("Sólo se puede generar el Acta de una reunión Finalizada o con Quórum No Alcanzado.");

            var existente = await ec.GetActaByReunionAsync(idReunion);
            if (existente != null && existente.Estado == EstadoActa.Firmada)
                throw new InvalidOperationException("El Acta ya está firmada y es inmutable.");

            var reunionCompleta = await GetReunionByIdAsync(idReunion)
                ?? throw new InvalidOperationException("Reunión no encontrada.");
            var contenido = await ComponerContenidoActaAsync(reunionCompleta);

            if (existente == null)
            {
                var acta = new Acta
                {
                    IdActa = Guid.NewGuid(),
                    IdReunion = idReunion,
                    ContenidoGenerado = contenido,
                    Estado = EstadoActa.Borrador,
                    CreatedBy = generadoPor
                };
                await ec.AddNewRecordAsync(acta);
                return acta;
            }

            await ec.UpdateActaContenidoAsync(existente.IdActa, contenido);
            existente.ContenidoGenerado = contenido;
            return existente;
        }

        public async Task FirmarActaAsync(Guid idActa, string nombrePresidente, string nombreSecretario)
            => await ec.UpdateActaFirmaAsync(idActa, nombrePresidente, nombreSecretario);

        // Texto plano estructurado (no HTML) -- se muestra tal cual en pantalla
        // (white-space: pre-wrap) y alimenta directo el PDF (ActaExportService),
        // sin necesidad de parsear marcado. Una vez la Reunion pasó a
        // Finalizada/QuorumNoAlcanzado sus datos ya no cambian (no hay ningún
        // flujo que reabra una Reunion cerrada), así que "inmutable tras
        // firmar" es coherente: el contenido compuesto acá para una Reunion en
        // ese estado no puede quedar desactualizado por un cambio posterior.
        private async Task<string> ComponerContenidoActaAsync(Reunion reunion)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(reunion.Tipo == TipoReunion.Ordinaria ? "ACTA DE REUNIÓN ORDINARIA" : "ACTA DE REUNIÓN EXTRAORDINARIA");
            sb.AppendLine(reunion.Titulo);
            sb.AppendLine();
            sb.AppendLine($"Fecha: {reunion.FechaReunion:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"Modalidad: {reunion.Modalidad}");
            if (!string.IsNullOrEmpty(reunion.LugarOVinculo))
                sb.AppendLine($"Lugar / Enlace: {reunion.LugarOVinculo}");
            if (reunion.IdReunionOrigen != null)
                sb.AppendLine("Esta reunión corresponde a una Segunda Convocatoria.");
            sb.AppendLine();

            sb.AppendLine("QUÓRUM");
            sb.AppendLine($"Requerido: {reunion.QuorumRequerido:0.##}%");
            sb.AppendLine($"Alcanzado: {(reunion.QuorumAlcanzado?.ToString("0.##") ?? "0")}%");
            sb.AppendLine(reunion.Estado == EstadoReunion.QuorumNoAlcanzado
                ? "Resultado: Quórum NO alcanzado. La reunión no pudo instalarse."
                : "Resultado: Quórum alcanzado. La reunión quedó válidamente instalada.");
            sb.AppendLine();

            sb.AppendLine($"ASISTENTES ({reunion.Asistentes.Count} unidades, {reunion.Asistentes.Sum(a => a.Alicuota):0.##}% de alícuota)");
            foreach (var a in reunion.Asistentes.OrderBy(a => a.NombreUnidad))
                sb.AppendLine($"- {a.NombreUnidad} ({a.Alicuota:0.####}%)");
            sb.AppendLine();

            sb.AppendLine("AGENDA Y ACUERDOS");
            foreach (var item in reunion.Agenda.OrderBy(a => a.Orden))
            {
                sb.AppendLine($"{item.Orden}. {item.Titulo} [{(item.Tipo == TipoAgendaItem.Informativo ? "Informativo" : "Sujeto a Votación")}]");
                if (!string.IsNullOrEmpty(item.Descripcion))
                    sb.AppendLine($"   {item.Descripcion}");

                if (item.Tipo == TipoAgendaItem.SujetoAVotacion)
                {
                    var votaciones = await GetVotacionesAsync(item.IdAgendaItem);
                    foreach (var v in votaciones.Where(v => v.Estado == EstadoVotacion.Cerrada).OrderBy(v => v.NroRonda))
                    {
                        sb.AppendLine($"   Ronda {v.NroRonda}: a favor {v.AlicuotaAFavor:0.##}% -- en contra {v.AlicuotaEnContra:0.##}% -- abstención {v.AlicuotaAbstencion:0.##}% -- {(v.MayoriaAlcanzada == true ? "mayoría alcanzada" : "mayoría no alcanzada")}");
                    }
                }
                sb.AppendLine($"   Resultado: {TraducirEstadoAgendaParaActa(item.Estado)}");
                sb.AppendLine();
            }

            sb.AppendLine("Documento generado automáticamente por el sistema a partir de los datos registrados en la Reunión.");
            return sb.ToString();
        }

        private static string TraducirEstadoAgendaParaActa(EstadoAgendaItem estado) => estado switch
        {
            EstadoAgendaItem.Pendiente => "Pendiente (sin resolver)",
            EstadoAgendaItem.Informado => "Informado",
            EstadoAgendaItem.Aprobado => "Aprobado",
            EstadoAgendaItem.Rechazado => "Rechazado",
            _ => estado.ToString()
        };
    }
}
