using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Docs/Pendientes-Negocio-Consolidado.md #21, "Gobernanza -- Meetings,
    // Votación y MeetingMinutes como un solo flujo" -- diseño cerrado 2026-09-11,
    // alcance de la primera entrega decidido 2026-09-15 (Meetings+Votación+
    // MeetingMinutes juntas, Encuestas después). Esta es la Fase 1: Convocatoria +
    // Agenda + Attendance + Quórum + Segunda Convocatoria. La Votación real
    // (Fase 2) y las MeetingMinutes (Fase 3) se construyen sobre esta base.
    //
    // Flujo: Convocada -> (Administrador/Junta registra Attendance) ->
    // IniciarMeetingAsync calcula el quórum -> si alcanza: EnCurso -> luego
    // FinalizarAsync; si no alcanza: QuorumNoAlcanzado -> se puede crear una
    // Segunda Convocatoria (nueva Meeting con el mismo agenda, quórum
    // reducido según el Reglamento Interno de cada edificio -- el sistema no
    // asume un número fijo, lo ingresa el Administrador).
    public interface IMeetingService
    {
        Task<List<Meeting>> GetMeetingsAsync(Guid idBuilding);

        // Compone la Meeting con su Agenda y sus Asistentes (con NombreUnidad
        // resuelto) -- GET_MeetingById solo trae la fila de Meeting.
        Task<Meeting?> GetMeetingByIdAsync(Guid idMeeting);

        // Roster de unidades del edificio con su alícuota ya calculada
        // (OwnerUnitView.TotalArea / Building.TotalArea) y, si se pasa
        // idMeeting, marcadas Presente según la Attendance ya registrada.
        Task<List<UnitWithShare>> GetRosterAlicuotasAsync(Guid idBuilding, Guid? idMeeting = null);

        // Crea la Meeting + su Agenda + el CalendarItem asociado (inserción
        // directa, sin correo masivo -- mismo patrón que Reserva).
        Task<ConvokeMeetingResult> ConvocarAsync(Meeting reunion, List<AgendaItem> agenda);

        // Sólo válido mientras Estado=Convocada -- el service lo valida.
        Task ActualizarConvocatoriaAsync(Meeting reunion);

        Task<AgendaItem> AgregarAgendaItemAsync(AgendaItem item);

        Task EditarAgendaItemAsync(AgendaItem item);

        Task EliminarAgendaItemAsync(Guid idAgendaItem);

        Task RegistrarAttendanceAsync(Guid idMeeting, Guid idGroupUnit, Guid registradoPor);

        Task QuitarAttendanceAsync(Guid idMeeting, Guid idGroupUnit);

        // Suma las alícuotas de los asistentes registrados y decide si se
        // alcanza QuorumRequerido -- Estado pasa a EnCurso o
        // QuorumNoAlcanzado según el resultado.
        Task<StartMeetingResult> IniciarMeetingAsync(Guid idMeeting);

        // Sólo válido sobre una Meeting en QuorumNoAlcanzado. Clona la Agenda
        // completa (la reunión original nunca llegó a tratarla) en una
        // Meeting nueva con IdMeetingOrigen apuntando a la original.
        Task<ConvokeMeetingResult> CrearSegundaConvocatoriaAsync(Guid idMeetingOrigen, DateTime nuevaFechaMeeting, decimal nuevoQuorumRequerido, string? nuevoLugarOVinculo);

        // Marca todos los puntos Informativos como Informado y cierra la
        // Meeting. Los puntos Sujetos a Votación quedan como estén (Fase 2
        // les da su mecánica real) -- no se fuerza ningún resultado acá.
        Task FinalizarMeetingAsync(Guid idMeeting);

        Task CancelarMeetingAsync(Guid idMeeting);

        // Override manual de un punto de agenda -- para un punto Informativo,
        // marcar "Informado" durante la reunión (antes de Finalizar); para un
        // punto Sujeto a Votación, permite al operador cerrar el punto como
        // Rechazado a mano cuando decide no abrir más rondas (ver
        // CerrarVotingRoundAsync).
        Task MarcarAgendaItemEstadoAsync(Guid idAgendaItem, AgendaItemStatus nuevoEstado);

        // Notas de quien dirige la reunión sobre lo conversado en un punto de agenda --
        // a diferencia de EditarAgendaItemAsync (restringido a Estado=Convocada), esto
        // vale mientras la reunión está EnCurso o ya Finalizada, hasta que el Acta quede
        // Firmada (ahí sí queda inmutable, igual que el resto del contenido del Acta).
        Task ActualizarNotasAgendaItemAsync(Guid idAgendaItem, string? notas);

        // ===================== Votación (Fase 2) =====================

        // Todas las rondas de votación de un punto de agenda, con sus Votes
        // (NombreUnidad resuelto sólo si VotingType=Nominal -- una votación
        // Secreta no expone en la UI quién votó qué, aunque el voto se
        // guarda con su IdGroupUnit real para poder validar quórum e impedir
        // doble voto; una secrecía criptográfica real queda fuera de alcance
        // de esta primera versión).
        Task<List<VotingRound>> GetVotingRoundsAsync(Guid idAgendaItem);

        // Abre una nueva ronda sobre un AgendaItem.Tipo=SujetoAVotacion --
        // sólo válido con la Meeting En Curso, el punto todavía Pendiente, y
        // sin ninguna ronda ya abierta para ese mismo punto.
        Task<VotingRound> IniciarVotingRoundAsync(Guid idAgendaItem, Guid iniciadoPor);

        // Sólo pueden votar unidades ya registradas como Attendance de la
        // Meeting (usa esa misma Alicuota). Permite corregir un voto ya
        // cargado mientras la Votación sigue Abierta (upsert).
        Task RegistrarVoteAsync(Guid idVotingRound, Guid idGroupUnit, VoteOption opcion, Guid registradoPor);

        // Suma las alícuotas por opción y evalúa la mayoría requerida por el
        // AgendaItem (Simple/Calificada/75% legal). Si alcanza: el punto
        // queda Aprobado. Si no alcanza y PermiteRevotacion está apagado: el
        // punto queda Rechazado automáticamente. Si no alcanza y
        // PermiteRevotacion está prendido: el punto queda Pendiente -- el
        // operador decide si abre una nueva ronda (IniciarVotingRoundAsync) o
        // lo rechaza a mano (MarcarAgendaItemEstadoAsync).
        Task<CloseVotingRoundResult> CerrarVotingRoundAsync(Guid idVotingRound);

        // ===================== MeetingMinutes (Fase 3) =====================

        Task<MeetingMinutes?> GetMeetingMinutesAsync(Guid idMeeting);

        // Compone el contenido desde Meeting+Agenda+Attendance+VotingRound y
        // crea el MeetingMinutes si no existe, o la regenera si sigue en Borrador --
        // sólo válido con la Meeting Finalizada o QuorumNoAlcanzado (son los
        // dos estados con un desenlace real que registrar), y rechaza
        // regenerar un MeetingMinutes ya Firmada (inmutable).
        Task<MeetingMinutes> GenerarBorradorMeetingMinutesAsync(Guid idMeeting, Guid generadoPor);

        // Marca el MeetingMinutes Firmada -- desde ahí en adelante es inmutable
        // (GenerarBorradorMeetingMinutesAsync la rechaza). Recibe idMeeting (no
        // idMeetingMinutes) porque valida contra el MeetingMinutes actual de esa Meeting --
        // evita firmar dos veces por una carrera entre dos operadores con la
        // misma pantalla abierta (rechaza si ya está Firmada).
        Task FirmarMeetingMinutesAsync(Guid idMeeting, string nombrePresidente, string nombreSecretario);
    }

    public class MeetingService : IMeetingService
    {
        private readonly ICalendarService _calendarService;
        private readonly ILogger<MeetingService> _logger;
        private BDLayout ec { get; set; }

        public MeetingService(
            IDbContextFactory<SpiderHoodContext> contextFactory,
            ICalendarService calendarService,
            ILogger<MeetingService> logger)
        {
            _calendarService = calendarService;
            _logger = logger;
            ec = new BDLayout(contextFactory);
        }

        public async Task<List<Meeting>> GetMeetingsAsync(Guid idBuilding)
            => await ec.GetMeetingsByBuildingAsync(idBuilding);

        public async Task<Meeting?> GetMeetingByIdAsync(Guid idMeeting)
        {
            Meeting reunion;
            try
            {
                reunion = await ec.GetMeetingByIdAsync(idMeeting);
            }
            catch (EntityNotFoundException)
            {
                return null;
            }

            reunion.Agenda = await ec.GetAgendaItemsByMeetingAsync(idMeeting);

            var asistentes = await ec.GetAttendancesByMeetingAsync(idMeeting);
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

        public async Task<List<UnitWithShare>> GetRosterAlicuotasAsync(Guid idBuilding, Guid? idMeeting = null)
        {
            var building = await ec.GetBuildingByIdAsync(idBuilding);
            var unidades = await ec.GetOwnersByBuildingAsync(idBuilding);

            var presentes = idMeeting.HasValue
                ? (await ec.GetAttendancesByMeetingAsync(idMeeting.Value)).Select(a => a.IdGroupUnit).ToHashSet()
                : new HashSet<Guid>();

            return unidades
                .GroupBy(u => u.IdGroupUnit)
                .Select(g =>
                {
                    var primero = g.First();
                    return new UnitWithShare
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

        public async Task<ConvokeMeetingResult> ConvocarAsync(Meeting reunion, List<AgendaItem> agenda)
        {
            if (string.IsNullOrWhiteSpace(reunion.Titulo))
                return new ConvokeMeetingResult { Exito = false, Mensaje = "El título es obligatorio." };
            if (reunion.QuorumRequerido < 0 || reunion.QuorumRequerido > 100)
                return new ConvokeMeetingResult { Exito = false, Mensaje = "El quórum requerido debe estar entre 0% y 100%." };
            if (agenda.Count == 0)
                return new ConvokeMeetingResult { Exito = false, Mensaje = "La agenda debe tener al menos un punto." };
            if (agenda.Any(a => string.IsNullOrWhiteSpace(a.Titulo)))
                return new ConvokeMeetingResult { Exito = false, Mensaje = "Todos los puntos de agenda necesitan un título." };

            reunion.IdMeeting = Guid.NewGuid();
            reunion.Estado = MeetingStatus.Convocada;

            var calendarItem = await CrearCalendarItemDeMeetingAsync(reunion);
            reunion.IdCalendarItem = calendarItem.IdCalendarItem;

            await ec.AddNewRecordAsync(reunion);

            var orden = 1;
            foreach (var item in agenda)
            {
                item.IdAgendaItem = Guid.NewGuid();
                item.IdMeeting = reunion.IdMeeting;
                item.Orden = orden++;
                item.Estado = AgendaItemStatus.Pendiente;
                await ec.AddNewRecordAsync(item);
            }

            return new ConvokeMeetingResult { Exito = true, Mensaje = "Reunión convocada.", IdMeeting = reunion.IdMeeting };
        }

        public async Task ActualizarConvocatoriaAsync(Meeting reunion)
        {
            var existente = await ec.GetMeetingByIdAsync(reunion.IdMeeting);
            if (existente.Estado != MeetingStatus.Convocada)
                throw new InvalidOperationException("Sólo se puede editar la convocatoria mientras está en estado Convocada.");

            await ec.UpdateMeetingAsync(reunion);

            var calendarItem = await _calendarService.GetByIdAsync(existente.IdCalendarItem!.Value);
            calendarItem.Title = $"Asamblea: {reunion.Titulo}";
            calendarItem.StartDate = reunion.FechaMeeting;
            calendarItem.Location = reunion.LugarOVinculo ?? string.Empty;
            calendarItem.ModifiedOn = DateTime.UtcNow;
            await _calendarService.UpdateAsync(calendarItem);
        }

        public async Task<AgendaItem> AgregarAgendaItemAsync(AgendaItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Titulo))
                throw new InvalidOperationException("El título del punto de agenda es obligatorio.");

            var reunion = await ec.GetMeetingByIdAsync(item.IdMeeting);
            if (reunion.Estado != MeetingStatus.Convocada)
                throw new InvalidOperationException("Sólo se pueden agregar puntos de agenda mientras la reunión está Convocada.");

            var agendaActual = await ec.GetAgendaItemsByMeetingAsync(item.IdMeeting);
            item.IdAgendaItem = Guid.NewGuid();
            item.Orden = agendaActual.Count + 1;
            item.Estado = AgendaItemStatus.Pendiente;
            await ec.AddNewRecordAsync(item);
            return item;
        }

        // Igual que Agregar -- sólo tiene sentido mientras no arrancó ningún
        // tratamiento real de la agenda (ni asistencia ni votación pudieron
        // haber pasado todavía si la Meeting sigue Convocada).
        public async Task EditarAgendaItemAsync(AgendaItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Titulo))
                throw new InvalidOperationException("El título del punto de agenda es obligatorio.");

            var existente = await ec.GetAgendaItemByIdAsync(item.IdAgendaItem);
            var reunion = await ec.GetMeetingByIdAsync(existente.IdMeeting);
            if (reunion.Estado != MeetingStatus.Convocada)
                throw new InvalidOperationException("Sólo se puede editar un punto de agenda mientras la reunión está Convocada.");

            await ec.UpdateAgendaItemAsync(item);
        }

        public async Task ActualizarNotasAgendaItemAsync(Guid idAgendaItem, string? notas)
        {
            var item = await ec.GetAgendaItemByIdAsync(idAgendaItem);
            var meetingMinutes = await ec.GetMeetingMinutesByMeetingAsync(item.IdMeeting);
            if (meetingMinutes?.Estado == MeetingMinutesStatus.Firmada)
                throw new InvalidOperationException("El Acta ya está firmada -- las notas de la reunión quedaron cerradas.");

            await ec.UpdateAgendaItemNotasAsync(idAgendaItem, notas);
        }

        // Mismo motivo -- borrar un punto que ya tiene VotingRound asociada
        // (FK_VotingRound_AgendaItem) rompería con un error SQL crudo en vez de
        // un mensaje claro; restringir a Convocada lo evita de raíz, porque
        // en ese estado todavía no pudo abrirse ninguna VotingRound.
        public async Task EliminarAgendaItemAsync(Guid idAgendaItem)
        {
            var existente = await ec.GetAgendaItemByIdAsync(idAgendaItem);
            var reunion = await ec.GetMeetingByIdAsync(existente.IdMeeting);
            if (reunion.Estado != MeetingStatus.Convocada)
                throw new InvalidOperationException("Sólo se puede eliminar un punto de agenda mientras la reunión está Convocada.");

            await ec.DeleteAgendaItemAsync(idAgendaItem);
        }

        // Restringido a Convocada -- registrar/quitar asistencia después de
        // Iniciar (que ya sumó las alícuotas presentes para decidir el
        // quórum) desincroniza la lista de asistentes del cálculo que ya se
        // usó: el MeetingMinutes (Fase 3) lee Attendance en vivo al generarse, así que
        // un registro tardío mostraría una lista distinta a la que realmente
        // decidió el quórum -- y si ya hubo Votación, la unidad pudo haber
        // votado con una alícuota que luego "desaparece" al quitarla acá.
        // Feedback del usuario tras probar la reunión en vivo: antes esto sólo valía
        // mientras Convocada -- alguien que llegaba tarde (con la reunión ya En Curso)
        // no se podía registrar. Ahora se permite, a criterio de quien dirige la
        // reunión, dentro de una ventana de MinutosLimiteAsistenciaTardia desde que
        // arrancó realmente (FechaInicioReal) -- pasada esa ventana, sigue rechazando
        // como antes. El % de Quórum Alcanzado se recalcula cada vez (decisión
        // confirmada: sí se recalcula, no queda fijado al momento de Iniciar); el
        // recién llegado puede votar en cualquier ronda que siga Abierta porque
        // RegistrarVoteAsync vuelve a traer Attendance en cada llamada, no necesita
        // ningún cambio aparte.
        public async Task RegistrarAttendanceAsync(Guid idMeeting, Guid idGroupUnit, Guid registradoPor)
        {
            var reunion = await ec.GetMeetingByIdAsync(idMeeting);
            var puedeRegistrarTardio = reunion.Estado == MeetingStatus.EnCurso
                && reunion.FechaInicioReal != null
                && DateTime.UtcNow <= reunion.FechaInicioReal.Value.AddMinutes(reunion.MinutosLimiteAsistenciaTardia);

            if (reunion.Estado != MeetingStatus.Convocada && !puedeRegistrarTardio)
            {
                if (reunion.Estado == MeetingStatus.EnCurso)
                    throw new InvalidOperationException($"Ya pasaron los {reunion.MinutosLimiteAsistenciaTardia} minutos permitidos para registrar asistencia tardía.");
                throw new InvalidOperationException("Sólo se puede registrar asistencia mientras la reunión está Convocada, o En Curso dentro de la ventana de asistencia tardía.");
            }

            var roster = await GetRosterAlicuotasAsync(reunion.IdBuilding);
            var unidad = roster.FirstOrDefault(u => u.IdGroupUnit == idGroupUnit)
                ?? throw new InvalidOperationException("La unidad no pertenece a este edificio.");

            await ec.AddNewRecordAsync(new Attendance
            {
                IdAttendance = Guid.NewGuid(),
                IdMeeting = idMeeting,
                IdGroupUnit = idGroupUnit,
                Alicuota = unidad.Alicuota,
                RegistradoPor = registradoPor
            });

            if (reunion.Estado == MeetingStatus.EnCurso)
            {
                var asistentes = await ec.GetAttendancesByMeetingAsync(idMeeting);
                await ec.UpdateMeetingEstadoAsync(idMeeting, reunion.Estado, quorumAlcanzado: asistentes.Sum(a => a.Alicuota));
            }
        }

        public async Task QuitarAttendanceAsync(Guid idMeeting, Guid idGroupUnit)
        {
            var reunion = await ec.GetMeetingByIdAsync(idMeeting);
            if (reunion.Estado != MeetingStatus.Convocada)
                throw new InvalidOperationException("Sólo se puede quitar asistencia mientras la reunión está Convocada (antes de Iniciar).");

            await ec.DeleteAttendanceAsync(idMeeting, idGroupUnit);
        }

        public async Task<StartMeetingResult> IniciarMeetingAsync(Guid idMeeting)
        {
            var reunion = await ec.GetMeetingByIdAsync(idMeeting);
            if (reunion.Estado != MeetingStatus.Convocada)
                return new StartMeetingResult { Exito = false, Mensaje = "La reunión ya fue iniciada, finalizada o cancelada." };

            var asistentes = await ec.GetAttendancesByMeetingAsync(idMeeting);
            var porcentajeAlcanzado = asistentes.Sum(a => a.Alicuota);
            var alcanzaQuorum = porcentajeAlcanzado >= reunion.QuorumRequerido;

            await ec.UpdateMeetingEstadoAsync(
                idMeeting,
                alcanzaQuorum ? MeetingStatus.EnCurso : MeetingStatus.QuorumNoAlcanzado,
                quorumAlcanzado: porcentajeAlcanzado,
                fechaInicioReal: alcanzaQuorum ? DateTime.UtcNow : null);

            return new StartMeetingResult
            {
                Exito = true,
                Mensaje = alcanzaQuorum ? "Quórum alcanzado -- la reunión está en curso." : "Quórum no alcanzado.",
                QuorumAlcanzado = alcanzaQuorum,
                PorcentajeAlcanzado = porcentajeAlcanzado
            };
        }

        public async Task<ConvokeMeetingResult> CrearSegundaConvocatoriaAsync(Guid idMeetingOrigen, DateTime nuevaFechaMeeting, decimal nuevoQuorumRequerido, string? nuevoLugarOVinculo)
        {
            var original = await ec.GetMeetingByIdAsync(idMeetingOrigen);
            if (original.Estado != MeetingStatus.QuorumNoAlcanzado)
                return new ConvokeMeetingResult { Exito = false, Mensaje = "Sólo se puede convocar una segunda convocatoria cuando la original no alcanzó quórum." };

            var agendaOriginal = await ec.GetAgendaItemsByMeetingAsync(idMeetingOrigen);

            var nueva = new Meeting
            {
                IdBuilding = original.IdBuilding,
                Tipo = original.Tipo,
                Titulo = $"{original.Titulo} (Segunda Convocatoria)",
                FechaConvocatoria = DateTime.UtcNow,
                FechaMeeting = nuevaFechaMeeting,
                Modalidad = original.Modalidad,
                LugarOVinculo = nuevoLugarOVinculo ?? original.LugarOVinculo,
                QuorumRequerido = nuevoQuorumRequerido,
                IdMeetingOrigen = idMeetingOrigen,
                CreatedBy = original.CreatedBy
            };

            var agendaClonada = agendaOriginal.Select(a => new AgendaItem
            {
                Titulo = a.Titulo,
                Descripcion = a.Descripcion,
                Tipo = a.Tipo,
                VotingType = a.VotingType,
                MajorityType = a.MajorityType,
                PorcentajeMayoriaCalificada = a.PorcentajeMayoriaCalificada,
                PermiteRevotacion = a.PermiteRevotacion
            }).ToList();

            return await ConvocarAsync(nueva, agendaClonada);
        }

        public async Task FinalizarMeetingAsync(Guid idMeeting)
        {
            var reunion = await ec.GetMeetingByIdAsync(idMeeting);
            if (reunion.Estado != MeetingStatus.EnCurso)
                throw new InvalidOperationException("Sólo se puede finalizar una reunión que está En Curso.");

            var agenda = await ec.GetAgendaItemsByMeetingAsync(idMeeting);
            foreach (var item in agenda.Where(a => a.Tipo == AgendaItemType.Informativo && a.Estado == AgendaItemStatus.Pendiente))
                await ec.UpdateAgendaItemEstadoAsync(item.IdAgendaItem, AgendaItemStatus.Informado);

            await ec.UpdateMeetingEstadoAsync(idMeeting, MeetingStatus.Finalizada);

            // UPD_CalendarItem (usado por ICalendarService.UpdateAsync) no toca la
            // columna Status -- tiene su propio SP dedicado (mismo motivo que
            // UPD_ReservaEstado está separado de un UPDATE genérico). Setear
            // calendarItem.Status y llamar UpdateAsync no hace nada en silencio.
            if (reunion.IdCalendarItem.HasValue)
                await _calendarService.ChangeStatusAsync(reunion.IdCalendarItem.Value, CalendarItemStatus.Completed, reunion.CreatedBy.ToString());
        }

        public async Task CancelarMeetingAsync(Guid idMeeting)
        {
            var reunion = await ec.GetMeetingByIdAsync(idMeeting);
            if (reunion.Estado == MeetingStatus.Finalizada || reunion.Estado == MeetingStatus.Cancelada)
                throw new InvalidOperationException("La reunión ya está finalizada o cancelada.");

            await ec.UpdateMeetingEstadoAsync(idMeeting, MeetingStatus.Cancelada);

            if (reunion.IdCalendarItem.HasValue)
                await _calendarService.DeleteAsync(reunion.IdCalendarItem.Value, deleteSeries: false, reunion.CreatedBy.ToString());
        }

        // Guardas explícitas por transición -- sin esto, este método genérico
        // podía usarse para forzar Aprobado en un punto Sujeto a Votación sin
        // ningún voto real detrás (saltándose por completo el cálculo de
        // mayoría de CerrarVotingRoundAsync, que es el único lugar legítimo que
        // debe decidir un Aprobado). Sólo quedan habilitadas las dos
        // transiciones manuales que de verdad tienen sentido: marcar
        // Informado un punto Informativo, y rechazar a mano un punto Sujeto a
        // Votación cuando el operador decide no abrir más rondas (y sólo si
        // no hay ninguna ronda abierta en ese momento).
        public async Task MarcarAgendaItemEstadoAsync(Guid idAgendaItem, AgendaItemStatus nuevoEstado)
        {
            var item = await ec.GetAgendaItemByIdAsync(idAgendaItem);
            var reunion = await ec.GetMeetingByIdAsync(item.IdMeeting);
            if (reunion.Estado != MeetingStatus.EnCurso)
                throw new InvalidOperationException("Sólo se puede cambiar el estado de un punto mientras la reunión está En Curso.");
            if (item.Estado != AgendaItemStatus.Pendiente)
                throw new InvalidOperationException("Este punto ya fue resuelto.");

            if (item.Tipo == AgendaItemType.Informativo && nuevoEstado == AgendaItemStatus.Informado)
            {
                await ec.UpdateAgendaItemEstadoAsync(idAgendaItem, nuevoEstado);
                return;
            }

            if (item.Tipo == AgendaItemType.SujetoAVotacion && nuevoEstado == AgendaItemStatus.Rechazado)
            {
                var rondas = await ec.GetVotingRoundsByAgendaItemAsync(idAgendaItem);
                if (rondas.Any(v => v.Estado == VotingRoundStatus.Abierta))
                    throw new InvalidOperationException("Hay una votación abierta para este punto -- ciérrala antes de rechazarlo a mano.");
                await ec.UpdateAgendaItemEstadoAsync(idAgendaItem, nuevoEstado);
                return;
            }

            throw new InvalidOperationException("Esa transición de estado no está permitida para este punto de agenda.");
        }

        // Se inserta directo por BDLayout, sin pasar por ICalendarService.CreateAsync --
        // mismo motivo que Reserva (Docs/Pendientes-Negocio-Consolidado.md #21): ese
        // método manda un correo a TODOS los residentes por cada CalendarItem nuevo, y acá
        // ya existe el propio flujo de convocatoria (con acuse de recibo, cuando el módulo
        // de Comunicaciones esté listo) para notificar la Reunión -- un correo genérico de
        // "Nuevo evento programado" sería redundante y confuso.
        private async Task<CalendarItem> CrearCalendarItemDeMeetingAsync(Meeting reunion)
        {
            var calendarItem = new CalendarItem
            {
                IdCalendarItem = Guid.NewGuid(),
                IdBuilding = reunion.IdBuilding,
                Title = $"Asamblea: {reunion.Titulo}",
                Description = $"{(reunion.Tipo == MeetingType.Ordinaria ? "Reunión Ordinaria" : "Reunión Extraordinaria")} -- {reunion.Modalidad}",
                Type = CalendarItemType.Event,
                StartDate = reunion.FechaMeeting,
                Location = reunion.LugarOVinculo ?? string.Empty,
                Status = CalendarItemStatus.Scheduled,
                CreatedBy = reunion.CreatedBy.ToString(),
                CreatedOn = DateTime.UtcNow
            };
            await ec.AddNewRecordAsync(calendarItem);
            return calendarItem;
        }

        // ===================== Votación (Fase 2) =====================

        public async Task<List<VotingRound>> GetVotingRoundsAsync(Guid idAgendaItem)
        {
            var votaciones = await ec.GetVotingRoundsByAgendaItemAsync(idAgendaItem);
            if (votaciones.Count == 0) return votaciones;

            var agendaItem = await ec.GetAgendaItemByIdAsync(idAgendaItem);
            var reunion = await ec.GetMeetingByIdAsync(agendaItem.IdMeeting);
            var unidades = await ec.GetOwnersByBuildingAsync(reunion.IdBuilding);
            var nombresPorGrupo = ConstruirNombresPorGrupo(unidades);

            foreach (var votacion in votaciones)
            {
                var votos = await ec.GetVotesByVotingRoundAsync(votacion.IdVotingRound);
                if (agendaItem.VotingType == AgendaVotingType.Nominal)
                {
                    foreach (var voto in votos)
                        voto.NombreUnidad = nombresPorGrupo.TryGetValue(voto.IdGroupUnit, out var nombre) ? nombre : "Unidad";
                }
                votacion.Votes = votos;
            }

            return votaciones;
        }

        public async Task<VotingRound> IniciarVotingRoundAsync(Guid idAgendaItem, Guid iniciadoPor)
        {
            var agendaItem = await ec.GetAgendaItemByIdAsync(idAgendaItem);
            if (agendaItem.Tipo != AgendaItemType.SujetoAVotacion)
                throw new InvalidOperationException("Este punto de agenda no está sujeto a votación.");
            if (agendaItem.Estado != AgendaItemStatus.Pendiente)
                throw new InvalidOperationException("Este punto ya fue resuelto.");

            var reunion = await ec.GetMeetingByIdAsync(agendaItem.IdMeeting);
            if (reunion.Estado != MeetingStatus.EnCurso)
                throw new InvalidOperationException("Sólo se puede votar mientras la reunión está En Curso.");

            var rondasPrevias = await ec.GetVotingRoundsByAgendaItemAsync(idAgendaItem);
            if (rondasPrevias.Any(v => v.Estado == VotingRoundStatus.Abierta))
                throw new InvalidOperationException("Ya hay una votación abierta para este punto.");

            var votacion = new VotingRound
            {
                IdVotingRound = Guid.NewGuid(),
                IdAgendaItem = idAgendaItem,
                NroRonda = rondasPrevias.Count + 1,
                Estado = VotingRoundStatus.Abierta,
                CreatedBy = iniciadoPor
            };
            await ec.AddNewRecordAsync(votacion);
            return votacion;
        }

        public async Task RegistrarVoteAsync(Guid idVotingRound, Guid idGroupUnit, VoteOption opcion, Guid registradoPor)
        {
            var votacion = await ec.GetVotingRoundByIdAsync(idVotingRound);
            if (votacion.Estado != VotingRoundStatus.Abierta)
                throw new InvalidOperationException("Esta votación ya está cerrada.");

            var agendaItem = await ec.GetAgendaItemByIdAsync(votacion.IdAgendaItem);
            var asistentes = await ec.GetAttendancesByMeetingAsync(agendaItem.IdMeeting);
            var asistente = asistentes.FirstOrDefault(a => a.IdGroupUnit == idGroupUnit)
                ?? throw new InvalidOperationException("Sólo pueden votar las unidades registradas como asistentes de la reunión.");

            // Permite corregir un voto ya cargado mientras la votación sigue abierta.
            await ec.DeleteVoteAsync(idVotingRound, idGroupUnit);
            await ec.AddNewRecordAsync(new Vote
            {
                IdVote = Guid.NewGuid(),
                IdVotingRound = idVotingRound,
                IdGroupUnit = idGroupUnit,
                Opcion = opcion,
                Alicuota = asistente.Alicuota,
                RegistradoPor = registradoPor
            });
        }

        public async Task<CloseVotingRoundResult> CerrarVotingRoundAsync(Guid idVotingRound)
        {
            var votacion = await ec.GetVotingRoundByIdAsync(idVotingRound);
            if (votacion.Estado != VotingRoundStatus.Abierta)
                return new CloseVotingRoundResult { Exito = false, Mensaje = "Esta votación ya está cerrada." };

            var votos = await ec.GetVotesByVotingRoundAsync(idVotingRound);
            var aFavor = votos.Where(v => v.Opcion == VoteOption.AFavor).Sum(v => v.Alicuota);
            var enContra = votos.Where(v => v.Opcion == VoteOption.EnContra).Sum(v => v.Alicuota);
            var abstencion = votos.Where(v => v.Opcion == VoteOption.Abstencion).Sum(v => v.Alicuota);

            var agendaItem = await ec.GetAgendaItemByIdAsync(votacion.IdAgendaItem);
            var alcanzada = EvaluarMayoria(agendaItem, aFavor, enContra, abstencion);

            await ec.UpdateVotingRoundCierreAsync(idVotingRound, aFavor, enContra, abstencion, alcanzada);

            string mensaje;
            if (alcanzada)
            {
                await ec.UpdateAgendaItemEstadoAsync(agendaItem.IdAgendaItem, AgendaItemStatus.Aprobado);
                mensaje = "Mayoría alcanzada -- punto aprobado.";
            }
            else if (!agendaItem.PermiteRevotacion)
            {
                await ec.UpdateAgendaItemEstadoAsync(agendaItem.IdAgendaItem, AgendaItemStatus.Rechazado);
                mensaje = "Mayoría no alcanzada -- punto rechazado.";
            }
            else
            {
                // El punto permite revotación y no la agotó -- queda Pendiente para
                // que el operador abra una nueva ronda o lo rechace a mano.
                mensaje = "Mayoría no alcanzada -- se puede abrir una nueva ronda de votación.";
            }

            return new CloseVotingRoundResult
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
        // 2026-09-15_114_Gobernanza_Fase2_VotingRound.sql): Simple compara
        // alícuota a favor vs. en contra; Calificada es un % sobre los votos
        // EMITIDOS (a favor + en contra + abstención), no sobre el edificio
        // completo; Legal75 (Art. 14.1 D.L. 1568) sí es sobre el edificio
        // completo, tal como lo fija la ley.
        private static bool EvaluarMayoria(AgendaItem item, decimal aFavor, decimal enContra, decimal abstencion)
        {
            var totalEmitido = aFavor + enContra + abstencion;
            return item.MajorityType switch
            {
                MajorityType.Legal75 => aFavor >= 75m,
                MajorityType.Calificada => totalEmitido > 0 && (aFavor / totalEmitido * 100m) >= (item.PorcentajeMayoriaCalificada ?? 100m),
                _ => aFavor > enContra // Simple
            };
        }

        // ===================== MeetingMinutes (Fase 3) =====================

        public async Task<MeetingMinutes?> GetMeetingMinutesAsync(Guid idMeeting)
            => await ec.GetMeetingMinutesByMeetingAsync(idMeeting);

        public async Task<MeetingMinutes> GenerarBorradorMeetingMinutesAsync(Guid idMeeting, Guid generadoPor)
        {
            var reunion = await ec.GetMeetingByIdAsync(idMeeting);
            if (reunion.Estado != MeetingStatus.Finalizada && reunion.Estado != MeetingStatus.QuorumNoAlcanzado)
                throw new InvalidOperationException("Sólo se puede generar el Acta de una reunión Finalizada o con Quórum No Alcanzado.");

            var existente = await ec.GetMeetingMinutesByMeetingAsync(idMeeting);
            if (existente != null && existente.Estado == MeetingMinutesStatus.Firmada)
                throw new InvalidOperationException("El Acta ya está firmada y es inmutable.");

            var reunionCompleta = await GetMeetingByIdAsync(idMeeting)
                ?? throw new InvalidOperationException("Reunión no encontrada.");
            var contenido = await ComponerContenidoMeetingMinutesAsync(reunionCompleta);

            if (existente == null)
            {
                var acta = new MeetingMinutes
                {
                    IdMeetingMinutes = Guid.NewGuid(),
                    IdMeeting = idMeeting,
                    ContenidoGenerado = contenido,
                    Estado = MeetingMinutesStatus.Borrador,
                    CreatedBy = generadoPor
                };
                await ec.AddNewRecordAsync(acta);
                return acta;
            }

            await ec.UpdateMeetingMinutesContenidoAsync(existente.IdMeetingMinutes, contenido);
            existente.ContenidoGenerado = contenido;
            return existente;
        }

        public async Task FirmarMeetingMinutesAsync(Guid idMeeting, string nombrePresidente, string nombreSecretario)
        {
            if (string.IsNullOrWhiteSpace(nombrePresidente) || string.IsNullOrWhiteSpace(nombreSecretario))
                throw new InvalidOperationException("Los nombres del Presidente y el Secretario son obligatorios.");

            var acta = await ec.GetMeetingMinutesByMeetingAsync(idMeeting)
                ?? throw new InvalidOperationException("Todavía no se generó el borrador del Acta.");
            if (acta.Estado == MeetingMinutesStatus.Firmada)
                throw new InvalidOperationException("El Acta ya está firmada.");

            await ec.UpdateMeetingMinutesFirmaAsync(acta.IdMeetingMinutes, nombrePresidente.Trim(), nombreSecretario.Trim());
        }

        // Texto plano estructurado (no HTML) -- se muestra tal cual en pantalla
        // (white-space: pre-wrap) y alimenta directo el PDF (MeetingMinutesExportService),
        // sin necesidad de parsear marcado. Una vez la Meeting pasó a
        // Finalizada/QuorumNoAlcanzado sus datos ya no cambian (no hay ningún
        // flujo que reabra una Meeting cerrada), así que "inmutable tras
        // firmar" es coherente: el contenido compuesto acá para una Meeting en
        // ese estado no puede quedar desactualizado por un cambio posterior.
        private async Task<string> ComponerContenidoMeetingMinutesAsync(Meeting reunion)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(reunion.Tipo == MeetingType.Ordinaria ? "ACTA DE REUNIÓN ORDINARIA" : "ACTA DE REUNIÓN EXTRAORDINARIA");
            sb.AppendLine(reunion.Titulo);
            sb.AppendLine();
            sb.AppendLine($"Fecha: {reunion.FechaMeeting:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"Modalidad: {reunion.Modalidad}");
            if (!string.IsNullOrEmpty(reunion.LugarOVinculo))
                sb.AppendLine($"Lugar / Enlace: {reunion.LugarOVinculo}");
            if (reunion.IdMeetingOrigen != null)
                sb.AppendLine("Esta reunión corresponde a una Segunda Convocatoria.");
            sb.AppendLine();

            sb.AppendLine("QUÓRUM");
            sb.AppendLine($"Requerido: {reunion.QuorumRequerido:0.##}%");
            sb.AppendLine($"Alcanzado: {(reunion.QuorumAlcanzado?.ToString("0.##") ?? "0")}%");
            sb.AppendLine(reunion.Estado == MeetingStatus.QuorumNoAlcanzado
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
                sb.AppendLine($"{item.Orden}. {item.Titulo} [{(item.Tipo == AgendaItemType.Informativo ? "Informativo" : "Sujeto a Votación")}]");
                if (!string.IsNullOrEmpty(item.Descripcion))
                    sb.AppendLine($"   {item.Descripcion}");
                if (!string.IsNullOrEmpty(item.Notas))
                    sb.AppendLine($"   Notas: {item.Notas}");

                if (item.Tipo == AgendaItemType.SujetoAVotacion)
                {
                    var votaciones = await GetVotingRoundsAsync(item.IdAgendaItem);
                    foreach (var v in votaciones.Where(v => v.Estado == VotingRoundStatus.Cerrada).OrderBy(v => v.NroRonda))
                    {
                        sb.AppendLine($"   Ronda {v.NroRonda}: a favor {v.AlicuotaAFavor:0.##}% -- en contra {v.AlicuotaEnContra:0.##}% -- abstención {v.AlicuotaAbstencion:0.##}% -- {(v.MayoriaAlcanzada == true ? "mayoría alcanzada" : "mayoría no alcanzada")}");
                    }
                }
                sb.AppendLine($"   Resultado: {TraducirEstadoAgendaParaMeetingMinutes(item.Estado)}");
                sb.AppendLine();
            }

            sb.AppendLine("Documento generado automáticamente por el sistema a partir de los datos registrados en la Reunión.");
            return sb.ToString();
        }

        private static string TraducirEstadoAgendaParaMeetingMinutes(AgendaItemStatus estado) => estado switch
        {
            AgendaItemStatus.Pendiente => "Pendiente (sin resolver)",
            AgendaItemStatus.Informado => "Informado",
            AgendaItemStatus.Aprobado => "Aprobado",
            AgendaItemStatus.Rechazado => "Rechazado",
            _ => estado.ToString()
        };
    }
}
