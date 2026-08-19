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

                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }, "UpdateUserPassword", cancellationToken);
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
}
