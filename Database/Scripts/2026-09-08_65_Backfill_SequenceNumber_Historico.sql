-- =============================================================================
-- Backfill único: renumera las filas de AccountStatementDetail que quedaron con
-- SequenceNumber = 0 (todas las cargadas antes del fix de
-- 2026-09-08_64_Fix_INS_AccountStatementDetail_SequenceNumber.sql), asignando
-- un correlativo por CUENTA BANCARIA en orden cronológico (StatementDate de la
-- transacción; si dos quedan en la misma fecha, se ordena por la fecha de
-- carga del archivo -- AccountStatementHeader.UploadDate -- y por último por
-- IdStatementDetail solo para que el orden sea determinístico).
--
-- Este script SOLO toca filas con SequenceNumber = 0 -- si ya corriste el fix
-- de INS_AccountStatementDetail y cargaste algo nuevo antes de correr este
-- backfill, esas filas nuevas ya tienen un número real y no se tocan; el
-- correlativo del histórico arranca después del máximo que ya exista en cada
-- cuenta (CTE Offsets), así que no genera números repetidos sin importar el
-- orden en que se corran ambos scripts.
--
-- Es un script de datos, no un procedure -- correr UNA sola vez. Correrlo de
-- nuevo no hace nada (ya no quedan filas en 0 para tocar).
-- =============================================================================

BEGIN TRY
    BEGIN TRANSACTION;

    WITH RowsToFix AS (
        SELECT  md.IdStatementDetail,
                mh.IdBankAccount,
                ROW_NUMBER() OVER (
                    PARTITION BY mh.IdBankAccount
                    ORDER BY md.StatementDate, mh.UploadDate, md.IdStatementDetail
                ) AS rn
        FROM    AccountStatementDetail md
        JOIN    AccountStatementHeader mh ON mh.IdStatementHeader = md.IdStatementHeader
        WHERE   md.SequenceNumber = 0
    ),
    Offsets AS (
        SELECT  mh.IdBankAccount,
                MAX(md.SequenceNumber) AS MaxActual
        FROM    AccountStatementDetail md
        JOIN    AccountStatementHeader mh ON mh.IdStatementHeader = md.IdStatementHeader
        WHERE   md.SequenceNumber > 0
        GROUP BY mh.IdBankAccount
    )
    UPDATE  md
    SET     md.SequenceNumber = r.rn + ISNULL(o.MaxActual, 0)
    FROM    AccountStatementDetail md
    JOIN    RowsToFix r ON r.IdStatementDetail = md.IdStatementDetail
    LEFT JOIN Offsets o ON o.IdBankAccount = r.IdBankAccount;

    PRINT CONCAT('Filas renumeradas: ', @@ROWCOUNT);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH
GO
