-- =============================================================================
-- Fix crítico: GET_AllContacts no filtraba por @IdRelatedEntity -- devolvía TODOS
-- los Contact de la base sin importar el parámetro. Efecto observado: cualquier
-- edificio mostraba el contacto Admin/Inmobiliaria/Mantenimiento del PRIMER
-- Contact de ese TypeContact que hubiera en toda la tabla (en la práctica, el del
-- edificio Template, creado primero) -- BuildingService.GetConfigurationAsync
-- hace `contacts.FirstOrDefault(c => c.TypeContact == 1)` sobre la lista completa
-- que devolvía el SP, así que "filtraba" mal en memoria en vez de no filtrar nada.
--
-- Nunca se detectó por consulta directa a Contact porque cada Contact individual
-- SÍ tiene el IdRelatedEntity correcto -- el bug es que el SP los devolvía todos
-- juntos, no que las filas estuvieran mal grabadas.
-- =============================================================================

CREATE OR ALTER PROCEDURE dbo.GET_AllContacts
    @IdRelatedEntity    UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  IdContact,
            TypeContact,
            Name,
            Phone,
            Email,
            Address,
            ISNULL(OfficePhone,'')  AS OfficePhone,
            ISNULL(MobilePhone,'')  AS MobilePhone,
            IdRelatedEntity
    FROM    Contact
    WHERE   IdRelatedEntity = @IdRelatedEntity
END
GO
