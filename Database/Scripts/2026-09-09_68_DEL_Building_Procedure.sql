-- Crea el stored procedure DEL_Building (no existía -- no había ninguna función de
-- borrado de edificios en la app, ver Docs/Pendientes-Negocio-Migracion.md #6.3).
-- Implementado a pedido explícito del usuario, gateado en la UI (BuildingPage.razor)
-- a SysAdmin únicamente, sólo para poder borrar edificios de PRUEBA sin tener que
-- hacerlo a mano en la BD como hasta ahora (ver 2026-09-02_23_Cleanup_TestBuildings.sql,
-- el borrado manual que se hacía antes de esto).
--
-- SOLO borra UserBuildingAssociation + BuildingConfiguration + Building, en ese
-- orden, dentro de una transacción -- a propósito, sin cascada hacia Category/
-- Parameter/Unit/Owner/BankAccount/Contact/Installment/etc.: si el edificio tiene
-- cualquiera de esas filas, el DELETE de BuildingConfiguration o Building falla por
-- FK (error 547, atrapado en IBuildingService.DeleteBuildingAsync) en vez de dejar
-- datos huérfanos -- mismo criterio que DEL_Unit/DEL_Category (fallan por FK, no
-- arrastran en cascada).
--
-- OJO -- no se pudo confirmar contra la base de datos real (este entorno de trabajo
-- no tiene acceso a ella) qué tablas, de las ~12 que tienen columna IdBuilding además
-- de Category/Parameter/Contact (que sí tienen FK real confirmada, ver
-- 2026-09-02_24_Category_RealFK.sql y 2026-09-08_57_Contact_Parameter_RealFK.sql),
-- tienen hoy un FK real hacia Building/BuildingConfiguration. Si alguna no lo tiene,
-- borrar un edificio que sí tiene filas ahí NO va a fallar -- va a dejarlas
-- huérfanas. Probar primero contra un edificio de prueba genuinamente vacío (recién
-- creado, sin unidades/propietarios/cuentas/movimientos) antes de confiar en esto
-- para cualquier edificio con actividad real.
--
-- Idempotente: CREATE OR ALTER no falla si el proc ya existe.
--
-- Ejecutar contra la base de datos de la app (ver DEPLOY-Production.md §2).

CREATE OR ALTER PROCEDURE dbo.DEL_Building
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRANSACTION;

    BEGIN TRY
        DELETE FROM dbo.UserBuildingAssociation WHERE IdBuilding = @IdBuilding;
        DELETE FROM dbo.BuildingConfiguration WHERE IdBuilding = @IdBuilding;
        DELETE FROM dbo.Building WHERE IdBuilding = @IdBuilding;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
