-- =============================================================================
-- GET_OwnerByBuilding (grilla de /Owners) filtra VW_OwnerUnit por IdBuilding --
-- esa vista hace JOIN GroupUnit -> RealEstateUnit -> OwnerGroupRole ->
-- ApartmentOwner (ver 2026-09-03_36_VW_OwnerUnit_IdTypeIdNumber.sql). Ninguna
-- de esas 4 tablas tiene un índice creado por ningún script de esta carpeta --
-- son todas anteriores a este proyecto de migración, así que lo único que
-- garantizan es su Primary Key. Sin un índice sobre RealEstateUnit.IdBuilding
-- (la columna real del WHERE) ni sobre las columnas de JOIN, SQL Server no
-- tiene forma de resolver esto con un seek: escanea las 4 tablas completas
-- cada vez. Con datos ya acumulados (varios edificios de prueba a lo largo de
-- esta sesión), eso explica el síntoma reportado: 25 segundos la primera vez
-- que corre GET_OwnerByBuilding en un circuito (páginas de datos frías, sin
-- caché de buffer pool), mucho más rápido después (mismas páginas ya en RAM,
-- aunque el plan siga sin seek).
--
-- Índices agregados, todos con guard idempotente contra sys.indexes (las
-- tablas son preexistentes, no se crean acá, así que no alcanza con el guard
-- "IF NOT EXISTS de la tabla" que usan los scripts de tablas nuevas):
--   - RealEstateUnit.IdBuilding: la columna del WHERE de GET_OwnerByBuilding.
--   - RealEstateUnit.IdGroupUnit / OwnerGroupRole.IdGroupUnit: el JOIN a
--     GroupUnit (ambos lados).
--   - OwnerGroupRole.IdOwner: el JOIN a ApartmentOwner.
-- No se toca ninguna Primary Key ni se cambia ningún dato -- sólo agrega
-- estructuras de búsqueda adicionales, seguro de correr en cualquier momento.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_RealEstateUnit_IdBuilding' AND object_id = OBJECT_ID('dbo.RealEstateUnit')
)
BEGIN
    CREATE INDEX IX_RealEstateUnit_IdBuilding ON dbo.RealEstateUnit (IdBuilding) INCLUDE (IdGroupUnit);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_RealEstateUnit_IdGroupUnit' AND object_id = OBJECT_ID('dbo.RealEstateUnit')
)
BEGIN
    CREATE INDEX IX_RealEstateUnit_IdGroupUnit ON dbo.RealEstateUnit (IdGroupUnit);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_OwnerGroupRole_IdGroupUnit' AND object_id = OBJECT_ID('dbo.OwnerGroupRole')
)
BEGIN
    CREATE INDEX IX_OwnerGroupRole_IdGroupUnit ON dbo.OwnerGroupRole (IdGroupUnit) INCLUDE (IdOwner, [Role]);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_OwnerGroupRole_IdOwner' AND object_id = OBJECT_ID('dbo.OwnerGroupRole')
)
BEGIN
    CREATE INDEX IX_OwnerGroupRole_IdOwner ON dbo.OwnerGroupRole (IdOwner);
END
GO
