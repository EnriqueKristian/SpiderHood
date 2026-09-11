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

        Task HacerCheckInAsync(Guid idReserva, Guid usuario, List<(string Descripcion, ChecklistEstado Estado, string? Observacion)> checklist, List<(byte[] Contenido, string FileName, string ContentType)> fotos);

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
        private readonly ILogger<ReservaService> _logger;
        private BDLayout ec { get; set; }

        public ReservaService(
            IDbContextFactory<SpiderHoodContext> contextFactory,
            IFileStorageService fileStorageService,
            IExtraChargeService extraChargeService,
            ILogger<ReservaService> logger)
        {
            _fileStorageService = fileStorageService;
            _extraChargeService = extraChargeService;
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

            await ec.AddNewRecordAsync(reserva);

            resultado.Exito = true;
            resultado.IdReserva = reserva.IdReserva;
            resultado.Mensaje = "Reserva solicitada -- queda pendiente de aprobación de la Junta.";
            return resultado;
        }

        public async Task AprobarAsync(Guid idReserva, Guid aprobadoPor)
            => await ec.UpdateReservaEstadoAsync(idReserva, ReservaEstado.Aprobada, aprobadoPor: aprobadoPor);

        public async Task RechazarAsync(Guid idReserva, Guid aprobadoPor, string motivo)
            => await ec.UpdateReservaEstadoAsync(idReserva, ReservaEstado.Rechazada, motivoRechazo: motivo, aprobadoPor: aprobadoPor);

        public async Task CancelarAsync(Guid idReserva, AreaComun areaComun)
        {
            var reserva = await ec.GetReservaByIdAsync(idReserva);

            var diasParaEvento = (reserva.FechaInicio - DateTime.Now).TotalDays;
            var aplicaPenalidad = areaComun.PenalidadCancelacionHabilitada
                && areaComun.DiasMinimosSinPenalidad.HasValue
                && diasParaEvento < areaComun.DiasMinimosSinPenalidad.Value;

            var montoRetenido = aplicaPenalidad ? reserva.MontoGarantia : 0;
            await ec.UpdateReservaEstadoAsync(idReserva, ReservaEstado.Cancelada, montoRetenido: montoRetenido);
        }

        public async Task MarcarNoPresentadoAsync(Guid idReserva, AreaComun areaComun)
        {
            var reserva = await ec.GetReservaByIdAsync(idReserva);
            var montoRetenido = areaComun.PenalidadNoPresentadoHabilitada ? reserva.MontoGarantia : 0;

            await ec.UpdateReservaEstadoAsync(idReserva, ReservaEstado.NoPresentado, montoRetenido: montoRetenido);
        }

        public async Task HacerCheckInAsync(Guid idReserva, Guid usuario, List<(string Descripcion, ChecklistEstado Estado, string? Observacion)> checklist, List<(byte[] Contenido, string FileName, string ContentType)> fotos)
        {
            await GuardarChecklistYFotosAsync(idReserva, usuario, ChecklistEtapa.Entrega, checklist, fotos);
            await ec.UpdateReservaEstadoAsync(idReserva, ReservaEstado.Entregada);
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
