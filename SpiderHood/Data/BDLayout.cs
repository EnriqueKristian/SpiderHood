using BlazorBootstrap;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using DocumentFormat.OpenXml.Vml.Spreadsheet;
using Humanizer.Localisation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Security;

namespace SpiderHood.Data
{
    public interface IBDLayout
    {
        // Common interface methods could be defined here if needed
    }

    public class BDLayout(SpiderHoodContext dbContext) : IBDLayout
    {
        private readonly SpiderHoodContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        //private readonly ILogger<BDLayout> _logger;
        //private SpiderHoodContext context;

        /*public BDLayout(SpiderHoodContext context)
        {
            this.context = context;
        }*/

        #region Constants and Stored Procedure Names
        private static class StoredProcedures
        {
            // Insert Procedures
            public const string INS_MenuItemPermission = "INS_MenuItemPermission";
            public const string INS_MenuItem = "INS_MenuItem";
            public const string INS_RolePermissions = "INS_RolePermissions";
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

            // Delete Procedures
            public const string DEL_MenuItemPermission = "DEL_MenuItemPermission";
            public const string DEL_Category = "DEL_Category";
            public const string DEL_BudgetHeader = "DEL_BudgetHeader";
            public const string DEL_BudgetDetail = "DEL_BudgetDetail";
            public const string DEL_Exoneration = "DEL_Exoneration";
            public const string DEL_Parameter = "DEL_Parameter";
            public const string DEL_Owner = "DEL_Owner";
            public const string DEL_Unit = "DEL_Unit";

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
        }
        #endregion

        #region Helper Methods
        private async Task<T> ExecuteWithErrorHandlingAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                //_logger.LogDebug($"Excute operation {operation.ToString()}");
                return await operation();
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

        #region Delete Operations

