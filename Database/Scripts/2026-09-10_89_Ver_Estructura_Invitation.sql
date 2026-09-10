-- Sólo LECTURA. Estructura real de dbo.Invitation, para diseñar el flujo
-- "Invitar Residente" (email + edificio + rol + unidad) sin adivinar columnas
-- que no existen (ver GET_InvitationByCode, que hace SELECT * sin JOIN).
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Invitation'
ORDER BY ORDINAL_POSITION;
