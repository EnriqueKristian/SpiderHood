-- =====================================================================================
-- Cuotas Extraordinarias + Multas y Mora
-- =====================================================================================
-- Este script NO se aplica automáticamente: no hay conexión a la base de datos real
-- desde el entorno donde se generó este cambio, así que hay que revisarlo y correrlo
-- a mano contra SQL Server (idealmente primero en un ambiente de prueba).
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
-- Requiere 3 columnas nuevas en Installment (ver ExtraChargeService.cs /
-- Models/Models.cs / Models/enum.cs en el código C#):
--   - Type                 INT              (InstallmentType: 0=Ordinaria, 1=Extraordinaria, 2=Multa, 3=Mora)
--   - Concept               NVARCHAR(200)    (descripción libre del cargo)
--   - SourceInstallmentId   UNIQUEIDENTIFIER (para Multa/Mora: IdInstallment de la cuota Ordinaria que originó el cargo)
--
-- No se necesitan procedimientos NUEVOS: solo ajustar 3 existentes (abajo el detalle
-- de qué agregar a cada uno). BudgetHeader no cambia — BudgetType ya es una columna
-- existente que hoy siempre se guarda vacía.
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- 1) ALTER TABLE — agrega las 3 columnas nuevas a Installment.
--    Con DEFAULT + NOT NULL para que las filas existentes queden como Type=0 (Ordinaria),
--    Concept='' y SourceInstallmentId=00000000-0000-0000-0000-000000000000 sin necesidad
--    de backfill manual.
-- -------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Installment') AND name = 'Type')
BEGIN
    ALTER TABLE dbo.Installment ADD
        [Type] INT NOT NULL CONSTRAINT DF_Installment_Type DEFAULT (0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Installment') AND name = 'Concept')
BEGIN
    ALTER TABLE dbo.Installment ADD
        [Concept] NVARCHAR(200) NOT NULL CONSTRAINT DF_Installment_Concept DEFAULT ('');
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
-- esta cuota de origen?" constantemente al correr el proceso de Multas y Mora.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Installment_SourceInstallmentId' AND object_id = OBJECT_ID('dbo.Installment'))
BEGIN
    CREATE INDEX IX_Installment_SourceInstallmentId ON dbo.Installment (SourceInstallmentId)
        WHERE SourceInstallmentId <> '00000000-0000-0000-0000-000000000000';
END
GO

-- -------------------------------------------------------------------------------------
-- 2) ALTER PROC INS_Installment
--    El código C# (BDLayout.Add.cs) ahora llama a este proc con 3 parámetros nuevos
--    AGREGADOS AL FINAL de la lista existente (los parámetros se pasan posicionalmente,
--    así que el orden importa: no insertar en medio, solo agregar al final):
--
--      EXEC INS_Installment
--          @p0  = IdInstallment,
--          @p1  = IdBudgetHeader,
--          @p2  = UnitName,
--          @p3  = OwnerName,
--          @p4  = CreationDate,
--          @p5  = Amount,
--          @p6  = Percent,
--          @p7  = TotalArea,
--          @p8  = CreatedBy,
--          @p9  = Status,
--          @p10 = IdGroupUnit,
--          @p11 = DueDate,
--          @p12 = Number,
--          @p13 = Type                 <-- NUEVO (INT)
--          @p14 = Concept               <-- NUEVO (NVARCHAR(200))
--          @p15 = SourceInstallmentId   <-- NUEVO (UNIQUEIDENTIFIER)
--
--    Acción manual: abrir el proc actual (sp_helptext INS_Installment) y:
--      a) Agregar los 3 parámetros nuevos a la firma, en ese orden, al final:
--           @Type INT = 0,
--           @Concept NVARCHAR(200) = '',
--           @SourceInstallmentId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'
--      b) Agregar Type, Concept, SourceInstallmentId a la lista de columnas del
--         INSERT INTO Installment (...) y a los VALUES (...) correspondientes.
--    El resto del proc (cómo calcula/inicializa Debt, AmountPaid, Period, etc.) se deja
--    intacto — no se toca nada de esa lógica.
-- -------------------------------------------------------------------------------------

-- -------------------------------------------------------------------------------------
-- 3) ALTER PROC GET_PendingInstallments  y  GET_InstallmentsByBudget
--    ExtraChargeService lee estas cuotas de vuelta con Dapper/EF (FromSqlRaw), que
--    mapea por NOMBRE de columna — una columna de más o de menos no rompe nada, pero
--    sin Type/Concept/SourceInstallmentId en el SELECT esas propiedades vuelven en 0/''/
--    Guid.Empty y el sistema no puede distinguir Ordinaria de Multa/Mora/Extraordinaria
--    ni calcular la mora incremental correctamente.
--
--    Acción manual en AMBOS procs: agregar i.Type, i.Concept, i.SourceInstallmentId
--    (o el alias que corresponda) a la lista de columnas del SELECT, sin tocar el resto
--    del proc (joins, filtros de Debt > 0, orden, etc. quedan igual).
-- -------------------------------------------------------------------------------------

-- -------------------------------------------------------------------------------------
-- Verificación rápida después de aplicar los 3 procs (reemplazar @IdBuilding real):
-- -------------------------------------------------------------------------------------
-- EXEC GET_PendingInstallments @IdBuilding = 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx';
-- -- Debe traer las columnas Type, Concept, SourceInstallmentId (todo en 0/''/GUID vacío
-- -- para las cuotas Ordinarias existentes).
