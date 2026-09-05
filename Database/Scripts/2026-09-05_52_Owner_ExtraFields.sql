-- =============================================================================
-- Mejoras a Propietario (dbo.ApartmentOwner, clase C# Models.Owner).
--
-- Confirmado contra sys.columns (no adivinado): FirstName, LastName, Email e
-- IsActive YA EXISTÍAN como columnas reales -- OwnerUnitView (la vista de sólo
-- lectura de Owners.razor) ya las traía -- pero INS_Owner/UPD_Owner nunca las
-- persistían, así que el formulario de Alta/Edición (ModalOwner.razor) no
-- podía cargarlas. Se agregan acá.
--
-- (Names/Surname del modelo C# YA se guardaban en FirstName/LastName --
-- son sólo nombres de parámetro distintos a los de columna, no un campo
-- faltante.)
--
-- Campos genuinamente nuevos, todos NULL-able (fail-open):
--   - Contacto: MobilePhone, WorkPhone, RelationshipType (texto libre --
--     Propietario/Co-Propietario/Inquilino/Usufructuario, todavía no hay un
--     catálogo de Parameter para esto).
--   - Sólo Persona Jurídica: BusinessName, LegalRepresentative, RucType --
--     adicionales, no reemplazan que "Razón Social" hoy reutilice Names.
--   - Legales: Nationality, CivilStatus, BirthDate.
--   - IsDelinquent (BIT, default 0): la columna queda lista, pero
--     deliberadamente NO se expone en el formulario de Alta/Edición -- debería
--     calcularse de las cuotas vencidas del propietario (Installment), no
--     tipearse a mano (mismo criterio que CurrentBalance en BankAccount).
--
-- No se toca GET_OwnerByBuilding (devuelve OwnerUnitView, no Owner -- y ya
-- trae Email/IsActive sin cambios) -- los campos genuinamente nuevos no se
-- agregan a OwnerUnitView en este script, sólo al formulario de Alta/Edición;
-- mostrarlos en la grilla de Owners.razor queda para más adelante.
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ApartmentOwner') AND name = 'MobilePhone')
    ALTER TABLE dbo.ApartmentOwner ADD MobilePhone NVARCHAR(20) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ApartmentOwner') AND name = 'WorkPhone')
    ALTER TABLE dbo.ApartmentOwner ADD WorkPhone NVARCHAR(20) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ApartmentOwner') AND name = 'RelationshipType')
    ALTER TABLE dbo.ApartmentOwner ADD RelationshipType NVARCHAR(30) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ApartmentOwner') AND name = 'BusinessName')
    ALTER TABLE dbo.ApartmentOwner ADD BusinessName NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ApartmentOwner') AND name = 'LegalRepresentative')
    ALTER TABLE dbo.ApartmentOwner ADD LegalRepresentative NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ApartmentOwner') AND name = 'RucType')
    ALTER TABLE dbo.ApartmentOwner ADD RucType NVARCHAR(10) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ApartmentOwner') AND name = 'Nationality')
    ALTER TABLE dbo.ApartmentOwner ADD Nationality NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ApartmentOwner') AND name = 'CivilStatus')
    ALTER TABLE dbo.ApartmentOwner ADD CivilStatus NVARCHAR(20) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ApartmentOwner') AND name = 'BirthDate')
    ALTER TABLE dbo.ApartmentOwner ADD BirthDate DATE NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ApartmentOwner') AND name = 'IsDelinquent')
    ALTER TABLE dbo.ApartmentOwner ADD IsDelinquent BIT NOT NULL CONSTRAINT DF_ApartmentOwner_IsDelinquent DEFAULT (0);
GO

-- INS_Owner / UPD_Owner: se reescriben completos (mismo criterio que
-- INS_Building/INS_Unit en scripts anteriores) -- son procs de INSERT/UPDATE
-- directo sobre una sola tabla, sin JOINs, así que reconstruirlos desde las
-- columnas confirmadas por sys.columns es seguro.
CREATE OR ALTER PROCEDURE dbo.INS_Owner
    @IdOwner              UNIQUEIDENTIFIER,
    @IdNumber             NVARCHAR(20),
    @Names                NVARCHAR(100),
    @Surname              NVARCHAR(100) = NULL,
    @Address              NVARCHAR(80),
    @PhoneNumber          NVARCHAR(20),
    @IdBuilding           UNIQUEIDENTIFIER,
    @IdTypeIdNumber       INT = NULL,
    @Email                NVARCHAR(100) = NULL,
    @IsActive             BIT = 1,
    @MobilePhone          NVARCHAR(20) = NULL,
    @WorkPhone            NVARCHAR(20) = NULL,
    @RelationshipType     NVARCHAR(30) = NULL,
    @BusinessName         NVARCHAR(200) = NULL,
    @LegalRepresentative  NVARCHAR(200) = NULL,
    @RucType              NVARCHAR(10) = NULL,
    @Nationality          NVARCHAR(50) = NULL,
    @CivilStatus          NVARCHAR(20) = NULL,
    @BirthDate            DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ApartmentOwner
        (IdOwner, IdentityDocument, FirstName, LastName, Address, PhoneNumber, IdBuilding, IdTypeIdNumber,
         Email, IsActive, MobilePhone, WorkPhone, RelationshipType,
         BusinessName, LegalRepresentative, RucType, Nationality, CivilStatus, BirthDate)
    VALUES
        (@IdOwner, @IdNumber, @Names, @Surname, @Address, @PhoneNumber, @IdBuilding, @IdTypeIdNumber,
         @Email, @IsActive, @MobilePhone, @WorkPhone, @RelationshipType,
         @BusinessName, @LegalRepresentative, @RucType, @Nationality, @CivilStatus, @BirthDate);
END;
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Owner
    @IdOwner              UNIQUEIDENTIFIER,
    @IdNumber             NVARCHAR(20),
    @Names                NVARCHAR(100),
    @Surname              NVARCHAR(100) = NULL,
    @Address              NVARCHAR(80),
    @PhoneNumber          NVARCHAR(20),
    @IdTypeIdNumber       INT = NULL,
    @Email                NVARCHAR(100) = NULL,
    @IsActive             BIT = NULL,
    @MobilePhone          NVARCHAR(20) = NULL,
    @WorkPhone            NVARCHAR(20) = NULL,
    @RelationshipType     NVARCHAR(30) = NULL,
    @BusinessName         NVARCHAR(200) = NULL,
    @LegalRepresentative  NVARCHAR(200) = NULL,
    @RucType              NVARCHAR(10) = NULL,
    @Nationality          NVARCHAR(50) = NULL,
    @CivilStatus          NVARCHAR(20) = NULL,
    @BirthDate            DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ApartmentOwner
    SET IdentityDocument = @IdNumber,
        FirstName = @Names,
        LastName = @Surname,
        Address = @Address,
        PhoneNumber = @PhoneNumber,
        IdTypeIdNumber = @IdTypeIdNumber,
        Email = @Email,
        IsActive = COALESCE(@IsActive, IsActive),
        MobilePhone = @MobilePhone,
        WorkPhone = @WorkPhone,
        RelationshipType = @RelationshipType,
        BusinessName = @BusinessName,
        LegalRepresentative = @LegalRepresentative,
        RucType = @RucType,
        Nationality = @Nationality,
        CivilStatus = @CivilStatus,
        BirthDate = @BirthDate
    WHERE IdOwner = @IdOwner;
END;
GO
