-- =============================================================================
-- Cuenta de facturación (Account) + colaboradores -- ver
-- Docs/Design-Account-Facturacion.md. Complementa (no reemplaza) a
-- UserBuildingAssociation: Account es sólo quién paga y de qué "pool" de
-- edificios sale el MaxBuildings del plan; el acceso real persona-a-edificio
-- lo sigue gobernando UserBuildingAssociation.
--
-- Idempotente: se puede correr más de una vez. Backfill incluido para que las
-- cuentas/edificios/suscripciones creados antes de este feature sigan
-- funcionando (fail-open) -- ver EnsureCanCreateBuildingAsync/GetSubscriptionByUser
-- más abajo, que caen de vuelta al comportamiento viejo si un usuario todavía
-- no tiene ninguna Account.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Account')
BEGIN
    CREATE TABLE dbo.Account
    (
        IdAccount   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        RazonSocial NVARCHAR(200)    NULL,
        RucDni      NVARCHAR(20)     NULL,
        Telefono    NVARCHAR(30)     NULL,
        CreatedAt   DATETIME2        NOT NULL CONSTRAINT DF_Account_CreatedAt DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AccountUser')
BEGIN
    CREATE TABLE dbo.AccountUser
    (
        IdAccountUser UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdAccount     UNIQUEIDENTIFIER NOT NULL,
        IdUser        UNIQUEIDENTIFIER NOT NULL,
        -- Owner (quien creó la cuenta, uno solo por ahora) | Colaborador (invitado).
        Role          NVARCHAR(20)     NOT NULL CONSTRAINT DF_AccountUser_Role DEFAULT ('Owner'),
        CreatedAt     DATETIME2        NOT NULL CONSTRAINT DF_AccountUser_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_AccountUser_Account FOREIGN KEY (IdAccount) REFERENCES dbo.Account (IdAccount),
        CONSTRAINT UQ_AccountUser_Account_User UNIQUE (IdAccount, IdUser)
    );

    CREATE INDEX IX_AccountUser_IdUser ON dbo.AccountUser (IdUser);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AccountInvitation')
BEGIN
    CREATE TABLE dbo.AccountInvitation
    (
        IdAccountInvitation UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdAccount           UNIQUEIDENTIFIER NOT NULL,
        Email               NVARCHAR(256)    NOT NULL,
        Code                NVARCHAR(64)     NOT NULL,
        -- Pending | Accepted | Cancelled -- texto libre a propósito, mismo criterio
        -- que Subscription.Status (ver 2026-09-04_44_Subscription.sql).
        Status              NVARCHAR(20)     NOT NULL CONSTRAINT DF_AccountInvitation_Status DEFAULT ('Pending'),
        InvitedByIdUser     UNIQUEIDENTIFIER NOT NULL,
        CreatedAt           DATETIME2        NOT NULL CONSTRAINT DF_AccountInvitation_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_AccountInvitation_Account FOREIGN KEY (IdAccount) REFERENCES dbo.Account (IdAccount),
        CONSTRAINT UQ_AccountInvitation_Code UNIQUE (Code)
    );
END
GO

-- Nullable a propósito: los edificios creados antes de este feature quedan
-- con IdAccount = NULL (fail-open, no se retroactivan salvo por el backfill
-- de abajo, que sólo cubre edificios con un Administrador aprobado).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'IdAccount')
BEGIN
    ALTER TABLE dbo.Building ADD IdAccount UNIQUEIDENTIFIER NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Subscription') AND name = 'IdAccount')
BEGIN
    ALTER TABLE dbo.Subscription ADD IdAccount UNIQUEIDENTIFIER NULL;
END
GO

