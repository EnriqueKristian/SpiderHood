-- =============================================================================
-- Integración con Stripe para la Suscripción SaaS del Administrador (sigue a
-- 2026-09-04_44_Subscription.sql). Ver Docs/Design-Subscripcion-Administrador.md.
--
-- StripePriceId en SubscriptionPlan queda NULL hasta que se cree el Product +
-- Price recurrente correspondiente en el Dashboard de Stripe (modo test) y se
-- pegue acá con un UPDATE manual -- ver el runbook en el documento de diseño.
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SubscriptionPlan') AND name = 'StripePriceId')
BEGIN
    ALTER TABLE dbo.SubscriptionPlan ADD StripePriceId NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Subscription') AND name = 'StripeCustomerId')
BEGIN
    ALTER TABLE dbo.Subscription ADD StripeCustomerId NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Subscription') AND name = 'StripeSubscriptionId')
BEGIN
    ALTER TABLE dbo.Subscription ADD StripeSubscriptionId NVARCHAR(100) NULL;
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
        s.StripeCustomerId,
        s.StripeSubscriptionId
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
    SELECT IdSubscriptionPlan, Name, MaxBuildings, IsActive, StripePriceId
    FROM dbo.SubscriptionPlan
    WHERE IsActive = 1;
END
GO

-- Llamado desde el webhook de Stripe (checkout.session.completed) una vez que
-- el pago se confirmó de verdad -- nunca desde el redirect de éxito, que no es
-- confiable (ver documento de diseño). Upsert sobre la fila MÁS RECIENTE del
-- usuario (la del Trial automático, normalmente): si existe, la pisa con el
-- plan pago; si no existe ninguna (cuenta vieja, de antes del Trial
-- automático), inserta una nueva.
CREATE OR ALTER PROCEDURE dbo.UPD_ActivateSubscription
    @IdUser UNIQUEIDENTIFIER,
    @IdSubscriptionPlan INT,
    @StripeCustomerId NVARCHAR(100),
    @StripeSubscriptionId NVARCHAR(100)
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
            StripeCustomerId = @StripeCustomerId,
            StripeSubscriptionId = @StripeSubscriptionId
        WHERE IdSubscription = @IdSubscription;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.Subscription
            (IdSubscription, IdUser, IdSubscriptionPlan, Status, StartDate, EndDate, CreatedAt, StripeCustomerId, StripeSubscriptionId)
        VALUES
            (NEWID(), @IdUser, @IdSubscriptionPlan, 'Active', SYSUTCDATETIME(), NULL, SYSUTCDATETIME(), @StripeCustomerId, @StripeSubscriptionId);
    END
END
GO
