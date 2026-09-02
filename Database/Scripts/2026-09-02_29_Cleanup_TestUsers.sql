-- =============================================================================
-- Limpieza de datos de prueba (housekeeping del entorno del usuario, no es parte
-- del plan -- a pedido explícito, sigue del diagnóstico
-- 2026-09-02_28_Diagnostico_UsersConReferencias.sql).
--
-- Borra los 2 usuarios que no son admin@spiderhood.com:
--   - aliagarasu@gmail.com (Aracely Aliaga)
--   - enriquek@outlook.com (Enrique Echevarría)
--
-- El diagnóstico confirmó que lo único que los referencia es dbo.UserRole (2
-- filas, una por usuario) -- dbo.UserBuildingAssociation ya estaba en 0 (la
-- limpieza anterior, 2026-09-02_23_Cleanup_TestBuildings.sql, ya había sacado a
-- enriquek@outlook.com de NOVA Alzamora). No hay ninguna otra tabla con FK real
-- hacia Users, así que borrar UserRole primero alcanza.
--
-- Por nombre/email, no por IdUser fijo -- así no depende de que los valores de
-- este dump coincidan con los de tu BD real. Todo dentro de una transacción -- si
-- algo falla a mitad de camino (p.ej. una FK que el diagnóstico no vio, como
-- algo que referencie a UserRole y no a Users directamente), no queda nada a
-- medio borrar.
-- =============================================================================

SET NOCOUNT ON;
GO

BEGIN TRANSACTION;

BEGIN TRY

    DECLARE @IdAracely UNIQUEIDENTIFIER = (SELECT IdUser FROM dbo.Users WHERE Email = N'aliagarasu@gmail.com');
    DECLARE @IdEnrique  UNIQUEIDENTIFIER = (SELECT IdUser FROM dbo.Users WHERE Email = N'enriquek@outlook.com');

    IF @IdAracely IS NULL OR @IdEnrique IS NULL
    BEGIN
        RAISERROR('No se encontró alguno de los 2 usuarios esperados por email -- revisá los emails antes de correr esto (pudieron cambiar desde que se escribió el script).', 16, 1);
    END

    DELETE FROM dbo.UserRole WHERE IdUser IN (@IdAracely, @IdEnrique);
    DELETE FROM dbo.UserBuildingAssociation WHERE IdUser IN (@IdAracely, @IdEnrique); -- ya en 0, por las dudas
    DELETE FROM dbo.Users WHERE IdUser IN (@IdAracely, @IdEnrique);

    COMMIT TRANSACTION;
    PRINT 'Limpieza completada -- sólo debería quedar admin@spiderhood.com.';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH
GO
