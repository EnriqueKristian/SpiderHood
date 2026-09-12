using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Docs/Pendientes-Negocio-Consolidado.md #21 -- diseño cerrado 2026-09-11.
    // Estado: PendienteDeAprobacion -> (Junta) Aprobada/Rechazada -> Cancelada/
    // NoPresentado (penalidad según AreaComun) -> Entregada (check-in) ->
    // Finalizada (check-out) -> Cerrada (garantía liquidada). El Administrador
    // hace el check-in/check-out; la Junta aprueba/rechaza.
    public interface IReservaService
    {
        Task<List<Reserva>> GetReservasAsync(Guid idBuilding);

        Task<List<Reserva>> GetReservasPendientesAsync(Guid idBuilding);

        Task<List<Reserva>> GetReservasByGroupUnitAsync(Guid idGroupUnit);

        Task<List<Reserva>> GetProximasAsync(Guid idAreaComun);

        Task<List<ReservaChecklistItem>> GetChecklistAsync(Guid idReserva);

        Task<List<ReservaAttachment>> GetAdjuntosAsync(Guid idReserva);

        // Autocuración manual para una reserva que quedó sin CalendarItem (ej. una
        // solicitada/aprobada antes de que existiera esta integración, o cualquier otra
        // causa) -- AprobarAsync ya se autocura solo, pero eso no ayuda a una reserva que
        // YA está Aprobada (no hay botón "Aprobar" para volver a disparar el fix). Devuelve
        // false sin hacer nada si ya tenía CalendarItem o si el estado ya no lo necesita
        // (Rechazada/Cancelada/NoPresentado/Cerrada).
        Task<bool> AsegurarCalendarItemAsync(Guid idReserva);

        // Valida ventanas de anticipación/duración del Área Común, chequea
        // solapamiento con GET_ReservasConflicto, calcula Garantía/Alquiler/
        // Limpieza según EsExterno, y guarda en PendienteDeAprobacion.
        Task<SolicitarReservaResultado> SolicitarAsync(Reserva reserva, AreaComun areaComun);

        Task AprobarAsync(Guid idReserva, Guid aprobadoPor);

        Task RechazarAsync(Guid idReserva, Guid aprobadoPor, string motivo);

        // Penalidad según AreaComun.PenalidadCancelacionHabilitada +
        // DiasMinimosSinPenalidad -- retiene el 100% de la garantía si cancela
        // con menos anticipación que ese número de días.
        Task CancelarAsync(Guid idReserva, AreaComun areaComun);

        // Penalidad según AreaComun.PenalidadNoPresentadoHabilitada -- retiene
        // el 100% de la garantía, sin campo de días (es binario).
        Task MarcarNoPresentadoAsync(Guid idReserva, AreaComun areaComun);

        // No hay pasarela de pago ni conciliación bancaria conectada (Docs/
        // Pendientes-Negocio-Consolidado.md #21, "Cobro") -- esto es un check
        // manual del Administrador confirmando que recibió la Garantía/Alquiler/
        // Limpieza, sin validar nada contra una cuenta real.
        Task ConfirmarPagoAsync(Guid idReserva, decimal? montoConfirmado, DateTime? fechaPago, Guid confirmadoPor);

        // Falla (Exito=false) si la reserva todavía no tiene el pago confirmado --
        // feedback del usuario 2026-09-12: no había ninguna verificación de pago
        // antes de entregar el área.
        Task<CheckInResultado> HacerCheckInAsync(Guid idReserva, Guid usuario, List<(string Descripcion, ChecklistEstado Estado, string? Observacion)> checklist, List<(byte[] Contenido, string FileName, string ContentType)> fotos);

        Task HacerCheckOutAsync(Guid idReserva, Guid usuario, List<(string Descripcion, ChecklistEstado Estado, string? Observacion)> checklist, List<(byte[] Contenido, string FileName, string ContentType)> fotos);

        // Liquida la garantía. Dos caminos:
        // - Reserva Finalizada (check-out ya hecho): montoDanio es la evaluación del
        //   Administrador sobre el checklist final; genera Ingreso por Alquiler +
        //   Limpieza (el servicio sí se prestó) y liquida la Garantía (devuelve /
        //   retiene / genera cuota extraordinaria si el daño la supera).
        // - Reserva Cancelada/NoPresentado (la penalidad ya quedó decidida al
        //   cancelar/marcar no-presentado): sólo liquida la Garantía ya retenida,
        //   sin cobrar Alquiler/Limpieza (el servicio no se prestó). montoDanio se
        //   ignora en este camino.
        Task<CerrarReservaResultado> CerrarAsync(Guid idReserva, Guid usuario, decimal montoDanio = 0);
    }

    public class ReservaService : IReservaService
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IExtraChargeService _extraChargeService;
        private readonly ICalendarService _calendarService;
        private readonly ILogger<ReservaService> _logger;
        private BDLayout ec { get; set; }

        public ReservaService(
            IDbContextFactory<SpiderHoodContext> contextFactory,
            IFileStorageService fileStorageService,
            IExtraChargeService extraChargeService,
            ICalendarService calendarService,
            ILogger<ReservaService> logger)
        {
            _fileStorageService = fileStorageService;
            _extraChargeService = extraChargeService;
            _calendarService = calendarService;
            _logger = logger;
            ec = new BDLayout(contextFactory);
        }

        public async Task<List<Reserva>> GetReservasAsync(Guid idBuilding)
            => await ec.GetReservasByBuildingAsync(idBuilding);

        public async Task<List<Reserva>> GetReservasPendientesAsync(Guid idBuilding)
            => await ec.GetReservasPendientesByBuildingAsync(idBuilding);

        public async Task<List<Reserva>> GetReservasByGroupUnitAsync(Guid idGroupUnit)
            => await ec.GetReservasByGroupUnitAsync(idGroupUnit);

        public async Task<List<Reserva>> GetProximasAsync(Guid idAreaComun)
            => await ec.GetReservasProximasByAreaComunAsync(idAreaComun);

        public async Task<List<ReservaChecklistItem>> GetChecklistAsync(Guid idReserva)
            => await ec.GetReservaChecklistItemsByReservaAsync(idReserva);

        public async Task<List<ReservaAttachment>> GetAdjuntosAsync(Guid idReserva)
            => await ec.GetReservaAttachmentsByReservaAsync(idReserva);

        public async Task<SolicitarReservaResultado> SolicitarAsync(Reserva reserva, AreaComun areaComun)
        {
            var resultado = new SolicitarReservaResultado();

            if (reserva.FechaInicio >= reserva.FechaFin)
            {
                resultado.Mensaje = "La fecha de inicio debe ser anterior a la fecha de fin.";
                return resultado;
            }

            if (reserva.FechaInicio < DateTime.Now.AddHours(areaComun.AnticipacionMinHoras))
            {
                resultado.Mensaje = $"Esta área requiere al menos {areaComun.AnticipacionMinHoras} horas de anticipación.";
                return resultado;
            }

            if (areaComun.AnticipacionMaxDias.HasValue && reserva.FechaInicio > DateTime.Now.AddDays(areaComun.AnticipacionMaxDias.Value))
            {
                resultado.Mensaje = $"Esta área no admite reservas con más de {areaComun.AnticipacionMaxDias} días de anticipación.";
                return resultado;
            }

            var duracionMinutos = (reserva.FechaFin - reserva.FechaInicio).TotalMinutes;
            if (areaComun.DuracionMinMinutos.HasValue && duracionMinutos < areaComun.DuracionMinMinutos.Value)
            {
                resultado.Mensaje = $"La duración mínima de la reserva es de {areaComun.DuracionMinMinutos} minutos.";
                return resultado;
            }

            if (areaComun.DuracionMaxMinutos.HasValue && duracionMinutos > areaComun.DuracionMaxMinutos.Value)
            {
                resultado.Mensaje = $"La duración máxima de la reserva es de {areaComun.DuracionMaxMinutos} minutos.";
                return resultado;
            }

            if (areaComun.TopeReservasActivasPorUnidad.HasValue)
            {
                var reservasUnidad = await ec.GetReservasByGroupUnitAsync(reserva.IdGroupUnit);
                var activas = reservasUnidad.Count(r => r.IdAreaComun == areaComun.IdAreaComun
                    && r.Estado is ReservaEstado.PendienteDeAprobacion or ReservaEstado.Aprobada or ReservaEstado.Entregada);

                if (activas >= areaComun.TopeReservasActivasPorUnidad.Value)
                {
                    resultado.Mensaje = $"Ya alcanzaste el tope de {areaComun.TopeReservasActivasPorUnidad} reserva(s) activa(s) para esta área.";
                    return resultado;
                }
            }

            var buffer = TimeSpan.FromMinutes(areaComun.BufferMinutos);
            var conflictos = await ec.GetReservasConflictoAsync(areaComun.IdAreaComun, reserva.FechaInicio - buffer, reserva.FechaFin + buffer);
            if (conflictos.Any())
            {
                resultado.Mensaje = "El área ya está reservada (o no hay suficiente buffer de limpieza) en ese horario.";
                return resultado;
            }

            reserva.IdReserva = Guid.NewGuid();
            reserva.IdAreaComun = areaComun.IdAreaComun;
            reserva.Estado = ReservaEstado.PendienteDeAprobacion;
            reserva.MontoGarantia = reserva.EsExterno ? areaComun.GarantiaExternos : areaComun.GarantiaInternos;
            reserva.MontoAlquiler = reserva.EsExterno ? areaComun.AlquilerExternos : areaComun.AlquilerInternos;
            reserva.MontoLimpieza = areaComun.Limpieza;
            reserva.CreatedOn = DateTime.Now;

            // Feedback del usuario tras probar en vivo (2026-09-11): sin un CalendarItem,
            // una Reserva Pendiente/Aprobada no se veía en el Calendario general, así que
            // otro propietario no tenía forma visual de saber que el área ya estaba
            // comprometida en ese horario (más allá del chequeo de conflicto de arriba).
            var calendarItem = await CrearCalendarItemDeReservaAsync(reserva, areaComun.Nombre, areaComun.IdBuilding, "Pendiente de aprobación de la Junta.");
            reserva.IdCalendarItem = calendarItem.IdCalendarItem;

            await ec.AddNewRecordAsync(reserva);

            resultado.Exito = true;
            resultado.IdReserva = reserva.IdReserva;
            resultado.Mensaje = "Reserva solicitada -- queda pendiente de aprobación de la Junta.";
            return resultado;
        }

        public async Task AprobarAsync(Guid idReserva, Guid aprobadoPor)
        {
            var reserva = await ec.GetReservaByIdAsync(idReserva);

            if (reserva.IdCalendarItem.HasValue)
            {
                var calendarItem = await _calendarService.GetByIdAsync(reserva.IdCalendarItem.Value);
                calendarItem.Description = "Aprobada por la Junta.";
                calendarItem.ModifiedBy = aprobadoPor.ToString();
                calendarItem.ModifiedOn = DateTime.Now;
                await _calendarService.UpdateAsync(calendarItem);

                await ec.UpdateReservaEstadoAsync(idReserva, ReservaEstado.Aprobada, aprobadoPor: aprobadoPor);
            }
            else
            {
                // Autocuración: reservas solicitadas antes de que existiera esta
                // integración con el Calendario (o cualquier otra causa por la que
                // haya quedado sin CalendarItem) no tienen nada que actualizar -- se
                // les crea uno recién ahora en vez de dejarlas invisibles para
                // siempre. Visto en vivo (2026-09-12): una reserva ya Aprobada de una
                // ronda de pruebas anterior a este fix no aparecía en el Calendario.
                var nuevoCalendarItem = await CrearCalendarItemDeReservaAsync(reserva, reserva.NombreAreaComun, reserva.IdBuilding, "Aprobada por la Junta.");
                await ec.UpdateReservaEstadoAsync(idReserva, ReservaEstado.Aprobada, aprobadoPor: aprobadoPor, idCalendarItem: nuevoCalendarItem.IdCalendarItem);
            }
        }

        public async Task<bool> AsegurarCalendarItemAsync(Guid idReserva)
        {
            var reserva = await ec.GetReservaByIdAsync(idReserva);

            if (reserva.IdCalendarItem.HasValue
                || reserva.Estado is ReservaEstado.Rechazada or ReservaEstado.Cancelada or ReservaEstado.NoPresentado or ReservaEstado.Cerrada)
            {
                return false;
            }

            var descripcion = reserva.Estado == ReservaEstado.PendienteDeAprobacion
                ? "Pendiente de aprobación de la Junta."
                : "Aprobada por la Junta.";
            var calendarItem = await CrearCalendarItemDeReservaAsync(reserva, reserva.NombreAreaComun, reserva.IdBuilding, descripcion);
            await ec.UpdateReservaEstadoAsync(idReserva, reserva.Estado, idCalendarItem: calendarItem.IdCalendarItem);
            return true;
        }

        public async Task RechazarAsync(Guid idReserva, Guid aprobadoPor, string motivo)
        {
            await ec.UpdateReservaEstadoAsync(idReserva, ReservaEstado.Rechazada, motivoRechazo: motivo, aprobadoPor: aprobadoPor);

            var reserva = await ec.GetReservaByIdAsync(idReserva);
            if (reserva.IdCalendarItem.HasValue)
            {
                // Rechazada libera el horario -- se borra el CalendarItem para que el área
                // vuelva a verse disponible para otro propietario.
                await _calendarService.DeleteAsync(reserva.IdCalendarItem.Value, deleteSeries: false, aprobadoPor.ToString());
            }
        }

        public async Task CancelarAsync(Guid idReserva, AreaComun areaComun)
        {
            var reserva = await ec.GetReservaByIdAsync(idReserva);

            var diasParaEvento = (reserva.FechaInicio - DateTime.Now).TotalDays;
            var aplicaPenalidad = areaComun.PenalidadCancelacionHabilitada
                && areaComun.DiasMinimosSinPenalidad.HasValue
                && diasParaEvento < areaComun.DiasMinimosSinPenalidad.Value;

            var montoRetenido = aplicaPenalidad ? reserva.MontoGarantia : 0;
            await ec.UpdateReservaEstadoAsync(idReserva, ReservaEstado.Cancelada, montoRetenido: montoRetenido);

            if (reserva.IdCalendarItem.HasValue)
            {
                // Cancelada libera el horario -- mismo motivo que Rechazada arriba.
                await _calendarService.DeleteAsync(reserva.IdCalendarItem.Value, deleteSeries: false, reserva.CreatedBy.ToString());
            }
        }

        public async Task MarcarNoPresentadoAsync(Guid idReserva, AreaComun areaComun)
        {
            var reserva = await ec.GetReservaByIdAsync(idReserva);
            var montoRetenido = areaComun.PenalidadNoPresentadoHabilitada ? reserva.MontoGarantia : 0;

            await ec.UpdateReservaEstadoAsync(idReserva, ReservaEstado.NoPresentado, montoRetenido: montoRetenido);

            if (reserva.IdCalendarItem.HasValue)
            {
                // El horario ya pasó (no hubo check-in), pero igual se borra para no dejar
                // basura visual de una reserva que nunca se concretó.
                await _calendarService.DeleteAsync(reserva.IdCalendarItem.Value, deleteSeries: false, reserva.CreatedBy.ToString());
            }
        }

        // Mismo filtro Role==1 && TypeUnit==1 que ya usa IExtraChargeService.GetUnidadesAsync
        // -- sólo para armar un título legible del CalendarItem, no crítico si no matchea.
        private async Task<string> ResolverNombreUnidadAsync(Guid idBuilding, Guid idGroupUnit)
        {
            var unidades = await ec.GetOwnersByBuildingAsync(idBuilding);
            var unidad = unidades.FirstOrDefault(u => u.IdGroupUnit == idGroupUnit);
            return unidad != null ? $"DPTO {unidad.UnitNumber}" : "unidad";
        }

        // Se inserta directo por BDLayout, sin pasar por ICalendarService.CreateAsync a
        // propósito: ese método manda un correo a TODOS los residentes del edificio
        // ("Nuevo evento programado...") cada vez que se crea un CalendarItem -- bien para
        // un evento real cargado por el Administrador, pero acá saldría un correo masivo
        // por cada Solicitud de reserva (o cada vez que Aprobar necesita autocurar una sin
        // vínculo), incluso antes de que la Junta la apruebe. Este CalendarItem es sólo un
        // marcador visual de "horario ocupado", no un anuncio -- si en el futuro se quiere
        // avisar de una reserva Aprobada, debería salir del módulo de Comunicados, no de acá.
        private async Task<CalendarItem> CrearCalendarItemDeReservaAsync(Reserva reserva, string nombreAreaComun, Guid idBuilding, string descripcion)
        {
            var nombreUnidad = await ResolverNombreUnidadAsync(idBuilding, reserva.IdGroupUnit);
            var calendarItem = new CalendarItem
            {
                IdCalendarItem = Guid.NewGuid(),
                IdBuilding = idBuilding,
                Title = $"Reserva: {nombreAreaComun} ({nombreUnidad})",
                Description = descripcion,
                Type = CalendarItemType.Event,
                StartDate = reserva.FechaInicio,
                EndDate = reserva.FechaFin,
                Location = nombreAreaComun,
                Status = CalendarItemStatus.Scheduled,
                CreatedBy = reserva.CreatedBy.ToString(),
                CreatedOn = DateTime.Now
            };
            await ec.AddNewRecordAsync(calendarItem);
            return calendarItem;
        }

        public async Task ConfirmarPagoAsync(Guid idReserva, decimal? montoConfirmado, DateTime? fechaPago, Guid confirmadoPor)
            => await ec.ConfirmarPagoReservaAsync(idReserva, montoConfirmado, fechaPago, confirmadoPor);

        public async Task<CheckInResultado> HacerCheckInAsync(Guid idReserva, Guid usuario, List<(string Descripcion, ChecklistEstado Estado, string? Observacion)> checklist, List<(byte[] Contenido, string FileName, string ContentType)> fotos)
        {
            var reserva = await ec.GetReservaByIdAsync(idReserva);
            if (!reserva.PagoConfirmado)
            {
                return new CheckInResultado { Mensaje = "Falta confirmar el pago de la Garantía/Alquiler/Limpieza antes de hacer el check-in." };
            }

            await GuardarChecklistYFotosAsync(idReserva, usuario, ChecklistEtapa.Entrega, checklist, fotos);
            await ec.UpdateReservaEstadoAsync(idReserva, ReservaEstado.Entregada);
            return new CheckInResultado { Exito = true, Mensaje = "Check-in registrado." };
        }

        public async Task HacerCheckOutAsync(Guid idReserva, Guid usuario, List<(string Descripcion, ChecklistEstado Estado, string? Observacion)> checklist, List<(byte[] Contenido, string FileName, string ContentType)> fotos)
        {
            await GuardarChecklistYFotosAsync(idReserva, usuario, ChecklistEtapa.Devolucion, checklist, fotos);
            await ec.UpdateReservaEstadoAsync(idReserva, ReservaEstado.Finalizada);
        }

        private async Task GuardarChecklistYFotosAsync(Guid idReserva, Guid usuario, ChecklistEtapa etapa, List<(string Descripcion, ChecklistEstado Estado, string? Observacion)> checklist, List<(byte[] Contenido, string FileName, string ContentType)> fotos)
        {
            foreach (var item in checklist)
            {
                await ec.AddNewRecordAsync(new ReservaChecklistItem
                {
                    IdChecklistItem = Guid.NewGuid(),
                    IdReserva = idReserva,
                    Etapa = etapa,
                    Descripcion = item.Descripcion,
                    Estado = item.Estado,
                    Observacion = item.Observacion,
                    CreatedBy = usuario,
                    CreatedOn = DateTime.Now
                });
            }

            foreach (var foto in fotos)
            {
                var filePath = await _fileStorageService.SaveAsync(
                    new[] { "reservas", idReserva.ToString() }, foto.FileName, foto.Contenido);

                await ec.AddNewRecordAsync(new ReservaAttachment
                {
                    IdAttachment = Guid.NewGuid(),
                    IdReserva = idReserva,
                    Etapa = etapa,
                    FileName = foto.FileName,
                    ContentType = foto.ContentType,
                    FileSizeBytes = foto.Contenido.Length,
                    FilePath = filePath,
                    UploadedBy = usuario,
                    UploadedOn = DateTime.Now
                });
            }
        }

        public async Task<CerrarReservaResultado> CerrarAsync(Guid idReserva, Guid usuario, decimal montoDanio = 0)
        {
            var resultado = new CerrarReservaResultado();

            Reserva todas;
            try
            {
                todas = await ec.GetReservaByIdAsync(idReserva);
            }
            catch (EntityNotFoundException)
            {
                resultado.Mensaje = "Reserva no encontrada.";
                return resultado;
            }

            if (todas.Estado is ReservaEstado.Cancelada or ReservaEstado.NoPresentado)
            {
                var retenido = todas.MontoRetenido.GetValueOrDefault();
                if (retenido > 0)
                {
                    await ec.AddNewRecordAsync(new IngresoComunidad
                    {
                        IdIngreso = Guid.NewGuid(),
                        IdBuilding = todas.IdBuilding,
                        Concepto = todas.Estado == ReservaEstado.Cancelada
                            ? "Penalidad por Cancelación - Reserva"
                            : "Penalidad por No Presentarse - Reserva",
                        Monto = retenido,
                        IdReserva = idReserva,
                        CreatedBy = usuario,
                        CreatedOn = DateTime.Now
                    });
                }

                await ec.UpdateReservaEstadoAsync(idReserva, ReservaEstado.Cerrada, montoRetenido: retenido);

                resultado.Exito = true;
                resultado.MontoRetenido = retenido;
                resultado.MontoDevuelto = 0;
                resultado.Mensaje = "Reserva cerrada.";
                return resultado;
            }

            if (todas.Estado != ReservaEstado.Finalizada)
            {
                resultado.Mensaje = "Sólo se puede cerrar una reserva Finalizada, Cancelada o No Presentada.";
                return resultado;
            }

            if (todas.MontoAlquiler > 0)
            {
                await ec.AddNewRecordAsync(new IngresoComunidad
                {
                    IdIngreso = Guid.NewGuid(),
                    IdBuilding = todas.IdBuilding,
                    Concepto = $"Alquiler {todas.NombreAreaComun}",
                    Monto = todas.MontoAlquiler,
                    IdReserva = idReserva,
                    CreatedBy = usuario,
                    CreatedOn = DateTime.Now
                });
            }

            if (todas.MontoLimpieza > 0)
            {
                await ec.AddNewRecordAsync(new IngresoComunidad
                {
                    IdIngreso = Guid.NewGuid(),
                    IdBuilding = todas.IdBuilding,
                    Concepto = $"Limpieza {todas.NombreAreaComun}",
                    Monto = todas.MontoLimpieza,
                    IdReserva = idReserva,
                    CreatedBy = usuario,
                    CreatedOn = DateTime.Now
                });
            }

            var danio = Math.Max(0, montoDanio);
            var montoRetenidoGarantia = Math.Min(danio, todas.MontoGarantia);
            var montoDevuelto = todas.MontoGarantia - montoRetenidoGarantia;

            if (montoRetenidoGarantia > 0)
            {
                await ec.AddNewRecordAsync(new IngresoComunidad
                {
                    IdIngreso = Guid.NewGuid(),
                    IdBuilding = todas.IdBuilding,
                    Concepto = "Reposición de Daños - Reserva",
                    Monto = montoRetenidoGarantia,
                    IdReserva = idReserva,
                    CreatedBy = usuario,
                    CreatedOn = DateTime.Now
                });
            }

            if (danio > todas.MontoGarantia)
            {
                var excedente = danio - todas.MontoGarantia;
                var cuota = await _extraChargeService.GenerarCuotaExtraordinariaAsync(
                    todas.IdBuilding,
                    $"Daño en {todas.NombreAreaComun} (excede garantía) - Reserva del {todas.FechaInicio:dd/MM/yyyy}",
                    DateTime.Today.AddDays(15),
                    new Dictionary<Guid, decimal> { [todas.IdGroupUnit] = excedente },
                    usuario.ToString());

                resultado.SeGeneroCuotaExtraordinaria = cuota.Exito;
                resultado.MontoCuotaExtraordinaria = cuota.Exito ? excedente : 0;

                if (!cuota.Exito)
                {
                    _logger.LogWarning("No se pudo generar la cuota extraordinaria por daño de la reserva {IdReserva}: {Mensaje}", idReserva, cuota.Mensaje);
                }
            }

            await ec.UpdateReservaEstadoAsync(idReserva, ReservaEstado.Cerrada, montoRetenido: montoRetenidoGarantia);

            resultado.Exito = true;
            resultado.MontoDevuelto = montoDevuelto;
            resultado.MontoRetenido = montoRetenidoGarantia;
            resultado.Mensaje = "Reserva cerrada y garantía liquidada.";
            return resultado;
        }
    }
}
