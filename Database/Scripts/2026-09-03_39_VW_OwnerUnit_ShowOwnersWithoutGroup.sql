-- =============================================================================
-- Enrique: "/Owners debe traer los Propietarios asociados al edificio donde se
-- creó, indistintamente tengan o no Grupo de unidad asociados". VW_OwnerUnit usa
-- INNER JOIN contra OwnerGroupRole/GroupUnit/RealEstateUnit -- por diseño, eso
-- excluye a cualquier propietario sin unidad asignada todavía (es lo que ya
-- habíamos diagnosticado: un propietario recién creado no aparecía en la
-- grilla). Se cambia a LEFT JOIN partiendo de ApartmentOwner, así que ahora
-- todo propietario del edificio aparece siempre, tenga o no grupo.
--
-- ISNULL(...) en cada columna que puede quedar NULL por el LEFT JOIN: mismo
-- criterio que ya usa el resto del código (Guid.Empty = "sin grupo", ver
-- RealEstateUnit.IdGroupOwner/SaveGroupUnit en Owners.razor) en vez de agregar
-- tipos nullable nuevos en Models.OwnerUnitView -- así ningún otro código C#
-- necesita cambiar para leer esta vista. TypeUnit y Role se coalescean a 1
-- (no a 0) a propósito: Owners.razor.LoadDataAsync ya arma una fila de grupo
-- ("GroupUnit1") sólo cuando ve TypeUnit==1 && Role==1 en la fila -- con este
-- coalesce, un propietario sin grupo entra por ese mismo camino sin tener que
-- tocar esa lógica, sólo con IdGroupUnit en Guid.Empty (que es lo que el
-- código ya usa para decidir "sin unidad" -- ver GroupUnit1.HasGroup).
--
-- De paso corrige un crash real (SqlNullValueException en GetString) --
-- ApartmentOwner.FirstName/LastName/Email son NULL-ables en la tabla real
-- (confirmado con la captura de columnas), y Models.OwnerUnitView los mapea a
-- `string` no-nullable -- el materializador de EF Core no los protege con
-- IsDBNull y tira excepción apenas alguna fila trae NULL ahí. Con ISNULL(...,'')
-- nunca llega un NULL a esas columnas, así que no hace falta tocar el modelo
-- C# tampoco.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER VIEW dbo.VW_OwnerUnit
AS
	SELECT
		ISNULL(gu.IdGroupUnit, '00000000-0000-0000-0000-000000000000') AS IdGroupUnit,
		ISNULL(gu.TotalArea, 0) AS TotalArea,
		ISNULL(gu.GroupNumber, 0) AS GroupNumber,
		ISNULL(r.IdUnit, '00000000-0000-0000-0000-000000000000') AS IdUnit,
		ISNULL(r.UnitNumber, '') AS UnitNumber,
		ISNULL(r.Area, 0) AS Area,
		ISNULL(r.TypeUnit, 1) AS TypeUnit,
		ISNULL(r.Number, 0) AS Number,
		ISNULL(r.IsAvailable, 0) AS IsAvailable,
		ISNULL(owr.IdGroupOwnerRol, '00000000-0000-0000-0000-000000000000') AS IdGroupOwnerRol,
		ISNULL(owr.[Role], 1) AS [Role],
		o.IdOwner,
		o.IdentityDocument,
		o.[Address],
		o.PhoneNumber,
		ISNULL(o.FirstName, '') AS FirstName,
		ISNULL(o.LastName, '') AS LastName,
		ISNULL(o.Email, '') AS Email,
		o.IsActive,
		o.IdBuilding,
		o.IdTypeIdNumber
	FROM	ApartmentOwner o
	LEFT JOIN OwnerGroupRole owr ON owr.IdOwner = o.IdOwner
	LEFT JOIN GroupUnit gu ON gu.IdGroupUnit = owr.IdGroupUnit
	LEFT JOIN RealEstateUnit r ON r.IdGroupUnit = gu.IdGroupUnit
GO
