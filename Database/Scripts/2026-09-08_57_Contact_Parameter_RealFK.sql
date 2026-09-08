-- =============================================================================
-- FK real para Contact.IdRelatedEntity -> BuildingConfiguration.IdBuildingConfiguration
-- y Parameter.IdBuilding -> Building.IdBuilding (Docs/Pendientes-Negocio-Migracion.md,
-- punto 6.3). Hoy ninguna de las dos relaciones tiene constraint física -- confirmado
-- revisando el código: Contact sólo se usa hoy para los 3 contactos de
-- BuildingConfiguration (Admin/Inmobiliaria/Mantenimiento -- BuildingPage.razor.cs,
-- SaveSection casos "admin"/"realty"/"maintenance"), siempre con
-- IdRelatedEntity = IdBuildingConfiguration, así que la FK es segura.
--
-- No existe hoy ninguna función de borrado de edificios en la app (no hay
-- DeleteBuildingAsync en IBuildingService) -- esto es una protección preventiva para
-- cuando exista, no corrige un bug visible en producción todavía.
--
-- Mismo criterio que 2026-09-02_24_Category_RealFK.sql: cada tabla se trata
-- independiente, se saltea sola si ya tiene un FK o si tiene filas huérfanas (no
-- aborta el script entero), y sin ON DELETE/UPDATE explícito (default NO ACTION) --
-- borrar un edificio todavía en uso tiene que fallar, no arrastrar el borrado en
-- cascada. Re-ejecutable.
-- =============================================================================

SET NOCOUNT ON;
GO

PRINT '--- Diagnóstico: filas huérfanas por tabla (antes de crear cualquier FK) ---';
SELECT 'Contact' AS Tabla, COUNT(*) AS Huerfanos
FROM dbo.Contact c
WHERE NOT EXISTS (SELECT 1 FROM dbo.BuildingConfiguration bc WHERE bc.IdBuildingConfiguration = c.IdRelatedEntity)
UNION ALL
SELECT 'Parameter', COUNT(*)
FROM dbo.Parameter p
WHERE p.IdBuilding IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Building b WHERE b.IdBuilding = p.IdBuilding);
GO

-- ---------------------------------------------------------------------------
-- Contact.IdRelatedEntity -> BuildingConfiguration.IdBuildingConfiguration
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    WHERE fk.parent_object_id = OBJECT_ID('dbo.Contact')
      AND fk.referenced_object_id = OBJECT_ID('dbo.BuildingConfiguration')
      AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = 'IdRelatedEntity'
)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Contact c WHERE NOT EXISTS (SELECT 1 FROM dbo.BuildingConfiguration bc WHERE bc.IdBuildingConfiguration = c.IdRelatedEntity))
        PRINT 'Contact: OMITIDA -- tiene IdRelatedEntity huérfanos, limpiar antes de reintentar.';
    ELSE
    BEGIN
        ALTER TABLE dbo.Contact WITH CHECK ADD CONSTRAINT FK_Contact_BuildingConfiguration FOREIGN KEY (IdRelatedEntity) REFERENCES dbo.BuildingConfiguration(IdBuildingConfiguration);
        PRINT 'Contact: FK_Contact_BuildingConfiguration creado.';
    END
END
ELSE
    PRINT 'Contact: ya tenía un FK sobre IdRelatedEntity -> BuildingConfiguration, sin cambios.';
GO

-- ---------------------------------------------------------------------------
-- Parameter.IdBuilding -> Building.IdBuilding (nullable: hay Parameter globales
-- sin edificio -- ver Classes/Parameter.cs)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    WHERE fk.parent_object_id = OBJECT_ID('dbo.Parameter')
      AND fk.referenced_object_id = OBJECT_ID('dbo.Building')
      AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = 'IdBuilding'
)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Parameter p WHERE p.IdBuilding IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Building b WHERE b.IdBuilding = p.IdBuilding))
        PRINT 'Parameter: OMITIDA -- tiene IdBuilding huérfanos, limpiar antes de reintentar.';
    ELSE
    BEGIN
        ALTER TABLE dbo.Parameter WITH CHECK ADD CONSTRAINT FK_Parameter_Building FOREIGN KEY (IdBuilding) REFERENCES dbo.Building(IdBuilding);
        PRINT 'Parameter: FK_Parameter_Building creado.';
    END
END
ELSE
    PRINT 'Parameter: ya tenía un FK sobre IdBuilding -> Building, sin cambios.';
GO
