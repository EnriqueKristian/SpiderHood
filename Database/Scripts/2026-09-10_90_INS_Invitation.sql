-- =============================================================================
-- Nueva SP INS_Invitation, para el botón "Invitar" agregado en
-- /Settings/UserRoles (invitar a un Residente/Junta por email a un edificio,
-- con su unidad ya asignada de una vez).
--
-- dbo.Invitation ya existía (confirmado con INFORMATION_SCHEMA.COLUMNS) --
-- sólo la lectura (GET_InvitationByCode) y el registro con invitación
-- (RegisterWithInvitationAsync) estaban armados; no había ninguna SP ni
-- pantalla para CREAR una fila nueva, así que ese flujo era inalcanzable en
-- la práctica (con la app real, corriendo). Columnas confirmadas por el
-- usuario: IdInvitation, Code, Email, IdBuilding, BuildingName, InvitedBy,
-- Role, ApartmentNumber (int, NOT NULL), RequiresApproval, AdminMessage,
-- ExpirationDate, Status, CreatedAt, Location.
--
-- ApartmentNumber es un INT suelto (no un FK a la unidad real,
-- IdGroupUnit) -- por eso la UI nueva lo arma a partir de OwnerUnitView.GroupNumber
-- (el mismo número que ya se muestra como "Grupo N" en /Settings/UserRoles y
-- en la solicitud de acceso), y AuthService.AcceptInvitationAsync ahora
-- resuelve ese número de vuelta al IdGroupUnit real al aceptar la invitación
-- (antes ese dato se guardaba en la invitación pero nunca se usaba).
-- =============================================================================

CREATE OR ALTER PROCEDURE dbo.INS_Invitation
    @IdInvitation UNIQUEIDENTIFIER,
    @Code NVARCHAR(50),
    @Email NVARCHAR(200),
    @IdBuilding UNIQUEIDENTIFIER,
    @BuildingName NVARCHAR(200),
    @InvitedBy NVARCHAR(200),
    @Role NVARCHAR(100),
    @ApartmentNumber INT,
    @RequiresApproval BIT,
    @AdminMessage NVARCHAR(255) = NULL,
    @ExpirationDate DATETIME,
    @Location NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Invitation
        (IdInvitation, Code, Email, IdBuilding, BuildingName, InvitedBy, Role,
         ApartmentNumber, RequiresApproval, AdminMessage, ExpirationDate, Status,
         CreatedAt, Location)
    VALUES
        (@IdInvitation, @Code, @Email, @IdBuilding, @BuildingName, @InvitedBy, @Role,
         @ApartmentNumber, @RequiresApproval, @AdminMessage, @ExpirationDate, 'Pending',
         SYSDATETIME(), @Location);
END
