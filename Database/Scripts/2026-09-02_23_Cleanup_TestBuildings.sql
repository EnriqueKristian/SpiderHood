-- =============================================================================
-- Limpieza de datos de prueba (una sola vez, no forma parte del plan de
-- implementación en Docs/Design-Defaults-Sistema-Mixto.md -- es housekeeping del
-- entorno de desarrollo del usuario, a pedido explícito).
--
-- Hace dos cosas:
--   1) Borra los edificios de prueba "Edifcio de Prueba" y "Prueba 2" (Building +
--      BuildingConfiguration + Parameter Mixto propios + Category +
--      UserBuildingAssociation). NO toca BankAccount/Contact/Exoneration -- estos
--      dos edificios nunca pasaron por esas pantallas, así que deberían estar
--      vacíos; si BuildingConfiguration tuviera alguna fila hija ahí, el DELETE
--      de BuildingConfiguration va a fallar por FK en vez de dejar basura
--      huérfana -- avisame si pasa eso y lo agrego.
--   2) Saca las asociaciones de enriquek@outlook.com (Administrador y Residente)
--      con "Edificio NOVA Alzamora", que pasa a ser el template -- confirmado por
--      el usuario. No hace falta recrear nada a mano: si esta misma cuenta crea
--      el "Nova Alzamora" real después, CreateBuildingAsync le arma una
--      UserBuildingAssociation nueva automáticamente.
--
-- Todo dentro de una transacción -- si algo falla a mitad de camino, no queda
-- el edificio a medio borrar.
-- =============================================================================

SET NOCOUNT ON;
GO

BEGIN TRANSACTION;

BEGIN TRY

    DECLARE @IdEdificioPrueba1 UNIQUEIDENTIFIER = (SELECT IdBuilding FROM dbo.Building WHERE Name = N'Edifcio de Prueba');
    DECLARE @IdEdificioPrueba2 UNIQUEIDENTIFIER = (SELECT IdBuilding FROM dbo.Building WHERE Name = N'Prueba 2');
    DECLARE @IdNovaAlzamora UNIQUEIDENTIFIER = (SELECT IdBuilding FROM dbo.Building WHERE Name = N'Edificio NOVA Alzamora');
    DECLARE @IdUserEnrique UNIQUEIDENTIFIER = (SELECT IdUser FROM dbo.Users WHERE Email = N'enriquek@outlook.com');

    IF @IdEdificioPrueba1 IS NULL OR @IdEdificioPrueba2 IS NULL OR @IdNovaAlzamora IS NULL OR @IdUserEnrique IS NULL
    BEGIN
        RAISERROR('No se encontró alguno de los edificios/usuario esperados por nombre -- revisá los nombres antes de correr esto (pudieron cambiar desde que se escribió el script).', 16, 1);
    END

    -- ---------------------------------------------------------------------------
    -- 1) Borrar los 2 edificios de prueba por completo
    -- ---------------------------------------------------------------------------
    DELETE FROM dbo.UserBuildingAssociation WHERE IdBuilding IN (@IdEdificioPrueba1, @IdEdificioPrueba2);
    DELETE FROM dbo.Category WHERE IdBuilding IN (@IdEdificioPrueba1, @IdEdificioPrueba2);
    DELETE FROM dbo.Parameter WHERE IdBuilding IN (@IdEdificioPrueba1, @IdEdificioPrueba2);
    DELETE FROM dbo.BuildingConfiguration WHERE IdBuilding IN (@IdEdificioPrueba1, @IdEdificioPrueba2);
    DELETE FROM dbo.Building WHERE IdBuilding IN (@IdEdificioPrueba1, @IdEdificioPrueba2);

    -- ---------------------------------------------------------------------------
    -- 2) Sacar a enriquek@outlook.com de NOVA Alzamora (pasa a ser template)
    -- ---------------------------------------------------------------------------
    DELETE FROM dbo.UserBuildingAssociation
    WHERE IdUser = @IdUserEnrique AND IdBuilding = @IdNovaAlzamora;

    COMMIT TRANSACTION;
    PRINT 'Limpieza completada.';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH
GO
