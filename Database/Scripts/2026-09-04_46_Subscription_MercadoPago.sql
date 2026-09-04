-- =============================================================================
-- Reemplaza la integración de pago de la Suscripción: Stripe -> MercadoPago
-- (no hay Stripe disponible para Perú). Ver Docs/Design-Subscripcion-Administrador.md.
--
-- A diferencia de Stripe (que exigía crear un Price recurrente a mano en su
-- Dashboard antes de poder cobrar), la Preapproval API de MercadoPago acepta
-- el monto/frecuencia directo en la llamada -- así que en vez de guardar un ID
-- externo de "plan" en SubscriptionPlan, se guarda el precio real (Amount +
-- CurrencyId) acá mismo.
--
-- Las columnas de Stripe (StripePriceId/StripeCustomerId/StripeSubscriptionId,
-- agregadas en 2026-09-04_45) se sacan -- nunca llegó a haber una suscripción
-- real activada con ellas (el runbook de Stripe no se completó). Idempotente:
-- se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SubscriptionPlan') AND name = 'StripePriceId')
BEGIN
    ALTER TABLE dbo.SubscriptionPlan DROP COLUMN StripePriceId;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SubscriptionPlan') AND name = 'Amount')
BEGIN
    -- NULL en el plan Trial a propósito -- nunca se cobra.
    ALTER TABLE dbo.SubscriptionPlan ADD Amount DECIMAL(18, 2) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SubscriptionPlan') AND name = 'CurrencyId')
BEGIN
    ALTER TABLE dbo.SubscriptionPlan ADD CurrencyId NVARCHAR(10) NULL;
END
GO

UPDATE dbo.SubscriptionPlan SET Amount = 49.00, CurrencyId = 'PEN' WHERE Name = 'Basico' AND Amount IS NULL;
UPDATE dbo.SubscriptionPlan SET Amount = 99.00, CurrencyId = 'PEN' WHERE Name = 'Empresarial' AND Amount IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Subscription') AND name = 'StripeCustomerId')
BEGIN
    ALTER TABLE dbo.Subscription DROP COLUMN StripeCustomerId;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Subscription') AND name = 'StripeSubscriptionId')
BEGIN
    ALTER TABLE dbo.Subscription DROP COLUMN StripeSubscriptionId;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Subscription') AND name = 'MercadoPagoPreapprovalId')
BEGIN
    -- El Id del recurso Preapproval en MercadoPago -- lo que en Stripe eran
    -- CustomerId+SubscriptionId juntos (acá alcanza con uno: Preapproval ya
    -- carga el payer adentro, no hace falta duplicarlo).
    ALTER TABLE dbo.Subscription ADD MercadoPagoPreapprovalId NVARCHAR(100) NULL;
END
GO

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
        s.EndDate,
        s.MercadoPagoPreapprovalId
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
    SELECT IdSubscriptionPlan, Name, MaxBuildings, IsActive, Amount, CurrencyId
    FROM dbo.SubscriptionPlan
    WHERE IsActive = 1;
END
GO

-- Llamado desde el webhook de MercadoPago (evento subscription_preapproval,
-- status "authorized") una vez confirmado -- nunca desde el redirect del
-- navegador. Upsert sobre la fila más reciente del usuario (la del Trial
-- automático, normalmente): si existe la pisa, si no inserta una nueva.
CREATE OR ALTER PROCEDURE dbo.UPD_ActivateSubscription
    @IdUser UNIQUEIDENTIFIER,
    @IdSubscriptionPlan INT,
    @MercadoPagoPreapprovalId NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdSubscription UNIQUEIDENTIFIER = (
        SELECT TOP 1 IdSubscription
        FROM dbo.Subscription
        WHERE IdUser = @IdUser
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
            (IdSubscription, IdUser, IdSubscriptionPlan, Status, StartDate, EndDate, CreatedAt, MercadoPagoPreapprovalId)
        VALUES
            (NEWID(), @IdUser, @IdSubscriptionPlan, 'Active', SYSUTCDATETIME(), NULL, SYSUTCDATETIME(), @MercadoPagoPreapprovalId);
    END
END
GO
