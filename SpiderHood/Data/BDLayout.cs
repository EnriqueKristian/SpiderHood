using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.InkML;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using SpiderHood.Models;

namespace SpiderHood.Data
{
    public class BDLayout(SpiderHoodContext? _db)
    {
        protected readonly SpiderHoodContext? _dbcontext = _db;
        public SpiderHood.Models.UnitType AddNewRecord(SpiderHood.Models.UnitType? ec)
        {
            //Here define the ExecuteSqlRaw extension method to execute a stored procedure with INS_UnitType 
            _dbcontext!.Database.ExecuteSqlRaw("INS_UnitType {0},{1},{2},{3}", Guid.NewGuid(), ec?.Type!, ec?.Status!, ec!.Description);
            _dbcontext.SaveChanges();
            return ec;
        }

        public async Task<bool> DeleteRecordAsync(Models.Category? ec)
        {
            // Ejecuta el procedimiento almacenado DEL_Category de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync("DEL_Category {0}", ec?.IdCategory!);
            await _dbcontext.SaveChangesAsync();
            return true;
        }


        public async Task<bool> DeleteRecordAsync(Models.BudgetHeader? ec)
        {
            // Ejecuta el procedimiento almacenado DEL_Category de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync("DEL_BudgetHeader {0}", ec?.IdBudgetHeader!);
            await _dbcontext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DelBudgetDetailByHeaderAsync(Guid IdBudgetHeader)
        {
            // Ejecuta el procedimiento almacenado DEL_Category de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync("DEL_BudgetDetail {0}", IdBudgetHeader!);
            await _dbcontext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteRecordAsync(Models.BudgetDetail? ec)
        {
            // Ejecuta el procedimiento almacenado DEL_Category de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync("DEL_BudgetDetail {0}", ec?.IdBudgetDetail!);
            await _dbcontext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteRecordAsync(Models.Exoneration? ec)
        {
            // Ejecuta el procedimiento almacenado DEL_Category de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync("DEL_Exoneration {0},{1}", ec?.IdExoneration!, ec?.UpdatedBy!);
            await _dbcontext.SaveChangesAsync();
            return true;
        }


        public async Task<bool> DeleteRecordAsync(SpiderHood.Models.Parameter? ec)
        {
            // Ejecuta el procedimiento almacenado DEL_Parameter de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync("DEL_Parameter {0}", ec?.IdTabla!);
            await _dbcontext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteRecordAsync(Models.Owner? ec)
        {
            // Ejecuta el procedimiento almacenado DEL_Category de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync("DEL_Category {0}", ec?.IdOwner!);
            await _dbcontext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteRecordAsync(Models.Unit? ec)
        {
            // Ejecuta el procedimiento almacenado DEL_Category de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync("DEL_Category {0}", ec?.IdUnit!);
            await _dbcontext.SaveChangesAsync();
            return true;
        }

        public async Task<SpiderHood.Models.UnitType> AddNewRecordAsync(SpiderHood.Models.UnitType? ec)
        {
            // Ejecuta el procedimiento almacenado INS_UnitType de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_UnitType {0},{1},{2},{3}",
                Guid.NewGuid(),
                ec?.Type!,
                ec?.Status!,
                ec!.Description
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.Exoneration> AddNewRecordAsync(SpiderHood.Models.Exoneration? ec)
        {
            // Ejecuta el procedimiento almacenado INS_UnitType de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_Exoneration {0},{1},{2},{3},{4},{5},{6},{7}",
                ec?.IdExoneration!,
                ec?.IdGroupUnit!,
                ec?.IdCategory!,
                ec?.Description!,
                ec?.IsActive!,
                ec?.CreatedBy!,
                ec?.UpdatedBy!,
                ec!.IdBuilding
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.Period> AddNewRecordAsync(SpiderHood.Models.Period? ec)
        {
            // Ejecuta el procedimiento almacenado INS_UnitType de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_Periods {0},{1},{2},{3},{4},{5},{6},{7},{8}",
                ec?.IdPeriod!,
                ec?.Name!,
                ec?.PeriodType!,
                ec?.StartDate!,
                ec?.EndDate!,
                ec?.ClosingDate!,
                ec?.Status!,
                ec?.IsCurrentPeriod!,
                ec?.IdBuilding!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.ServiceReading> AddNewRecordAsync(SpiderHood.Models.ServiceReading? ec)
        {
                // Ejecuta el procedimiento almacenado INS_WaterReading de forma asincrónica
                await _dbcontext!.Database.ExecuteSqlRawAsync(
                        "INS_ServiceReading {0},{1},{2},{3},{4},{5},{6}",
                        ec?.IdServiceReading!,
                        ec?.Period!,
                        ec?.Status!,
                        ec!.IdBuilding,
                        ec!.FileName,
                        ec?.TotalAmount!,
                        ec?.IdPeriod!
                    );
            
            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.ServiceReadingDetail> AddNewRecordAsync(List<SpiderHood.Models.ServiceReadingDetail>? ec)
        {
            foreach (var item in ec!)
            {
                await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_ServiceReadingDetail {0},{1},{2},{3},{4},{5},{6},{7},{8}",
                item?.IdServiceReadingDetail!,
                item?.IdGroupUnit!,
                Math.Round(Convert.ToDecimal(item?.CurrentReading), 4),
                Math.Round(Convert.ToDecimal(item?.Consumption), 4),
                item?.ReadingDate!,
                item?.Code!,
                item!.CalculatedAmount!,
                item!.Minimum!,
                item.IdServiceReading
                );
                await _dbcontext.SaveChangesAsync();
            }
            return new();
        }

        public async Task<SpiderHood.Models.Contact> AddNewRecordAsync(SpiderHood.Models.Contact? ec)
        {
            // Ejecuta el procedimiento almacenado INS_Contact de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_Contact {0},{1},{2},{3},{4},{5},{6},{7},{8}",
                ec?.IdContact!,
                ec?.TypeContact!,
                ec?.Name!,
                ec!.Phone!,
                ec!.Email!,
                ec!.Address!,
                ec!.OfficePhone!,
                ec!.MobilePhone!,
                ec!.IdRelatedEntity
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }


        public async Task<SpiderHood.Models.BankAccount> AddNewRecordAsync(SpiderHood.Models.BankAccount? ec)
        {
            //TODO: Ejecuta el procedimiento almacenado INS_UnitType de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_UnitType {0},{1},{2},{3}, {4},{5},{6}",
                ec?.IdBankAccount!,
                ec?.AccountName!,
                ec?.AccountNumber!,
                ec!.BankName!,
                ec!.AccountType!,
                ec!.IdBuilding!,
                ec!.Status!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.BuildingConfiguration> AddNewRecordAsync(SpiderHood.Models.BuildingConfiguration? ec)
        {
            // Ejecuta el procedimiento almacenado INS_BuildingConfiguration de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_BuildingConfiguration {0},{1},{2},{3}, {4},{5},{6},{7},{8},{9},{10}",
                ec?.IdBuildingConfiguration!,
                ec?.Currency!,
                ec?.PaymentMethods!,
                ec?.PaymentPeriod!,
                ec?.DueDay!,
                ec?.FineAmount!,
                ec?.LateInterestRate!,
                ec?.InvoiceDay!,
                ec?.MinWaterConsumtion!,
                ec?.DefaultFixedCharge!,
                ec?.IdBuilding!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }


        public async Task<SpiderHood.Models.MovementHeader> AddNewRecordAsync(SpiderHood.Models.MovementHeader? ec)
        {
            // Ejecuta el procedimiento almacenado INS_MovementHeader de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_MovementHeader {0},{1},{2},{3},{4},{5}",
                ec?.IdStatementHeader!,
                ec?.FileName!,
                ec?.IdUser!,
                ec?.TotalRecords!,
                ec?.UploadState!,
                ec?.IdBankAccount!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.Expense> AddNewRecordAsync(SpiderHood.Models.Expense? ec)
        {
            // Ejecuta el procedimiento almacenado INS_Expense de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_Expense {0},{1},{2},{3},{4},{5},{6}",
                ec?.ExpenseDescription!,
                ec?.TotalAmount!,
                0,
                ec?.DueDate!,
                ec?.IdDistribution!,
                ec?.IdBuilding!,
                ec?.IdSubCategory!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }


        public async Task<SpiderHood.Models.ViewExpense> AddNewRecordAsync(SpiderHood.Models.ViewExpense? ec)
        {
            // Ejecuta el procedimiento almacenado INS_Expense de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_Expense {0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
                ec?.IdExpense!,
                ec?.Description!,
                ec?.Amount!,
                ec?.IncludeInQuota!,
                ec?.ExpenseDate!,
                ec?.Distribution!,
                ec?.Supplier!,
                ec?.IdBuilding!,
                ec?.ReconciledTransactionId!,
                ec?.IdCategory!,
                ec?.Notes!,
                ec?.Status!,
                ec?.PaymentMethod!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }


        public async Task<SpiderHood.Models.MovementDetail> AddNewRecordAsync(SpiderHood.Models.MovementDetail? ec)
        {
            // Ejecuta el procedimiento almacenado INS_MovementDetail de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_MovementDetail {0},{1},{2},{3},{4},{5},{6}",
                ec?.IdMovHeader!,
                ec?.Description!,
                ec?.MovDate!,
                ec?.ITF!,
                ec?.Currency!,
                ec?.Amount!,
                ec?.UploadDetState!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.Category> AddNewRecordAsync(SpiderHood.Models.Category? ec)
        {
            // Ejecuta el procedimiento almacenado INS_Category de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_Category {0},{1},{2},{3},{4},{5},{6},{7},{8}",
                ec?.IdCategory!,
                ec?.Description!,
                ec?.ShortDescript!,
                ec?.Icon!,
                ec?.Color!,
                ec?.Distribution!,
                ec?.ParentId! == Guid.Empty ? null! : ec?.ParentId!,
                ec?.IdBuilding!,
                ec?.Nivel!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.Owner> AddNewRecordAsync(SpiderHood.Models.Owner? ec)
        {

            // Ejecuta el procedimiento almacenado INS_Owner de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_Owner {0},{1},{2},{3},{4},{5},{6},{7}",
                Guid.NewGuid(),
                ec?.IdNumber!,
                ec?.Names!,
                ec?.Surname!,
                ec?.Address!,
                ec?.PhoneNumber!,
                ec?.IdTypeIdNumber!,
                ec?.IdBuilding!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }
        public async Task<SpiderHood.Models.OwnerUnit> AddNewRecordAsync(SpiderHood.Models.OwnerUnit? ec)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_GroupOnwer {0},{1},{2},{3},{4}",
                ec?.IdGroupOwner!,
                ec?.IdOwner!,
                ec?.GroupName!,
                ec?.AreaTotal!,
                ec?.TypeOwner!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.OwnerGroupOwner> AddNewRecordAsync(SpiderHood.Models.OwnerGroupOwner? ec)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_OwnerGroupOnwer {0},{1},{2}",
                ec?.IdGroupOwner!,
                ec?.IdOwner!,
                ec?.TypeOwner!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.GroupUnit> AddNewRecordAsync(SpiderHood.Models.GroupUnit? ec)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "INS_GroupUnitOwner {0},{1},{2}",
                ec?.IdUnit!,
                ec?.IdGroupOwner!,
                ec?.TypeGroupUnit!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.Building> UpdateRecordAsync(SpiderHood.Models.Building? ec)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "UPD_Building {0},{1},{2},{3}",
                ec?.IdBuilding!,
                ec?.Name!,
                ec?.Location!,
                ec?.TotalArea!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.BudgetDetail> UpdateRecordAsync(SpiderHood.Models.BudgetDetail? ec)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "UPD_BudgetDetail {0},{1},{2},{3},{4},{5},{6},{7},{8}",
                ec?.IdBudgetDetail!,
                ec?.IdSection!,
                ec?.ItemNumber!,
                ec?.Description!, 
                ec?.MonthlyAmount!,
                ec?.AnnualAmount!,
                ec?.Frequency!,
                ec?.Type!,
                ec?.IsHeader!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }


        public async Task<SpiderHood.Models.ServiceReading> UpdateRecordAsync(SpiderHood.Models.ServiceReading? ec)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "UPD_ServiceReading {0},{1}",
                ec?.IdServiceReading!,
                ec?.Status!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.Contact> UpdateRecordAsync(SpiderHood.Models.Contact? ec)
        {
            try
            {
                // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
                await _dbcontext!.Database.ExecuteSqlRawAsync(
                    "UPD_Contact {0},{1},{2},{3},{4},{5},{6},{7}",
                    ec?.IdContact!,
                    ec?.TypeContact!,
                    ec?.Name!,
                    ec?.Phone!,
                    ec?.Email!,
                    ec?.Address!,
                    ec?.OfficePhone!,
                    ec?.MobilePhone!
                );

            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching products", ex);
            }
            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<Boolean> UpdateRecordAsync(SpiderHood.Models.TransactionBankView? ec)
        {

            try
            {
                // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
                await _dbcontext!.Database.ExecuteSqlRawAsync(
                    "UPD_ExpenseReconcilied {0},{1},{2},{3},{4}",
                    ec?.IdStatementDetail!,
                    ec?.ReconciliationStatus!,
                    ec?.ReconciliationDate!,
                    ec?.GastoConciliado!.IdExpense!,
                    ec?.GastoConciliado!.AutoReconcile!
                );
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching products", ex);
            }
            

            await _dbcontext.SaveChangesAsync();
            return true!;
        }

        public async Task<SpiderHood.Models.Expense> UpdateRecordAsync(SpiderHood.Models.Expense? ec)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "UPD_Expense {0},{1},{2},{3},{4},{5}",
                ec?.IdExpense!,
                ec?.ExpenseDescription!,
                ec?.TotalAmount!,
                ec?.IdDistribution!,
                0,
                ec?.IdSubCategory!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }


        public async Task<SpiderHood.Models.Parameter> UpdateRecordAsync(SpiderHood.Models.Parameter? ec)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "UPD_Parameter {0},{1},{2},{3},{4},{5},{6}",
                ec?.IdTabla!,
                ec?.Description!,
                ec?.ShortDescription!,
                ec?.Value!,
                ec?.Sort!,
                ec?.IdParent!,
                ec?.Estado!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }



        public async Task<bool> UpdateRecordAsync(Guid IdGroupUnit, decimal AreaTotal)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "UPD_GroupOwner {0},{1}",
                IdGroupUnit!,
                AreaTotal!
            );

            await _dbcontext.SaveChangesAsync();
            return true;
        }

        public async Task<SpiderHood.Models.Unit> UpdateRecordAsync(SpiderHood.Models.Unit? ec)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "UPD_Unit {0},{1},{2}",
                ec?.IdUnit!,
                ec?.UnitNumber!,
                ec?.Area!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.BankAccount> UpdateRecordAsync(SpiderHood.Models.BankAccount? ec)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "UPD_BankAccount {0},{1},{2},{3},{4},{5}",
                ec?.IdBankAccount!,
                ec?.AccountName!,
                ec?.AccountNumber!,
                ec?.BankName!,
                ec?.AccountType!,
                ec?.Status!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.BuildingConfiguration> UpdateRecordAsync(SpiderHood.Models.BuildingConfiguration? ec)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "UPD_BuildingConfiguration {0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
                ec?.IdBuildingConfiguration!,
                ec?.Currency!,
                ec?.PaymentMethods!,
                ec?.PaymentPeriod!,
                ec?.DueDay!,
                ec?.FineAmount!,
                ec?.LateInterestRate!,
                ec?.InvoiceDay!,
                ec?.MinWaterConsumtion!,
                ec?.DefaultFixedCharge!,
                ec?.DefaultCategory!,
                ec?.WaterReadingDefault!,
                ec?.IdBuilding!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.BudgetHeader> UpdateRecordAsync(SpiderHood.Models.BudgetHeader? ec)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "UPD_BudgetHeader {0},{1},{2},{3},{4},{5},{6}",
                ec?.IdBudgetHeader!,
                ec?.BudgetName!,
                ec?.Amount!,
                ec?.AnnualAmount!,
                ec?.BudgetType!,
                ec?.Status!,
                ec?.IdPeriod!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<bool> UpdateRecordAsync(Guid IdBuilding, DateTime Period)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "UPD_ClosePastBudgets {0},{1}",
                Period!,
                IdBuilding!
                
            );

            await _dbcontext.SaveChangesAsync();
            return true;
        }

        public async Task<SpiderHood.Models.Category> UpdateRecordAsync(SpiderHood.Models.Category? ec)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "UPD_Category {0},{1},{2},{3},{4},{5}",
                ec?.IdCategory!,
                ec?.Description!,
                ec?.ShortDescript!,
                ec?.Color!,
                ec?.Icon!,
                ec?.Distribution!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.Owner> UpdateRecordAsync(SpiderHood.Models.Owner? ec)
        {
            // Ejecuta el procedimiento almacenado UPD_Building de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync(
                "UPD_Owner {0},{1},{2},{3},{4},{5},{6},{7},{8}",
                ec?.IdOwner!,
                ec?.IdNumber!,
                ec?.Names!,
                ec?.Surname!,
                ec?.Address!,
                ec?.PhoneNumber!,
                ec?.IdTypeIdNumber!
            );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public SpiderHood.Models.Unit AddNewRecord(SpiderHood.Models.Unit? ec)
        {
            //Here define the ExecuteSqlRaw extension method to execute a stored procedure with INS_Unit
            _dbcontext!.Database.ExecuteSqlRaw("INS_Unit {0},{1},{2},{3},{4},{5},{6}", Guid.NewGuid(), ec?.UnitNumber!, ec!.Area, ec!.Number, ec!.TypeUnit, ec!.IsAvailable, ec!.IdBuilding);
            _dbcontext.SaveChanges();
            return ec;
        }


        public async Task<SpiderHood.Models.InstallmentExoneration> AddNewRecordAsync(SpiderHood.Models.InstallmentExoneration? ec)
        {
            // Ejecuta el procedimiento almacenado DEL_Category de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync("INS_InstallmentExoneration {0},{1}", ec?.IdBuilding!, ec?.IdBudgetHeader!);
            await _dbcontext.SaveChangesAsync();
            return ec!;
        }




        public async Task<SpiderHood.Models.Parameter> AddNewRecordAsync(SpiderHood.Models.Parameter? ec)
        {
            //Here define the ExecuteSqlRaw extension method to execute a stored procedure with INS_Unit
            await _dbcontext!.Database.ExecuteSqlRawAsync("INS_Parameter {0},{1},{2},{3},{4},{5},{6}", ec?.Description!, ec?.ShortDescription!, ec?.Value!, ec?.Sort!, ec?.IdParent! == 0 ? null! : ec?.IdParent!, ec?.Estado!, ec!.IdBuilding);
            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.Unit> AddNewRecordAsync(SpiderHood.Models.Unit? ec)
        {
            // Ejecuta el procedimiento almacenado INS_Unit de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync("INS_Unit {0},{1},{2},{3},{4},{5},{6}", Guid.NewGuid(), ec?.UnitNumber!, ec!.Area, ec!.Number, ec!.TypeUnit, ec!.IsAvailable, ec!.IdBuilding );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.BudgetHeader> AddNewRecordAsync(SpiderHood.Models.BudgetHeader? ec)
        {
            // Ejecuta el procedimiento almacenado INS_UnitType de forma asincrónica
            await _dbcontext!.Database.ExecuteSqlRawAsync("INS_BudgetHeader {0},{1},{2},{3},{4},{5},{6},{7},{8},{9}", ec?.IdBudgetHeader!, ec?.BudgetName!, ec!.BudgetDate, ec!.Amount, ec!.AnnualAmount, ec!.BudgetType, ec!.IdBuilding, ec!.Status, ec!.CreatedBy, ec!.IdPeriod );

            await _dbcontext.SaveChangesAsync();
            return ec!;
        }

        public async Task<SpiderHood.Models.BudgetDetail> AddNewRecordAsync(SpiderHood.Models.BudgetDetail? ec)
        {
            try {
                // Ejecuta el procedimiento almacenado INS_UnitType de forma asincrónica
                await _dbcontext!.Database.ExecuteSqlRawAsync("INS_BudgetDetail {0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}", ec?.IdBudgetDetail!, ec?.IdCategory!, ec!.IdSection, ec!.ItemNumber, ec!.Description, ec!.MonthlyAmount, ec!.AnnualAmount, ec!.Frequency, ec!.Type, ec!.IsHeader, ec!.IdBudgetHeader);

                await _dbcontext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching products", ex);
            }
            return ec!;
        }

        public async Task<SpiderHood.Models.Installment> AddNewRecordAsync(SpiderHood.Models.Installment? ec)
        {
            try
            {
                // Ejecuta el procedimiento almacenado INS_UnitType de forma asincrónica
                await _dbcontext!.Database.ExecuteSqlRawAsync("INS_Installment {0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}", ec?.IdInstallment!, ec?.IdBudgetHeader!, ec!.UnitName, ec!.OwnerName, ec!.CreationDate, ec!.Amount, ec!.Percent, ec!.TotalArea, ec!.CreatedBy, ec!.Status, ec!.IdGroupUnit, ec!.DueDate, ec!.Number);

                await _dbcontext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching products", ex);
            }
            return ec!;
        }

        public async Task<SpiderHood.Models.PaidInstallments> AddNewRecordAsync(SpiderHood.Models.PaidInstallments? ec)
        {
            try
            {
                // Ejecuta el procedimiento almacenado INS_UnitType de forma asincrónica
                await _dbcontext!.Database.ExecuteSqlRawAsync("INS_Installment {0},{1},{2},{3},{4}", ec?.IdPaid!, ec?.IdInstallment!, ec!.Amount, ec!.CreatedBy, ec!.Status);

                await _dbcontext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching products", ex);
            }
            return ec!;
        }

        public List<Building> GetBuilding(Guid IdOwner)
        {
            try
            {
                return [.._dbcontext!.Building
                    .FromSqlInterpolated($"EXEC GET_AllBuildings")
                    ];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching products", ex);
            }
        }


        public async Task<Building?> GetBuildingById(Guid idBuilding)
        {
            try
            {
                var list = await _dbcontext!.Building
                    .FromSqlInterpolated($"EXEC GET_BuildingById {idBuilding}")
                    .ToListAsync();

                return list.FirstOrDefault();
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching building", ex);
            }
        }

        public async Task<CuotaViewModel> GetCuotasMensuales(Guid IdBuilding)
        {
            try
            {
                if (_dbcontext == null)
                {
                    throw new InvalidOperationException("Database context is not initialized.");
                }

                var cuota = await _dbcontext.CuotaViewModel
                            .FromSqlInterpolated($"EXEC GET_Building {IdBuilding}")
                            .FirstOrDefaultAsync();

                return cuota!;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching products", ex);
            }
        }

        public List<MovDetKey> GetAllMovementDetail(Guid IdBankAccout, DateTime star, DateTime end)
        {
            try
            {
                return [.._dbcontext!.MovDetKey
                    .FromSqlInterpolated($"EXEC GET_AllMovementDetail {IdBankAccout}, {star}, {end}")
                    ];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching products", ex);
            }
        }


        public List<TransactionBankView> GetBankTransactionsNoConcilied(Guid IdBuilding, DateTime star, DateTime end)
        {
            try
            {
                return [.._dbcontext!.TransactionBankView
                    .FromSqlInterpolated($"GET_BankTransactionsNoConcilied {IdBuilding}, {star}, {end}")
                    ];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching products", ex);
            }
        }


        public List<MovementHeader> GetMovHeadByFileName(string FileName, Guid IdBuilding)
        {
            try
            {
                return [.._dbcontext!.MovementHeader
                    .FromSqlInterpolated($"EXEC GET_MovementByName {FileName}, {IdBuilding}")
                    ];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching products", ex);
            }
        }

        public List<UnitType> GetUniType()
        {
            try
            {
                return [.._dbcontext!.UnitType
                    .FromSqlInterpolated($"EXEC GET_UnitType")
                    ];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching UnitType", ex);
            }
        }
        public List<Models.Unit> GetUnitsByBuilding(Guid IdBuilding)
        {
            try
            {
                var result = _dbcontext!.Unit.FromSqlInterpolated($"EXEC GET_UnitsByBuilding {IdBuilding}").ToList();
                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public List<Models.UnitView> GetGroupUnitsByType(Guid IdBuilding, int _type)
        {
            try
            {
                var result = _dbcontext!.UnitView.FromSqlInterpolated($"EXEC GET_UnitsByType {IdBuilding},{_type}").ToList();
                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<Models.Parameter>> GetParametersByBuildingAsync(Guid IdBuilding)
        {
            try
            {
                var result = _dbcontext!.Parameter.FromSqlInterpolated($"EXEC GET_AllParameters {IdBuilding}").ToList();
                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public List<Models.Parameter> GetParametersByBuilding(Guid IdBuilding)
        {
            try
            {
                var result = _dbcontext!.Parameter.FromSqlInterpolated($"EXEC GET_AllParameters {IdBuilding}").ToList();
                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }


        public List<Models.Parameter> GetListParametersParentByBuilding(Guid IdBuilding)
        {
            try
            {
                var result = _dbcontext!.Parameter.FromSqlInterpolated($"EXEC GET_ListParameterParent {IdBuilding}").ToList();
                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }


        public List<Models.BudgetHeader> GetBudgets(Guid IdBuilding)
        {
            try
            {
                var result = _dbcontext!.BudgetHeader.FromSqlInterpolated($"EXEC GET_Budgets {IdBuilding}").ToList();
                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public List<Models.BudgetSumCategory> GetBudgetSum(Guid IdBuilding)
        {
            try
            {
                var result = _dbcontext!.BudgetSumCategory.FromSqlInterpolated($"EXEC GET_BudgetDetails_Sum {IdBuilding}").ToList();
                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public List<Models.Presupuesto> GetPresupuestos(Guid IdBuilding)
        {
            try
            {
                var result = _dbcontext!.Presupuestos.FromSqlInterpolated($"EXEC GET_Presupuesto_TEMP").ToList();
                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }


        public List<Models.Categoria> GetCategorias(Guid IdBuilding)
        {
            try
            {
                var result = _dbcontext!.Categorias.FromSqlInterpolated($"EXEC GET_Categorias_TEMP").ToList();
                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public List<Models.PresupuestoDetalle> GetPresupuestoDetalles(int presupuestoid)
        {
            try
            {
                var result = _dbcontext!.PresupuestoDetalles.FromSqlInterpolated($"EXEC GET_PresupuestoDetalles_TEMP {presupuestoid}").ToList();
                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public List<Models.PresupuestoCategoria> GetPresupuestoCategoria(Guid presupuestoid)
        {
            try
            {
                var result = _dbcontext!.PresupuestoCategorias.FromSqlInterpolated($"EXEC GET_PresupuestoCategorias_TEMP {presupuestoid}").ToList();
                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public List<Models.Expense> GetExpensesByBuilding(Guid IdBuilding)
        {
            try
            {
                var result = _dbcontext!.Expense.FromSqlInterpolated($"EXEC GET_ExpensesByBuilding {IdBuilding}, {TypeDistribution.Distribution}").ToList();
                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public List<Models.OwnerUnitView> GetOwnersByBuilding(Guid IdBuilding)
        {
            try
            {
                var result = _dbcontext!.OwnerUnitView.FromSqlInterpolated($"EXEC GET_OwnerByBuilding {IdBuilding}").ToList();
                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public List<Models.Category> GetCategories(Guid IdBuilding)
        {
            try
            {
                var result = _dbcontext!.Category.FromSqlInterpolated($"EXEC GET_Categories {IdBuilding}").ToList();
                return result;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public Category GetCategoryById(Guid IdCategory)
        {
            try
            {

                var result = _dbcontext!.Category
                    .FromSql($"EXEC GET_CategorybyId {IdCategory}")
                    .AsEnumerable()
                    .FirstOrDefault();

                return result!;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }


        public BudgetHeader GetBudgetById(Guid IdBudgetHeader)
        {
            try
            {

                var result = _dbcontext!.BudgetHeader
                    .FromSql($"EXEC GET_BudgetById {IdBudgetHeader}")
                    .AsEnumerable()
                    .FirstOrDefault();

                return result!;
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<Exoneration>> GetExonerationsByBuildingAsync(Guid IdBuilding)
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.Exoneration
                    .FromSql($"EXEC GET_Exoneration_All {IdBuilding}")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<Exoneration>> GetExonerationByBudgetHeader(Guid IdBudgetHeader)
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.Exoneration
                    .FromSql($"EXEC GET_ExonerationByBudgetHeader {IdBudgetHeader}")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<Models.Period>> GetPeriodByBuilding(Guid IdBuilding)
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.Period
                    .FromSql($"EXEC GET_PeriodsByBuilding {IdBuilding}")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<DetalleCuotaViewModel>> GetDetallesCuotaByCuotaId(int cuotaId)
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.DetalleCuotaViewModel
                    .FromSql($"EXEC TEMP_GET_DetalleCuota")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<GastoResumen>> GetGastosPrincipales()
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.GastoResumen
                    .FromSql($"EXEC TEMP_GET_GastosPrincipales")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<ViewExpense>> GetGastos()
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.ViewExpense
                    .FromSql($"EXEC temp_GetGasto")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<Departamento>> GetDptos()
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.Departamento
                    .FromSql($"EXEC TEMP_GETDpto")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<CategoriaGasto>> GetCategoriasGasto()
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.CategoriaGasto
                    .FromSql($"EXEC TEMP_GET_Caqtegoria")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<GastoPendienteViewModel>> ObtenerGastosPendientes()
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.GastoPendienteViewModel
                    .FromSql($"EXEC TEMP_GET_GastosPendientes")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<CuotaMensual>> ObtenerCuotaMensuales()
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.CuotaMensual
                    .FromSql($"EXEC TEMP_GET_CuotasMensuales")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }



        public async Task<List<BankAccount>> GetBankAccountsByBuilding(Guid idBuilding)
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.BankAccount
                    .FromSql($"EXEC GET_BankAccountsByBuilding {idBuilding}")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<BuildingConfiguration>> GetBuildingConfiguration(Guid idBuilding)
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.BuildingConfiguration
                    .FromSql($"EXEC GET_BuildingConfiguration {idBuilding}")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<ServiceReading>> GetWaterReadingList(Guid IdBuilding)
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.ServiceReading
                    .FromSql($"EXEC GET_ServiceReadingList {IdBuilding}")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<Installment>> GetInstallmentsByBudget(Guid IdBudgetHeader)
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.Installment
                    .FromSql($"EXEC GET_InstallmentsByBudget {IdBudgetHeader}")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<ServiceReading>> GetServiceReadingbyPeriod(DateTime period)
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.ServiceReading
                    .FromSql($"EXEC GET_ServiceReading {period}")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<ServiceReadingDetail>> GetServiceReadingDetailbyPeriod(DateTime period)
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.ServiceReadingDetail
                    .FromSql($"EXEC GET_ServiceReadingDetailList {period}")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }


        public async Task<List<ServiceReadingDetail>> GetFirstWaterReadingDetailList(Guid IdBuilding)
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.ServiceReadingDetail
                    .FromSql($"EXEC GET_FirstWaterReadingDetailList {IdBuilding}")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }



        public async Task<List<ViewBudgetDetail>> GetBudgetDetailDefault(Guid idBuilding)
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.ViewBudgetDetail
                    .FromSql($"EXEC GET_BudgetDetailDefault {idBuilding}")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

        public async Task<List<BudgetDetail>> GetBudgetDetail(Guid idBudgetHeader)
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.BudgetDetail
                    .FromSql($"EXEC GET_List_BudgetDetail {idBudgetHeader}")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching BudgetDetail", ex);
            }
        }

        public async Task<List<Contact>> GetAllContacts(Guid idBuildingConfiguration)
        {
            if (_dbcontext == null)
            {
                throw new InvalidOperationException("Database context is not initialized.");
            }
            try
            {
                var result = await _dbcontext!.Contact
                    .FromSql($"EXEC GET_AllContacts {idBuildingConfiguration}")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }


        public async Task<List<ViewExpense>> GetPendingConciliationExpenses(Guid idBuilding, DateTime from, DateTime to)
        {
            try
            {
                var result = await _dbcontext!.ViewExpense
                    .FromSql($"EXEC GET_PendingConciliationExpenses {idBuilding}")
                    .ToListAsync();

                return result ?? [];
            }
            catch (Exception ex)
            {
                // Log error here
                throw new ApplicationException("Error fetching unit", ex);
            }
        }

    }
}