-- ---------------------------------------------------------------------------
-- Backfill: toda Subscription existente (de antes de este feature, atada
-- sólo a IdUser) recibe su propia Account nueva, con ese usuario como Owner.
-- Fila por fila (no set-based) porque cada Subscription necesita un IdAccount
-- nuevo distinto -- el volumen esperado acá es mínimo (una por usuario que ya
-- probó el flujo de Trial/pago).
-- ---------------------------------------------------------------------------
DECLARE @IdSubscription UNIQUEIDENTIFIER, @IdUserBackfill UNIQUEIDENTIFIER, @NewIdAccount UNIQUEIDENTIFIER;

DECLARE subscription_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT IdSubscription, IdUser FROM dbo.Subscription WHERE IdAccount IS NULL;

OPEN subscription_cursor;
FETCH NEXT FROM subscription_cursor INTO @IdSubscription, @IdUserBackfill;

WHILE @@FETCH_STATUS = 0
BEGIN
    -- Si el usuario ya tiene una Account de una Subscription previa procesada
    -- en esta misma corrida, reusarla en vez de crear una segunda.
    SELECT TOP 1 @NewIdAccount = IdAccount FROM dbo.AccountUser WHERE IdUser = @IdUserBackfill;

    IF @NewIdAccount IS NULL
    BEGIN
        SET @NewIdAccount = NEWID();

        INSERT INTO dbo.Account (IdAccount, RazonSocial, RucDni, Telefono, CreatedAt)
        VALUES (@NewIdAccount, NULL, NULL, NULL, SYSUTCDATETIME());

        INSERT INTO dbo.AccountUser (IdAccountUser, IdAccount, IdUser, Role, CreatedAt)
        VALUES (NEWID(), @NewIdAccount, @IdUserBackfill, 'Owner', SYSUTCDATETIME());
    END

    UPDATE dbo.Subscription SET IdAccount = @NewIdAccount WHERE IdSubscription = @IdSubscription;

    SET @NewIdAccount = NULL;
    FETCH NEXT FROM subscription_cursor INTO @IdSubscription, @IdUserBackfill;
END

CLOSE subscription_cursor;
DEALLOCATE subscription_cursor;
GO

-- Backfill de Building.IdAccount a partir del Administrador aprobado del
-- edificio (si ya tiene Account por el backfill de arriba). Edificios sin
-- ningún Administrador aprobado (p.ej. el Template del SysAdmin) quedan con
-- IdAccount NULL, que es el comportamiento fail-open esperado.
UPDATE b
SET b.IdAccount = au.IdAccount
FROM dbo.Building b
INNER JOIN dbo.UserBuildingAssociation uba
    ON uba.IdBuilding = b.IdBuilding AND uba.Role = 'Administrador' AND uba.IsApproved = 1
INNER JOIN dbo.AccountUser au ON au.IdUser = uba.IdUser
WHERE b.IdAccount IS NULL;
GO

