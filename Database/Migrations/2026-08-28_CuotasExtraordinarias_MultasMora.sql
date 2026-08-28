-- =====================================================================================
-- Cuotas Extraordinarias + Multas y Mora
-- =====================================================================================
-- Verificado contra el cuerpo real de INS_Installment / GET_PendingInstallments /
-- GET_InstallmentsByBudget (sp_helptext, 2026-08-28). No hay conexión a la base de
-- datos real desde el entorno donde se generó este cambio, así que igual hay que
-- correrlo a mano contra SQL Server (idealmente primero en un ambiente de prueba).
--
-- Qué habilita:
--   1. Cuotas Extraordinarias (fondo de obras, cuotas especiales, etc.) — página
--      /cuotaextraordinaria — reutiliza 100% la tabla Installment existente, solo
--      agrupadas bajo un BudgetHeader con BudgetType = 'Extraordinario' (columna que
--      ya existía y no se usaba).
--   2. Multas y Mora — página /multasymora — genera cargos de Multa (monto fijo,
--      Configuration.FineAmount) y Mora (Deuda x Configuration.LateInterestRate% x
--      meses de atraso) contra las cuotas Ordinarias vencidas, agrupados bajo un
--      BudgetHeader con BudgetType = 'Cargos'.
--
-- GET_PendingInstallments y GET_InstallmentsByBudget hacen SELECT i.* — con eso solo
-- alcanza agregar las columnas a la tabla, NO hace falta tocar esos dos procs.
-- Solo hay que alterar INS_Installment (para insertar las 3 columnas nuevas).
-- =====================================================================================


-- =====================================================================================
-- UP — aplicar
-- =====================================================================================

-- 1) Columnas nuevas en Installment.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Installment') AND name = 'Type')
BEGIN
    ALTER TABLE dbo.Installment ADD
        [Type] INT NOT NULL CONSTRAINT DF_Installment_Type DEFAULT (0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Installment') AND name = 'Concept')
BEGIN
    ALTER TABLE dbo.Installment ADD
        [Concept] NVARCHAR(200) NOT NULL CONSTRAINT DF_Installment_Concept DEFAULT (N'');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Installment') AND name = 'SourceInstallmentId')
BEGIN
    ALTER TABLE dbo.Installment ADD
        [SourceInstallmentId] UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Installment_SourceInstallmentId
            DEFAULT ('00000000-0000-0000-0000-000000000000');
END
GO

-- Índice de apoyo: ExtraChargeService busca "¿ya existe un cargo de Multa/Mora para
-- esta cuota de origen?" en cada corrida del proceso de Multas y Mora.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Installment_SourceInstallmentId' AND object_id = OBJECT_ID('dbo.Installment'))
BEGIN
    CREATE INDEX IX_Installment_SourceInstallmentId ON dbo.Installment (SourceInstallmentId);
END
GO

-- 2) INS_Installment — 3 parámetros nuevos agregados AL FINAL (con DEFAULT, así que
--    cualquier otro caller que todavía llame con los 13 parámetros originales sigue
--    funcionando igual). El código C# (BDLayout.Add.cs) ya manda los 16 en este orden.
ALTER PROCEDURE dbo.INS_Installment
    @IdInstallment  UNIQUEIDENTIFIER,
    @IdBudgetHeader UNIQUEIDENTIFIER,
    @UnitName       NVARCHAR(200),
    @OwnerName      NVARCHAR(200),
    @CreationDate   DATETIME,
    @Amount         DECIMAL(18, 2),
    @Percent        DECIMAL(18, 2),
    @TotalArea      DECIMAL(18, 2),
    @CreatedBy      NVARCHAR(100),
    @Status         INT,
    @IdGroupUnit    UNIQUEIDENTIFIER,
    @DueDate        DATETIME,
    @Number         INT,
    @Type                INT = 0,
    @Concept             NVARCHAR(200) = N'',
    @SourceInstallmentId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'
AS
BEGIN
    INSERT INTO Installment (
        IdInstallment, IdBudgetHeader, UnitName, OwnerName, CreationDate,
        Amount, [Percent], TotalArea, CreatedBy, Status, IdGroupUnit, DueDate, Number,
        [Type], Concept, SourceInstallmentId
    )
    VALUES (
        @IdInstallment, @IdBudgetHeader, @UnitName, @OwnerName, @CreationDate,
        @Amount, @Percent, @TotalArea, @CreatedBy, @Status, @IdGroupUnit, @DueDate, @Number,
        @Type, @Concept, @SourceInstallmentId
    );
END;
GO

-- Verificación rápida (reemplazar @IdBuilding real):
-- EXEC GET_PendingInstallments @IdBuilding = 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx';
-- Debe traer las columnas Type, Concept, SourceInstallmentId (0/''/GUID vacío para las
-- cuotas Ordinarias existentes, ya que ahí no las está seteando nadie todavía).


-- =====================================================================================
-- DOWN — deshacer (para volver al estado anterior y poder probar/reintentar el UP)
-- =====================================================================================

/*

-- 1) INS_Installment de vuelta a la versión original de 13 parámetros.
ALTER PROCEDURE dbo.INS_Installment
    @IdInstallment  UNIQUEIDENTIFIER,
    @IdBudgetHeader UNIQUEIDENTIFIER,
    @UnitName       NVARCHAR(200),
    @OwnerName      NVARCHAR(200),
    @CreationDate   DATETIME,
    @Amount         DECIMAL(18, 2),
    @Percent        DECIMAL(18, 2),
    @TotalArea      DECIMAL(18, 2),
    @CreatedBy      NVARCHAR(100),
    @Status         INT,
    @IdGroupUnit    UNIQUEIDENTIFIER,
    @DueDate        DATETIME,
    @Number         INT
AS
BEGIN
    INSERT INTO Installment (
        IdInstallment, IdBudgetHeader, UnitName, OwnerName, CreationDate,
        Amount, [Percent], TotalArea, CreatedBy, Status, IdGroupUnit, DueDate, Number
    )
    VALUES (
        @IdInstallment, @IdBudgetHeader, @UnitName, @OwnerName, @CreationDate,
        @Amount, @Percent, @TotalArea, @CreatedBy, @Status, @IdGroupUnit, @DueDate, @Number
    );
END;
GO

-- 2) Índice.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Installment_SourceInstallmentId' AND object_id = OBJECT_ID('dbo.Installment'))
BEGIN
    DROP INDEX IX_Installment_SourceInstallmentId ON dbo.Installment;
END
GO

-- 3) Columnas (hay que tumbar el DEFAULT CONSTRAINT antes de poder dropear la columna).
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Installment') AND name = 'Type')
BEGIN
    ALTER TABLE dbo.Installment DROP CONSTRAINT DF_Installment_Type;
    ALTER TABLE dbo.Installment DROP COLUMN [Type];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Installment') AND name = 'Concept')
BEGIN
    ALTER TABLE dbo.Installment DROP CONSTRAINT DF_Installment_Concept;
    ALTER TABLE dbo.Installment DROP COLUMN [Concept];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Installment') AND name = 'SourceInstallmentId')
BEGIN
    ALTER TABLE dbo.Installment DROP CONSTRAINT DF_Installment_SourceInstallmentId;
    ALTER TABLE dbo.Installment DROP COLUMN [SourceInstallmentId];
END
GO

*/
