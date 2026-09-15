-- =============================================================================
-- Fix: INS_UserBuildingAssociation nunca insertaba la columna Status -- se
-- queda siempre en el default de la tabla ('Pending'), sin importar el valor
-- de @IsApproved que reciba. Cualquier fila creada por este SP (aceptar una
-- invitación, crear tu propio edificio como Administrador -- ver
-- IBuildingService.CreateBuildingAsync, que pasa IsApproved=true) terminaba
-- con Status='Pending' de todos modos.
--
-- /Settings/Users lee Status (no IsApproved) para decidir Activo/Inactivo
-- para un Administrador (ver Users.razor, línea ~443: "filasEstado.Any(f =>
-- f.Status == 'Approved')") -- así que un Administrador que acababa de crear
-- su propio edificio se veía a sí mismo como "Inactivo" en la lista de
-- usuarios, con acceso funcional normal.
--
-- Se deriva Status de @IsApproved al insertar: Approved/Pending según
-- corresponda. El único caller que pasa IsApproved=false a propósito
-- (AuthService.CreatePendingAssociationAsync, solicitud de acceso de un
-- Residente) sigue quedando en Pending, como debe ser.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[INS_UserBuildingAssociation]
    @IdUser UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @Role NVARCHAR(100),
    @IsApproved BIT,
    @RequestedAt DATETIME = NULL,
    @ApprovedAt DATETIME = NULL,
    @ApprovedBy UNIQUEIDENTIFIER = NULL
AS
BEGIN
    DECLARE @IdRole UNIQUEIDENTIFIER;

    SELECT @IdRole = IdRole
    FROM Roles
    WHERE RoleName = @Role;

    IF @IdRole IS NULL
    BEGIN
        RAISERROR('No existe un rol llamado ''%s'' en la tabla Roles.', 16, 1, @Role);
        RETURN;
    END

    INSERT INTO UserBuildingAssociation (
        IdUser, IdBuilding, Role, IsApproved, RequestedAt, ApprovedAt, ApprovedBy, IdRole, Status
    )
    VALUES (
        @IdUser, @IdBuilding, @Role, @IsApproved, @RequestedAt, @ApprovedAt, @ApprovedBy, @IdRole,
        CASE WHEN @IsApproved = 1 THEN 'Approved' ELSE 'Pending' END
    );
END;
GO

-- Backfill: filas ya creadas con este mismo defecto (IsApproved=1 pero
-- Status todavía en 'Pending' porque nunca se insertó explícitamente).
UPDATE UserBuildingAssociation
SET Status = 'Approved'
WHERE IsApproved = 1 AND Status = 'Pending';
GO
