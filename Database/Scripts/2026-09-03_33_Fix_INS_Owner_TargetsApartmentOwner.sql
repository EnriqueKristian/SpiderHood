-- =============================================================================
-- Reemplaza al script 32 (2026-09-03_32_Fix_INS_Owner_IdTypeIdNumber.sql): ese
-- diagnóstico asumía que la tabla se llamaba "Owner" (como en el
-- CREATE PROCEDURE original que pasó Enrique) y que el único problema era el tipo
-- de @IdTypeIdNumber. Enrique aclaró que esa tabla ya no existe -- la tabla real
-- es dbo.ApartmentOwner, con columnas distintas:
--   IdOwner, IdentityDocument, Address, PhoneNumber, IdBuilding,
--   FirstName, LastName, Email, IsActive, CreatedBy, CreatedOn, ModifiedBy, ModifiedOn
-- (ni IdNumber, ni Names/Surname, ni IdTypeIdNumber existen en la tabla real -- de
-- ahí que INS_Owner fallara: no era una conversión de tipo, era una tabla/columnas
-- que ya no existen). Si alguien llegó a correr el script 32, no rompió nada --
-- CREATE OR ALTER sobre un INSERT INTO Owner inexistente hubiera fallado recién al
-- ejecutar el proc, no al crearlo.
--
-- Mapeo real (Classes/Owner.cs -> ApartmentOwner), mismo criterio que ya usa el
-- resto del proc (Names/Surname del formulario son en la práctica "nombre" y
-- "apellido" -- ver ModalOwner.razor, InputText id="names"/"surnamem"):
--   Owner.IdNumber  -> ApartmentOwner.IdentityDocument
--   Owner.Names     -> ApartmentOwner.FirstName
--   Owner.Surname   -> ApartmentOwner.LastName
--   Owner.IdTypeIdNumber ya no tiene columna a dónde ir -- se deja de mandar (ver
--   también el cambio en BDLayout.Add.cs). El combo "Tipo Doc" de ModalOwner.razor
--   queda huérfano -- pendiente de que Enrique confirme si se saca del formulario o
--   si hace falta agregar la columna de vuelta.
--   Email no lo captura el formulario todavía -- queda NULL.
--   IsActive nace en 1 (mismo criterio que el fix de INS_User, script 31): un
--   propietario nuevo no tiene por qué nacer inactivo.
--   CreatedBy/CreatedOn no se mandan desde el proc -- OwnerService.AddOwnerAsync ya
--   llama a ec.StampAuditAsync(AuditableEntity.Owner, ...) aparte, mismo patrón que
--   el resto de los Add*Async (ver BuildingService.CreateBuildingAsync). Se deja
--   CreatedOn con el DEFAULT que tenga la tabla (si no tiene, queda NULL -- no es
--   peor que hoy).
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.INS_Owner
    @IdOwner        UNIQUEIDENTIFIER,
    @IdNumber       NVARCHAR(20),
    @Names          NVARCHAR(100),
    @Surname        NVARCHAR(100),
    @Address        NVARCHAR(80),
    @PhoneNumber    NVARCHAR(20),
    @IdBuilding     UNIQUEIDENTIFIER
AS
BEGIN
    INSERT INTO ApartmentOwner (
        IdOwner, IdentityDocument, FirstName, LastName, Address, PhoneNumber,
        IdBuilding, IsActive
    )
    VALUES (
        @IdOwner, @IdNumber, @Names, @Surname, @Address, @PhoneNumber,
        @IdBuilding, 1
    );
END;
GO

-- UPD_Owner tiene el mismo problema: BDLayout.Update.cs (UpdateRecordAsync(Owner))
-- también manda owner.IdTypeIdNumber, y el proc actual seguramente sigue apuntando
-- a la tabla vieja Owner.
CREATE OR ALTER PROCEDURE dbo.UPD_Owner
    @IdOwner        UNIQUEIDENTIFIER,
    @IdNumber       NVARCHAR(20),
    @Names          NVARCHAR(100),
    @Surname        NVARCHAR(100),
    @Address        NVARCHAR(80),
    @PhoneNumber    NVARCHAR(20)
AS
BEGIN
    UPDATE ApartmentOwner
    SET IdentityDocument = @IdNumber,
        FirstName = @Names,
        LastName = @Surname,
        Address = @Address,
        PhoneNumber = @PhoneNumber
    WHERE IdOwner = @IdOwner;
END;
GO
