-- =============================================================================
-- Carga el flujo de Presupuestos en el catálogo de documentación de Workflow
-- (tabla Workflow/WorkflowStep, pantalla /workflow) -- distinto de
-- WorkflowAuditLog (esa es la auditoría real de qué pasó con cada presupuesto;
-- esta es solo documentación de qué flujos de negocio existen).
--
-- Usa los SPs INS_Workflow/INS_WorkflowStep que ya existen y usa el resto de
-- la app (no un INSERT crudo). Los parámetros están nombrados igual que las
-- propiedades C# (Classes/WorkFlow.cs) -- si el nombre real de algún parámetro
-- en tu SP difiere, SQL Server lo va a rechazar con un error claro ("no es un
-- parámetro..."), no lo va a asignar mal en silencio.
--
-- Todos los Id van como literales fijos (ni DECLARE ni NEWID() inline dentro
-- del EXEC -- son los dos errores de sintaxis que tiró la vez pasada con el
-- script de Incidentes). Es la forma más simple y segura de que corra igual
-- sin importar cómo se ejecute el script.
--
-- NO es idempotente (mismo motivo que 2026-09-02_09_Seed_IncidentWorkflowCatalog.sql).
-- Si lo corrés dos veces vas a duplicar el workflow -- revisá /workflow antes
-- de repetirlo.
-- =============================================================================

SET NOCOUNT ON;
GO

EXEC dbo.INS_Workflow
    @IdWorkflow = '01017DB9-7A37-4AD4-BF7D-0AB30AA2562B',
    @Name = N'Gestión de Presupuestos',
    @Description = N'Generación del presupuesto/cuotas mensuales, con aprobación de Junta antes de publicarlo a los residentes.',
    @Status = 2; -- WorkflowImplementationStatus.Implementado
GO

EXEC dbo.INS_WorkflowStep
    @IdWorkflowStep = 'FE8043E2-5F07-4FE0-95F3-3726CDCB8BD4', @IdWorkflow = '01017DB9-7A37-4AD4-BF7D-0AB30AA2562B', @StepOrder = 1,
    @Name = N'Crear', @Description = N'Generar el presupuesto/cuota mensual a partir de los gastos del período.',
    @Responsible = N'Administrador', @IsImplemented = 1;
GO

EXEC dbo.INS_WorkflowStep
    @IdWorkflowStep = '4B277D93-ED75-4173-93F0-5B1E0B1823D7', @IdWorkflow = '01017DB9-7A37-4AD4-BF7D-0AB30AA2562B', @StepOrder = 2,
    @Name = N'Enviar a Aprobación', @Description = N'Somete el presupuesto generado a la Junta para su revisión.',
    @Responsible = N'Administrador', @IsImplemented = 1;
GO

EXEC dbo.INS_WorkflowStep
    @IdWorkflowStep = '7A87F251-FCD6-48D6-8C22-7A3F5718C5B8', @IdWorkflow = '01017DB9-7A37-4AD4-BF7D-0AB30AA2562B', @StepOrder = 3,
    @Name = N'Aprobar', @Description = N'Aprueba el presupuesto, queda listo para publicar.',
    @Responsible = N'Junta', @IsImplemented = 1;
GO

EXEC dbo.INS_WorkflowStep
    @IdWorkflowStep = '2E6EF035-19CA-49CB-8806-C6EDF8117FC9', @IdWorkflow = '01017DB9-7A37-4AD4-BF7D-0AB30AA2562B', @StepOrder = 4,
    @Name = N'Rechazar', @Description = N'Alternativa a Aprobar -- corta el flujo y vuelve a Administrador para ajustar el presupuesto.',
    @Responsible = N'Junta', @IsImplemented = 1;
GO

EXEC dbo.INS_WorkflowStep
    @IdWorkflowStep = 'B9751CA0-8ED0-4F92-BA82-C8F811FEBFDB', @IdWorkflow = '01017DB9-7A37-4AD4-BF7D-0AB30AA2562B', @StepOrder = 5,
    @Name = N'Publicar', @Description = N'Publica el presupuesto aprobado -- genera las cuotas visibles para los residentes.',
    @Responsible = N'Administrador', @IsImplemented = 1;
GO

EXEC dbo.INS_WorkflowStep
    @IdWorkflowStep = 'B4B13B08-69AA-4E84-AE65-BB818E4C0525', @IdWorkflow = '01017DB9-7A37-4AD4-BF7D-0AB30AA2562B', @StepOrder = 6,
    @Name = N'Cerrar', @Description = N'Cierra el período presupuestario una vez conciliados los pagos.',
    @Responsible = N'Administrador', @IsImplemented = 1;
GO
