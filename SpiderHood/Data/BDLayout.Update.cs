using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace SpiderHood.Data
{
    public partial class BDLayout
    {
        #region Update Operations

        public async Task<Models.UserModel> UpdateRecordAsync(Models.UserModel user, CancellationToken cancellationToken = default)
        {
            ValidateEntity(user, nameof(user));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_USER,
                    cancellationToken,
                    user.IdUser,
                    user.Email,
                    user.PasswordHash,
                    user.FirstName,
                    user.LastName,
                    user.PhoneNumber,
                    user.IsActive);
                return user;
            }, "UpdateUser", cancellationToken);
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
                return true;
            }, "UpdateTokenUser", cancellationToken);
        }

        // Persiste un password hash nuevo (usado para migrar transparentemente
        // usuarios con hash legado SHA-256 al formato PasswordHasher/PBKDF2
        // cuando hacen login exitosamente).
        public async Task<bool> UpdateUserPasswordAsync(Guid idUser, string newPasswordHash, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_UserPassword,
                    cancellationToken,
                    idUser,
                    newPasswordHash);
                return true;
            }, "UpdateUserPassword", cancellationToken);
        }

        public async Task<Models.Role> UpdateRecordAsync(Models.Role role, CancellationToken cancellationToken = default)
        {
            ValidateEntity(role, nameof(role));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_Role,
                    cancellationToken,
                    role.IdRole,
                    role.RoleName,
                    role.Description);
                return role;
            }, "UpdateRole", cancellationToken);
        }

        public async Task<Models.MenuItemDefinition> UpdateRecordAsync(Models.MenuItemDefinition item, CancellationToken cancellationToken = default)
        {
            ValidateEntity(item, nameof(item));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_MenuItem,
                    cancellationToken,
                    item.IdMenu!,
                    item.IdParent!,
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
                return ownerunit;
            }, "UpdateOwnerUnit", cancellationToken);
        }

        public async Task<Models.BuildingConfiguration> UpdateRecordAsync(Models.BuildingConfiguration configuration, CancellationToken cancellationToken = default)
        {
            ValidateEntity(configuration, nameof(configuration));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                // DefaultCategory/WaterReadingDefault son Guid? -- un valor null "pelado"
                // en el array de parámetros hace que EF no pueda inferir un tipo de
                // columna (mismo problema que ya vimos con Parameter.IdParent), así que
                // se envuelven en un SqlParameter tipado cuando no tienen valor.
                object defaultCategoryParam = configuration.DefaultCategory.HasValue
                    ? configuration.DefaultCategory.Value
                    : new SqlParameter("@DefaultCategory", SqlDbType.UniqueIdentifier) { Value = DBNull.Value };
                object waterReadingDefaultParam = configuration.WaterReadingDefault.HasValue
                    ? configuration.WaterReadingDefault.Value
                    : new SqlParameter("@WaterReadingDefault", SqlDbType.UniqueIdentifier) { Value = DBNull.Value };

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
                    defaultCategoryParam,
                    waterReadingDefaultParam,
                    configuration.IdBuilding!,
                    configuration.DebtWarningDays!,
                    configuration.DebtCriticalDays!,
                    configuration.ReceiptFooterText!);
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
                    bankaccount.Status!,
                    bankaccount.CCI!);
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
                return unit;
            }, "Updateunit", cancellationToken);
        }

        public async Task<Models.Parameter> UpdateRecordAsync(Models.Parameter parameter, CancellationToken cancellationToken = default)
        {
            ValidateEntity(parameter, nameof(parameter));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                // UPD_Parameter real (confirmado contra el CREATE PROCEDURE):
                //   UPD_Parameter(@IdTabla, @Description, @ShortDescription, @Value,
                //                 @Sort, @IdParent = NULL, @Estado INT)
                // No toma @IdBuilding (a diferencia de INS_Parameter), y @Estado es INT
                // -- no BIT como se asumía antes -- así que se manda el valor crudo del
                // enum (Activo=1, Inactivo=2); mandar un bool guardaba 0 para Inactivo
                // en vez de 2.
                //
                // IdParent=0 es la convención de la app para "grupo raíz" (Parameter.
                // IdParent es int no-nullable, así que una fila raíz con IdParent NULL
                // en la BD se lee acá como 0 -- ver ResolveGroupChildren/GroupParameters).
                // Pero FK_Parametros_Parent exige que IdParent sea NULL o una fila
                // IdTabla existente -- no existe ninguna fila con IdTabla=0, así que
                // mandar el 0 literal rompía la FK apenas se editaba un grupo raíz
                // ("Tipo Incidente" incluido).
                //
                // Se arma la llamada a mano con SqlParameter explícitos (en vez de
                // ExecuteStoredProcedureAsync, que arma "@p0,@p1,..." a partir de
                // valores sueltos) porque un DBNull.Value sin tipo en ese mecanismo
                // hace que EF tire "no store type mapping for properties of type
                // 'DBNull'" al intentar inferrar el tipo de columna -- acá el tipo de
                // cada parámetro (incluido el NULL de @IdParent) queda explícito.
                var idParentParam = new SqlParameter("@IdParent", SqlDbType.Int)
                {
                    Value = parameter.IdParent == 0 ? (object)DBNull.Value : parameter.IdParent
                };

                var dbContext = await RentContextAsync(cancellationToken);
                try
                {
                    await dbContext.Database.ExecuteSqlRawAsync(
                        "EXEC dbo.UPD_Parameter @IdTabla, @Description, @ShortDescription, @Value, @Sort, @IdParent, @Estado",
                        new object[]
                        {
                            new SqlParameter("@IdTabla", SqlDbType.Int) { Value = parameter.IdTabla },
                            new SqlParameter("@Description", SqlDbType.NVarChar, 255) { Value = parameter.Description! },
                            new SqlParameter("@ShortDescription", SqlDbType.NVarChar, 100) { Value = parameter.ShortDescription! },
                            new SqlParameter("@Value", SqlDbType.Int) { Value = parameter.Value },
                            new SqlParameter("@Sort", SqlDbType.Int) { Value = parameter.Sort },
                            idParentParam,
                            new SqlParameter("@Estado", SqlDbType.Int) { Value = (int)parameter.Estado },
                        },
                        cancellationToken);
                }
                finally
                {
                    ReturnContext(dbContext);
                }

                return parameter;
            }, "UpdateParameter", cancellationToken);
        }

        public async Task<Models.Period> UpdateRecordAsync(Models.Period period, CancellationToken cancellationToken = default)
        {
            ValidateEntity(period, nameof(period));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_Period,
                    cancellationToken,
                    period.IdPeriod,
                    period.Name,
                    period.PeriodType,
                    period.StartDate,
                    period.EndDate,
                    period.ClosingDate,
                    period.Status,
                    period.IsCurrentPeriod,
                    period.Description);
                return period;
            }, "UpdatePeriod", cancellationToken);
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
                    category.Distribution!,
                    category.ShowDetailInReceipt);
                return category;
            }, "UpdateCategory", cancellationToken);
        }

        public async Task<Building> UpdateRecordAsync(Building building, CancellationToken cancellationToken = default)
        {
            ValidateEntity(building, nameof(building));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                // El modal de edición (BuildingPage.razor) también edita Type/Floors/
                // Basements/Apartments/Parkings/Deposits/Others/IsActive -- este método
                // sólo mandaba Name/Location/TotalArea, así que el resto se perdía en
                // cada guardado sin ningún aviso. Ver
                // Database/Scripts/2026-09-02_17_Persist_BuildingCreation.sql, que
                // reemplaza UPD_Building con la firma completa. Number es IDENTITY
                // (autogenerada) -- no se puede actualizar, ver
                // Database/Scripts/2026-09-02_18_Fix_Building_NumberIsIdentity.sql.
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_Building,
                    cancellationToken,
                    building.IdBuilding,
                    building.Name,
                    building.Location,
                    building.Type,
                    building.Floors,
                    building.Basements,
                    building.Apartments,
                    building.Parkings,
                    building.Deposits,
                    building.Others,
                    building.TotalArea,
                    building.IsActive,
                    building.IsTemplate);
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
                return true;
            }, "UpdateExpenseReconciliation", cancellationToken);
        }
        public async Task<Models.Workflow> UpdateRecordAsync(Models.Workflow workflow, CancellationToken cancellationToken = default)
        {
            ValidateEntity(workflow, nameof(workflow));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_Workflow,
                    cancellationToken,
                    workflow.IdWorkflow,
                    workflow.Name,
                    workflow.Description,
                    (int)workflow.Status);
                return workflow;
            }, "UpdateWorkflow", cancellationToken);
        }

        public async Task<Models.WorkflowStep> UpdateRecordAsync(Models.WorkflowStep step, CancellationToken cancellationToken = default)
        {
            ValidateEntity(step, nameof(step));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_WorkflowStep,
                    cancellationToken,
                    step.IdWorkflowStep,
                    step.StepOrder,
                    step.Name,
                    step.Description,
                    step.Responsible,
                    step.IsImplemented);
                return step;
            }, "UpdateWorkflowStep", cancellationToken);
        }

        public async Task UpdateIncidentStatusAsync(Guid idIncident, Models.IncidentStatus status, Guid? assignedTo, string modifiedBy, CancellationToken cancellationToken = default)
        {
            await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_IncidentStatus,
                    cancellationToken,
                    idIncident,
                    status.ToString(),
                    (object?)assignedTo,
                    modifiedBy);
                return true;
            }, "UpdateIncidentStatus", cancellationToken);
        }

        public async Task UpdateSystemLogSettingsAsync(Models.SystemLogSettings settings, CancellationToken cancellationToken = default)
        {
            ValidateEntity(settings, nameof(settings));

            await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_SystemLogSettings,
                    cancellationToken,
                    settings.IsEnabled,
                    settings.MinLevel,
                    settings.RetentionDays,
                    settings.UpdatedBy!);
                return true;
            }, "UpdateSystemLogSettings", cancellationToken);
        }

        public async Task UpdateCalendarItemAsync(Models.CalendarItem item, CancellationToken cancellationToken = default)
        {
            ValidateEntity(item, nameof(item));

            await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_CalendarItem,
                    cancellationToken,
                    item.IdCalendarItem,
                    item.Title,
                    item.Description,
                    (object?)item.IdCategory,
                    item.StartDate,
                    (object?)item.EndDate,
                    item.Location,
                    item.Responsible,
                    (object?)item.Cost,
                    item.ModifiedBy!);
                return true;
            }, "UpdateCalendarItem", cancellationToken);
        }

        public async Task UpdateCalendarItemStatusAsync(Guid idCalendarItem, Models.CalendarItemStatus status, string modifiedBy, CancellationToken cancellationToken = default)
        {
            await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_CalendarItemStatus,
                    cancellationToken,
                    idCalendarItem,
                    status.ToString(),
                    modifiedBy);
                return true;
            }, "UpdateCalendarItemStatus", cancellationToken);
        }
        #endregion
    }
}