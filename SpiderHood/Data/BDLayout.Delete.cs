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
                return true;
            }, "DeleteRole", cancellationToken);
        }

        public async Task<bool> DeleteRolePermissionsByRoleAsync(Guid idRole, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_RolePermissionsByRole, cancellationToken, idRole);
                return true;
            }, "DeleteRolePermissionsByRole", cancellationToken);
        }

        public async Task<bool> DeleteUserRoleByUserAsync(Guid idUser, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_UserRoleByUser, cancellationToken, idUser);
                return true;
            }, "DeleteUserRoleByUser", cancellationToken);
        }

        public async Task<bool> RevokeUserBuildingRoleAsync(Guid idUser, Guid idBuilding, string role, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_UserBuildingRole, cancellationToken, idUser, idBuilding, role);
                return true;
            }, "RevokeUserBuildingRole", cancellationToken);
        }

        public async Task<bool> DeleteInstallmentPaidByTransactionAsync(Guid idTransaction, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_InstallmentPaidByTransaction, cancellationToken, idTransaction);
                return true;
            }, "DeleteInstallmentPaidByTransaction", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(MenuPermissions item, CancellationToken cancellationToken = default)
        {
            ValidateEntity(item, nameof(item));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_MenuItemPermission, cancellationToken, item.IdMenu, item.IdRole);
                return true;
            }, "DeleteMenuPermission", cancellationToken);
        }

        // Borra el item de menú en sí (y sus permisos + hijos directos, ver
        // DEL_MenuItem) -- distinto de DeleteRecordAsync(MenuPermissions), que sólo
        // borra una fila puntual de permisos.
        public async Task<bool> DeleteMenuItemAsync(Guid idMenu, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_MenuItem, cancellationToken, idMenu);
                return true;
            }, "DeleteMenuItem", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(Category category, CancellationToken cancellationToken = default)
        {
            ValidateEntity(category, nameof(category));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_Category, cancellationToken, category.IdCategory);
                return true;
            }, "DeleteCategory", cancellationToken);
        }

        // Igual que DeleteRecordAsync(Category): un DELETE simple por Id -- si el gasto
        // tiene alguna referencia real desde otra tabla (ver duda de schema documentada
        // en IExpenseService.DeleteExpenseAsync), falla acá con error 547 en vez de dejar
        // datos huérfanos, y ExpenseService la traduce a un mensaje legible.
        public async Task<bool> DeleteRecordAsync(Expense expense, CancellationToken cancellationToken = default)
        {
            ValidateEntity(expense, nameof(expense));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_Expense, cancellationToken, expense.IdExpense);
                return true;
            }, "DeleteExpense", cancellationToken);
        }

        // Ver Docs/Pendientes-Negocio-Migracion.md #6.3 -- DEL_Building (script
        // Database/Scripts/2026-09-09_68_DEL_Building_Procedure.sql) sólo borra
        // UserBuildingAssociation + BuildingConfiguration + Building, en ese orden,
        // dentro de una transacción. NO borra en cascada Category/Parameter/Unit/
        // Owner/BankAccount/Contact/etc. -- si el edificio ya tiene cualquiera de
        // esas filas, el DELETE de BuildingConfiguration o Building falla por FK
        // (error 547, atrapado en IBuildingService.DeleteBuildingAsync) en vez de
        // dejar datos huérfanos. Pensado para borrar edificios de PRUEBA vacíos,
        // no para borrar edificios con actividad real.
        public async Task<bool> DeleteRecordAsync(Building building, CancellationToken cancellationToken = default)
        {
            ValidateEntity(building, nameof(building));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_Building, cancellationToken, building.IdBuilding);
                return true;
            }, "DeleteBuilding", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(Period period, CancellationToken cancellationToken = default)
        {
            ValidateEntity(period, nameof(period));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_Period, cancellationToken, period.IdPeriod);
                return true;
            }, "DeletePeriod", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(BudgetHeader budgetHeader, CancellationToken cancellationToken = default)
        {
            ValidateEntity(budgetHeader, nameof(budgetHeader));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_BudgetHeader, cancellationToken, budgetHeader.IdBudgetHeader);
                return true;
            }, "DeleteBudgetHeader", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(Guid idBudgetHeader, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_BudgetDetail, cancellationToken, idBudgetHeader);
                return true;
            }, "DeleteBudgetDetailByHeader", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(BudgetDetail budgetDetail, CancellationToken cancellationToken = default)
        {
            ValidateEntity(budgetDetail, nameof(budgetDetail));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_BudgetDetail, cancellationToken, budgetDetail.IdBudgetDetail);
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
                return true;
            }, "DeleteExoneration", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(Parameter parameter, CancellationToken cancellationToken = default)
        {
            ValidateEntity(parameter, nameof(parameter));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_Parameter, cancellationToken, parameter.IdTabla);
                return true;
            }, "DeleteParameter", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(Owner owner, CancellationToken cancellationToken = default)
        {
            ValidateEntity(owner, nameof(owner));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_Owner, cancellationToken, owner.IdOwner);
                return true;
            }, "DeleteOwner", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(RealEstateUnit unit, CancellationToken cancellationToken = default)
        {
            ValidateEntity(unit, nameof(unit));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_Unit, cancellationToken, unit.IdUnit);
                return true;
            }, "DeleteUnit", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(Models.Workflow workflow, CancellationToken cancellationToken = default)
        {
            ValidateEntity(workflow, nameof(workflow));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_Workflow, cancellationToken, workflow.IdWorkflow);
                return true;
            }, "DeleteWorkflow", cancellationToken);
        }

        public async Task<bool> DeleteRecordAsync(Models.WorkflowStep step, CancellationToken cancellationToken = default)
        {
            ValidateEntity(step, nameof(step));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_WorkflowStep, cancellationToken, step.IdWorkflowStep);
                return true;
            }, "DeleteWorkflowStep", cancellationToken);
        }

        // Purga por retención (ver Services/Logging/SystemLogPurgeService.cs).
        public async Task<bool> PurgeSystemLogsAsync(DateTime cutoffDateUtc, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_SystemLogOlderThan, cancellationToken, cutoffDateUtc);
                return true;
            }, "PurgeSystemLogs", cancellationToken);
        }

        // deleteSeries = true borra esta ocurrencia y las futuras del mismo
        // IdRecurrenceGroup -- ver DEL_CalendarItem en el script de BD.
        public async Task<bool> DeleteCalendarItemAsync(Guid idCalendarItem, bool deleteSeries = false, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(StoredProcedures.DEL_CalendarItem, cancellationToken, idCalendarItem, deleteSeries);
                return true;
            }, "DeleteCalendarItem", cancellationToken);
        }
        #endregion
    }
}