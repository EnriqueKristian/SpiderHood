-- =============================================================================
-- Bug preexistente, no introducido hoy: INS_Owner declara @IdTypeIdNumber como
-- UNIQUEIDENTIFIER, pero Owner.IdTypeIdNumber (Classes/Owner.cs) es un int -- una
-- referencia a Parameter.Value dentro del grupo "Tipo de Documento" (mismo patrón
-- que Incident.Type/RealEstateUnit.TypeUnit, ver ModalOwner.razor:
-- ViewOwner.IdTypeIdNumber se llena con @param.Value de
-- ParameterService.ListParameters, no con un Guid). BDLayout.Add.cs
-- (AddNewRecordAsync(Owner)) manda ese int tal cual -- SQL Server no permite la
-- conversión implícita int -> uniqueidentifier, así que cualquier alta de
-- propietario fallaba en el proc con un error de conversión, y OwnerService.
-- AddOwnerAsync atrapa la excepción y sólo la loguea (no llega a la UI), por eso
-- se veía como que "no pasaba nada" en vez de un error visible.
--
-- Se corrige el tipo del parámetro a INT, igual que la columna real que referencia
-- (Parameter.Value). No se toca ninguna otra columna del proc.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.INS_Owner
    @IdOwner        UNIQUEIDENTIFIER,
    @IdNumber       NVARCHAR(12),
    @Names          NVARCHAR(100),
    @Surname        NVARCHAR(50),
    @Address        NVARCHAR(50),
    @PhoneNumber    NVARCHAR(20),
    @IdTypeIdNumber INT,
    @IdBuilding     UNIQUEIDENTIFIER
AS
BEGIN
    INSERT INTO Owner (IdOwner, IdNumber, Names, Surname, Address, PhoneNumber, IdTypeIdNumber, IdBuilding)
    VALUES (@IdOwner, @IdNumber, @Names, @Surname, @Address, @PhoneNumber, @IdTypeIdNumber, @IdBuilding);
END;
GO
