using Dapper;
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
    public class SpiderHoodContext : DbContext
    {
        public SpiderHoodContext (DbContextOptions<SpiderHoodContext> options)
            : base(options)
        {
        }

        // Optional: Configure table mapping if needed
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Models.Category>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Unit>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.OwnerUnit>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Owner>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Building>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Parameter>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Expense>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.MovDetKey>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.MovementHeader>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.MovementDetail>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.GastoResumen>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.BankAccount>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.ViewExpense>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.TransactionBankView>().HasNoKey(); // If SP doesn't return a primary key
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
            modelBuilder.Entity<Models.CategoryException>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.InstallmentException>().HasNoKey(); // If SP doesn't return a primary key

            base.OnModelCreating(modelBuilder);
        }

        public DbSet<SpiderHood.Models.Owner> Owner { get; set; } = default!;
        public DbSet<SpiderHood.Models.Unit> Unit { get; set; } = default!;
        public DbSet<SpiderHood.Models.UnitType> UnitType { get; set; } = default!;
        public DbSet<SpiderHood.Models.Building> Building { get; set; } = default!;
        public DbSet<SpiderHood.Models.Category> Category { get; set; } = default!;
        public DbSet<SpiderHood.Models.OwnerUnit> OwnerUnit { get; set; } = default!;
        public DbSet<SpiderHood.Models.Parameter> Parameter { get; set; } = default!;
        public DbSet<SpiderHood.Models.Expense> Expense{ get; set; } = default!;
        public DbSet<SpiderHood.Models.MovDetKey> MovDetKey { get; set; } = default!;
        public DbSet<SpiderHood.Models.MovementHeader> MovementHeader { get; set; } = default!;
        public DbSet<SpiderHood.Models.MovementDetail> MovementDetail { get; set; } = null!;
        public DbSet<SpiderHood.Models.CuotaViewModel> CuotaViewModel { get; set; } = null!;
        public DbSet<SpiderHood.Models.CuotaMensual> CuotasMensuales { get; set; } = null!;
        public DbSet<SpiderHood.Models.Departamento> Departamento { get; set; } = null!;
        public DbSet<SpiderHood.Models.ViewExpense> ViewExpense { get; set; } = null!;
        public DbSet<SpiderHood.Models.DetalleCuota> DetallesCuota { get; set; } = null!;
        public DbSet<SpiderHood.Models.DetalleCuotaViewModel> DetalleCuotaViewModel { get; set; } = null!;
        public DbSet<SpiderHood.Models.GastoResumen> GastoResumen { get; set; } = null!;
        public DbSet<SpiderHood.Models.CategoriaGasto> CategoriaGasto { get; set; } = null!;
        public DbSet<SpiderHood.Models.GastoPendienteViewModel> GastoPendienteViewModel { get; set; } = null!;
        public DbSet<SpiderHood.Models.CuotaMensual> CuotaMensual { get; set; } = null!;
        public DbSet<SpiderHood.Models.BankAccount> BankAccount { get; set; } = null!;
        public DbSet<SpiderHood.Models.TransactionBankView> TransactionBankView { get; set; } = null!;
        public DbSet<SpiderHood.Models.BuildingConfiguration> BuildingConfiguration { get; set; } = null!;
        public DbSet<SpiderHood.Models.Contact> Contact { get; set; } = null!;
        public DbSet<SpiderHood.Models.ViewBudgetDetail> ViewBudgetDetail { get; set; } = null!;
        public DbSet<SpiderHood.Models.BudgetHeader> BudgetHeader{ get; set; } = null!;
        public DbSet<SpiderHood.Models.BudgetDetail> BudgetDetail { get; set; } = null!;
        public DbSet<SpiderHood.Models.Presupuesto> Presupuestos { get; set; } = null!;
        public DbSet<SpiderHood.Models.PresupuestoDetalle> PresupuestoDetalles { get; set; } = null!;
        public DbSet<SpiderHood.Models.Categoria> Categorias { get; set; } = null!;
        public DbSet<SpiderHood.Models.PresupuestoCategoria> PresupuestoCategorias { get; set; } = null!;
        public DbSet<SpiderHood.Models.ServiceReading> ServiceReading { get; set; } = null!;
        public DbSet<SpiderHood.Models.ServiceReadingDetail> ServiceReadingDetail { get; set; } = null!;
        public DbSet<SpiderHood.Models.UnitView> UnitView { get; set; } = null!;
        public DbSet<SpiderHood.Models.OwnerUnitView> OwnerUnitView { get; set; } = null!;
        public DbSet<SpiderHood.Models.BudgetSumCategory> BudgetSumCategory { get; set; } = null!;
        public DbSet<SpiderHood.Models.Period> Period { get; set; } = null!;
        public DbSet<SpiderHood.Models.CategoryException> CategoryException { get; set; } = null!;
        public DbSet<SpiderHood.Models.InstallmentException> InstallmentException { get; set; } = null!;

    }
}




