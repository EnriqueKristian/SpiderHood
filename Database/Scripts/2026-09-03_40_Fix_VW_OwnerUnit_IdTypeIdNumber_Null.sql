-- =============================================================================
-- Se me pasó esta columna en el script 39: todas las demás quedaron envueltas en
-- ISNULL(...), pero o.IdTypeIdNumber no -- y es INT NULL en ApartmentOwner
-- (script 34, sin backfill para filas creadas antes de esa columna existir).
-- Models.OwnerUnitView.IdTypeIdNumber es un int no-nullable, así que
-- SqlDataReader.GetInt32 revienta (SqlNullValueException) apenas aparece una
-- fila con esa columna en NULL -- exactamente el crash del trace de Enrique.
-- Antes no se notaba porque el INNER JOIN original excluía a cualquier
-- propietario sin grupo (y de paso, probablemente, al propietario de prueba más
-- viejo, creado antes de que la columna existiera).
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
		ISNULL(o.IdTypeIdNumber, 0) AS IdTypeIdNumber
	FROM	ApartmentOwner o
	LEFT JOIN OwnerGroupRole owr ON owr.IdOwner = o.IdOwner
	LEFT JOIN GroupUnit gu ON gu.IdGroupUnit = owr.IdGroupUnit
	LEFT JOIN RealEstateUnit r ON r.IdGroupUnit = gu.IdGroupUnit
GO
