-- =============================================================================
-- Limpieza de datos de prueba -- no es un fix de esquema/proc, es puntual para
-- el ambiente donde se estuvo debugueando. Durante las varias vueltas de
-- INS_GroupOwner/UPD_GroupOwner/INS_GroupUnitOwner fallando, quedaron grupos
-- "fantasma": filas en GroupUnit (y su OwnerGroupRole) que ningún
-- RealEstateUnit.IdGroupUnit referencia -- exactamente lo que se ve en el
-- resultado de VW_OwnerUnit que mandó Enrique (mismo propietario repetido 3
-- veces, sólo uno de los 3 grupos con una unidad real enlazada).
--
-- Correr primero el SELECT para confirmar cuáles son antes de borrar.
-- =============================================================================

-- 1) Revisar qué grupos quedarían borrados
SELECT gu.IdGroupUnit, gu.TotalArea, gu.GroupNumber, owr.IdOwner, owr.Role
FROM GroupUnit gu
LEFT JOIN OwnerGroupRole owr ON owr.IdGroupUnit = gu.IdGroupUnit
WHERE NOT EXISTS (SELECT 1 FROM RealEstateUnit r WHERE r.IdGroupUnit = gu.IdGroupUnit);

-- 2) Si la lista de arriba es la esperada (los grupos fantasma, no uno real),
--    descomentar y correr esto:

/*
DELETE FROM OwnerGroupRole
WHERE IdGroupUnit IN (
    SELECT gu.IdGroupUnit FROM GroupUnit gu
    WHERE NOT EXISTS (SELECT 1 FROM RealEstateUnit r WHERE r.IdGroupUnit = gu.IdGroupUnit)
);

DELETE FROM GroupUnit
WHERE NOT EXISTS (SELECT 1 FROM RealEstateUnit r WHERE r.IdGroupUnit = GroupUnit.IdGroupUnit);
*/