        public async Task<bool> DeleteRecordAsync(MenuPermissions item, CancellationToken cancellationToken = default)
        {
            ValidateEntity(item, nameof(item));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_MenuItemPermission, cancellationToken, item.MenuId);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "DeleteMenuPermission", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(Category category, CancellationToken cancellationToken = default)
        {
            ValidateEntity(category, nameof(category));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_Category, cancellationToken, category.IdCategory );
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "DeleteCategory", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(BudgetHeader budgetHeader, CancellationToken cancellationToken = default)
        {
            ValidateEntity(budgetHeader, nameof(budgetHeader));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_BudgetHeader, cancellationToken, budgetHeader.IdBudgetHeader);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "DeleteBudgetHeader", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(Guid idBudgetHeader, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_BudgetDetail, cancellationToken, idBudgetHeader);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "DeleteBudgetDetailByHeader", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(BudgetDetail budgetDetail, CancellationToken cancellationToken = default)
        {
            ValidateEntity(budgetDetail, nameof(budgetDetail));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_BudgetDetail, cancellationToken, budgetDetail.IdBudgetDetail);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "DeleteBudgetDetail", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(Exoneration exoneration, CancellationToken cancellationToken = default)
        {
            ValidateEntity(exoneration, nameof(exoneration));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.DEL_Exoneration,
                    cancellationToken,
                    exoneration.IdExoneration,
                    exoneration.UpdatedBy);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "DeleteExoneration", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(Parameter parameter, CancellationToken cancellationToken = default)
        {
            ValidateEntity(parameter, nameof(parameter));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_Parameter, cancellationToken, parameter.IdTabla);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "DeleteParameter", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(Owner owner, CancellationToken cancellationToken = default)
        {
            ValidateEntity(owner, nameof(owner));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_Owner, cancellationToken, owner.IdOwner);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "DeleteOwner", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(RealEstateUnit unit, CancellationToken cancellationToken = default)
        {
            ValidateEntity(unit, nameof(unit));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_Unit, cancellationToken, unit.IdUnit);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "DeleteUnit", cancellationToken);
        }
        #endregion

        #region Add Operations

        public async Task<MenuPermissions> AddNewRecordAsync(MenuPermissions item, CancellationToken cancellationToken = default)
        {
            ValidateEntity(item, nameof(item));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_MenuItemPermission,
                    cancellationToken,
                    item.MenuId!,
                    item.PermissionId!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return item;
            }, "AddMenuItemPermission", cancellationToken);
        }

        public async Task<MenuItemDefinition> AddNewRecordAsync(MenuItemDefinition item, CancellationToken cancellationToken = default)
        {
            ValidateEntity(item, nameof(item));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_MenuItem,
                    cancellationToken,
                    item.MenuId!,
                    item.ParentId!,
                    item.ItemKey!,
                    item.Title!,
                    item.Icon!,
                    item.Url!,
                    item.Target!,
                    item.DisplayOrder!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return item;
            }, "AddMenuItem", cancellationToken);
        }

        public async Task<Models.RolePermissions> AddNewRecordAsync(Models.RolePermissions permissions, CancellationToken cancellationToken = default)
        {
            ValidateEntity(permissions, nameof(permissions));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_RolePermissions,
                    cancellationToken,
                    permissions.IdRole!,
                    permissions.IdPermission!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return permissions;
            }, "AddRolePermissions", cancellationToken);
        }

        public async Task<Models.InstallmentExoneration> AddNewRecordAsync(Models.InstallmentExoneration exoneration, CancellationToken cancellationToken = default)
        {
            ValidateEntity(exoneration, nameof(exoneration));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_InstallmentExoneration,
                    cancellationToken,
                    exoneration.IdBuilding!,
                    exoneration.IdBudgetHeader!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return exoneration;
            }, "AddInstallmentExoneration", cancellationToken);
        }

        public async Task<Models.UserModel> AddNewRecordAsync(Models.UserModel user, CancellationToken cancellationToken = default)
        {
            ValidateEntity(user, nameof(user));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_User,
                    cancellationToken,
                    user.IdUser!,
                    user.Email!,
                    user.PasswordHash!,
                    user.FirstName!,
                    user.LastName!,
                    user.PhoneNumber!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return user;
            }, "AddInstallmentExoneration", cancellationToken);
        }

        public async Task<Models.InstallmentPaid> AddNewRecordAsync(Models.InstallmentPaid paid, CancellationToken cancellationToken = default)
        {
            ValidateEntity(paid, nameof(paid));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_InstallmentPaid,
                    cancellationToken,
                    paid.IdPaid,
                    paid.IdInstallment,
                    paid.Amount,
                    paid.CreatedBy,
                    paid.Status,
                    paid.PaymentDate,
                    paid.IdTransaction,
                    paid.IsAutoReconcile,
                    paid.IsPartialPayment);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return paid;
            }, "AddInstallmentPaid", cancellationToken);
        }

        public async Task<Models.Installment> AddNewRecordAsync(Models.Installment installment, CancellationToken cancellationToken = default)
        {
            ValidateEntity(installment, nameof(installment));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_Installment,
                    cancellationToken,
                    installment.IdInstallment!,
                    installment.IdBudgetHeader!,
                    installment.UnitName,
                    installment.OwnerName,
                    installment.CreationDate, 
                    installment.Amount, 
                    installment.Percent,
                    installment.TotalArea,
                    installment.CreatedBy,
                    installment.Status,
                    installment.IdGroupUnit,
                    installment.DueDate,
                    installment.Number);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return installment;
            }, "AddInstallment", cancellationToken);
        }

        public async Task<Models.ViewExpense> AddNewRecordAsync(Models.ViewExpense viewexpense, CancellationToken cancellationToken = default)
        {
            ValidateEntity(viewexpense, nameof(viewexpense));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_Expense,
                    cancellationToken,
                    viewexpense.IdExpense!,
                    viewexpense.Description!,
                    viewexpense.Amount!,
                    viewexpense.IncludeInQuota!,
                    viewexpense.ExpenseDate!,
                    viewexpense.Distribution!,
                    viewexpense.Supplier!,
                    viewexpense.IdBuilding!,
                    viewexpense.ReconciledTransactionId!,
                    viewexpense.IdCategory!,
                    viewexpense.Notes!,
                    viewexpense.Status!,
                    viewexpense.PaymentMethod!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return viewexpense;
            }, "AddViewExpense", cancellationToken);
        }

        public async Task<Models.OwnerGroupOwner> AddNewRecordAsync(Models.OwnerGroupOwner ownergroupowner, CancellationToken cancellationToken = default)
        {
            ValidateEntity(ownergroupowner, nameof(ownergroupowner));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_OwnerGroupOwner,
                    cancellationToken,
                    ownergroupowner.IdGroupOwner!,
                    ownergroupowner.IdOwner!,
                    ownergroupowner.TypeOwner!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return ownergroupowner;
            }, "AddOwnerUnit", cancellationToken);
        }

        public async Task<Models.OwnerUnit> AddNewRecordAsync(Models.OwnerUnit ownerunit, CancellationToken cancellationToken = default)
        {
            ValidateEntity(ownerunit, nameof(ownerunit));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_GroupOwner,
                    cancellationToken,
                    ownerunit.IdGroupOwner!,
                    ownerunit.IdOwner!,
                    ownerunit.GroupName!,
                    ownerunit.AreaTotal!,
                    ownerunit.TypeOwner!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return ownerunit;
            }, "AddOwnerUnit", cancellationToken);
        }

        public async Task<Models.GroupUnit> AddNewRecordAsync(Models.GroupUnit groupunit, CancellationToken cancellationToken = default)
        {
            ValidateEntity(groupunit, nameof(groupunit));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_GroupUnitOwner,
                    cancellationToken,
                    groupunit.IdUnit!,
                    groupunit.IdGroupOwner!,
                    groupunit.TypeGroupUnit!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return groupunit;
            }, "AddGroupUnit", cancellationToken);
        }

        public async Task<Models.Parameter> AddNewRecordAsync(Models.Parameter parameter, CancellationToken cancellationToken = default)
        {
            ValidateEntity(parameter, nameof(parameter));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_Parameter,
                    cancellationToken,
                    parameter.IdTabla!,
                    parameter.Description!,
                    parameter.ShortDescription!,
                    parameter.Value!,
                    parameter.Sort!,
                    parameter.IdParent!,
                    parameter.Estado!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return parameter;
            }, "AddParameter", cancellationToken);
        }

        public async Task<BuildingConfiguration> AddNewRecordAsync(BuildingConfiguration configuration, CancellationToken cancellationToken = default)
        {
            ValidateEntity(configuration, nameof(configuration));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_BuildingConfiguration,
                    cancellationToken,
                    configuration.IdBuildingConfiguration!,
                    configuration.Currency!,
                    configuration.PaymentMethods!,
                    configuration.PaymentPeriod!,
                    configuration.DueDay!,
                    configuration.FineAmount!,
                    configuration.LateInterestRate!,
                    configuration.InvoiceDay!,
                    configuration.MinWaterConsumtion!,
                    configuration.DefaultFixedCharge!,
                    configuration.IdBuilding!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return configuration;
            }, "AddBuildingConfiguration", cancellationToken);
        }

        public async Task<TransactionBankHeader> AddNewRecordAsync(TransactionBankHeader movementheader, CancellationToken cancellationToken = default)
        {
            ValidateEntity(movementheader, nameof(movementheader));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_MovementHeader,
                    cancellationToken,
                    movementheader.IdStatementHeader!,
                    movementheader.FileName!,
                    movementheader.IdUser!,
                    movementheader.TotalRecords!,
                    movementheader.UploadState!,
                    movementheader.IdBankAccount!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return movementheader;
            }, "AddMovementHeader", cancellationToken);
        }

        public async Task<TransactionBankDetail> AddNewRecordAsync(TransactionBankDetail movementdetail, CancellationToken cancellationToken = default)
        {
            ValidateEntity(movementdetail, nameof(movementdetail));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_AccountStatementDetail,
                    cancellationToken,
                    movementdetail.IdStatementDetail,
                    movementdetail.IdStatementHeader      ,
                    movementdetail.StatementDate          ,
                    movementdetail.Description            ,
                    movementdetail.ITF                    ,
                    movementdetail.Currency               ,
                    movementdetail.Amount                 ,
                    movementdetail.SequenceNumber         ,
                    movementdetail.ReconciliationStatus   ,
                    movementdetail.ReconciliationDate!    ,
                    movementdetail.IdParent               ,
                    movementdetail.Origen);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return movementdetail;
            }, "AddMovementDetail", cancellationToken);
        }

        public async Task<Expense> AddNewRecordAsync(Expense expense, CancellationToken cancellationToken = default)
        {
            ValidateEntity(expense, nameof(expense));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_Expense,
                    cancellationToken,
                    expense.ExpenseDescription!,
                    expense.TotalAmount!,
                    expense.IsIncludedInQuota!,
                    expense.DueDate!,
                    expense.IdDistribution!,
                    expense.IdBuilding!,
                    expense.IdSubCategory!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return expense;
            }, "AddExpense", cancellationToken);
        }

        public async Task<BankAccount> AddNewRecordAsync(BankAccount bankaccount, CancellationToken cancellationToken = default)
        {
            ValidateEntity(bankaccount, nameof(bankaccount));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_BankAccount,
                    cancellationToken,
                    bankaccount.IdBankAccount!,
                    bankaccount.AccountName!,
                    bankaccount.AccountNumber!,
                    bankaccount.BankName!,
                    bankaccount.AccountType!,
                    bankaccount.IdBuilding!,
                    bankaccount.Status!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return bankaccount;
            }, "AddExoneration", cancellationToken);
        }

        public async Task<Exoneration> AddNewRecordAsync(Exoneration exoneration, CancellationToken cancellationToken = default)
        {
            ValidateEntity(exoneration, nameof(exoneration));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_Exoneration,
                    cancellationToken,
                    exoneration.IdExoneration,
                    exoneration.IdGroupUnit,
                    exoneration.IdCategory,
                    exoneration.Description,
                    exoneration.IsActive,
                    exoneration.CreatedBy,
                    exoneration.UpdatedBy,
                    exoneration.IdBuilding);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return exoneration;
            }, "AddExoneration", cancellationToken);
        }

        public async Task<Period> AddNewRecordAsync(Period period, CancellationToken cancellationToken = default)
        {
            ValidateEntity(period, nameof(period));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_Periods,
                    cancellationToken,
                    period.IdPeriod,
                    period.Name,
                    period.PeriodType,
                    period.StartDate,
                    period.EndDate,
                    period.ClosingDate,
                    period.Status,
                    period.IsCurrentPeriod,
                    period.Description,
                    period.IdBuilding);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return period;
            }, "AddPeriod", cancellationToken);
        }

        public async Task<ServiceReading> AddNewRecordAsync(ServiceReading serviceReading, CancellationToken cancellationToken = default)
        {
            ValidateEntity(serviceReading, nameof(serviceReading));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_ServiceReading,
                    cancellationToken,
                    serviceReading.IdServiceReading,
                    serviceReading.Period,
                    serviceReading.Status,
                    serviceReading.IdBuilding,
                    serviceReading.FileName,
                    serviceReading.TotalAmount,
                    serviceReading.IdPeriod);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return serviceReading;
            }, "AddServiceReading", cancellationToken);
        }

        public async Task AddNewRecordAsync(List<ServiceReadingDetail> serviceReadingDetails, CancellationToken cancellationToken = default)
        {
            ValidateEntity(serviceReadingDetails, nameof(serviceReadingDetails));

            await ExecuteWithErrorHandlingAsync(async () =>
            {
                //using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    foreach (var detail in serviceReadingDetails)
                    {
                        ValidateEntity(detail, nameof(detail));

                        await ExecuteStoredProcedureAsync(
                            StoredProcedures.INS_ServiceReadingDetail,
                            cancellationToken,
                            detail.IdServiceReadingDetail,
                            detail.IdGroupUnit,
                            Math.Round(Convert.ToDecimal(detail.CurrentReading), 4),
                            Math.Round(Convert.ToDecimal(detail.Consumption), 4),
                            detail.ReadingDate,
                            detail.Code,
                            detail.CalculatedAmount,
                            detail.Minimum,
                            detail.IdServiceReading);
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);
                   // await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    //await transaction.RollbackAsync(cancellationToken);
                    throw new Exception (ex.Message);
                }

                return true;
            }, "AddServiceReadingDetails", cancellationToken);
        }

        public async Task<Contact> AddNewRecordAsync(Contact contact, CancellationToken cancellationToken = default)
        {
            ValidateEntity(contact, nameof(contact));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_Contact,
                    cancellationToken,
                    contact.IdContact,
                    contact.TypeContact,
                    contact.Name,
                    contact.Phone,
                    contact.Email,
                    contact.Address,
                    contact.OfficePhone,
                    contact.MobilePhone,
                    contact.IdRelatedEntity);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return contact;
            }, "AddContact", cancellationToken);
        }

        public async Task<Category> AddNewRecordAsync(Models.Category category, CancellationToken cancellationToken = default)
        {
            ValidateEntity(category, nameof(category));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_Category,
                    cancellationToken,
                    category.IdCategory,
                    category.Description,
                    category.ShortDescript,
                    category.Icon,
                    category.Color,
                    category.Distribution,
                    category.ParentId! == Guid.Empty ? null! : category.ParentId!,
                    category.IdBuilding,
                    category.Nivel);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return category;
            }, "AddCategory", cancellationToken);
        }

        public async Task<Owner> AddNewRecordAsync(Owner owner, CancellationToken cancellationToken = default)
        {
            ValidateEntity(owner, nameof(owner));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var newId = Guid.NewGuid();
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_Owner,
                    cancellationToken,
                    newId,
                    owner.IdNumber,
                    owner.Names,
                    owner.Surname!,
                    owner.Address,
                    owner.PhoneNumber,
                    owner.IdTypeIdNumber,
                    owner.IdBuilding);

                await _dbContext.SaveChangesAsync(cancellationToken);
                owner.IdOwner = newId;
                return owner;
            }, "AddOwner", cancellationToken);
        }

        public async Task<RealEstateUnit> AddNewRecordAsync(RealEstateUnit unit, CancellationToken cancellationToken = default)
        {
            ValidateEntity(unit, nameof(unit));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var newId = Guid.NewGuid();
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_Unit,
                    cancellationToken,
                    newId,
                    unit.UnitNumber,
                    unit.Area,
                    unit.Number,
                    unit.TypeUnit,
                    unit.IsAvailable,
                    unit.IdBuilding);

                await _dbContext.SaveChangesAsync(cancellationToken);
                unit.IdUnit = newId;
                return unit;
            }, "AddUnit", cancellationToken);
        }

        public async Task<BudgetHeader> AddNewRecordAsync(BudgetHeader budgetHeader, CancellationToken cancellationToken = default)
        {
            ValidateEntity(budgetHeader, nameof(budgetHeader));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_BudgetHeader,
                    cancellationToken,
                    budgetHeader.IdBudgetHeader,
                    budgetHeader.BudgetName,
                    budgetHeader.BudgetDate,
                    budgetHeader.Amount,
                    budgetHeader.AnnualAmount,
                    budgetHeader.BudgetType,
                    budgetHeader.IdBuilding,
                    budgetHeader.Status,
                    budgetHeader.CreatedBy,
                    budgetHeader.IdPeriod);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return budgetHeader;
            }, "AddBudgetHeader", cancellationToken);
        }

        public async Task<BudgetDetail> AddNewRecordAsync(BudgetDetail budgetDetail, CancellationToken cancellationToken = default)
        {
            ValidateEntity(budgetDetail, nameof(budgetDetail));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_BudgetDetail,
                    cancellationToken,
                    budgetDetail.IdBudgetDetail,
                    budgetDetail.IdCategory,
                    budgetDetail.IdSection,
                    budgetDetail.ItemNumber,
                    budgetDetail.Description,
                    budgetDetail.MonthlyAmount,
                    budgetDetail.AnnualAmount,
                    budgetDetail.Frequency,
                    budgetDetail.Type,
                    budgetDetail.IsHeader,
                    budgetDetail.IdBudgetHeader);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return budgetDetail;
            }, "AddBudgetDetail", cancellationToken);
        }
        #endregion

        #region Update Operations

        public async Task<Models.MenuItemDefinition> UpdateRecordAsync(Models.MenuItemDefinition item, CancellationToken cancellationToken = default)
        {
            ValidateEntity(item, nameof(item));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_MenuItem,
                    cancellationToken,
                    item.MenuId!,
                    item.ParentId!,
                    item.ItemKey!,
                    item.Title!,
                    item.Icon!,
                    item.Url!,
                    item.Target!,
                    item.DisplayOrder!, 
                    item.IsVisible!,
                    item.BadgeText!,
                    item.BadgeColor!,
                    item.UpdatedAt!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return item;
            }, "UpdateMenuItem", cancellationToken);
        }

        public async Task<Models.ServiceReading> UpdateRecordAsync(Models.ServiceReading servicereading, CancellationToken cancellationToken = default)
        {
            ValidateEntity(servicereading, nameof(servicereading));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_ServiceReading,
                    cancellationToken,
                    servicereading.IdServiceReading!,
                    servicereading.Status!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return servicereading;
            }, "UpdateServiceReading", cancellationToken);
        }

        public async Task<Models.BudgetHeader> UpdateRecordAsync(Models.BudgetHeader budgetheader, CancellationToken cancellationToken = default)
        {
            ValidateEntity(budgetheader, nameof(budgetheader));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_BudgetHeader,
                    cancellationToken,
                    budgetheader.IdBudgetHeader!,
                    budgetheader.BudgetName!,
                    budgetheader.Amount!,
                    budgetheader.AnnualAmount!,
                    budgetheader.BudgetType!,
                    budgetheader.Status!,
                    budgetheader.IdPeriod!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return budgetheader;
            }, "UpdateBudgetHeader", cancellationToken);
        }

        public async Task<Models.Owner> UpdateRecordAsync(Models.Owner owner, CancellationToken cancellationToken = default)
        {
            ValidateEntity(owner, nameof(owner));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_Owner,
                    cancellationToken,
                    owner.IdOwner!,
                    owner.IdNumber!,
                    owner.Names!,
                    owner.Surname!,
                    owner.Address!,
                    owner.PhoneNumber!,
                    owner.IdTypeIdNumber!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return owner;
            }, "UpdateOwner", cancellationToken);
        }

        public async Task<Models.OwnerUnit> UpdateRecordAsync(Models.OwnerUnit ownerunit, CancellationToken cancellationToken = default)
        {
            ValidateEntity(ownerunit, nameof(ownerunit));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_GroupOwner,
                    cancellationToken,
                    ownerunit.IdGroupOwner!,
                    ownerunit.AreaTotal!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return ownerunit;
            }, "UpdateOwnerUnit", cancellationToken);
        }

        public async Task<Models.BuildingConfiguration> UpdateRecordAsync(Models.BuildingConfiguration configuration, CancellationToken cancellationToken = default)
        {
            ValidateEntity(configuration, nameof(configuration));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_BuildingConfiguration,
                    cancellationToken,
                    configuration.IdBuildingConfiguration!,
                    configuration.Currency!,
                    configuration.PaymentMethods!,
                    configuration.PaymentPeriod!,
                    configuration.DueDay!,
                    configuration.FineAmount!,
                    configuration.LateInterestRate!,
                    configuration.InvoiceDay!,
                    configuration.MinWaterConsumtion!,
                    configuration.DefaultFixedCharge!,
                    configuration.DefaultCategory!,
                    configuration.WaterReadingDefault!,
                    configuration.IdBuilding!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return configuration;
            }, "UpdateBuildingConfiguration", cancellationToken);
        }

        public async Task<Models.BankAccount> UpdateRecordAsync(Models.BankAccount bankaccount, CancellationToken cancellationToken = default)
        {
            ValidateEntity(bankaccount, nameof(bankaccount));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_BankAccount,
                    cancellationToken,
                    bankaccount.IdBankAccount!,
                    bankaccount.AccountName!,
                    bankaccount.AccountNumber!,
                    bankaccount.BankName!,
                    bankaccount.AccountType!,
                    bankaccount.Status!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return bankaccount;
            }, "UpdateBankAccount", cancellationToken);
        }

        public async Task<Models.RealEstateUnit> UpdateRecordAsync(Models.RealEstateUnit unit, CancellationToken cancellationToken = default)
        {
            ValidateEntity(unit, nameof(unit));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_Unit,
                    cancellationToken,
                    unit.IdUnit!,
                    unit.UnitNumber!,
                    unit.Area!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return unit;
            }, "Updateunit", cancellationToken);
        }

        public async Task<Models.Parameter> UpdateRecordAsync(Models.Parameter parameter, CancellationToken cancellationToken = default)
        {
            ValidateEntity(parameter, nameof(parameter));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_Parameter,
                    cancellationToken,
                    parameter.IdTabla!,
                    parameter.Description!,
                    parameter.ShortDescription!,
                    parameter.Value!,
                    parameter.Sort!,
                    parameter.IdParent!,
                    parameter.Estado!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return parameter;
            }, "UpdateParameter", cancellationToken);
        }

        public async Task<Models.Category> UpdateRecordAsync(Models.Category category, CancellationToken cancellationToken = default)
        {
            ValidateEntity(category, nameof(category));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_Category,
                    cancellationToken,
                    category.IdCategory!,
                    category.Description!,
                    category.ShortDescript!,
                    category.Color!,
                    category.Icon!,
                    category.Distribution!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return category;
            }, "UpdateCategory", cancellationToken);
        }

        public async Task<Building> UpdateRecordAsync(Building building, CancellationToken cancellationToken = default)
        {
            ValidateEntity(building, nameof(building));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_Building,
                    cancellationToken,
                    building.IdBuilding,
                    building.Name,
                    building.Location,
                    building.TotalArea);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return building;
            }, "UpdateBuilding", cancellationToken);
        }

        public async Task<BudgetDetail> UpdateRecordAsync(BudgetDetail budgetDetail, CancellationToken cancellationToken = default)
        {
            ValidateEntity(budgetDetail, nameof(budgetDetail));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_BudgetDetail,
                    cancellationToken,
                    budgetDetail.IdBudgetDetail,
                    budgetDetail.IdSection,
                    budgetDetail.ItemNumber,
                    budgetDetail.Description,
                    budgetDetail.MonthlyAmount,
                    budgetDetail.AnnualAmount,
                    budgetDetail.Frequency,
                    budgetDetail.Type,
                    budgetDetail.IsHeader);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return budgetDetail;
            }, "UpdateBudgetDetail", cancellationToken);
        }

        public async Task<Contact> UpdateRecordAsync(Contact contact, CancellationToken cancellationToken = default)
        {
            ValidateEntity(contact, nameof(contact));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_Contact,
                    cancellationToken,
                    contact.IdContact,
                    contact.TypeContact,
                    contact.Name,
                    contact.Phone,
                    contact.Email,
                    contact.Address,
                    contact.OfficePhone,
                    contact.MobilePhone);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return contact;
            }, "UpdateContact", cancellationToken);
        }

        public async Task<Expense> UpdateRecordAsync(Expense expense, CancellationToken cancellationToken = default)
        {
            ValidateEntity(expense, nameof(expense));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_Expense,
                    cancellationToken,
                    expense.IdExpense!,
                    expense.ExpenseDescription!,
                    expense.TotalAmount!,
                    expense.IdDistribution!,
                    expense.IsIncludedInQuota,
                    expense.IdSubCategory!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return expense;
            }, "UpdateExpense", cancellationToken);
        }

        public async Task<bool> UpdateRecordAsync(
            TransactionBankDetail transaction,
            CancellationToken cancellationToken = default)
        {
            ValidateEntity(transaction, nameof(transaction));
            ValidateEntity(transaction.GastoConciliado, nameof(transaction.GastoConciliado));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_ExpenseReconcilied,
                    cancellationToken,
                    transaction.IdStatementDetail,
                    transaction.ReconciliationStatus,
                    transaction.ReconciliationDate!,
                    transaction.GastoConciliado!.IdExpense,
                    transaction.GastoConciliado.AutoReconcile);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "UpdateExpenseReconciliation", cancellationToken);
        }
        #endregion

        #region Get Operations

        public async Task<List<Models.MenuPermissions>> GetAllMenuPermissionsAsync(CancellationToken cancellationToken = default)
        {
            List<MenuPermissions> list = [];
            try
            {
                using var connection = new SqlConnection(_dbContext.Database.GetConnectionString());
                using var command = new SqlCommand(StoredProcedures.GET_AllMenuPemission, connection);

                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 30;

                // Usar SqlDataAdapter en lugar de DataReader
                using var adapter = new SqlDataAdapter(command);
                var dataTable = new DataTable();

                // Fill no es async pero no se cuelga con múltiples filas
                adapter.Fill(dataTable);

                foreach (DataRow row in dataTable.Rows)
                {
                    list.Add(
                        new MenuPermissions
                        {
                            MenuId = row["MenuId"] is Guid g1 ? g1 : Guid.Parse(row["MenuId"].ToString()!),
                            PermissionId = row["PermissionId"] is Guid g2 ? g2 : Guid.Parse(row["ParentId"].ToString()!)
                        });
                }

                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return [];
            }
            /*return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.MenuPermissions>(
                    StoredProcedures.GET_AllMenuPemission);
            }, "GetAllBuildingsConfig", cancellationToken);*/
        }

        public async Task<List<Models.BuildingConfiguration>> GetAllBuildingsConfigAsync(Guid IdUser, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.BuildingConfiguration>(
                    StoredProcedures.GET_AllBuildingsConfig,
                    IdUser);
            }, "GetAllBuildingsConfig", cancellationToken);
        }

        public async Task<List<Models.UserBuildingAssociation>> GetUserBuildingAssociationAsync(Guid IdUser, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.UserBuildingAssociation>(
                    StoredProcedures.GET_UserBuildingAssociation,
                    IdUser);
            }, "GetUserBuildingAssociation", cancellationToken);
        }

        public async Task<List<Models.UserModel>> GetUsersByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.UserModel>(
                    StoredProcedures.GET_UsersByEmail,
                    email);
            }, "GetUsersByEmail", cancellationToken);
        }

        public async Task<List<BudgetSumCategory>> GetBudgetSumAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<BudgetSumCategory>(
                    StoredProcedures.GET_BudgetDetails_Sum,
                    idBuilding);
            }, "GetBudgetSum", cancellationToken);
        }

        public async Task<List<BudgetHeader>> GetBudgetsAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<BudgetHeader>(
                    StoredProcedures.GET_Budgets,
                    idBuilding);
            }, "GetBudgets", cancellationToken);
        }

        public async Task<List<ViewExpense>> GetPendingConciliationExpensesAsync(Guid idBuilding, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<ViewExpense>(
                    StoredProcedures.GET_PendingConciliationExpenses,
                    idBuilding, from, to);
            }, "GetPendingConciliationExpenses", cancellationToken);
        }

        public async Task<List<OwnerUnitView>> GetOwnersByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<OwnerUnitView>(
                    StoredProcedures.GET_OwnerByBuilding,
                    idBuilding);
            }, "GetOwnersByBuilding", cancellationToken);
        }

        public async Task<List<ViewBudgetDetail>> GetBudgetDetailDefaultAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<ViewBudgetDetail>(
                    StoredProcedures.GET_BudgetDetailDefault,
                    idBuilding);
            }, "GetBudgetDetailDefault", cancellationToken);
        }

        public async Task<List<TransactionBankDetail>> GetBankTransactionsNoConciliedAsync(Guid idBuilding, DateTime star, DateTime end, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<TransactionBankDetail>(
                    StoredProcedures.GET_BankTransactionsNoConcilied,
                    idBuilding, star, end);
            }, "GetBankTransactionsNoConcilied", cancellationToken);
        }

        public async Task<List<Period>> GetPeriodsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Period>(
                    StoredProcedures.GET_PeriodsByBuilding,
                    idBuilding);
            }, "GetPeriodsByBuilding", cancellationToken);
        }

        public async Task<List<Contact>> GetAllContactsAsync(Guid idBuildingConfiguration, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Contact>(
                    StoredProcedures.GET_AllContacts,
                    idBuildingConfiguration);
            }, "GetAllContacts", cancellationToken);
        }

        public async Task<List<BuildingConfiguration>> GetBuildingConfigurationAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<BuildingConfiguration>(
                    StoredProcedures.GET_BuildingConfiguration,
                    idBuilding);
            }, "GetBuildingConfiguration", cancellationToken);
        }

        public async Task<List<Building>> GetAllBuildingByOwnerAsync(Guid idOwner, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Building>(
                    StoredProcedures.GET_AllBuildings,
                    idOwner);
            }, "GetAllBuildingByOwner", cancellationToken);
        }

        public async Task<List<RealEstateUnit>> GetUnitsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            var units = new List<RealEstateUnit>();

            try
            {
                using var connection = new SqlConnection(_dbContext.Database.GetConnectionString());
                using var command = new SqlCommand(StoredProcedures.GET_UnitsByBuilding, connection);

                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 30;
                command.Parameters.AddWithValue("@idBuilding", idBuilding);

                // Usar SqlDataAdapter en lugar de DataReader
                using var adapter = new SqlDataAdapter(command);
                var dataTable = new DataTable();

                // Fill no es async pero no se cuelga con múltiples filas
                adapter.Fill(dataTable);

                foreach (DataRow row in dataTable.Rows)
                {
                    units.Add(new RealEstateUnit
                    {
                        IdUnit = Guid.Parse(row["IdUnit"].ToString()!),
                        UnitNumber = row["UnitNumber"].ToString()!,
                        Area = Convert.ToDecimal(row["Area"]),
                        TypeGroupUnit = (GroupUnitType)Convert.ToInt32(row["TypeGroupUnit"]),
                        IdGroupOwner = Guid.Parse(row["IdGroupOwner"].ToString()!),
                        GroupName = row["GroupName"].ToString()!,
                        AreaTotal = Convert.ToDecimal(row["AreaTotal"]),
                        TypeOwner = (OwnerType)Convert.ToUInt32(row["TypeOwner"]),
                        Names = row["Names"].ToString()!,
                        Surname = row["Surname"].ToString()!,        // ✔ FIX
                        IdBuilding = Guid.Parse(row["IdBuilding"].ToString()!),
                        TypeUnit = Convert.ToInt32(row["TypeUnit"].ToString()!),
                        IdOwner = Guid.Parse(row["IdOwner"].ToString()!),
                        Number = Convert.ToInt32(row["Number"]),
                        IsAvailable = Convert.ToBoolean(row["IsAvailable"]),   // ✔ FIX
                        Building = row["Building"].ToString()!
                    });
                }

                return units;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        public async Task<List<MenuItem>> GetFullMenuAsync(Guid IdUSer, CancellationToken cancellationToken = default)
        {
            var menus = new Dictionary<Guid, MenuItem>();

            try
            {
                using var connection = new SqlConnection(_dbContext.Database.GetConnectionString());
                using var command = new SqlCommand("GET_FullMenu", connection)

                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 30,
                };

                command.Parameters.Add(new SqlParameter("@Rol", IdUSer));

                await connection.OpenAsync(cancellationToken);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    Guid menuId = Guid.Parse(reader["MenuId"].ToString()!);

                    if (!menus.ContainsKey(menuId))
                    {
                        menus[menuId] = new MenuItem
                        {
                            IdMenu = menuId,
                            ParentId = reader["ParentId"] != DBNull.Value ? Guid.Parse(reader["ParentId"].ToString()!) : Guid.Empty,
                            ItemKey = reader.GetString(reader.GetOrdinal("ItemKey")),
                            Title = reader.GetString(reader.GetOrdinal("Title")),
                            Icon = reader["Icon"]?.ToString(),
                            Url = reader["Url"]?.ToString(),
                            Target = reader["Target"]?.ToString(),
                            Order = reader.GetInt32(reader.GetOrdinal("DisplayOrder")),
                            RequiredPermissions = new List<string>(),
                            Children = new List<MenuItem>()
                        };
                    }

                    // Agregar permiso si existe
                    if (reader["PermissionKey"] != DBNull.Value)
                    {
                        menus[menuId].RequiredPermissions!
                            .Add(reader.GetString(reader.GetOrdinal("PermissionKey")));
                    }
                }

                // Convertir en lista jerárquica
                var menuList = menus.Values.ToList();

                // Asignar hijos
                var lookup = menuList.ToDictionary(m => m.IdMenu);

                foreach (var item in menuList)
                {
                    if (item.ParentId.HasValue && lookup.ContainsKey(item.ParentId.Value))
                    {
                        lookup[item.ParentId.Value].Children.Add(item);
                    }
                }

                // Solo elementos raíz
                var finalMenu = menuList
                    .Where(m => m.ParentId == Guid.Empty)
                    .OrderBy(m => m.Order)
                    .ToList();

                return finalMenu;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        public async Task<List<RolePermissions>> GetPermissionsForRoleAsync(string role, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await ExecuteQueryListAsync<RolePermissions>(
                        StoredProcedures.GET_PermissionsByRole,
                        role);
                }, "GetPermissionsForRole", cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return [];
            }
        }

        public async Task<List<PermissionDefinition>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await ExecuteQueryListAsync<PermissionDefinition>(
                        StoredProcedures.GET_ALLPermissions);
                }, "GetAllPermissions", cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return [];
            }
        }

        public async Task<List<Role>> GetRoleByIdAsync(Guid IdRole, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await ExecuteQueryListAsync<Role>(
                        StoredProcedures.GET_RoleById, IdRole);
                }, "GetRoleById", cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return [];
            }
        }

        public async Task<List<Role>> GetAllRolesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await ExecuteQueryListAsync<Role>(
                        StoredProcedures.GET_AllRoles);
                }, "GetAllRoles", cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return [];
            }
        }

        public async Task<List<MenuItemDefinition>> GetMenuItemsAsync(CancellationToken cancellationToken = default)
        {
            List<MenuItemDefinition> list = [];
            try
            {
                using var connection = new SqlConnection(_dbContext.Database.GetConnectionString());
                using var command = new SqlCommand(StoredProcedures.GET_MenuItem, connection);

                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 30;

                // Usar SqlDataAdapter en lugar de DataReader
                using var adapter = new SqlDataAdapter(command);
                var dataTable = new DataTable();

                // Fill no es async pero no se cuelga con múltiples filas
                adapter.Fill(dataTable);

                foreach (DataRow row in dataTable.Rows)
                {
                    list.Add(
                        new MenuItemDefinition
                        {

                            MenuId = row["MenuId"] is Guid g1 ? g1 : Guid.Parse(row["MenuId"].ToString()!),
                            ParentId = row["ParentId"] == DBNull.Value ? Guid.Empty : (row["ParentId"] is Guid g2 ? g2 : Guid.Parse(row["ParentId"].ToString()!)),
                            ItemKey = row["ItemKey"]?.ToString() ?? "",
                            Title = row["Title"]?.ToString() ?? "",
                            Icon = row["Icon"]?.ToString() ?? "",
                            Url = row["Url"]?.ToString() ?? "",
                            Target = row["Target"]?.ToString() ?? "",
                            ParentKey = row["ParentKey"]?.ToString() ?? "",
                            DisplayOrder = row["DisplayOrder"] is int d ? d : Convert.ToInt32(row["DisplayOrder"])
                        });
                }

                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return [];
            }
        }

        public async Task<List<RealEstateUnit>> GetUnitsByBuildingAsync1(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(async () =>
                {
                    return await ExecuteQueryListAsync<RealEstateUnit>(
                        StoredProcedures.GET_UnitsByBuilding,
                        idBuilding);
                }, "GetUnitsByBuilding", cancellationToken);
            }
            catch (Exception ex) { 
                Console.WriteLine(ex);
                return [];
            }
        }

        public async Task<List<ServiceReadingDetail>> GetServiceReadingDetailbyPeriodAsync(DateTime period, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<ServiceReadingDetail>(
                    StoredProcedures.GET_ServiceReadingDetailList,
                    period);
            }, "GetServiceReadingDetailbyPeriod", cancellationToken);
        }

        public async Task<List<ServiceReadingDetail>> GetFirstWaterReadingDetailListAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<ServiceReadingDetail>(
                    StoredProcedures.GET_FirstWaterReadingDetailList,
                    idBuilding);
            }, "GetFirstWaterReadingDetailList", cancellationToken);
        }

        public async Task<List<Parameter>> GetParametersByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Parameter>(
                    StoredProcedures.GET_AllParameters,
                    idBuilding);
            }, "GetParametersByBuilding", cancellationToken);
        }

        public async Task<List<Expense>> GetExpensesByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Expense>(
                    StoredProcedures.GET_ExpensesByBuilding,
                    idBuilding);
            }, "GetExpensesByBuilding", cancellationToken);
        }

        public async Task<List<TransactionBankHeader>> GetMovementByFileNameAsync(string fileName, Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<TransactionBankHeader>(
                    StoredProcedures.GET_MovementByName,
                    fileName, idBuilding);
            }, "GetMovementByFileName", cancellationToken);
        }

        public async Task<List<MovDetKey>> GetAllMovementDetailAsync(Guid idBankAccout, DateTime star, DateTime end, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<MovDetKey>(
                    StoredProcedures.GET_AllMovementDetail,
                    idBankAccout, star, end);
            }, "GetAllMovementDetail", cancellationToken);
        }

        public async Task<List<Installment>> GetInstallmentsByBudgetAsync(Guid idBudgetHeader, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Installment>(
                    StoredProcedures.GET_InstallmentsByBudget,
                    idBudgetHeader);
            }, "GetInstallmentsByBudget", cancellationToken);
        }

        public async Task<List<Installment>> GetPendingInstallmentsAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Installment>(
                    StoredProcedures.GET_PendingInstallments,
                    idBuilding);
            }, "GetPendingInstallments", cancellationToken);
        }

        public async Task<List<Exoneration>> GetExonerationByBudgetHeaderAsync(Guid idBudgetHeader, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Exoneration>(
                    StoredProcedures.GET_ExonerationByBudgetHeader,
                    idBudgetHeader);
            }, "GetExonerationByBudgetHeader", cancellationToken);
        }

        public async Task<List<BudgetHeader>> GetBudgetsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<BudgetHeader>(
                    StoredProcedures.GET_Budgets,
                    idBuilding);
            }, "GetBudgetsByBuilding", cancellationToken);
        }

        public async Task<List<Models.Category>> GetCategoriesAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.Category>(
                    StoredProcedures.GET_Categories,
                    idBuilding);
            }, "GetCategories", cancellationToken);
        }

        public async Task<List<Models.UnitView>> GetGroupUnitsByTypeAsync(Guid idBuilding, int _type, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Models.UnitView>(
                    StoredProcedures.GET_UnitsByType,
                    idBuilding, _type) ?? [];
            }, "GetGroupUnitsByType", cancellationToken);
        }

        public async Task<List<Exoneration>> GetExonerationsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<Exoneration>(
                    StoredProcedures.GET_Exoneration_All,
                    idBuilding) ?? new List<Exoneration>();
            }, "GetExonerationsByBuilding", cancellationToken);
        }

        public async Task<List<BankAccount>> GetBankAccountsByBuildingAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<BankAccount>(
                    StoredProcedures.GET_BankAccountsByBuilding,
                    idBuilding) ?? new List<BankAccount>();
            }, "GetBankAccountsByBuilding", cancellationToken);
        }

        public async Task<List<ServiceReading>> GetServiceReadingbyPeriodAsync(DateTime period, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<ServiceReading>(
                    StoredProcedures.GET_ServiceReading,
                    period) ?? new List<ServiceReading>();
            }, "GetServiceReadingbyPeriod", cancellationToken);
        }

        public async Task<List<ServiceReading>> GetServiceReadingListAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<ServiceReading>(
                    StoredProcedures.GET_ServiceReadingList,
                    idBuilding) ?? new List<ServiceReading>();
            }, "GetServiceReadingList", cancellationToken);
        }

        public async Task<List<BudgetDetail>> GetBudgetDetailAsync(Guid idBudgetHeader, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<BudgetDetail>(
                    StoredProcedures.GET_List_BudgetDetail,
                    idBudgetHeader) ?? new List<BudgetDetail>();
            }, "GetBudgetDetail", cancellationToken);
        }

        public async Task<List<InstallmentPaid>> GetInstallmentsPaidAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                return await ExecuteQueryListAsync<InstallmentPaid>(
                    StoredProcedures.GET_InstallmentPaid,
                    idBuilding) ?? new List<InstallmentPaid>();
            }, "GetInstallmentsPaid", cancellationToken);
        }

        public async Task<InvitationModel> GetInvitationByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<InvitationModel>(
                    StoredProcedures.GET_InvitationByCode,
                    code);
                return result ?? throw new EntityNotFoundException($"Invitation with code {code} not found");
            }, "GetInvitationByCode", cancellationToken);
        }

        public async Task<UserModel> GetUserByIdAsync(Guid idUser, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<UserModel>(
                    StoredProcedures.GET_UserById,
                    idUser);

                return result ?? throw new EntityNotFoundException($"User with ID {idUser} not found");
            }, "GetUserById", cancellationToken);
        }

        public async Task<Building> GetBuildingByIdAsync(Guid idBuilding, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<Building>(
                    StoredProcedures.GET_BuildingById,
                    idBuilding);

                return result ?? throw new EntityNotFoundException($"Building with ID {idBuilding} not found");
            }, "GetBuildingById", cancellationToken);
        }

        public async Task<Category> GetCategoryByIdAsync(Guid idCategory, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<Category>(
                    StoredProcedures.GET_CategoryById,
                    idCategory);

                return result ?? throw new EntityNotFoundException($"Category with ID {idCategory} not found");
            }, "GetCategoryById", cancellationToken);
        }

        public async Task<BudgetHeader> GetBudgetByIdAsync(Guid idBudgetHeader, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await ExecuteQuerySingleAsync<BudgetHeader>(
                    StoredProcedures.GET_BudgetById,
                    idBudgetHeader);

                return result ?? throw new EntityNotFoundException($"BudgetHeader with ID {idBudgetHeader} not found");
            }, "GetBudgetById", cancellationToken);
        }


        #endregion

        #region Additional Operations
        public async Task<bool> UnsetOtherCurrentPeriodsAsync(Guid idBuilding, Guid idCurrentPeriod, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_UnsetOtherCurrentPeriods,
                    cancellationToken,
                    idBuilding,
                    idCurrentPeriod);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "UnsetOtherCurrentPeriods", cancellationToken);
        }

        public async Task<bool> ClosePastBudgetsAsync(Guid idBuilding, DateTime period, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_ClosePastBudgets,
                    cancellationToken,
                    period,
                    idBuilding);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "ClosePastBudgets", cancellationToken);
        }

        public async Task<bool> UpdateTokenUserAsync(UserModel user, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_UserToken,
                    cancellationToken,
                    user.IdUser,
                    user.Token);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "ClosePastBudgets", cancellationToken);
        }

        public async Task<bool> CheckPeriodOverlapAsync(Period period, CancellationToken cancellationToken = default)
        {
            ValidateEntity(period, nameof(period));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var result = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"EXEC CHK_Period_CheckOverlap {period.IdBuilding}, {period.IdPeriod}, {period.EndDate}, {period.StartDate}",
                    cancellationToken);

                return result > 0;
            }, "CheckPeriodOverlap", cancellationToken);
        }

        public async Task<bool> ConciliarInstallmentAsync(Installment installment, TransactionBankDetail transaction , CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_InstallmentState,
                    cancellationToken,
                    installment.IdInstallment,
                    transaction.IdStatementDetail,
                    installment.Status,
                    transaction.ReconciliationStatus);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "ClosePastBudgets", cancellationToken);
        }

        public async Task<bool> AcceptInvitationAsync(UserBuildingAssociation invitation, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_UserBuildingAssociation,
                    cancellationToken,
                    invitation.IdUser,
                    invitation.IdBuilding,
                    invitation.Role,
                    invitation.IsApproved,
                    invitation.RequestedAt!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "ClosePastBudgets", cancellationToken);
        }


        #endregion
    }

    #region Custom Exceptions
    public class RepositoryException : Exception
    {
        public RepositoryException(string message) : base(message) { }
        public RepositoryException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class EntityNotFoundException : Exception
    {
        public EntityNotFoundException(string message) : base(message) { }
        public EntityNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }
    #endregion
}