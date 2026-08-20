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
        #region Add Operations

        public async Task<MenuPermissions> AddNewRecordAsync(MenuPermissions item, CancellationToken cancellationToken = default)
        {
            ValidateEntity(item, nameof(item));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_MenuItemPermission,
                    cancellationToken,
                    item.IdMenu!,
                    item.IdRole!);

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
                    item.BadgeColor!);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return item;
            }, "AddMenuItem", cancellationToken);
        }

        public async Task<Models.Role> AddNewRecordAsync(Models.Role role, CancellationToken cancellationToken = default)
        {
            ValidateEntity(role, nameof(role));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_Role,
                    cancellationToken,
                    role.IdRole,
                    role.RoleName,
                    role.Description,
                    role.IsSystem);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return role;
            }, "AddRole", cancellationToken);
        }

        public async Task AddUserRoleAsync(Guid idUser, Guid idRole, CancellationToken cancellationToken = default)
        {
            await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.INS_UserRole,
                    cancellationToken,
                    idUser,
                    idRole);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "AddUserRole", cancellationToken);
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
    }
}
