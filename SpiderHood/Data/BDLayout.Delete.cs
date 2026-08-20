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
        #region Delete Operations

        public async Task<bool> DeleteRecordAsync(Role role, CancellationToken cancellationToken = default)
        {
            ValidateEntity(role, nameof(role));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_Role, cancellationToken, role.IdRole);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "DeleteRole", cancellationToken);
        }

        public async Task<bool> DeleteRolePermissionsByRoleAsync(Guid idRole, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_RolePermissionsByRole, cancellationToken, idRole);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "DeleteRolePermissionsByRole", cancellationToken);
        }

        public async Task<bool> DeleteUserRoleByUserAsync(Guid idUser, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_UserRoleByUser, cancellationToken, idUser);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "DeleteUserRoleByUser", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(MenuPermissions item, CancellationToken cancellationToken = default)
        {
            ValidateEntity(item, nameof(item));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_MenuItemPermission, cancellationToken, item.IdMenu, item.IdRole);
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
    }
}
