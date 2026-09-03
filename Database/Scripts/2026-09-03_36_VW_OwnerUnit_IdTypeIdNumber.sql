-- =============================================================================
-- Completa los scripts 34/35: agrega o.IdTypeIdNumber a la vista que
-- GET_OwnerByBuilding usa para armar la grilla de /Owners. Con esto, la cadena
-- completa queda cerrada: ApartmentOwner tiene la columna (34) -> la vista la
-- expone (este script) -> la SP la trae en el SELECT (35) -> Models.OwnerUnitView
-- la mapea por nombre (ya en el código, commit anterior).
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER VIEW dbo.VW_OwnerUnit
AS
	SELECT
		gu.IdGroupUnit,
		gu.TotalArea,
		gu.GroupNumber,
		r.IdUnit,
		r.UnitNumber,
		r.Area,
		r.TypeUnit,
		r.Number,
		r.IsAvailable,
		owr.IdGroupOwnerRol,
		owr.[Role],
		o.IdOwner,
		o.IdentityDocument,
		o.[Address],
		o.PhoneNumber,
		o.FirstName,
		o.LastName,
		o.Email,
		o.IsActive,
		r.IdBuilding,
		o.IdTypeIdNumber
	FROM	GroupUnit gu
	JOIN	RealEstateUnit r ON gu.IdGroupUnit = r.IdGroupUnit
	JOIN	OwnerGroupRole owr ON owr.IdGroupUnit = gu.IdGroupUnit
	JOIN	ApartmentOwner o ON o.IdOwner = owr.IdOwner
GO
