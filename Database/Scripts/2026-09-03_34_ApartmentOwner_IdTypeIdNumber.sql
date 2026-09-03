-- =============================================================================
-- Enrique confirmó que el Tipo de Documento SÍ debe guardarse y referenciarse --
-- viene de Parameter (mismo patrón que Incident.Type/RealEstateUnit.TypeUnit: un
-- int que apunta a Parameter.Value dentro del grupo "Tipo de Documento", ver
-- ModalOwner.razor -- InputSelect id="typedoc" recorre
-- ParameterService.ListParameters.Where(c => c.IdParent ==
-- Convert.ToInt32(Models.ParamParent.DocumentType))). El script 33 lo había sacado
-- del INSERT/UPDATE porque ApartmentOwner no tenía la columna -- este script la
-- agrega de vuelta y reinstala el parámetro en INS_Owner/UPD_Owner.
--
-- NULL (no NOT NULL): las filas existentes en ApartmentOwner no tienen ningún
-- valor para esta columna todavía -- si se pusiera NOT NULL, el ALTER TABLE
-- fallaría (o forzaría un default arbitrario tipo 0, que no corresponde a ningún
-- Parameter real). Los propietarios nuevos sí lo van a mandar siempre porque el
-- combo del formulario es obligatorio.
--
-- Pendiente aparte (no en este script): GET_OwnerByBuilding -- la SP que lee
-- ApartmentOwner para la grilla de /Owners -- también necesita traer esta columna
-- para que se pueda mostrar/editar el tipo de documento ya guardado. No tengo el
-- texto de esa SP (no está versionada en este repo, a diferencia de INS_Owner que
-- Enrique pasó a mano) -- hace falta pasarla para agregar la columna al SELECT sin
-- arriesgarse a romper joins que no se ven desde acá.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.ApartmentOwner') AND name = 'IdTypeIdNumber'
)
BEGIN
    ALTER TABLE dbo.ApartmentOwner ADD IdTypeIdNumber INT NULL;
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_Owner
    @IdOwner        UNIQUEIDENTIFIER,
    @IdNumber       NVARCHAR(20),
    @Names          NVARCHAR(100),
    @Surname        NVARCHAR(100),
    @Address        NVARCHAR(80),
    @PhoneNumber    NVARCHAR(20),
    @IdBuilding     UNIQUEIDENTIFIER,
    @IdTypeIdNumber INT = NULL
AS
BEGIN
    INSERT INTO ApartmentOwner (
        IdOwner, IdentityDocument, FirstName, LastName, Address, PhoneNumber,
        IdBuilding, IsActive, IdTypeIdNumber
    )
    VALUES (
        @IdOwner, @IdNumber, @Names, @Surname, @Address, @PhoneNumber,
        @IdBuilding, 1, @IdTypeIdNumber
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Owner
    @IdOwner        UNIQUEIDENTIFIER,
    @IdNumber       NVARCHAR(20),
    @Names          NVARCHAR(100),
    @Surname        NVARCHAR(100),
    @Address        NVARCHAR(80),
    @PhoneNumber    NVARCHAR(20),
    @IdTypeIdNumber INT = NULL
AS
BEGIN
    UPDATE ApartmentOwner
    SET IdentityDocument = @IdNumber,
        FirstName = @Names,
        LastName = @Surname,
        Address = @Address,
        PhoneNumber = @PhoneNumber,
        IdTypeIdNumber = @IdTypeIdNumber
    WHERE IdOwner = @IdOwner;
END;
GO
