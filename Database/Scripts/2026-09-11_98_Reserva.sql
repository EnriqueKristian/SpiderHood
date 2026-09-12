-- =============================================================================
-- Módulo de Reservas de Áreas Comunes (Docs/Pendientes-Negocio-Consolidado.md
-- #21) -- diseño cerrado con el usuario el 2026-09-11. Tablas 100% nuevas, no
-- tocan nada existente (ni BuildingConfiguration ni CalendarItem).
--
-- Simplificaciones deliberadas de esta primera versión (documentadas también
-- en el backlog, no son un olvido):
--   1. El chequeo de solapamiento de horarios lo hace esta misma tabla
--      (GET_ReservasConflicto) -- CalendarItem no tiene ningún concepto de
--      "recurso" (Location es texto libre), agregarlo sería un cambio de
--      otro alcance. Integración visual con el calendario general: pendiente.
--   2. IngresoComunidad es un registro simple para Alquiler/Limpieza/Garantía
--      retenida -- NO está conectado todavía al Reporte de Ingresos y Egresos
--      (que hoy es 100% conciliación bancaria importada). Es la pregunta que
--      el propio usuario dejó abierta, no se resuelve acá.
--
-- Alcance: propietario ve disponibilidad del Área Común y solicita una
-- Reserva -> la aprueba/rechaza la JUNTA (approve_reservations) -> el
-- ADMINISTRADOR hace el check-in/check-out con checklist + fotos -> al cerrar,
-- la garantía se devuelve/retiene según el checklist y la penalidad
-- configurada por Área Común.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AreaComun')
BEGIN
    CREATE TABLE dbo.AreaComun
    (
        IdAreaComun                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdBuilding                     UNIQUEIDENTIFIER NOT NULL,
        Nombre                         NVARCHAR(150)    NOT NULL,
        Descripcion                    NVARCHAR(500)    NULL,
        AforoMaximo                    INT              NULL,
        DuracionMinMinutos             INT              NULL,
        DuracionMaxMinutos             INT              NULL,
        BufferMinutos                  INT              NOT NULL DEFAULT (0),
        AnticipacionMinHoras           INT              NOT NULL DEFAULT (0),
        AnticipacionMaxDias            INT              NULL,
        TopeReservasActivasPorUnidad   INT              NULL,
        GarantiaInternos               DECIMAL(18,2)    NOT NULL DEFAULT (0),
        GarantiaExternos               DECIMAL(18,2)    NOT NULL DEFAULT (0),
        AlquilerInternos               DECIMAL(18,2)    NOT NULL DEFAULT (0),
        AlquilerExternos               DECIMAL(18,2)    NOT NULL DEFAULT (0),
        Limpieza                       DECIMAL(18,2)    NOT NULL DEFAULT (0),
        PenalidadCancelacionHabilitada BIT              NOT NULL DEFAULT (0),
        DiasMinimosSinPenalidad        INT              NULL,
        PenalidadNoPresentadoHabilitada BIT             NOT NULL DEFAULT (0),
        Activo                         BIT              NOT NULL DEFAULT (1),
        CreatedBy                      UNIQUEIDENTIFIER NOT NULL,
        CreatedOn                      DATETIME2        NOT NULL
    );

    CREATE INDEX IX_AreaComun_Building ON dbo.AreaComun (IdBuilding);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Reserva')
BEGIN
    CREATE TABLE dbo.Reserva
    (
        IdReserva          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdBuilding         UNIQUEIDENTIFIER NOT NULL,
        IdAreaComun        UNIQUEIDENTIFIER NOT NULL,
        IdGroupUnit        UNIQUEIDENTIFIER NOT NULL,   -- propietario responsable (siempre)
        FechaInicio        DATETIME2        NOT NULL,
        FechaFin           DATETIME2        NOT NULL,
        EsExterno          BIT              NOT NULL DEFAULT (0),
        OrganizadorNombre  NVARCHAR(200)    NULL,       -- sólo si EsExterno
        OrganizadorDocumento NVARCHAR(30)   NULL,
        OrganizadorTelefono NVARCHAR(30)    NULL,
        Estado             INT              NOT NULL,   -- ver ReservaEstado (Classes/Reservations/Reserva.cs)
        MontoGarantia      DECIMAL(18,2)    NOT NULL DEFAULT (0),
        MontoAlquiler      DECIMAL(18,2)    NOT NULL DEFAULT (0),
        MontoLimpieza      DECIMAL(18,2)    NOT NULL DEFAULT (0),
        MontoRetenido      DECIMAL(18,2)    NULL,        -- se completa al Cerrar
        MotivoRechazo      NVARCHAR(500)    NULL,
        AprobadoPor        UNIQUEIDENTIFIER NULL,
        FechaAprobacion    DATETIME2        NULL,
        IdCalendarItem     UNIQUEIDENTIFIER NULL,       -- ver nota de IdCalendarItem abajo
        PagoConfirmado     BIT              NOT NULL DEFAULT (0), -- ver nota de PagoConfirmado abajo
        MontoPagoConfirmado DECIMAL(18,2)   NULL,
        FechaPagoConfirmado DATETIME2       NULL,
        PagoConfirmadoPor  UNIQUEIDENTIFIER NULL,
        CreatedBy          UNIQUEIDENTIFIER NOT NULL,
        CreatedOn          DATETIME2        NOT NULL
    );

    CREATE INDEX IX_Reserva_Building ON dbo.Reserva (IdBuilding, FechaInicio);
    CREATE INDEX IX_Reserva_AreaComun ON dbo.Reserva (IdAreaComun, FechaInicio, FechaFin);
END
GO

-- Feedback del usuario tras probar en vivo (2026-09-11): una reserva Pendiente/
-- Aprobada no aparecía en el Calendario general, así que otro propietario no
-- tenía forma de ver visualmente que el área ya estaba comprometida para ese
-- horario (más allá del chequeo de conflicto al solicitar). Se resuelve
-- creando un CalendarItem (Type=Event) por cada Reserva -- se borra si la
-- Reserva se Rechaza/Cancela/marca NoPresentado (libera el horario), se
-- mantiene visible el resto del ciclo de vida. IdCalendarItem guarda el
-- vínculo para poder actualizarlo/borrarlo después (columna agregada acá para
-- que también quede en instalaciones que ya habían corrido este script antes
-- de este agregado).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reserva') AND name = 'IdCalendarItem')
    ALTER TABLE dbo.Reserva ADD IdCalendarItem UNIQUEIDENTIFIER NULL;
GO

-- Feedback del usuario (2026-09-12): no hay pasarela de pago (brecha ya
-- señalada en el análisis de mercado) -- confirmar el cobro de Garantía/
-- Alquiler/Limpieza es, por ahora, un check manual del Administrador, sin
-- conectarse a ninguna cuenta bancaria real ni a la Conciliación existente
-- (eso queda para cuando se diseñe la pantalla de conciliación específica
-- de Reservas). El check-in NO se habilita hasta que esto esté marcado --
-- ver IReservaService.HacerCheckInAsync. Columnas agregadas acá también
-- para instalaciones que ya habían corrido este script antes.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reserva') AND name = 'PagoConfirmado')
    ALTER TABLE dbo.Reserva ADD PagoConfirmado BIT NOT NULL CONSTRAINT DF_Reserva_PagoConfirmado DEFAULT (0);
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reserva') AND name = 'MontoPagoConfirmado')
    ALTER TABLE dbo.Reserva ADD MontoPagoConfirmado DECIMAL(18,2) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reserva') AND name = 'FechaPagoConfirmado')
    ALTER TABLE dbo.Reserva ADD FechaPagoConfirmado DATETIME2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reserva') AND name = 'PagoConfirmadoPor')
    ALTER TABLE dbo.Reserva ADD PagoConfirmadoPor UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReservaChecklistItem')
BEGIN
    CREATE TABLE dbo.ReservaChecklistItem
    (
        IdChecklistItem UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdReserva       UNIQUEIDENTIFIER NOT NULL,
        Etapa           INT              NOT NULL,   -- 1=Entrega, 2=Devolución
        Descripcion     NVARCHAR(200)    NOT NULL,   -- ej. "10 sillas", "Proyector"
        Estado          INT              NOT NULL,   -- 1=OK, 2=Dañado, 3=Falta
        Observacion     NVARCHAR(500)    NULL,
        CreatedBy       UNIQUEIDENTIFIER NOT NULL,
        CreatedOn       DATETIME2        NOT NULL
    );

    CREATE INDEX IX_ReservaChecklistItem_Reserva ON dbo.ReservaChecklistItem (IdReserva, Etapa);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReservaAttachment')
BEGIN
    CREATE TABLE dbo.ReservaAttachment
    (
        IdAttachment  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdReserva     UNIQUEIDENTIFIER NOT NULL,
        Etapa         INT              NOT NULL,   -- 1=Entrega, 2=Devolución
        FileName      NVARCHAR(260)    NOT NULL,
        ContentType   NVARCHAR(100)    NOT NULL,
        FileSizeBytes INT              NOT NULL,
        FilePath      NVARCHAR(500)    NOT NULL,
        UploadedBy    UNIQUEIDENTIFIER NOT NULL,
        UploadedOn    DATETIME2        NOT NULL
    );

    CREATE INDEX IX_ReservaAttachment_Reserva ON dbo.ReservaAttachment (IdReserva, Etapa);
END
GO

-- Registro simple de Alquiler/Limpieza/Garantía retenida por una Reserva --
-- ver nota al inicio del script: no conectado todavía al Reporte de Ingresos
-- y Egresos (que hoy sólo lee conciliación bancaria importada).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'IngresoComunidad')
BEGIN
    CREATE TABLE dbo.IngresoComunidad
    (
        IdIngreso   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdBuilding  UNIQUEIDENTIFIER NOT NULL,
        Concepto    NVARCHAR(200)    NOT NULL,
        Monto       DECIMAL(18,2)    NOT NULL,
        IdReserva   UNIQUEIDENTIFIER NULL,
        CreatedBy   UNIQUEIDENTIFIER NOT NULL,
        CreatedOn   DATETIME2        NOT NULL
    );

    CREATE INDEX IX_IngresoComunidad_Building ON dbo.IngresoComunidad (IdBuilding, CreatedOn DESC);
