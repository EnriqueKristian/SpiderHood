-- =============================================================================
-- Fix: 2026-09-15_116 (rename Employee y Payroll a inglés) renombró
-- BoletaPago/BoletaPagoDetalle -> Payslip/PayslipDetail en todos lados, pero
-- 4 stored procedures usaban la palabra "Boleta" SIN "Pago" (ej.
-- GET_BoletasByPersonal, GET_BoletaById) -- el sed de esa migración sólo
-- cubría "BoletaPago"/"BoletaPagoDetalle" completos, no el caso suelto, así
-- que estos 4 quedaron con su nombre viejo aunque ya devuelven columnas de
-- la tabla Payslip (nueva). BDLayout.Core.cs ya espera los nombres nuevos --
-- visto en vivo: SqlException "Could not find stored procedure
-- 'GET_PayslipsByEmployee'" al abrir el detalle de un Employee.
-- =============================================================================

SET NOCOUNT ON;
GO

EXEC sp_rename 'dbo.GET_BoletaById', 'GET_PayslipById';
EXEC sp_rename 'dbo.GET_BoletasByAccountAndPeriodo', 'GET_PayslipsByAccountAndPeriodo';
EXEC sp_rename 'dbo.GET_BoletasByEmployee', 'GET_PayslipsByEmployee';
EXEC sp_rename 'dbo.GET_PayslipDetailByBoleta', 'GET_PayslipDetailByPayslip';
GO
