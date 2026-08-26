using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Build.Tasks.Deployment.Bootstrapper;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace SpiderHood.Data
{
    public class SpiderHoodContext : IdentityDbContext<IdentityUser>
    {
        public SpiderHoodContext (DbContextOptions<SpiderHoodContext> options)
            : base(options)
        {
        }

        // Optional: Configure table mapping if needed
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Models.Category>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.RealEstateUnit>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.OwnerUnit>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Owner>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Building>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Parameter>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Expense>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.MovDetKey>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.AccountStatementDetailView>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.TransactionBankHeader>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.GastoResumen>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.BankAccount>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.ViewExpense>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.TransactionBankDetail>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.BuildingConfiguration>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Contact>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.ViewBudgetDetail>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.BudgetHeader>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.BudgetDetail>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Presupuesto>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.ServiceReading>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.ServiceReadingDetail>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.UnitView>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.OwnerUnitView>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.BudgetSumCategory>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Period>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Exoneration>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.InstallmentException>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Installment>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.InstallmentPaid>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.UserModel>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.UserBuildingAssociation>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.InvitationModel>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.RolePermissions>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.PermissionDefinition>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.MenuItemDefinition>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Role>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.RoleAssignment>().HasNoKey(); // If SP doesn't return a primary 
            modelBuilder.Entity<Models.MenuPermissions>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Workflow>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.WorkflowStep>().HasNoKey(); // If SP doesn't return a primary key



            base.OnModelCreating(modelBuilder);
        }
    }
}




