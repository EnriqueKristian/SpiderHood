-- =============================================================================
-- Carga el flujo de Incidentes en el catálogo de documentación de Workflow
-- (tabla Workflow/WorkflowStep, pantalla /workflow) -- distinto de
-- WorkflowAuditLog (esa es la auditoría real de qué pasó con cada incidente;
-- esta es solo documentación de qué flujos de negocio existen).
--
-- Usa los SPs INS_Workflow/INS_WorkflowStep que ya existen y usa el resto de
-- la app (no un INSERT crudo). Los parámetros están nombrados igual que las
-- propiedades C# (Classes/WorkFlow.cs) -- si el nombre real de algún parámetro
-- en tu SP difiere, SQL Server lo va a rechazar con un error claro ("no es un
-- parámetro..."), no lo va a asignar mal en silencio.
--
-- NO es idempotente (mismo motivo que el item de menú de
-- 2026-09-02_05_Incidents.sql: no puedo confirmar desde acá el nombre real de
-- las tablas para armar un IF NOT EXISTS confiable). Si lo corrés dos veces
-- vas a duplicar el workflow -- revisá /workflow antes de repetirlo.
-- =============================================================================

SET NOCOUNT ON;
GO

DECLARE @IdWorkflow UNIQUEIDENTIFIER = 'C7A1E9D3-4B2F-4A6C-9E5D-1F8B3C7A2D6E';

EXEC dbo.INS_Workflow
    @IdWorkflow = @IdWorkflow,
    @Name = N'Gestión de Incidentes',
    @Description = N'Reclamos/tickets de mantenimiento reportados por Residente o Administrador, con revisión, asignación, resolución y cierre confirmado por quien reportó.',
    @Status = 2; -- WorkflowImplementationStatus.Implementado
GO

DECLARE @IdWorkflow UNIQUEIDENTIFIER = 'C7A1E9D3-4B2F-4A6C-9E5D-1F8B3C7A2D6E';

EXEC dbo.INS_WorkflowStep
    @IdWorkflowStep = NEWID(), @IdWorkflow = @IdWorkflow, @StepOrder = 1,
    @Name = N'Crear', @Description = N'Reportar un incidente nuevo.',
    @Responsible = N'Cualquier usuario (Residente o Administrador)', @IsImplemented = 1;

EXEC dbo.INS_WorkflowStep
    @IdWorkflowStep = NEWID(), @IdWorkflow = @IdWorkflow, @StepOrder = 2,
    @Name = N'Revisar', @Description = N'Confirmar que el reclamo es válido antes de asignarlo.',
    @Responsible = N'Administrador', @IsImplemented = 1;

EXEC dbo.INS_WorkflowStep
    @IdWorkflowStep = NEWID(), @IdWorkflow = @IdWorkflow, @StepOrder = 3,
    @Name = N'Rechazar', @Description = N'Alternativa a Revisar/Asignar -- corta el flujo (duplicado, no corresponde, etc.), con motivo obligatorio.',
    @Responsible = N'Administrador', @IsImplemented = 1;

EXEC dbo.INS_WorkflowStep
    @IdWorkflowStep = NEWID(), @IdWorkflow = @IdWorkflow, @StepOrder = 4,
    @Name = N'Asignar', @Description = N'Se autoasigna y pasa a En Proceso.',
    @Responsible = N'Administrador', @IsImplemented = 1;

EXEC dbo.INS_WorkflowStep
    @IdWorkflowStep = NEWID(), @IdWorkflow = @IdWorkflow, @StepOrder = 5,
    @Name = N'Resolver', @Description = N'Marca el incidente como resuelto, queda esperando confirmación.',
    @Responsible = N'Administrador', @IsImplemented = 1;

EXEC dbo.INS_WorkflowStep
    @IdWorkflowStep = NEWID(), @IdWorkflow = @IdWorkflow, @StepOrder = 6,
    @Name = N'Cerrar', @Description = N'Confirma que se solucionó (o reabre). SysAdmin tiene un cierre administrativo aparte si el reportante nunca responde.',
    @Responsible = N'Usuario que reportó', @IsImplemented = 1;
GO
