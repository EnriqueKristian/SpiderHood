-- =============================================================================
-- Renombra el módulo de Reservas de Áreas Comunes a inglés (Docs/Pendientes-
-- Negocio-Consolidado.md #21): tablas, stored procedures y las rutas de
-- MenuItems. Reemplaza el script 2026-09-11_98_Reserva.sql. Mismo criterio
-- que las migraciones anteriores (Employee/Payroll, Governance,
-- Announcement): DROP + CREATE en vez de sp_rename (entorno de pruebas sin
-- datos que preservar), texto de UI (Title de menú, mensajes al usuario)
-- queda en español.
--
-- Mapeo: AreaComun->CommonArea, Reserva->Reservation,
-- ReservaChecklistItem->ReservationChecklistItem, ReservaAttachment->
-- ReservationAttachment, IngresoComunidad->CommunityIncome. Los permission
-- keys (approve_reservations, manage_reservations) y los ItemKey de
-- MenuItems (reservations, reservations_admin) ya estaban en inglés desde
-- antes -- no se tocan, sólo su Url.
-- =============================================================================

SET NOCOUNT ON;
GO

-- ===================== DROP procedures viejos =====================
DROP PROCEDURE IF EXISTS dbo.INS_AreaComun;
DROP PROCEDURE IF EXISTS dbo.UPD_AreaComun;
DROP PROCEDURE IF EXISTS dbo.GET_AreaComunesByBuilding;
DROP PROCEDURE IF EXISTS dbo.INS_Reserva;
DROP PROCEDURE IF EXISTS dbo.UPD_ReservaEstado;
DROP PROCEDURE IF EXISTS dbo.UPD_ReservaPago;
DROP PROCEDURE IF EXISTS dbo.UPD_ReservaFechas;
DROP PROCEDURE IF EXISTS dbo.GET_ReservasByBuilding;
DROP PROCEDURE IF EXISTS dbo.GET_ReservaById;
DROP PROCEDURE IF EXISTS dbo.GET_ReservasPendientesByBuilding;
DROP PROCEDURE IF EXISTS dbo.GET_ReservasByGroupUnit;
DROP PROCEDURE IF EXISTS dbo.GET_ReservasConflicto;
DROP PROCEDURE IF EXISTS dbo.GET_ReservasProximasByAreaComun;
DROP PROCEDURE IF EXISTS dbo.INS_ReservaChecklistItem;
DROP PROCEDURE IF EXISTS dbo.GET_ReservaChecklistItemsByReserva;
DROP PROCEDURE IF EXISTS dbo.INS_ReservaAttachment;
DROP PROCEDURE IF EXISTS dbo.GET_ReservaAttachmentsByReserva;
DROP PROCEDURE IF EXISTS dbo.INS_IngresoComunidad;
DROP PROCEDURE IF EXISTS dbo.GET_IngresosComunidadByBuilding;
GO

-- ===================== DROP tablas viejas (hijas primero) =====================
DROP TABLE IF EXISTS dbo.IngresoComunidad;
DROP TABLE IF EXISTS dbo.ReservaAttachment;
DROP TABLE IF EXISTS dbo.ReservaChecklistItem;
DROP TABLE IF EXISTS dbo.Reserva;
DROP TABLE IF EXISTS dbo.AreaComun;
GO

-- ===================== CREATE tablas nuevas =====================
CREATE TABLE dbo.CommonArea
(
    IdCommonArea                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    IdBuilding                      UNIQUEIDENTIFIER NOT NULL,
    Nombre                          NVARCHAR(150)    NOT NULL,
    Descripcion                     NVARCHAR(500)    NULL,
    AforoMaximo                     INT              NULL,
    DuracionMinMinutos              INT              NULL,
    DuracionMaxMinutos              INT              NULL,
    BufferMinutos                   INT              NOT NULL DEFAULT (0),
    AnticipacionMinHoras            INT              NOT NULL DEFAULT (0),
    AnticipacionMaxDias             INT              NULL,
    TopeReservationsActivasPorUnidad INT             NULL,
    GarantiaInternos                DECIMAL(18,2)    NOT NULL DEFAULT (0),
    GarantiaExternos                DECIMAL(18,2)    NOT NULL DEFAULT (0),
    AlquilerInternos                DECIMAL(18,2)    NOT NULL DEFAULT (0),
    AlquilerExternos                DECIMAL(18,2)    NOT NULL DEFAULT (0),
    Limpieza                        DECIMAL(18,2)    NOT NULL DEFAULT (0),
    PenalidadCancelacionHabilitada  BIT              NOT NULL DEFAULT (0),
    DiasMinimosSinPenalidad         INT              NULL,
    PenalidadNoPresentadoHabilitada BIT              NOT NULL DEFAULT (0),
    Activo                          BIT              NOT NULL DEFAULT (1),
    CreatedBy                       UNIQUEIDENTIFIER NOT NULL,
    CreatedOn                       DATETIME2        NOT NULL
);

CREATE INDEX IX_CommonArea_Building ON dbo.CommonArea (IdBuilding);
GO

CREATE TABLE dbo.Reservation
(
    IdReservation        UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    IdBuilding           UNIQUEIDENTIFIER NOT NULL,
    IdCommonArea         UNIQUEIDENTIFIER NOT NULL,
    IdGroupUnit          UNIQUEIDENTIFIER NOT NULL,
    FechaInicio          DATETIME2        NOT NULL,
    FechaFin             DATETIME2        NOT NULL,
    EsExterno            BIT              NOT NULL DEFAULT (0),
    OrganizadorNombre    NVARCHAR(200)    NULL,
    OrganizadorDocumento NVARCHAR(30)     NULL,
    OrganizadorTelefono  NVARCHAR(30)     NULL,
    Estado               INT              NOT NULL,   -- ReservationStatus
    MontoGarantia        DECIMAL(18,2)    NOT NULL DEFAULT (0),
    MontoAlquiler        DECIMAL(18,2)    NOT NULL DEFAULT (0),
    MontoLimpieza        DECIMAL(18,2)    NOT NULL DEFAULT (0),
    MontoRetenido        DECIMAL(18,2)    NULL,
    MotivoRechazo        NVARCHAR(500)    NULL,
    AprobadoPor          UNIQUEIDENTIFIER NULL,
    FechaAprobacion      DATETIME2        NULL,
    IdCalendarItem       UNIQUEIDENTIFIER NULL,
    PagoConfirmado       BIT              NOT NULL DEFAULT (0),
    MontoPagoConfirmado  DECIMAL(18,2)    NULL,
    FechaPagoConfirmado  DATETIME2        NULL,
    PagoConfirmadoPor    UNIQUEIDENTIFIER NULL,
    CreatedBy            UNIQUEIDENTIFIER NOT NULL,
    CreatedOn            DATETIME2        NOT NULL
);

CREATE INDEX IX_Reservation_Building ON dbo.Reservation (IdBuilding, FechaInicio);
CREATE INDEX IX_Reservation_CommonArea ON dbo.Reservation (IdCommonArea, FechaInicio, FechaFin);
GO

CREATE TABLE dbo.ReservationChecklistItem
(
    IdChecklistItem UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    IdReservation   UNIQUEIDENTIFIER NOT NULL,
    Etapa           INT              NOT NULL,   -- ChecklistStage: 1=Entrega, 2=Devolucion
    Descripcion     NVARCHAR(200)    NOT NULL,
    Estado          INT              NOT NULL,   -- ChecklistStatus: 1=OK, 2=Danado, 3=Falta
    Observacion     NVARCHAR(500)    NULL,
    CreatedBy       UNIQUEIDENTIFIER NOT NULL,
    CreatedOn       DATETIME2        NOT NULL
);

CREATE INDEX IX_ReservationChecklistItem_Reservation ON dbo.ReservationChecklistItem (IdReservation, Etapa);
GO

CREATE TABLE dbo.ReservationAttachment
(
    IdAttachment  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    IdReservation UNIQUEIDENTIFIER NOT NULL,
    Etapa         INT              NOT NULL,
    FileName      NVARCHAR(260)    NOT NULL,
    ContentType   NVARCHAR(100)    NOT NULL,
    FileSizeBytes INT              NOT NULL,
    FilePath      NVARCHAR(500)    NOT NULL,
    UploadedBy    UNIQUEIDENTIFIER NOT NULL,
    UploadedOn    DATETIME2        NOT NULL
);

CREATE INDEX IX_ReservationAttachment_Reservation ON dbo.ReservationAttachment (IdReservation, Etapa);
GO

CREATE TABLE dbo.CommunityIncome
(
    IdIncome      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    IdBuilding    UNIQUEIDENTIFIER NOT NULL,
    Concepto      NVARCHAR(200)    NOT NULL,
    Monto         DECIMAL(18,2)    NOT NULL,
    IdReservation UNIQUEIDENTIFIER NULL,
    CreatedBy     UNIQUEIDENTIFIER NOT NULL,
    CreatedOn     DATETIME2        NOT NULL
);

CREATE INDEX IX_CommunityIncome_Building ON dbo.CommunityIncome (IdBuilding, CreatedOn DESC);
GO

-- ===================== Stored procedures =====================

-- ----- CommonArea -----
CREATE OR ALTER PROCEDURE dbo.INS_CommonArea
    @IdCommonArea UNIQUEIDENTIFIER, @IdBuilding UNIQUEIDENTIFIER, @Nombre NVARCHAR(150), @Descripcion NVARCHAR(500) = NULL,
    @AforoMaximo INT = NULL, @DuracionMinMinutos INT = NULL, @DuracionMaxMinutos INT = NULL, @BufferMinutos INT = 0,
    @AnticipacionMinHoras INT = 0, @AnticipacionMaxDias INT = NULL, @TopeReservationsActivasPorUnidad INT = NULL,
    @GarantiaInternos DECIMAL(18,2) = 0, @GarantiaExternos DECIMAL(18,2) = 0,
    @AlquilerInternos DECIMAL(18,2) = 0, @AlquilerExternos DECIMAL(18,2) = 0, @Limpieza DECIMAL(18,2) = 0,
    @PenalidadCancelacionHabilitada BIT = 0, @DiasMinimosSinPenalidad INT = NULL, @PenalidadNoPresentadoHabilitada BIT = 0,
    @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.CommonArea
        (IdCommonArea, IdBuilding, Nombre, Descripcion, AforoMaximo, DuracionMinMinutos, DuracionMaxMinutos, BufferMinutos,
         AnticipacionMinHoras, AnticipacionMaxDias, TopeReservationsActivasPorUnidad, GarantiaInternos, GarantiaExternos,
         AlquilerInternos, AlquilerExternos, Limpieza, PenalidadCancelacionHabilitada, DiasMinimosSinPenalidad,
         PenalidadNoPresentadoHabilitada, Activo, CreatedBy, CreatedOn)
    VALUES
        (@IdCommonArea, @IdBuilding, @Nombre, @Descripcion, @AforoMaximo, @DuracionMinMinutos, @DuracionMaxMinutos, @BufferMinutos,
         @AnticipacionMinHoras, @AnticipacionMaxDias, @TopeReservationsActivasPorUnidad, @GarantiaInternos, @GarantiaExternos,
         @AlquilerInternos, @AlquilerExternos, @Limpieza, @PenalidadCancelacionHabilitada, @DiasMinimosSinPenalidad,
         @PenalidadNoPresentadoHabilitada, 1, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_CommonArea
    @IdCommonArea UNIQUEIDENTIFIER, @Nombre NVARCHAR(150), @Descripcion NVARCHAR(500) = NULL,
    @AforoMaximo INT = NULL, @DuracionMinMinutos INT = NULL, @DuracionMaxMinutos INT = NULL, @BufferMinutos INT = 0,
    @AnticipacionMinHoras INT = 0, @AnticipacionMaxDias INT = NULL, @TopeReservationsActivasPorUnidad INT = NULL,
    @GarantiaInternos DECIMAL(18,2) = 0, @GarantiaExternos DECIMAL(18,2) = 0,
    @AlquilerInternos DECIMAL(18,2) = 0, @AlquilerExternos DECIMAL(18,2) = 0, @Limpieza DECIMAL(18,2) = 0,
    @PenalidadCancelacionHabilitada BIT = 0, @DiasMinimosSinPenalidad INT = NULL, @PenalidadNoPresentadoHabilitada BIT = 0,
    @Activo BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.CommonArea SET
        Nombre = @Nombre, Descripcion = @Descripcion, AforoMaximo = @AforoMaximo,
        DuracionMinMinutos = @DuracionMinMinutos, DuracionMaxMinutos = @DuracionMaxMinutos, BufferMinutos = @BufferMinutos,
        AnticipacionMinHoras = @AnticipacionMinHoras, AnticipacionMaxDias = @AnticipacionMaxDias,
        TopeReservationsActivasPorUnidad = @TopeReservationsActivasPorUnidad,
        GarantiaInternos = @GarantiaInternos, GarantiaExternos = @GarantiaExternos,
        AlquilerInternos = @AlquilerInternos, AlquilerExternos = @AlquilerExternos, Limpieza = @Limpieza,
        PenalidadCancelacionHabilitada = @PenalidadCancelacionHabilitada, DiasMinimosSinPenalidad = @DiasMinimosSinPenalidad,
        PenalidadNoPresentadoHabilitada = @PenalidadNoPresentadoHabilitada, Activo = @Activo
    WHERE IdCommonArea = @IdCommonArea;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_CommonAreasByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.CommonArea WHERE IdBuilding = @IdBuilding ORDER BY Nombre ASC;
END
GO

-- ----- Reservation -----
CREATE OR ALTER PROCEDURE dbo.INS_Reservation
    @IdReservation UNIQUEIDENTIFIER, @IdBuilding UNIQUEIDENTIFIER, @IdCommonArea UNIQUEIDENTIFIER, @IdGroupUnit UNIQUEIDENTIFIER,
    @FechaInicio DATETIME2, @FechaFin DATETIME2, @EsExterno BIT = 0,
    @OrganizadorNombre NVARCHAR(200) = NULL, @OrganizadorDocumento NVARCHAR(30) = NULL, @OrganizadorTelefono NVARCHAR(30) = NULL,
    @Estado INT, @MontoGarantia DECIMAL(18,2), @MontoAlquiler DECIMAL(18,2), @MontoLimpieza DECIMAL(18,2),
    @IdCalendarItem UNIQUEIDENTIFIER = NULL,
    @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Reservation
        (IdReservation, IdBuilding, IdCommonArea, IdGroupUnit, FechaInicio, FechaFin, EsExterno, OrganizadorNombre,
         OrganizadorDocumento, OrganizadorTelefono, Estado, MontoGarantia, MontoAlquiler, MontoLimpieza, IdCalendarItem, CreatedBy, CreatedOn)
    VALUES
        (@IdReservation, @IdBuilding, @IdCommonArea, @IdGroupUnit, @FechaInicio, @FechaFin, @EsExterno, @OrganizadorNombre,
         @OrganizadorDocumento, @OrganizadorTelefono, @Estado, @MontoGarantia, @MontoAlquiler, @MontoLimpieza, @IdCalendarItem, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_ReservationStatus
    @IdReservation UNIQUEIDENTIFIER, @Estado INT, @MotivoRechazo NVARCHAR(500) = NULL,
    @AprobadoPor UNIQUEIDENTIFIER = NULL, @MontoRetenido DECIMAL(18,2) = NULL,
    @IdCalendarItem UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Reservation SET
        Estado = @Estado,
        MotivoRechazo = COALESCE(@MotivoRechazo, MotivoRechazo),
        AprobadoPor = COALESCE(@AprobadoPor, AprobadoPor),
        FechaAprobacion = CASE WHEN @AprobadoPor IS NOT NULL THEN SYSUTCDATETIME() ELSE FechaAprobacion END,
        MontoRetenido = COALESCE(@MontoRetenido, MontoRetenido),
        IdCalendarItem = COALESCE(@IdCalendarItem, IdCalendarItem)
    WHERE IdReservation = @IdReservation;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_ReservationPago
    @IdReservation UNIQUEIDENTIFIER, @MontoPagoConfirmado DECIMAL(18,2) = NULL,
    @FechaPagoConfirmado DATETIME2 = NULL, @PagoConfirmadoPor UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Reservation SET
        PagoConfirmado = 1,
        MontoPagoConfirmado = @MontoPagoConfirmado,
        FechaPagoConfirmado = COALESCE(@FechaPagoConfirmado, SYSUTCDATETIME()),
        PagoConfirmadoPor = @PagoConfirmadoPor
    WHERE IdReservation = @IdReservation;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_ReservationFechas
    @IdReservation UNIQUEIDENTIFIER, @FechaInicio DATETIME2, @FechaFin DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Reservation SET
        FechaInicio = @FechaInicio,
        FechaFin = @FechaFin,
        Estado = 1, -- PendienteDeAprobacion
        AprobadoPor = NULL,
        FechaAprobacion = NULL
    WHERE IdReservation = @IdReservation;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReservationsByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, a.Nombre AS NombreCommonArea,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Reservation r
    LEFT JOIN dbo.CommonArea a ON a.IdCommonArea = r.IdCommonArea
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdBuilding = @IdBuilding
    ORDER BY r.FechaInicio DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReservationById
    @IdReservation UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, a.Nombre AS NombreCommonArea,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Reservation r
    LEFT JOIN dbo.CommonArea a ON a.IdCommonArea = r.IdCommonArea
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdReservation = @IdReservation;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReservationsPendientesByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, a.Nombre AS NombreCommonArea,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Reservation r
    LEFT JOIN dbo.CommonArea a ON a.IdCommonArea = r.IdCommonArea
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdBuilding = @IdBuilding AND r.Estado = 1 -- PendienteDeAprobacion
    ORDER BY r.FechaInicio ASC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReservationsByGroupUnit
    @IdGroupUnit UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, a.Nombre AS NombreCommonArea,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Reservation r
    LEFT JOIN dbo.CommonArea a ON a.IdCommonArea = r.IdCommonArea
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdGroupUnit = @IdGroupUnit
    ORDER BY r.FechaInicio DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReservationsConflicto
    @IdCommonArea UNIQUEIDENTIFIER, @FechaInicio DATETIME2, @FechaFin DATETIME2,
    @ExcluirIdReservation UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, a.Nombre AS NombreCommonArea,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Reservation r
    LEFT JOIN dbo.CommonArea a ON a.IdCommonArea = r.IdCommonArea
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdCommonArea = @IdCommonArea
      AND r.Estado NOT IN (3, 4, 5) -- Rechazada, Cancelada, NoPresentado
      AND r.FechaInicio < @FechaFin
      AND r.FechaFin > @FechaInicio
      AND (@ExcluirIdReservation IS NULL OR r.IdReservation <> @ExcluirIdReservation);
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReservationsProximasByCommonArea
    @IdCommonArea UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, a.Nombre AS NombreCommonArea,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Reservation r
    LEFT JOIN dbo.CommonArea a ON a.IdCommonArea = r.IdCommonArea
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdCommonArea = @IdCommonArea
      AND r.Estado IN (1, 2, 6) -- PendienteDeAprobacion, Aprobada, Entregada
      AND r.FechaFin >= SYSUTCDATETIME()
    ORDER BY r.FechaInicio ASC;
END
GO

-- ----- Checklist -----
CREATE OR ALTER PROCEDURE dbo.INS_ReservationChecklistItem
    @IdChecklistItem UNIQUEIDENTIFIER, @IdReservation UNIQUEIDENTIFIER, @Etapa INT, @Descripcion NVARCHAR(200),
    @Estado INT, @Observacion NVARCHAR(500) = NULL, @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ReservationChecklistItem (IdChecklistItem, IdReservation, Etapa, Descripcion, Estado, Observacion, CreatedBy, CreatedOn)
    VALUES (@IdChecklistItem, @IdReservation, @Etapa, @Descripcion, @Estado, @Observacion, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReservationChecklistItemsByReservation
    @IdReservation UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.ReservationChecklistItem WHERE IdReservation = @IdReservation ORDER BY Etapa ASC, CreatedOn ASC;
END
GO

-- ----- Attachments -----
CREATE OR ALTER PROCEDURE dbo.INS_ReservationAttachment
    @IdAttachment UNIQUEIDENTIFIER, @IdReservation UNIQUEIDENTIFIER, @Etapa INT, @FileName NVARCHAR(260),
    @ContentType NVARCHAR(100), @FileSizeBytes INT, @FilePath NVARCHAR(500), @UploadedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ReservationAttachment (IdAttachment, IdReservation, Etapa, FileName, ContentType, FileSizeBytes, FilePath, UploadedBy, UploadedOn)
    VALUES (@IdAttachment, @IdReservation, @Etapa, @FileName, @ContentType, @FileSizeBytes, @FilePath, @UploadedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReservationAttachmentsByReservation
    @IdReservation UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.ReservationAttachment WHERE IdReservation = @IdReservation ORDER BY Etapa ASC, UploadedOn ASC;
END
GO

-- ----- CommunityIncome -----
CREATE OR ALTER PROCEDURE dbo.INS_CommunityIncome
    @IdIncome UNIQUEIDENTIFIER, @IdBuilding UNIQUEIDENTIFIER, @Concepto NVARCHAR(200), @Monto DECIMAL(18,2),
    @IdReservation UNIQUEIDENTIFIER = NULL, @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.CommunityIncome (IdIncome, IdBuilding, Concepto, Monto, IdReservation, CreatedBy, CreatedOn)
    VALUES (@IdIncome, @IdBuilding, @Concepto, @Monto, @IdReservation, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_CommunityIncomesByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.CommunityIncome WHERE IdBuilding = @IdBuilding ORDER BY CreatedOn DESC;
END
GO

-- ===================== MenuItems =====================
-- 'reservations' (residente, "Mis Reservas"): la última UPD_MenuItem del
-- script original le dejó Url='reservas' (sin barra inicial) -- se
-- actualiza a 'reservations' preservando esa misma convención sin barra.
UPDATE dbo.MenuItems
SET Url = 'reservations'
WHERE ItemKey = 'reservations' AND Url IN ('reservas', '/reservas');

-- 'reservations_admin' ("Gestión de Reservas"): nunca se sobreescribió,
-- mantiene la barra inicial del INS_MenuItem original.
UPDATE dbo.MenuItems
SET Url = '/reservations-admin'
WHERE ItemKey = 'reservations_admin' AND Url = '/reservas-admin';
GO
