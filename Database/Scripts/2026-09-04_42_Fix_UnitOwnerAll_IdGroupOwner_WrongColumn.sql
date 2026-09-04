-- =============================================================================
-- Root cause de "asignar como copropietario" fallando SIEMPRE con
-- FK_OwnerGroupRole_GroupUnit, incluso con IDs recién confirmados como válidos
-- por consulta directa. UnitOwnerAll (detrás de GET_UnitsByBuilding, que llena
-- FreeUnits en Owners.razor) arma la columna 'IdGroupOwner' con
-- gow.IdGroupOwnerRol -- la PK de la fila individual en OwnerGroupRole (el rol
-- del titular) -- en vez de gu.IdGroupUnit, que es la PK real de GroupUnit y lo
-- que la FK OwnerGroupRole.IdGroupUnit -> GroupUnit.IdGroupUnit espera.
--
-- Son dos GUID totalmente distintos. Por eso:
--   - "Crear grupo nuevo" siempre funcionó: esa rama de SaveGroupUnit sólo
--     compara contra Guid.Empty, y ambas columnas dan Guid.Empty cuando la
--     unidad no tiene grupo (LEFT JOIN sin match).
--   - "Agregar como copropietario" a un grupo YA existente siempre falló: ahí
--     sí se manda ese IdGroupOwner (en realidad IdGroupOwnerRol) como
--     @IdGroupOwner a INS_OwnerGroupOwner, que lo inserta como
--     OwnerGroupRole.IdGroupUnit -- un valor que nunca es un GroupUnit.IdGroupUnit
--     real, sin importar qué tan reciente sea la recarga de FreeUnits.
--
-- Confirmado con el texto real de la vista (Enrique, sp_helptext UnitOwnerAll).
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER VIEW dbo.UnitOwnerAll
AS
	SELECT	u.IdUnit,
			u.UnitNumber,
			ISNULL(u.Area, 0.00)													AS 'Area',
			1																	AS 'TypeGroupUnit',
			ISNULL(gu.IdGroupUnit, '00000000-0000-0000-0000-000000000000')		AS 'IdGroupOwner',
			ISNULL(cast(gu.GroupNumber as varchar(50)), '')						AS 'GroupName',
			ISNULL(gu.TotalArea, 0.00)											AS 'AreaTotal',
			1																	AS 'TypeOwner',
			ISNULL(ogo.FirstName,'')											AS 'Names',
			ISNULL(ogo.LastName,'')												AS 'Surname',
			u.IdBuilding,
			p.value																AS 'TypeUnit',
			ISNULL(gow.IdOwner,'00000000-0000-0000-0000-000000000000')		AS  'IdOwner',
			u.Number,
			CAST(0 AS BIT)		as IsAvailable
	FROM	RealEstateUnit u
	JOIN	Parameter p ON p.Value = u.TypeUnit AND p.IdParent = 4
	LEFT OUTER JOIN	GroupUnit gu ON gu.IdGroupUnit = u.IdGroupUnit
	LEFT OUTER JOIN	OwnerGroupRole gow ON gu.IdGroupUnit = gow.IdGroupUnit AND gow.Role = 1	--TITULAR
	LEFT OUTER JOIN	ApartmentOwner ogo ON ogo.IdOwner = gow.IdOwner
--	ORDER BY	ut.Description, u.UnitNumber
GO
