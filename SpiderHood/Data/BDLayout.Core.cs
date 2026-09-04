using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace SpiderHood.Data
{
    public partial class BDLayout : IBDLayout
    {
        // Antes BDLayout recibía un SpiderHoodContext ya construido — inyectado como Scoped
        // por circuito de Blazor Server y compartido por TODOS los servicios/páginas que
        // cargan datos en paralelo (LeftMenu, Home, HeaderMainLayout, ...). EF Core no
        // permite operaciones concurrentes sobre la misma instancia, así que ese diseño
        // garantizaba colisiones ("A second operation was started on this context
        // instance...") cada vez que dos componentes cargaban al mismo tiempo; se mitigaban
        // con reintentos, no se eliminaban. Ahora BDLayout recibe la factory y cada
        // operación crea su propio SpiderHoodContext de corta vida, así que dos llamadas
        // concurrentes ya no pueden pisarse: cada una tiene su propia conexión/contexto.
        private readonly IDbContextFactory<SpiderHoodContext>? _contextFactory;
        private readonly SpiderHoodContext? _fixedContext;
        private readonly string? _connectionString;

        public BDLayout(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

            // Algunas consultas abren su propia SqlConnection en paralelo a EF (Dapper puro,
            // sin pasar por el change tracker). La cadena de conexión no cambia en runtime,
            // así que basta con leerla una vez de un contexto descartable.
            using var seedContext = _contextFactory.CreateDbContext();
            _connectionString = seedContext.Database.GetConnectionString();
        }

        // Modo transaccional: algunos servicios necesitan que VARIAS llamadas de BDLayout
        // (todas raw SQL vía ExecuteSqlRawAsync) participen en la MISMA transacción que el
        // caller abrió con context.Database.BeginTransactionAsync(). Si cada llamada creara
        // su propio contexto (modo normal, de arriba), cada una usaría su propia conexión y
        // quedaría fuera de esa transacción — un rollback no revertiría nada. Este
        // constructor reutiliza el contexto que ya trae el caller en vez de crear uno nuevo
        // por operación; el caller es dueño del contexto y de su disposición.
        public BDLayout(SpiderHoodContext existingContext)
        {
            _fixedContext = existingContext ?? throw new ArgumentNullException(nameof(existingContext));
            _connectionString = existingContext.Database.GetConnectionString();
        }

        private async Task<SpiderHoodContext> RentContextAsync(CancellationToken cancellationToken = default)
            => _fixedContext ?? await _contextFactory!.CreateDbContextAsync(cancellationToken);

        private void ReturnContext(SpiderHoodContext context)
        {
            // Solo descartamos el contexto si lo creamos nosotros para esta operación
            // (modo factory). El contexto "fijo" pasado por el caller es responsabilidad
            // del caller, no nuestra.
            if (_fixedContext == null)
            {
                context.Dispose();
            }
        }

        #region Constants and Stored Procedure Names
        private static class StoredProcedures
        {
            // Insert Procedures
            public const string INS_MenuItemPermission = "INS_MenuItemPermission";
            public const string INS_MenuItem = "INS_MenuItem";
            public const string INS_RolePermissions = "INS_RolePermissions";
            public const string INS_Role = "INS_Role";
            public const string INS_UserRole = "INS_UserRole";
            public const string INS_UserBuildingAssociation = "INS_UserBuildingAssociation";
            public const string INS_User = "INS_User";
            public const string INS_Building = "INS_Building";
            public const string INS_Category = "INS_Category";
            public const string INS_Exoneration = "INS_Exoneration";
            public const string INS_Periods = "INS_Periods";
            public const string INS_ServiceReading = "INS_ServiceReading";
            public const string INS_ServiceReadingDetail = "INS_ServiceReadingDetail";
            public const string INS_Contact = "INS_Contact";
            public const string INS_BankAccount = "INS_BankAccount";
            public const string INS_BuildingConfiguration = "INS_BuildingConfiguration";
            public const string INS_MovementHeader = "INS_MovementHeader";
            public const string INS_Expense = "INS_Expense";
            public const string INS_AccountStatementDetail = "INS_AccountStatementDetail";
            public const string INS_Owner = "INS_Owner";
            public const string INS_GroupOwner = "INS_GroupOwner";
            public const string INS_OwnerGroupOwner = "INS_OwnerGroupOwner";
            public const string INS_GroupUnitOwner = "INS_GroupUnitOwner";
            public const string INS_InstallmentExoneration = "INS_InstallmentExoneration";
            public const string INS_Parameter = "INS_Parameter";
            public const string INS_Unit = "INS_Unit";
            public const string INS_BudgetHeader = "INS_BudgetHeader";
            public const string INS_BudgetDetail = "INS_BudgetDetail";
            public const string INS_Installment = "INS_Installment";
            public const string INS_InstallmentPaid = "INS_InstallmentPaid";

            // Update Procedures
            public const string UPD_MenuItem = "UPD_MenuItem";
            public const string UPD_UserToken = "UPD_UserToken";
            public const string UPD_UserPassword = "UPD_UserPassword";
            public const string UPD_UserBuildingUnit = "UPD_UserBuildingUnit";
            public const string UPD_Building = "UPD_Building";
            public const string UPD_BudgetDetail = "UPD_BudgetDetail";
            public const string UPD_ServiceReading = "UPD_ServiceReading";
            public const string UPD_Contact = "UPD_Contact";
            public const string UPD_ExpenseReconcilied = "UPD_ExpenseReconcilied";
            public const string UPD_Expense = "UPD_Expense";
            public const string UPD_Parameter = "UPD_Parameter";
            public const string UPD_GroupOwner = "UPD_GroupOwner";
            public const string UPD_Unit = "UPD_Unit";
            public const string UPD_UnsetOtherCurrentPeriods = "UPD_UnsetOtherCurrentPeriods";
            public const string UPD_Period = "UPD_Period";
            public const string UPD_SetPeriodAsCurrent = "UPD_SetPeriodAsCurrent";
            public const string UPD_BankAccount = "UPD_BankAccount";
            public const string UPD_BuildingConfiguration = "UPD_BuildingConfiguration";
            public const string UPD_BudgetHeader = "UPD_BudgetHeader";
            public const string UPD_ClosePastBudgets = "UPD_ClosePastBudgets";
            public const string UPD_Category = "UPD_Category";
            public const string UPD_Owner = "UPD_Owner";
            public const string UPD_InstallmentState = "UPD_InstallmentState";
            public const string UPD_Role = "UPD_Role";
            public const string UPD_USER = "UPD_USER";

            // Delete Procedures
            public const string DEL_MenuItemPermission = "DEL_MenuItemPermission";
            public const string DEL_MenuItem = "DEL_MenuItem";
            public const string DEL_Category = "DEL_Category";
            public const string DEL_BudgetHeader = "DEL_BudgetHeader";
            public const string DEL_BudgetDetail = "DEL_BudgetDetail";
            public const string DEL_Exoneration = "DEL_Exoneration";
            public const string DEL_Parameter = "DEL_Parameter";
            public const string DEL_Period = "DEL_Period";
            public const string DEL_Owner = "DEL_Owner";
            public const string DEL_Unit = "DEL_Unit";
            public const string DEL_Role = "DEL_Role";
            public const string DEL_RolePermissionsByRole = "DEL_RolePermissionsByRole";
            public const string DEL_UserRoleByUser = "DEL_UserRoleByUser";
            public const string DEL_InstallmentPaidByTransaction = "DEL_InstallmentPaidByTransaction";

            // Get Procedures
            public const string GET_AllMenuPemission = "GET_AllMenuPemission";
            public const string GET_RoleById = "GET_RoleById";
            public const string GET_MenuItem = "GET_MenuItem";
            public const string GET_AllRoles = "GET_AllRoles";
            public const string GET_ALLPermissions = "GET_ALLPermissions";
            public const string GET_PermissionsByRole = "GET_PermissionsByRole";
            public const string GET_FullMenu = "GET_FullMenu";
            public const string GET_UserById = "GET_UserById";
            public const string GET_InvitationByCode = "GET_InvitationByCode";
            public const string GET_AllBuildings = "GET_AllBuildings";
            public const string GET_BuildingById = "GET_BuildingById";
            public const string GET_Building = "GET_Building";
            public const string GET_AllMovementDetail = "GET_AllMovementDetail";
            public const string GET_BankTransactionsNoConcilied = "GET_BankTransactionsNoConcilied";
            public const string GET_MovementByName = "GET_MovementByName";
            public const string GET_MovementHeaders = "GET_MovementHeaders";
            public const string GET_AccountStatementDetailByHeader = "GET_AccountStatementDetailByHeader";
            public const string GET_UnitsByBuilding = "GET_UnitsByBuilding";
            public const string GET_UnitsByType = "GET_UnitsByType";
            public const string GET_AllParameters = "GET_AllParameters";
            public const string GET_ListParameterParent = "GET_ListParameterParent";
            public const string GET_Budgets = "GET_Budgets";
            public const string GET_BudgetDetails_Sum = "GET_BudgetDetails_Sum";
            public const string GET_ExpensesByBuilding = "GET_ExpensesByBuilding";
            public const string GET_OwnerByBuilding = "GET_OwnerByBuilding";
            public const string GET_Categories = "GET_Categories";
            public const string GET_CategoryById = "GET_CategoryById";
            public const string GET_BudgetById = "GET_BudgetById";
            public const string GET_Exoneration_All = "GET_Exoneration_All";
            public const string GET_ExonerationByBudgetHeader = "GET_ExonerationByBudgetHeader";
            public const string GET_PeriodsByBuilding = "GET_PeriodsByBuilding";
            public const string GET_BankAccountsByBuilding = "GET_BankAccountsByBuilding";
            public const string GET_BuildingConfiguration = "GET_BuildingConfiguration";
            public const string GET_ServiceReadingList = "GET_ServiceReadingList";
            public const string GET_InstallmentsByBudget = "GET_InstallmentsByBudget";
            public const string GET_PendingInstallments = "GET_PendingInstallments";
            public const string GET_ServiceReading = "GET_ServiceReading";
            public const string GET_ServiceReadingDetailList = "GET_ServiceReadingDetailList";
            public const string GET_FirstWaterReadingDetailList = "GET_FirstWaterReadingDetailList";
            public const string GET_BudgetDetailDefault = "GET_BudgetDetailDefault";
            public const string GET_List_BudgetDetail = "GET_List_BudgetDetail";
            public const string GET_AllContacts = "GET_AllContacts";
            public const string GET_PendingConciliationExpenses = "GET_PendingConciliationExpenses";
            public const string GET_InstallmentPaid = "GET_InstallmentPaid";
            public const string GET_UsersByEmail = "GET_UsersByEmail";
            public const string GET_UserBuildingAssociation = "GET_UserBuildingAssociation";
            public const string GET_AllBuildingsConfig = "GET_AllBuildingsConfig";
            public const string GET_AllUsersWithRoles = "GET_AllUsersWithRoles";
            public const string GET_RoleByUserId = "GET_RoleByUserId";
            public const string GET_AllUserBuildingRoles = "GET_AllUserBuildingRoles";
            public const string INS_UserBuildingRole = "INS_UserBuildingRole";
            public const string DEL_UserBuildingRole = "DEL_UserBuildingRole";
            public const string GET_AllBuildingsPublic = "GET_AllBuildingsPublic";
            public const string GET_TemplateBuilding = "GET_TemplateBuilding";
            public const string GET_MixtoParameterCandidates = "GET_MixtoParameterCandidates";
            public const string UPD_PromoteParameterToGlobal = "UPD_PromoteParameterToGlobal";
            public const string UPD_MergeParameterInto = "UPD_MergeParameterInto";
            public const string UPD_UserBuildingApproval = "UPD_UserBuildingApproval";
            public const string INS_Workflow = "INS_Workflow";
            public const string UPD_Workflow = "UPD_Workflow";
            public const string DEL_Workflow = "DEL_Workflow";
            public const string GET_Workflows = "GET_Workflows";
            public const string INS_WorkflowStep = "INS_WorkflowStep";
            public const string UPD_WorkflowStep = "UPD_WorkflowStep";
            public const string DEL_WorkflowStep = "DEL_WorkflowStep";
            public const string GET_WorkflowStepsByWorkflow = "GET_WorkflowStepsByWorkflow";

            // Subscription Procedures (Docs/Design-Subscripcion-Administrador.md)
            public const string INS_Subscription = "INS_Subscription";
            public const string GET_SubscriptionByUser = "GET_SubscriptionByUser";
            public const string GET_AllSubscriptionPlans = "GET_AllSubscriptionPlans";

            // Audit Procedures (ver BDLayout.Audit.cs)
            public const string UPD_BuildingAudit = "UPD_BuildingAudit";
            public const string UPD_OwnerAudit = "UPD_OwnerAudit";
            public const string UPD_BudgetHeaderAudit = "UPD_BudgetHeaderAudit";
            public const string UPD_ExpenseAudit = "UPD_ExpenseAudit";
            public const string UPD_PeriodAudit = "UPD_PeriodAudit";
            public const string UPD_ServiceReadingAudit = "UPD_ServiceReadingAudit";
            public const string UPD_BankAccountAudit = "UPD_BankAccountAudit";
            public const string UPD_CategoryAudit = "UPD_CategoryAudit";
            public const string UPD_BuildingConfigurationAudit = "UPD_BuildingConfigurationAudit";

            // Workflow Audit Procedures
            public const string INS_WorkflowAuditLog = "INS_WorkflowAuditLog";
            public const string GET_WorkflowAuditLog = "GET_WorkflowAuditLog";

            // System Log Procedures
            public const string INS_SystemLog = "INS_SystemLog";
            public const string GET_SystemLogSettings = "GET_SystemLogSettings";
            public const string UPD_SystemLogSettings = "UPD_SystemLogSettings";
            public const string GET_SystemLogs_Recent = "GET_SystemLogs_Recent";
            public const string DEL_SystemLogOlderThan = "DEL_SystemLogOlderThan";

            // Incident Procedures
            public const string INS_Incident = "INS_Incident";
            public const string UPD_IncidentStatus = "UPD_IncidentStatus";
            public const string GET_IncidentsByBuilding = "GET_IncidentsByBuilding";
            public const string GET_IncidentsByReporter = "GET_IncidentsByReporter";
            public const string GET_IncidentById = "GET_IncidentById";
            public const string INS_IncidentComment = "INS_IncidentComment";
            public const string GET_IncidentCommentsByIncident = "GET_IncidentCommentsByIncident";

            // Calendar Procedures
            public const string INS_CalendarItem = "INS_CalendarItem";
            public const string UPD_CalendarItem = "UPD_CalendarItem";
            public const string UPD_CalendarItemStatus = "UPD_CalendarItemStatus";
            public const string DEL_CalendarItem = "DEL_CalendarItem";
            public const string GET_CalendarItemsByBuilding = "GET_CalendarItemsByBuilding";
            public const string GET_CalendarItemById = "GET_CalendarItemById";
        }
        #endregion

        #region Helper Methods

        // Cada operación de BDLayout ahora crea su propio SpiderHoodContext de corta vida
        // (ver el constructor), así que la colisión de concurrencia que este reintento
        // mitigaba ya no debería ocurrir DESDE BDLayout. Se deja como red de seguridad:
        // otros servicios del proyecto todavía comparten un SpiderHoodContext inyectado
        // como Scoped y podrían seguir generando el mismo InvalidOperationException hasta
        // que también migren a IDbContextFactory.
        private const int MaxConcurrencyRetries = 3;

        private static bool IsConcurrentDbContextUsage(Exception ex) =>
            ex is InvalidOperationException &&
            ex.Message.Contains("A second operation was started on this context instance", StringComparison.OrdinalIgnoreCase);

        private async Task<T> ExecuteWithErrorHandlingAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            for (int attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
            {
                try
                {
                    //_logger.LogDebug($"Excute operation {operation.ToString()}");
                    return await operation();
                }
                catch (Exception ex) when (IsConcurrentDbContextUsage(ex) && attempt < MaxConcurrencyRetries)
                {
                    await Task.Delay(75 * attempt, cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    // _logger.LogError(ex, "Database update error during {OperationName}: {Message}", operationName, ex.Message);
                    throw new RepositoryException($"Database update failed for {operationName}", ex);
                }
                catch (Exception ex)
                {
                    //_logger.LogError(ex, "Error during {OperationName}: {Message}", operationName, ex.Message);
                    throw new RepositoryException($"Operation {operationName} failed", ex);
                }
            }

            throw new RepositoryException($"Operation {operationName} failed after {MaxConcurrencyRetries} attempts");
        }

        private void ValidateEntity<T>(T entity, string entityName) where T : class
        {
            if (entity == null)
            {
                throw new ArgumentNullException(entityName, $"{entityName} cannot be null");
            }
        }

        private async Task<int> ExecuteStoredProcedureAsync(
            string storedProcedureName,
            CancellationToken cancellationToken = default,
            params object[] parameters)
        {
            var paramString = string.Join(",", parameters.Select((_, i) => $"{{{i}}}"));
            var sql = $"{storedProcedureName} {paramString}";

            //_logger.LogDebug("Executing stored procedure: {Sql}", sql);

            var dbContext = await RentContextAsync(cancellationToken);
            try
            {
                return await dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
            }
            finally
            {
                ReturnContext(dbContext);
            }
        }

        private async Task<T?> ExecuteQuerySingleAsync<T>(
            string storedProcedureName,
            params object[] parameters) where T : class
        {
            // Build parameter list for SQL
            var paramNames = new List<string>();
            var sqlParams = new List<object>();

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramName = $"@p{i}";
                paramNames.Add(paramName);

                // Create SqlParameter for better type handling
                sqlParams.Add(new SqlParameter(paramName, parameters[i] ?? DBNull.Value));
            }

            var sql = $"EXEC {storedProcedureName} {string.Join(", ", paramNames)}";

            var dbContext = await RentContextAsync();
            try
            {
                var item = await dbContext.Set<T>()
                    .FromSqlRaw(sql, sqlParams.ToArray())
                    .AsNoTracking()
                    .ToListAsync();

                return item.FirstOrDefault();
            }
            finally
            {
                ReturnContext(dbContext);
            }
        }

        // FIXED: Use FromSqlRaw with EXEC and call AsEnumerable() for client-side evaluation
        private async Task<List<T>> ExecuteQueryListAsync<T>(
            string storedProcedureName,
            params object[] parameters) where T : class
        {
            // Build parameter list for SQL
            var paramNames = new List<string>();
            var sqlParams = new List<object>();

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramName = $"@p{i}";
                paramNames.Add(paramName);

                // Create SqlParameter for better type handling
                sqlParams.Add(new SqlParameter(paramName, parameters[i] ?? DBNull.Value));
            }

            var sql = $"EXEC {storedProcedureName} {string.Join(", ", paramNames)}";

            var dbContext = await RentContextAsync();
            try
            {
                return await dbContext.Set<T>()
                    .FromSqlRaw(sql, sqlParams.ToArray())
                    .AsNoTracking()
                    .ToListAsync();
            }
            finally
            {
                ReturnContext(dbContext);
            }
        }

        #endregion
    }
}