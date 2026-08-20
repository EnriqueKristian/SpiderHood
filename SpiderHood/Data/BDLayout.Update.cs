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

                await _dbContext.SaveChangesAsync(cancellationToken);
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
    }
}
