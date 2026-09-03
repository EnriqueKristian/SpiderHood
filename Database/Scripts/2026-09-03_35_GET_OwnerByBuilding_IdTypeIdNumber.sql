-- =============================================================================
-- Completa el script 34: GET_OwnerByBuilding lee de la vista VW_OwnerUnit, así que
-- agregar la columna acá no alcanza si la vista no la trae también -- ver mensaje
-- de la sesión, falta el ALTER VIEW correspondiente (pendiente del texto real de
-- VW_OwnerUnit).
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[GET_OwnerByBuilding]
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  IdGroupUnit, TotalArea, GroupNumber, IdUnit, UnitNumber, Area, TypeUnit, Number, IsAvailable, IdGroupOwnerRol, [Role], IdOwner, IdentityDocument, [Address], PhoneNumber, FirstName, LastName, Email, IsActive, IdBuilding, IdTypeIdNumber
    FROM    VW_OwnerUnit
    WHERE   IdBuilding = @IdBuilding
    ORDER BY TypeUnit, [role], GroupNumber
END
GO
