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
            modelBuilder.Entity<Models.ParameterPromotionCandidate>().HasNoKey(); // If SP doesn't return a primary key
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
            modelBuilder.Entity<Models.UserBuildingRoleAssignment>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.InvitationModel>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.RolePermissions>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.PermissionDefinition>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.MenuItemDefinition>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Role>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.RoleAssignment>().HasNoKey(); // If SP doesn't return a primary 
            modelBuilder.Entity<Models.MenuPermissions>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Workflow>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.WorkflowStep>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Subscription>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.SubscriptionPlan>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.Account>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.AccountUserView>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.AccountInvitation>().HasNoKey(); // If SP doesn't return a primary key
            // Action es enum (WorkflowAction) pero se guarda como texto -- mismo motivo que
            // el comentario de más abajo sobre Incident.Type/Priority/Status.
            modelBuilder.Entity<Models.WorkflowAuditEntry>(entity =>
            {
                entity.HasNoKey();
                entity.Property(e => e.Action).HasConversion<string>();
            });
            modelBuilder.Entity<Models.SystemLogEntry>().HasNoKey(); // If SP doesn't return a primary key
            modelBuilder.Entity<Models.SystemLogSettings>().HasNoKey(); // If SP doesn't return a primary key
            // Docs/Pendientes-Negocio-Conciliacion.md #3
            modelBuilder.Entity<Models.ReconciliationSession>().HasNoKey(); // If SP doesn't return a primary key
            // Docs/Pendientes-Negocio-Conciliacion.md #5 -- Distribution (TypeDistribution)
            // no necesita HasConversion, igual que Category.Distribution más arriba: el
            // enum ya es int por default y la columna es INT.
            modelBuilder.Entity<Models.ExpenseTemplate>().HasNoKey(); // If SP doesn't return a primary key
            // Docs/Pendientes-Negocio-Consolidado.md #18b
            modelBuilder.Entity<Models.ReceiptFile>().HasNoKey(); // If SP doesn't return a primary key
            // Status sigue siendo un enum de C# guardado como texto en la BD (ver
            // Database/Scripts/2026-09-02_05_Incidents.sql) -- sin HasConversion<string>()
            // EF Core asume que un enum es int por default y GET_Incidents* revienta con
            // InvalidCastException al leer el NVARCHAR real. Type/Priority dejaron de ser
            // enums (ahora referencian Parameter.Value, ver Incident.cs), no necesitan
            // conversión.
            modelBuilder.Entity<Models.Incident>(entity =>
            {
                entity.HasNoKey(); // If SP doesn't return a primary key
                entity.Property(i => i.Status).HasConversion<string>();
            });
            modelBuilder.Entity<Models.IncidentComment>().HasNoKey(); // If SP doesn't return a primary key
            // Docs/Pendientes-Negocio-Consolidado.md #18a
            modelBuilder.Entity<Models.IncidentAttachment>().HasNoKey(); // If SP doesn't return a primary key

            // Docs/Pendientes-Negocio-Consolidado.md #17 -- Alcance/EstadoWhatsApp/
            // EstadoCorreo son columnas INT en la BD (no NVARCHAR como Incident.Status
            // arriba), así que no hace falta HasConversion<string>() -- EF mapea el
            // enum de C# (int por default) directo contra la columna INT.
            modelBuilder.Entity<Models.Announcement>().HasNoKey();
            modelBuilder.Entity<Models.AnnouncementRecipient>().HasNoKey();

            // Docs/Pendientes-Negocio-Consolidado.md #21 -- Estado/Etapa/Estado(checklist)
            // son columnas INT en la BD, igual que Announcement arriba.
            modelBuilder.Entity<Models.CommonArea>().HasNoKey();
            modelBuilder.Entity<Models.Reservation>().HasNoKey();
            modelBuilder.Entity<Models.ReservationChecklistItem>().HasNoKey();
            modelBuilder.Entity<Models.ReservationAttachment>().HasNoKey();
            modelBuilder.Entity<Models.CommunityIncome>().HasNoKey();

            // Docs/Pendientes-Negocio-Consolidado.md #21, Gobernanza Fase 1 --
            // mismo motivo: Tipo/Modalidad/Estado son columnas INT en la BD.
            modelBuilder.Entity<Models.Meeting>().HasNoKey();
            modelBuilder.Entity<Models.AgendaItem>().HasNoKey();
            modelBuilder.Entity<Models.Attendance>().HasNoKey();
            modelBuilder.Entity<Models.VotingRound>().HasNoKey();
            modelBuilder.Entity<Models.Vote>().HasNoKey();
            modelBuilder.Entity<Models.MeetingMinutes>().HasNoKey();

            // Type/Category/Status/Recurrence son enums de C# pero se guardan como texto
            // en la BD -- mismo motivo que Incident arriba.
            modelBuilder.Entity<Models.CalendarItem>(entity =>
            {
                entity.HasNoKey(); // If SP doesn't return a primary key
                entity.Property(c => c.Type).HasConversion<string>();
                entity.Property(c => c.Status).HasConversion<string>();
                entity.Property(c => c.Recurrence).HasConversion<string>();
            });

            // Módulo Employee y Payroll -- Fase 1, ver
            // Database/Scripts/2026-09-15_108_Employee_Payroll_Fase1.sql.
            modelBuilder.Entity<Models.Employee>().HasNoKey();
            modelBuilder.Entity<Models.Shift>().HasNoKey();
            modelBuilder.Entity<Models.EmployeeShiftAssignment>().HasNoKey();
            modelBuilder.Entity<Models.EmployeeBuildingAssignment>().HasNoKey();
            modelBuilder.Entity<Models.TimeEntry>().HasNoKey();
            modelBuilder.Entity<Models.HolidayConfiguration>().HasNoKey();

            // Módulo Employee y Payroll -- Fase 2, ver
            // Database/Scripts/2026-09-15_109_Employee_Payroll_Fase2.sql.
            modelBuilder.Entity<Models.LaborRegimeConfiguration>().HasNoKey();
            modelBuilder.Entity<Models.LegalParameters>().HasNoKey();
            modelBuilder.Entity<Models.Vacation>().HasNoKey();
            modelBuilder.Entity<Models.Payslip>().HasNoKey();
            modelBuilder.Entity<Models.PayslipDetail>().HasNoKey();

            // Módulo Employee y Payroll -- Fase 3 (Permisos y licencias), ver
            // Database/Scripts/2026-09-15_110_Employee_Payroll_Fase3_Permisos.sql.
            modelBuilder.Entity<Models.LeaveRequest>().HasNoKey();

            base.OnModelCreating(modelBuilder);
        }
    }
}




