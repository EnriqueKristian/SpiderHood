-- =============================================================================
-- Fix: TransactionBankDetail.SequenceNumber (usado para el texto "Ingreso
-- #0000"/"Gasto #0000" y, hasta hace poco, para la columna "Referencia" ya
-- retirada de ReconciliationWorkspace.razor) nunca se calculaba en ningún
-- lado -- CargarEstadoCuentaConciliacion.razor arma cada fila del Excel sin
-- asignarlo, y este procedure simplemente insertaba lo que le llegara (0
-- siempre), así que TODAS las filas de TODAS las cargas quedaban en 0000.
--
-- Regla acordada: correlativo POR CUENTA BANCARIA (no por edificio, no
-- separado entre Ingresos/Gastos), que nunca se reinicia. Se calcula server-
-- side en el mismo INSERT (ignora el @SequenceNumber que llega por parámetro
-- -- se deja el parámetro en la firma para no romper la llamada existente
-- desde BDLayout.Add.cs) usando MAX(SequenceNumber)+1 sobre las demás filas
-- de statements de la misma cuenta (vía JOIN a AccountStatementHeader).
--
-- BEGIN TRAN + WITH (UPDLOCK, HOLDLOCK) en el SELECT del máximo: evita que
-- dos cargas concurrentes para la MISMA cuenta bancaria calculen el mismo
-- siguiente número (que sí puede pasar si dos administradores cargan un
-- estado de cuenta al mismo tiempo) -- sin esto, dos INSERT en paralelo
-- podrían leer el mismo MAX antes de que el otro confirme el suyo.
--
-- Filas ya insertadas con SequenceNumber = 0 NO se corrigen acá (fuera de
-- alcance de este script -- avisar si se quiere un script aparte para
-- renumerar el histórico).
-- =============================================================================

CREATE OR ALTER PROCEDURE dbo.INS_AccountStatementDetail
    @IdStatementDetail      UNIQUEIDENTIFIER,
    @IdStatementHeader      UNIQUEIDENTIFIER,
    @StatementDate          DATETIME,
    @Description            VARCHAR(200),
    @ITF                    DECIMAL(18,2),
    @Currency               VARCHAR(10),
    @Amount                 DECIMAL(18,2),
    @SequenceNumber         INT,
    @ReconciliationStatus   BIT = 0,
    @ReconciliationDate     DATETIME = NULL,
    @IdParent               UNIQUEIDENTIFIER = NULL,
    @Origen                 INT = 0
AS
BEGIN
    DECLARE @IdBankAccount UNIQUEIDENTIFIER;
    DECLARE @NextSequence  INT;

    SELECT  @IdBankAccount = mh.IdBankAccount
    FROM    AccountStatementHeader mh
    WHERE   mh.IdStatementHeader = @IdStatementHeader;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT  @NextSequence = ISNULL(MAX(md.SequenceNumber), 0) + 1
        FROM    AccountStatementDetail md WITH (UPDLOCK, HOLDLOCK)
        JOIN    AccountStatementHeader mh ON mh.IdStatementHeader = md.IdStatementHeader
        WHERE   mh.IdBankAccount = @IdBankAccount;

        INSERT INTO AccountStatementDetail (IdStatementDetail, IdStatementHeader, StatementDate, Description, ITF, Currency, Amount, SequenceNumber, ReconciliationStatus, ReconciliationDate, IdParent, Origen)
        VALUES(@IdStatementDetail, @IdStatementHeader, @StatementDate, @Description, @ITF, @Currency, @Amount, @NextSequence, @ReconciliationStatus, @ReconciliationDate, @IdParent, @Origen)

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