END
GO

-- ===================== Área Común =====================
CREATE OR ALTER PROCEDURE dbo.INS_AreaComun
    @IdAreaComun UNIQUEIDENTIFIER, @IdBuilding UNIQUEIDENTIFIER, @Nombre NVARCHAR(150), @Descripcion NVARCHAR(500) = NULL,
    @AforoMaximo INT = NULL, @DuracionMinMinutos INT = NULL, @DuracionMaxMinutos INT = NULL, @BufferMinutos INT = 0,
    @AnticipacionMinHoras INT = 0, @AnticipacionMaxDias INT = NULL, @TopeReservasActivasPorUnidad INT = NULL,
    @GarantiaInternos DECIMAL(18,2) = 0, @GarantiaExternos DECIMAL(18,2) = 0,
    @AlquilerInternos DECIMAL(18,2) = 0, @AlquilerExternos DECIMAL(18,2) = 0, @Limpieza DECIMAL(18,2) = 0,
    @PenalidadCancelacionHabilitada BIT = 0, @DiasMinimosSinPenalidad INT = NULL, @PenalidadNoPresentadoHabilitada BIT = 0,
    @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AreaComun
        (IdAreaComun, IdBuilding, Nombre, Descripcion, AforoMaximo, DuracionMinMinutos, DuracionMaxMinutos, BufferMinutos,
         AnticipacionMinHoras, AnticipacionMaxDias, TopeReservasActivasPorUnidad, GarantiaInternos, GarantiaExternos,
         AlquilerInternos, AlquilerExternos, Limpieza, PenalidadCancelacionHabilitada, DiasMinimosSinPenalidad,
         PenalidadNoPresentadoHabilitada, Activo, CreatedBy, CreatedOn)
    VALUES
        (@IdAreaComun, @IdBuilding, @Nombre, @Descripcion, @AforoMaximo, @DuracionMinMinutos, @DuracionMaxMinutos, @BufferMinutos,
         @AnticipacionMinHoras, @AnticipacionMaxDias, @TopeReservasActivasPorUnidad, @GarantiaInternos, @GarantiaExternos,
         @AlquilerInternos, @AlquilerExternos, @Limpieza, @PenalidadCancelacionHabilitada, @DiasMinimosSinPenalidad,
         @PenalidadNoPresentadoHabilitada, 1, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_AreaComun
    @IdAreaComun UNIQUEIDENTIFIER, @Nombre NVARCHAR(150), @Descripcion NVARCHAR(500) = NULL,
    @AforoMaximo INT = NULL, @DuracionMinMinutos INT = NULL, @DuracionMaxMinutos INT = NULL, @BufferMinutos INT = 0,
    @AnticipacionMinHoras INT = 0, @AnticipacionMaxDias INT = NULL, @TopeReservasActivasPorUnidad INT = NULL,
    @GarantiaInternos DECIMAL(18,2) = 0, @GarantiaExternos DECIMAL(18,2) = 0,
    @AlquilerInternos DECIMAL(18,2) = 0, @AlquilerExternos DECIMAL(18,2) = 0, @Limpieza DECIMAL(18,2) = 0,
    @PenalidadCancelacionHabilitada BIT = 0, @DiasMinimosSinPenalidad INT = NULL, @PenalidadNoPresentadoHabilitada BIT = 0,
    @Activo BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.AreaComun SET
        Nombre = @Nombre, Descripcion = @Descripcion, AforoMaximo = @AforoMaximo,
        DuracionMinMinutos = @DuracionMinMinutos, DuracionMaxMinutos = @DuracionMaxMinutos, BufferMinutos = @BufferMinutos,
        AnticipacionMinHoras = @AnticipacionMinHoras, AnticipacionMaxDias = @AnticipacionMaxDias,
        TopeReservasActivasPorUnidad = @TopeReservasActivasPorUnidad,
        GarantiaInternos = @GarantiaInternos, GarantiaExternos = @GarantiaExternos,
        AlquilerInternos = @AlquilerInternos, AlquilerExternos = @AlquilerExternos, Limpieza = @Limpieza,
        PenalidadCancelacionHabilitada = @PenalidadCancelacionHabilitada, DiasMinimosSinPenalidad = @DiasMinimosSinPenalidad,
        PenalidadNoPresentadoHabilitada = @PenalidadNoPresentadoHabilitada, Activo = @Activo
    WHERE IdAreaComun = @IdAreaComun;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AreaComunesByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.AreaComun WHERE IdBuilding = @IdBuilding ORDER BY Nombre ASC;
END
GO

-- ===================== Reserva =====================
CREATE OR ALTER PROCEDURE dbo.INS_Reserva
    @IdReserva UNIQUEIDENTIFIER, @IdBuilding UNIQUEIDENTIFIER, @IdAreaComun UNIQUEIDENTIFIER, @IdGroupUnit UNIQUEIDENTIFIER,
    @FechaInicio DATETIME2, @FechaFin DATETIME2, @EsExterno BIT = 0,
    @OrganizadorNombre NVARCHAR(200) = NULL, @OrganizadorDocumento NVARCHAR(30) = NULL, @OrganizadorTelefono NVARCHAR(30) = NULL,
    @Estado INT, @MontoGarantia DECIMAL(18,2), @MontoAlquiler DECIMAL(18,2), @MontoLimpieza DECIMAL(18,2),
    @IdCalendarItem UNIQUEIDENTIFIER = NULL,
    @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Reserva
        (IdReserva, IdBuilding, IdAreaComun, IdGroupUnit, FechaInicio, FechaFin, EsExterno, OrganizadorNombre,
         OrganizadorDocumento, OrganizadorTelefono, Estado, MontoGarantia, MontoAlquiler, MontoLimpieza, IdCalendarItem, CreatedBy, CreatedOn)
    VALUES
        (@IdReserva, @IdBuilding, @IdAreaComun, @IdGroupUnit, @FechaInicio, @FechaFin, @EsExterno, @OrganizadorNombre,
         @OrganizadorDocumento, @OrganizadorTelefono, @Estado, @MontoGarantia, @MontoAlquiler, @MontoLimpieza, @IdCalendarItem, @CreatedBy, SYSUTCDATETIME());
END
GO

-- Transición de estado genérica -- cubre Aprobar/Rechazar/Cancelar/NoPresentado/
-- Entregar/Finalizar/Cerrar; los parámetros que no aplican a una transición
-- puntual quedan NULL y no pisan el valor ya guardado (COALESCE).
CREATE OR ALTER PROCEDURE dbo.UPD_ReservaEstado
    @IdReserva UNIQUEIDENTIFIER, @Estado INT, @MotivoRechazo NVARCHAR(500) = NULL,
    @AprobadoPor UNIQUEIDENTIFIER = NULL, @MontoRetenido DECIMAL(18,2) = NULL,
    @IdCalendarItem UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Reserva SET
        Estado = @Estado,
        MotivoRechazo = COALESCE(@MotivoRechazo, MotivoRechazo),
        AprobadoPor = COALESCE(@AprobadoPor, AprobadoPor),
        FechaAprobacion = CASE WHEN @AprobadoPor IS NOT NULL THEN SYSUTCDATETIME() ELSE FechaAprobacion END,
        MontoRetenido = COALESCE(@MontoRetenido, MontoRetenido),
        IdCalendarItem = COALESCE(@IdCalendarItem, IdCalendarItem)
    WHERE IdReserva = @IdReserva;
END
GO

-- Confirmación manual de pago (ver nota en la definición de la tabla más
-- arriba) -- separado de UPD_ReservaEstado porque no es una transición de
-- Estado, es un dato aparte que se puede confirmar en cualquier momento
-- después de Aprobada.
CREATE OR ALTER PROCEDURE dbo.UPD_ReservaPago
    @IdReserva UNIQUEIDENTIFIER, @MontoPagoConfirmado DECIMAL(18,2) = NULL,
    @FechaPagoConfirmado DATETIME2 = NULL, @PagoConfirmadoPor UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Reserva SET
        PagoConfirmado = 1,
        MontoPagoConfirmado = @MontoPagoConfirmado,
        FechaPagoConfirmado = COALESCE(@FechaPagoConfirmado, SYSUTCDATETIME()),
        PagoConfirmadoPor = @PagoConfirmadoPor
    WHERE IdReserva = @IdReserva;
END
GO

-- NombreUnidad NO se resuelve acá -- no hay certeza del nombre real de la
-- tabla/vista de unidades desde este script (VW_OwnerUnit es una VIEW, no
-- necesariamente 1:1 con una tabla "Unit"). Se resuelve del lado C# con el
-- mismo patrón ya usado en BuildingConfig.razor.cs (GetGroupUnitName, busca
-- en la lista de UnitView ya cargada) -- evita adivinar un nombre de tabla
-- mal y romper el SP en la base real.
CREATE OR ALTER PROCEDURE dbo.GET_ReservasByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, a.Nombre AS NombreAreaComun,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Reserva r
    LEFT JOIN dbo.AreaComun a ON a.IdAreaComun = r.IdAreaComun
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdBuilding = @IdBuilding
    ORDER BY r.FechaInicio DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReservaById
    @IdReserva UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, a.Nombre AS NombreAreaComun,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Reserva r
    LEFT JOIN dbo.AreaComun a ON a.IdAreaComun = r.IdAreaComun
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdReserva = @IdReserva;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReservasPendientesByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, a.Nombre AS NombreAreaComun,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Reserva r
    LEFT JOIN dbo.AreaComun a ON a.IdAreaComun = r.IdAreaComun
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdBuilding = @IdBuilding AND r.Estado = 1 -- PendienteDeAprobacion
    ORDER BY r.FechaInicio ASC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReservasByGroupUnit
    @IdGroupUnit UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, a.Nombre AS NombreAreaComun,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Reserva r
    LEFT JOIN dbo.AreaComun a ON a.IdAreaComun = r.IdAreaComun
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdGroupUnit = @IdGroupUnit
    ORDER BY r.FechaInicio DESC;
END
GO

-- Reservas que ocupan el Área Común en una ventana de fechas, para chequear
-- solapamiento antes de confirmar una nueva -- excluye estados que ya no
-- bloquean el horario (Rechazada/Cancelada/NoPresentado).
-- Igual que las demás GET_Reservas*: trae NombreAreaComun/CreatedByName vía
-- LEFT JOIN -- Models.Reserva es una entidad keyless mapeada 1:1 a estas dos
-- columnas "extra" en TODOS los SPs que la devuelven, así que EF exige que
-- estén siempre presentes (si un SP hace sólo "SELECT * FROM Reserva" sin
-- ellas, FromSqlRaw revienta con "required column ... was not present",
-- visto en vivo al abrir "Nueva Solicitud" -- Docs/Pendientes-Negocio-
-- Consolidado.md #21).
CREATE OR ALTER PROCEDURE dbo.GET_ReservasConflicto
    @IdAreaComun UNIQUEIDENTIFIER, @FechaInicio DATETIME2, @FechaFin DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, a.Nombre AS NombreAreaComun,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Reserva r
    LEFT JOIN dbo.AreaComun a ON a.IdAreaComun = r.IdAreaComun
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdAreaComun = @IdAreaComun
      AND r.Estado NOT IN (3, 4, 5) -- Rechazada, Cancelada, NoPresentado
      AND r.FechaInicio < @FechaFin
      AND r.FechaFin > @FechaInicio;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReservasProximasByAreaComun
    @IdAreaComun UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, a.Nombre AS NombreAreaComun,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Reserva r
    LEFT JOIN dbo.AreaComun a ON a.IdAreaComun = r.IdAreaComun
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdAreaComun = @IdAreaComun
      AND r.Estado IN (1, 2, 6) -- PendienteDeAprobacion, Aprobada, Entregada
      AND r.FechaFin >= SYSUTCDATETIME()
    ORDER BY r.FechaInicio ASC;
END
GO

-- ===================== Checklist =====================
CREATE OR ALTER PROCEDURE dbo.INS_ReservaChecklistItem
    @IdChecklistItem UNIQUEIDENTIFIER, @IdReserva UNIQUEIDENTIFIER, @Etapa INT, @Descripcion NVARCHAR(200),
    @Estado INT, @Observacion NVARCHAR(500) = NULL, @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ReservaChecklistItem (IdChecklistItem, IdReserva, Etapa, Descripcion, Estado, Observacion, CreatedBy, CreatedOn)
    VALUES (@IdChecklistItem, @IdReserva, @Etapa, @Descripcion, @Estado, @Observacion, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReservaChecklistItemsByReserva
    @IdReserva UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.ReservaChecklistItem WHERE IdReserva = @IdReserva ORDER BY Etapa ASC, CreatedOn ASC;
END
GO

-- ===================== Adjuntos =====================
CREATE OR ALTER PROCEDURE dbo.INS_ReservaAttachment
    @IdAttachment UNIQUEIDENTIFIER, @IdReserva UNIQUEIDENTIFIER, @Etapa INT, @FileName NVARCHAR(260),
    @ContentType NVARCHAR(100), @FileSizeBytes INT, @FilePath NVARCHAR(500), @UploadedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ReservaAttachment (IdAttachment, IdReserva, Etapa, FileName, ContentType, FileSizeBytes, FilePath, UploadedBy, UploadedOn)
    VALUES (@IdAttachment, @IdReserva, @Etapa, @FileName, @ContentType, @FileSizeBytes, @FilePath, @UploadedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReservaAttachmentsByReserva
    @IdReserva UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.ReservaAttachment WHERE IdReserva = @IdReserva ORDER BY Etapa ASC, UploadedOn ASC;
END
GO

-- ===================== Ingreso simple =====================
CREATE OR ALTER PROCEDURE dbo.INS_IngresoComunidad
    @IdIngreso UNIQUEIDENTIFIER, @IdBuilding UNIQUEIDENTIFIER, @Concepto NVARCHAR(200), @Monto DECIMAL(18,2),
    @IdReserva UNIQUEIDENTIFIER = NULL, @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.IngresoComunidad (IdIngreso, IdBuilding, Concepto, Monto, IdReserva, CreatedBy, CreatedOn)
    VALUES (@IdIngreso, @IdBuilding, @Concepto, @Monto, @IdReserva, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_IngresosComunidadByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.IngresoComunidad WHERE IdBuilding = @IdBuilding ORDER BY CreatedOn DESC;
END
GO

-- ===================== Permisos =====================
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = 'approve_reservations')
INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
VALUES (NEWID(), 'approve_reservations', 'Aprobar Reservas', 'Aprobar o rechazar una reserva de área común (Junta).', 'budget');

IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = 'manage_reservations')
INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
VALUES (NEWID(), 'manage_reservations', 'Gestionar Reservas', 'Hacer check-in/check-out de una reserva y liquidar su garantía (Administrador).', 'budget');
GO

-- Menú: "Reservas" (residente, solicitar) y "Gestión de Reservas" (admin,
-- check-in/check-out) -- se crean acá ambos standalone (IdParent NULL),
-- mismo criterio que Comunicados (2026-09-11_97_Comunicado.sql): el gateo
-- real es por permiso dentro de cada página, no por una tabla menú-a-
-- permiso. "Reservas" se reubica más abajo dentro de "Portal del
-- Residente" -- ver esa nota.
EXEC dbo.INS_MenuItem
    @IdMenu = 'C4A8E2D6-1F5B-4A9C-8E2D-3B7A6F1C9D80',
    @IdParent = NULL,
    @ItemKey = 'reservations',
    @Title = 'Reservas',
    @Icon = 'bi bi-calendar-check',
    @Url = '/reservas',
    @Target = NULL,
    @DisplayOrder = 31,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;

EXEC dbo.INS_MenuItem
    @IdMenu = 'D5B9F3E7-2A6C-4B0D-9F3E-4C8B7A2D0E91',
    @IdParent = NULL,
    @ItemKey = 'reservations_admin',
    @Title = 'Gestión de Reservas',
    @Icon = 'bi bi-clipboard-check',
    @Url = '/reservas-admin',
    @Target = NULL,
    @DisplayOrder = 32,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO

-- Feedback del usuario (2026-09-12): "Reservas" es la vista de autoservicio
-- del Residente (reserva a nombre de SU propia unidad, ve sólo sus propias
-- reservas) -- corresponde adentro de "Portal del Residente" igual que "Mis
-- Pagos"/"Comunicados"/etc (ver Database/Scripts/2026-09-10_85_Reorganizar_
-- Menu.sql), no como ítem raíz suelto, y el título debe dejar claro que es
-- personal. "Gestión de Reservas" (arriba) se queda como ítem raíz -- es la
-- vista del Administrador/Junta sobre TODAS las reservas del edificio.
-- 'C30303F7-DF5D-4526-976E-85C0881A1C79' es el IdMenu de "Portal del
-- Residente"; orden 7 sigue a "Mi consumo de agua" (orden 6, ver ese mismo
-- script).
EXEC dbo.UPD_MenuItem
    'C4A8E2D6-1F5B-4A9C-8E2D-3B7A6F1C9D80', 'C30303F7-DF5D-4526-976E-85C0881A1C79',
    'reservations', 'Mis Reservas', 'bi-calendar-check', 'reservas', NULL,
    7, 1, NULL, NULL, SYSUTCDATETIME();
GO
