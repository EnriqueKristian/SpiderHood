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
                return true;
            }, "ClosePastBudgets", cancellationToken);
        }

        public async Task<bool> SetPeriodAsCurrentAsync(Guid idPeriod, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    StoredProcedures.UPD_SetPeriodAsCurrent,
                    cancellationToken,
                    idPeriod);
                return true;
            }, "SetPeriodAsCurrent", cancellationToken);
        }

        public async Task<bool> CheckPeriodOverlapAsync(Period period, CancellationToken cancellationToken = default)
        {
            ValidateEntity(period, nameof(period));

            return await ExecuteWithErrorHandlingAsync(async () =>
            {
                var dbContext = await RentContextAsync(cancellationToken);
                try
                {
                    var result = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"EXEC CHK_Period_CheckOverlap {period.IdBuilding}, {period.IdPeriod}, {period.EndDate}, {period.StartDate}",
                        cancellationToken);

                    return result > 0;
                }
                finally
                {
                    ReturnContext(dbContext);
                }
            }, "CheckPeriodOverlap", cancellationToken);
        }

        public async Task<bool> ConciliarInstallmentAsync(Installment installment, TransactionBankDetail transaction, CancellationToken cancellationToken = default)
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
                return true;
            }, "ConciliarInstallment", cancellationToken);
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
                return true;
            }, "ClosePastBudgets", cancellationToken);
        }


        #endregion
    }
}