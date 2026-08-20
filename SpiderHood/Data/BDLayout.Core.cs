using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace SpiderHood.Data
{
    public partial class BDLayout(SpiderHoodContext dbContext) : IBDLayout
    {
        private readonly SpiderHoodContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

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
            public const string INS_PaidInstallment = "INS_Installment";
            public const string INS_InstallmentPaid = "INS_InstallmentPaid";

            // Update Procedures
            public const string UPD_MenuItem = "UPD_MenuItem";
            public const string UPD_UserToken = "UPD_UserToken";
            public const string UPD_UserPassword = "UPD_UserPassword";
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
            public const string DEL_Category = "DEL_Category";
            public const string DEL_BudgetHeader = "DEL_BudgetHeader";
            public const string DEL_BudgetDetail = "DEL_BudgetDetail";
            public const string DEL_Exoneration = "DEL_Exoneration";
            public const string DEL_Parameter = "DEL_Parameter";
            public const string DEL_Owner = "DEL_Owner";
            public const string DEL_Unit = "DEL_Unit";
            public const string DEL_Role = "DEL_Role";
            public const string DEL_RolePermissionsByRole = "DEL_RolePermissionsByRole";
            public const string DEL_UserRoleByUser = "DEL_UserRoleByUser";

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
        }
        #endregion

        #region Helper Methods

        // SpiderHoodContext se inyecta como Scoped y varios componentes de una misma
        // página (Header, LeftMenu, la página en sí) disparan sus propias consultas
        // async en OnAfterRenderAsync casi al mismo tiempo, todas contra la misma
        // instancia de DbContext. EF Core no permite operaciones concurrentes sobre
        // la misma instancia y lanza InvalidOperationException ("A second operation
        // was started..."). Reintentamos un par de veces con una espera corta en vez
        // de fallar la página: no resuelve la causa raíz (un DbContext por operación,
        // vía IDbContextFactory), pero evita que ese choque intermitente tumbe cualquier
        // carga de página que coincida con otra en el mismo circuito.
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

            return await _dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
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

            var item = await _dbContext.Set<T>()
                .FromSqlRaw(sql, sqlParams.ToArray())
                .AsNoTracking()
                .ToListAsync();

            return item.FirstOrDefault();
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

            return await _dbContext.Set<T>()
                .FromSqlRaw(sql, sqlParams.ToArray())
                .AsNoTracking()
                .ToListAsync();
        }

        #endregion
    }
}
