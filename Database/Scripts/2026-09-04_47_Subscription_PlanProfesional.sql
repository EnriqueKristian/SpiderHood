-- =============================================================================
-- Agrega el plan "Profesional" (nivel intermedio entre Básico y Empresarial),
-- para que coincida con los 3 planes que ya muestra la landing pública
-- (wwwroot/index.html, sección "Planes para cada comunidad"). Ver
-- Docs/Design-Subscripcion-Administrador.md.
--
-- El eje del límite sigue siendo cantidad de EDIFICIOS administrados (no
-- unidades/departamentos por edificio, que es lo que decía la landing --
-- corregido ahí también): Básico = 1, Profesional = 3, Empresarial = sin
-- límite. El precio (S/74.00) es un valor razonable a mitad de camino entre
-- Básico (S/49) y Empresarial (S/99) -- fácil de ajustar con un UPDATE.
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlan WHERE Name = 'Profesional')
BEGIN
    INSERT INTO dbo.SubscriptionPlan (Name, MaxBuildings, IsActive, Amount, CurrencyId)
    VALUES ('Profesional', 3, 1, 74.00, 'PEN');
END
GO
