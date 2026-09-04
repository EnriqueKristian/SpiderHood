-- =============================================================================
-- Suscripcion SaaS del Administrador (Docs/Design-Subscripcion-Administrador.md).
--
-- No confundir con las cuotas/expensas que un Residente le paga al edificio
-- (Installment, ya existente) -- esto es lo que un Administrador le paga a
-- SpiderHood por usar el sistema. Se ata al usuario (IdUser), no al Building:
-- el Plan define cuantos edificios puede administrar esa cuenta.
--
-- Tablas y SPs 100% nuevos -- no toca nada existente. Idempotente: se puede
-- correr mas de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SubscriptionPlan')
BEGIN
    CREATE TABLE dbo.SubscriptionPlan
    (
        IdSubscriptionPlan INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name               NVARCHAR(50)      NOT NULL,
        -- Cantidad maxima de edificios que puede administrar una cuenta en
        -- este plan. NULL = sin limite (Trial y Empresarial); Basico = 1.
        MaxBuildings       INT               NULL,
        IsActive           BIT               NOT NULL CONSTRAINT DF_SubscriptionPlan_IsActive DEFAULT (1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlan WHERE Name = 'Trial')
BEGIN
    INSERT INTO dbo.SubscriptionPlan (Name, MaxBuildings, IsActive) VALUES ('Trial', NULL, 1);
END
IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlan WHERE Name = 'Basico')
BEGIN
    INSERT INTO dbo.SubscriptionPlan (Name, MaxBuildings, IsActive) VALUES ('Basico', 1, 1);
END
IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlan WHERE Name = 'Empresarial')
BEGIN
    INSERT INTO dbo.SubscriptionPlan (Name, MaxBuildings, IsActive) VALUES ('Empresarial', NULL, 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Subscription')
BEGIN
    CREATE TABLE dbo.Subscription
    (
        IdSubscription     UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdUser             UNIQUEIDENTIFIER NOT NULL,
        IdSubscriptionPlan INT              NOT NULL,
        -- Trial / Active / Expired / Cancelled -- texto libre a proposito
        -- (todavia no hay maquina de estados real, ver documento de diseno).
        Status             NVARCHAR(20)     NOT NULL CONSTRAINT DF_Subscription_Status DEFAULT ('Trial'),
        StartDate          DATETIME2        NOT NULL,
        EndDate            DATETIME2        NULL,
        CreatedAt          DATETIME2        NOT NULL CONSTRAINT DF_Subscription_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Subscription_SubscriptionPlan FOREIGN KEY (IdSubscriptionPlan)
            REFERENCES dbo.SubscriptionPlan (IdSubscriptionPlan)
    );

    CREATE INDEX IX_Subscription_IdUser ON dbo.Subscription (IdUser);
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_Subscription
    @IdSubscription UNIQUEIDENTIFIER,
    @IdUser UNIQUEIDENTIFIER,
    @IdSubscriptionPlan INT,
    @Status NVARCHAR(20),
    @StartDate DATETIME2,
    @EndDate DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Subscription (IdSubscription, IdUser, IdSubscriptionPlan, Status, StartDate, EndDate, CreatedAt)
    VALUES (@IdSubscription, @IdUser, @IdSubscriptionPlan, @Status, @StartDate, @EndDate, SYSUTCDATETIME());
END
GO

-- La suscripcion mas reciente de la cuenta (una cuenta podria acumular mas de
-- una fila historica el dia que haya upgrade/downgrade real -- por ahora
-- siempre hay a lo sumo una, la del Trial automatico).
CREATE OR ALTER PROCEDURE dbo.GET_SubscriptionByUser
    @IdUser UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        s.IdSubscription,
        s.IdUser,
        s.IdSubscriptionPlan,
        p.Name AS PlanName,
        p.MaxBuildings,
        s.Status,
        s.StartDate,
        s.EndDate
    FROM dbo.Subscription s
    INNER JOIN dbo.SubscriptionPlan p ON p.IdSubscriptionPlan = s.IdSubscriptionPlan
    WHERE s.IdUser = @IdUser
    ORDER BY s.CreatedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AllSubscriptionPlans
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdSubscriptionPlan, Name, MaxBuildings, IsActive
    FROM dbo.SubscriptionPlan
    WHERE IsActive = 1;
END
GO