-- ---------------------------------------------------------------------------
-- Stored procedures nuevos de Account/AccountUser/AccountInvitation
-- ---------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_Account
    @IdAccount UNIQUEIDENTIFIER,
    @RazonSocial NVARCHAR(200) = NULL,
    @RucDni NVARCHAR(20) = NULL,
    @Telefono NVARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Account (IdAccount, RazonSocial, RucDni, Telefono, CreatedAt)
    VALUES (@IdAccount, @RazonSocial, @RucDni, @Telefono, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_AccountUser
    @IdAccountUser UNIQUEIDENTIFIER,
    @IdAccount UNIQUEIDENTIFIER,
    @IdUser UNIQUEIDENTIFIER,
    @Role NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AccountUser (IdAccountUser, IdAccount, IdUser, Role, CreatedAt)
    VALUES (@IdAccountUser, @IdAccount, @IdUser, @Role, SYSUTCDATETIME());
END
GO

-- La Account a la que pertenece un usuario -- Owner o Colaborador, da igual
-- (hoy un usuario pertenece a lo sumo una Account; si en el futuro pudiera
-- pertenecer a varias, esto sólo trae la primera).
CREATE OR ALTER PROCEDURE dbo.GET_AccountByUser
    @IdUser UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        a.IdAccount,
        a.RazonSocial,
        a.RucDni,
        a.Telefono,
        a.CreatedAt
    FROM dbo.Account a
    INNER JOIN dbo.AccountUser au ON au.IdAccount = a.IdAccount
    WHERE au.IdUser = @IdUser
    ORDER BY au.CreatedAt ASC;
END
GO

-- Todas las personas asociadas a una Account (Owner + Colaboradores), con
-- datos básicos del usuario para mostrar en Settings.razor.
CREATE OR ALTER PROCEDURE dbo.GET_AccountUsersByAccount
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        au.IdAccountUser,
        au.IdAccount,
        au.IdUser,
        au.Role,
        au.CreatedAt,
        u.FirstName,
        u.LastName,
        u.Email
    FROM dbo.AccountUser au
    INNER JOIN dbo.Users u ON u.IdUser = au.IdUser
    WHERE au.IdAccount = @IdAccount
    ORDER BY au.CreatedAt ASC;
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_AccountInvitation
    @IdAccountInvitation UNIQUEIDENTIFIER,
    @IdAccount UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Code NVARCHAR(64),
    @InvitedByIdUser UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AccountInvitation (IdAccountInvitation, IdAccount, Email, Code, Status, InvitedByIdUser, CreatedAt)
    VALUES (@IdAccountInvitation, @IdAccount, @Email, @Code, 'Pending', @InvitedByIdUser, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AccountInvitationByCode
    @Code NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        ai.IdAccountInvitation,
        ai.IdAccount,
        ai.Email,
        ai.Code,
        ai.Status,
        ai.InvitedByIdUser,
        ai.CreatedAt,
        a.RazonSocial
    FROM dbo.AccountInvitation ai
    INNER JOIN dbo.Account a ON a.IdAccount = ai.IdAccount
    WHERE ai.Code = @Code;
END
GO

-- Incluye a.RazonSocial (aunque acá no hace falta para la UI) porque EF exige
-- que TODA columna mapeada en Models.AccountInvitation esté en el resultado de
-- CUALQUIER proc que se lea con FromSqlRaw<AccountInvitation> -- mismo motivo
-- por el que 2026-09-02_20 tuvo que arreglar IsTemplate en su momento.
CREATE OR ALTER PROCEDURE dbo.GET_PendingInvitationsByAccount
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ai.IdAccountInvitation, ai.IdAccount, ai.Email, ai.Code, ai.Status, ai.InvitedByIdUser, ai.CreatedAt, a.RazonSocial
    FROM dbo.AccountInvitation ai
    INNER JOIN dbo.Account a ON a.IdAccount = ai.IdAccount
    WHERE ai.IdAccount = @IdAccount AND ai.Status = 'Pending'
    ORDER BY ai.CreatedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_AccountInvitationStatus
    @IdAccountInvitation UNIQUEIDENTIFIER,
    @Status NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.AccountInvitation SET Status = @Status WHERE IdAccountInvitation = @IdAccountInvitation;
END
GO

-- ---------------------------------------------------------------------------
-- Building: agrega @IdAccount a INS/UPD, e IdAccount a todos los procs que se
-- leen con FromSqlRaw<Building> (EF exige que TODA columna mapeada esté en el
-- SELECT de CUALQUIER proc que se use para hidratar ese tipo -- mismo motivo
-- por el que 2026-09-02_20 tuvo que arreglar IsTemplate en su momento).
-- ---------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_Building
    @IdBuilding UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Location NVARCHAR(300),
    @Type INT,
    @Floors INT,
    @Basements INT,
    @Apartments INT,
    @Parkings INT,
    @Deposits INT,
    @Others INT,
    @TotalArea DECIMAL(18, 2),
    @IsActive BIT,
    @IsTemplate BIT,
    @IdAccount UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Building
        (IdBuilding, Name, Location, Type, Floors, Basements, Apartments, Parkings, Deposits, Others, TotalArea, IsActive, IsTemplate, IdAccount)
    VALUES
        (@IdBuilding, @Name, @Location, @Type, @Floors, @Basements, @Apartments, @Parkings, @Deposits, @Others, @TotalArea, @IsActive, @IsTemplate, @IdAccount);
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Building
    @IdBuilding UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Location NVARCHAR(300),
    @Type INT,
    @Floors INT,
    @Basements INT,
    @Apartments INT,
    @Parkings INT,
    @Deposits INT,
    @Others INT,
    @TotalArea DECIMAL(18, 2),
    @IsActive BIT,
    @IsTemplate BIT,
    @IdAccount UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Building
    SET Name = @Name,
        Location = @Location,
        Type = @Type,
        Floors = @Floors,
        Basements = @Basements,
        Apartments = @Apartments,
        Parkings = @Parkings,
        Deposits = @Deposits,
        Others = @Others,
        TotalArea = @TotalArea,
        IsActive = @IsActive,
        IsTemplate = @IsTemplate,
        IdAccount = COALESCE(@IdAccount, IdAccount)
    WHERE IdBuilding = @IdBuilding;
END
GO

-- Texto real (sp_helptext) de antes de este script, +IdAccount al SELECT --
-- mismo criterio que 2026-09-02_20 usó para IsTemplate: JOIN/WHERE sin tocar.
CREATE OR ALTER PROCEDURE dbo.GET_AllBuildings
    @IdUser UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  b.IdBuilding,
            b.[Name],
            b.[Location],
            b.TotalArea,
            b.Number,
            b.[Type],
            b.Floors,
            b.Basements,
            b.Apartments,
            b.Parkings,
            b.Deposits,
            b.Others,
            b.IsActive,
            b.IsTemplate,
            b.IdAccount
    FROM    Building b
    JOIN    UserBuildingAssociation ub ON b.IdBuilding = ub.IdBuilding
    WHERE   ub.IdUser = @IdUser
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AllBuildingsPublic
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        IdBuilding,
        Name,
        Location,
        Number,
        Type,
        Floors,
        Basements,
        Apartments,
        Parkings,
        Deposits,
        Others,
        TotalArea,
        IsActive,
        IsTemplate,
        IdAccount
    FROM dbo.Building
    WHERE IsActive = 1
    ORDER BY Name;
END
GO

-- GET_TemplateBuilding no se toca: usa "SELECT TOP 1 *" (ver
-- 2026-09-02_19_Building_IsTemplate.sql), así que IdAccount ya viaja solo.

-- Edificios de una Account -- lo usa AccountService al aceptar una invitación
-- de colaborador, para replicarle UserBuildingAssociation a cada uno (así ve
-- los mismos edificios que el resto de la cuenta, ver
-- Docs/Design-Account-Facturacion.md, decisión 3).
CREATE OR ALTER PROCEDURE dbo.GET_BuildingsByAccount
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdBuilding, Number, Name, Location, Type, Floors, Basements,
           Apartments, Parkings, Deposits, Others, TotalArea, IsActive, IsTemplate, IdAccount
    FROM dbo.Building
    WHERE IdAccount = @IdAccount;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_BuildingById
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  IdBuilding,
            Name,
            Location,
            TotalArea,
            Number,
            Type,
            Floors,
            Basements,
            Apartments,
            Parkings,
            Deposits,
            Others,
            IsActive,
            IsTemplate,
            IdAccount
    FROM    Building
    WHERE   IdBuilding = @IdBuilding
END
GO

-- ---------------------------------------------------------------------------
-- Subscription: pasa a resolver por Account puertas adentro. Fail-open: si el
-- usuario todavía no tiene ninguna Account (no debería pasar para cuentas
-- nuevas, que la crean en el registro), cae al comportamiento viejo por
-- IdUser directo, para no romper nada.
-- ---------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.GET_SubscriptionByUser
    @IdUser UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdAccount UNIQUEIDENTIFIER = (SELECT TOP 1 IdAccount FROM dbo.AccountUser WHERE IdUser = @IdUser);

    IF @IdAccount IS NOT NULL
    BEGIN
        SELECT TOP 1
            s.IdSubscription, s.IdUser, s.IdAccount, s.IdSubscriptionPlan,
            p.Name AS PlanName, p.MaxBuildings, s.Status, s.StartDate, s.EndDate, s.MercadoPagoPreapprovalId
        FROM dbo.Subscription s
        INNER JOIN dbo.SubscriptionPlan p ON p.IdSubscriptionPlan = s.IdSubscriptionPlan
        WHERE s.IdAccount = @IdAccount
        ORDER BY s.CreatedAt DESC;
        RETURN;
    END

    SELECT TOP 1
        s.IdSubscription, s.IdUser, s.IdAccount, s.IdSubscriptionPlan,
        p.Name AS PlanName, p.MaxBuildings, s.Status, s.StartDate, s.EndDate, s.MercadoPagoPreapprovalId
    FROM dbo.Subscription s
    INNER JOIN dbo.SubscriptionPlan p ON p.IdSubscriptionPlan = s.IdSubscriptionPlan
    WHERE s.IdUser = @IdUser
    ORDER BY s.CreatedAt DESC;
END
GO

-- @IdAccount ahora obligatorio -- se llama únicamente desde
-- CreateTrialSubscriptionAsync, justo después de crear la Account del
-- registro (ver AuthService.RegisterNewAdministratorAsync).
CREATE OR ALTER PROCEDURE dbo.INS_Subscription
    @IdSubscription UNIQUEIDENTIFIER,
    @IdUser UNIQUEIDENTIFIER,
    @IdAccount UNIQUEIDENTIFIER,
    @IdSubscriptionPlan INT,
    @Status NVARCHAR(20),
    @StartDate DATETIME2,
    @EndDate DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Subscription (IdSubscription, IdUser, IdAccount, IdSubscriptionPlan, Status, StartDate, EndDate, CreatedAt)
    VALUES (@IdSubscription, @IdUser, @IdAccount, @IdSubscriptionPlan, @Status, @StartDate, @EndDate, SYSUTCDATETIME());
END
GO

-- Llamado únicamente desde el webhook de MercadoPago. Resuelve la Account del
-- usuario (igual que GET_SubscriptionByUser) y pisa/crea sobre esa Account en
-- vez de por IdUser directo, para que un colaborador que paga la suscripción
-- actualice la cuenta compartida, no una propia.
CREATE OR ALTER PROCEDURE dbo.UPD_ActivateSubscription
    @IdUser UNIQUEIDENTIFIER,
    @IdSubscriptionPlan INT,
    @MercadoPagoPreapprovalId NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdAccount UNIQUEIDENTIFIER = (SELECT TOP 1 IdAccount FROM dbo.AccountUser WHERE IdUser = @IdUser);

    DECLARE @IdSubscription UNIQUEIDENTIFIER = (
        SELECT TOP 1 IdSubscription
        FROM dbo.Subscription
        WHERE (@IdAccount IS NOT NULL AND IdAccount = @IdAccount) OR (@IdAccount IS NULL AND IdUser = @IdUser)
        ORDER BY CreatedAt DESC
    );

    IF @IdSubscription IS NOT NULL
    BEGIN
        UPDATE dbo.Subscription
        SET IdSubscriptionPlan = @IdSubscriptionPlan,
            Status = 'Active',
            StartDate = SYSUTCDATETIME(),
            EndDate = NULL,
            MercadoPagoPreapprovalId = @MercadoPagoPreapprovalId
        WHERE IdSubscription = @IdSubscription;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.Subscription
            (IdSubscription, IdUser, IdAccount, IdSubscriptionPlan, Status, StartDate, EndDate, CreatedAt, MercadoPagoPreapprovalId)
        VALUES
            (NEWID(), @IdUser, @IdAccount, @IdSubscriptionPlan, 'Active', SYSUTCDATETIME(), NULL, SYSUTCDATETIME(), @MercadoPagoPreapprovalId);
    END
END
GO
