using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace SpiderHood.Data
{
    public partial class BDLayout : IBDLayout
    {
        // Antes BDLayout recibía un SpiderHoodContext ya construido — inyectado como Scoped
        // por circuito de Blazor Server y compartido por TODOS los servicios/páginas que
        // cargan datos en paralelo (LeftMenu, Home, HeaderMainLayout, ...). EF Core no
        // permite operaciones concurrentes sobre la misma instancia, así que ese diseño
        // garantizaba colisiones ("A second operation was started on this context
        // instance...") cada vez que dos componentes cargaban al mismo tiempo; se mitigaban
        // con reintentos, no se eliminaban. Ahora BDLayout recibe la factory y cada
        // operación crea su propio SpiderHoodContext de corta vida, así que dos llamadas
        // concurrentes ya no pueden pisarse: cada una tiene su propia conexión/contexto.
        private readonly IDbContextFactory<SpiderHoodContext>? _contextFactory;
        private readonly SpiderHoodContext? _fixedContext;
        private readonly string? _connectionString;

        public BDLayout(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

            // Algunas consultas abren su propia SqlConnection en paralelo a EF (Dapper puro,
            // sin pasar por el change tracker). La cadena de conexión no cambia en runtime,
            // así que basta con leerla una vez de un contexto descartable.
            using var seedContext = _contextFactory.CreateDbContext();
            _connectionString = seedContext.Database.GetConnectionString();
        }

        // Modo transaccional: algunos servicios necesitan que VARIAS llamadas de BDLayout
        // (todas raw SQL vía ExecuteSqlRawAsync) participen en la MISMA transacción que el
        // caller abrió con context.Database.BeginTransactionAsync(). Si cada llamada creara
        // su propio contexto (modo normal, de arriba), cada una usaría su propia conexión y
        // quedaría fuera de esa transacción — un rollback no revertiría nada. Este
        // constructor reutiliza el contexto que ya trae el caller en vez de crear uno nuevo
        // por operación; el caller es dueño del contexto y de su disposición.
        public BDLayout(SpiderHoodContext existingContext)
        {
            _fixedContext = existingContext ?? throw new ArgumentNullException(nameof(existingContext));
            _connectionString = existingContext.Database.GetConnectionString();
        }

        private async Task<SpiderHoodContext> RentContextAsync(CancellationToken cancellationToken = default)
            => _fixedContext ?? await _contextFactory!.CreateDbContextAsync(cancellationToken);

        private void ReturnContext(SpiderHoodContext context)
        {
            // Solo descartamos el contexto si lo creamos nosotros para esta operación
            // (modo factory). El contexto "fijo" pasado por el caller es responsabilidad
            // del caller, no nuestra.
            if (_fixedContext == null)
            {
                context.Dispose();
            }
        }

        #region Constants and Stored Procedure Names
        private static class StoredProcedures
        {
            // Insert Procedures
            public const string INS_MenuItemPermission = "INS_MenuItemPermission";
            public const string INS_MenuItem = "INS_MenuItem";
            public const string INS_RolePermissions = "INS_RolePermissions";
            public const string INS_Role = "INS_Role";
            public const string INS_UserRole = "INS_UserRole";
            public const string INS_UserBuildingAssociation = "INS_UserBuildingAssociation";
            public const string INS_User = "INS_User";
            public const string INS_Building = "INS_Building";
            public const string INS_Category = "INS_Category";
            public const string INS_Exoneration = "INS_Exoneration";
            public const string INS_Periods = "INS_Periods";
            public const string INS_ServiceReading = "INS_ServiceReading";
            public const string INS_ServiceReadingDetail = "INS_ServiceReadingDetail";
            public const string INS_Contact = "INS_Contact";
            public const string INS_BankAccount = "INS_BankAccount";
            public const string INS_BuildingConfiguration = "INS_BuildingConfiguration";
            public const string INS_MovementHeader = "INS_MovementHeader";
            public const string INS_Expense = "INS_Expense";
            public const string INS_AccountStatementDetail = "INS_AccountStatementDetail";
            public const string INS_Owner = "INS_Owner";
            public const string INS_GroupOwner = "INS_GroupOwner";
            public const string INS_OwnerGroupOwner = "INS_OwnerGroupOwner";
            public const string INS_GroupUnitOwner = "INS_GroupUnitOwner";
            public const string INS_InstallmentExoneration = "INS_InstallmentExoneration";
            public const string INS_Parameter = "INS_Parameter";
            public const string INS_Unit = "INS_Unit";
            public const string INS_BudgetHeader = "INS_BudgetHeader";
            public const string INS_BudgetDetail = "INS_BudgetDetail";
            public const string INS_Installment = "INS_Installment";
            public const string INS_InstallmentPaid = "INS_InstallmentPaid";

            // Update Procedures
            public const string UPD_MenuItem = "UPD_MenuItem";
            public const string UPD_UserToken = "UPD_UserToken";
            public const string UPD_UserPassword = "UPD_UserPassword";
            public const string UPD_UserBuildingUnit = "UPD_UserBuildingUnit";
            public const string UPD_Building = "UPD_Building";
            public const string UPD_BudgetDetail = "UPD_BudgetDetail";
            public const string UPD_ServiceReading = "UPD_ServiceReading";
            public const string UPD_Contact = "UPD_Contact";
            public const string UPD_ExpenseReconcilied = "UPD_ExpenseReconcilied";
            public const string UPD_ExpenseDeReconcilied = "UPD_ExpenseDeReconcilied";
            public const string UPD_Expense = "UPD_Expense";
            public const string UPD_Parameter = "UPD_Parameter";
            public const string UPD_GroupOwner = "UPD_GroupOwner";
            public const string UPD_Unit = "UPD_Unit";
            public const string UPD_UnsetOtherCurrentPeriods = "UPD_UnsetOtherCurrentPeriods";
            public const string UPD_Period = "UPD_Period";
            public const string UPD_SetPeriodAsCurrent = "UPD_SetPeriodAsCurrent";
            public const string UPD_BankAccount = "UPD_BankAccount";
            public const string UPD_BankAccount_InitialBalance = "UPD_BankAccount_InitialBalance";
            public const string UPD_BuildingConfiguration = "UPD_BuildingConfiguration";
            public const string UPD_BuildingConfiguration_ExpenseThreshold = "UPD_BuildingConfiguration_ExpenseThreshold";
            public const string UPD_BuildingConfiguration_DistributionMode = "UPD_BuildingConfiguration_DistributionMode";
            public const string UPD_SyncUnsoldUnitsToRealEstateCompany = "UPD_SyncUnsoldUnitsToRealEstateCompany";
            public const string UPD_Category_BulkDistribution = "UPD_Category_BulkDistribution";
            public const string UPD_BudgetHeader = "UPD_BudgetHeader";
            public const string UPD_ClosePastBudgets = "UPD_ClosePastBudgets";
            public const string UPD_Category = "UPD_Category";
            public const string UPD_Owner = "UPD_Owner";
            public const string UPD_InstallmentState = "UPD_InstallmentState";
            public const string UPD_Role = "UPD_Role";
            public const string UPD_USER = "UPD_USER";
            // Solo para migración de datos históricos (IMigrationImportService) -- no lo
            // usa ninguna pantalla ni flujo de uso diario.
            public const string UPD_TransactionBankDetail_OriginalReference = "UPD_TransactionBankDetail_OriginalReference";

            // Delete Procedures
            public const string DEL_MenuItemPermission = "DEL_MenuItemPermission";
            // Fix (Database/Scripts/2026-09-15_112_Fix_MenuAdmin_Permissions.sql): borra
            // TODAS las filas de un IdMenu sin importar el rol -- DEL_MenuItemPermission
            // exige (IdMenu, IdRole) exacto, así que no sirve para "limpiar antes de
            // re-insertar el set nuevo" cuando no se sabe de antemano qué roles tenían
            // acceso.
            public const string DEL_MenuItemPermissionsByMenu = "DEL_MenuItemPermissionsByMenu";
            public const string DEL_MenuItem = "DEL_MenuItem";
            public const string DEL_Category = "DEL_Category";
            public const string DEL_Expense = "DEL_Expense";
            public const string DEL_Building = "DEL_Building";
            public const string DEL_BudgetHeader = "DEL_BudgetHeader";
            public const string DEL_BudgetDetail = "DEL_BudgetDetail";
            public const string DEL_Exoneration = "DEL_Exoneration";
            public const string DEL_Parameter = "DEL_Parameter";
            public const string DEL_Period = "DEL_Period";
            public const string DEL_Owner = "DEL_Owner";
            public const string DEL_Unit = "DEL_Unit";
            public const string DEL_Role = "DEL_Role";
            public const string DEL_RolePermissionsByRole = "DEL_RolePermissionsByRole";
            public const string DEL_UserRoleByUser = "DEL_UserRoleByUser";
            public const string DEL_InstallmentPaidByTransaction = "DEL_InstallmentPaidByTransaction";

            // Get Procedures
            public const string GET_AllMenuPemission = "GET_AllMenuPemission";
            public const string GET_RoleById = "GET_RoleById";
            public const string GET_MenuItem = "GET_MenuItem";
            public const string GET_AllRoles = "GET_AllRoles";
            public const string GET_ALLPermissions = "GET_ALLPermissions";
            public const string GET_PermissionsByRole = "GET_PermissionsByRole";
            // Catálogo de Permisos -- antes sólo se podía crear/editar con un script SQL a
            // mano (ver Database/Scripts/2026-09-09_76_Seed_ReportPermissions.sql). Sólo
            // SysAdmin tiene esta pantalla (Components/Pages/SettingPages/PermissionsAdmin.razor).
            public const string INS_Permission = "INS_Permission";
            public const string UPD_Permission = "UPD_Permission";
            public const string GET_FullMenu = "GET_FullMenu";
            public const string GET_UserById = "GET_UserById";
            public const string GET_InvitationByCode = "GET_InvitationByCode";
            public const string INS_Invitation = "INS_Invitation";
            public const string GET_AllBuildings = "GET_AllBuildings";
            public const string GET_BuildingById = "GET_BuildingById";
            public const string GET_Building = "GET_Building";
            public const string GET_AllMovementDetail = "GET_AllMovementDetail";
            public const string GET_BankTransactionsNoConcilied = "GET_BankTransactionsNoConcilied";
            public const string GET_TransactionBankDetailById = "GET_TransactionBankDetailById";
            public const string UPD_AccountStatementDetail_Ignored = "UPD_AccountStatementDetail_Ignored";
            public const string GET_MovementByName = "GET_MovementByName";
            public const string GET_MovementHeaders = "GET_MovementHeaders";
            public const string GET_AccountStatementDetailByHeader = "GET_AccountStatementDetailByHeader";
            public const string GET_UnitsByBuilding = "GET_UnitsByBuilding";
            public const string GET_UnitsByType = "GET_UnitsByType";
            public const string GET_UnitExtraFieldsByBuilding = "GET_UnitExtraFieldsByBuilding";
            public const string GET_AllParameters = "GET_AllParameters";
            public const string GET_ListParameterParent = "GET_ListParameterParent";
            public const string GET_Budgets = "GET_Budgets";
            public const string GET_BudgetDetails_Sum = "GET_BudgetDetails_Sum";
            public const string GET_ExpensesByBuilding = "GET_ExpensesByBuilding";
            public const string GET_OwnerByBuilding = "GET_OwnerByBuilding";
            public const string GET_Categories = "GET_Categories";
            public const string GET_CategoryById = "GET_CategoryById";
            public const string GET_BudgetById = "GET_BudgetById";
            public const string GET_Exoneration_All = "GET_Exoneration_All";
            public const string GET_ExonerationByBudgetHeader = "GET_ExonerationByBudgetHeader";
            public const string GET_PeriodsByBuilding = "GET_PeriodsByBuilding";
            public const string GET_BankAccountsByBuilding = "GET_BankAccountsByBuilding";
            public const string GET_BuildingConfiguration = "GET_BuildingConfiguration";
            public const string GET_ServiceReadingList = "GET_ServiceReadingList";
            public const string GET_InstallmentsByBudget = "GET_InstallmentsByBudget";
            public const string GET_PendingInstallments = "GET_PendingInstallments";
            public const string GET_InstallmentsByBuilding = "GET_InstallmentsByBuilding";
            public const string GET_ServiceReading = "GET_ServiceReading";
            public const string GET_ServiceReadingDetailList = "GET_ServiceReadingDetailList";
            public const string GET_FirstWaterReadingDetailList = "GET_FirstWaterReadingDetailList";
            public const string GET_BudgetDetailDefault = "GET_BudgetDetailDefault";
            public const string GET_LastBudgetItemsByParentCategory = "GET_LastBudgetItemsByParentCategory";
            public const string GET_List_BudgetDetail = "GET_List_BudgetDetail";
            public const string GET_AllContacts = "GET_AllContacts";
            public const string GET_PendingConciliationExpenses = "GET_PendingConciliationExpenses";
            public const string GET_InstallmentPaid = "GET_InstallmentPaid";
            // Solo para migración de datos históricos (IMigrationImportService) -- no lo
            // usa ninguna pantalla ni flujo de uso diario.
            public const string GET_TransactionBankDetail_ByOriginalReference = "GET_TransactionBankDetail_ByOriginalReference";
            public const string GET_UsersByEmail = "GET_UsersByEmail";
            public const string GET_UserBuildingAssociation = "GET_UserBuildingAssociation";
            public const string GET_AllBuildingsConfig = "GET_AllBuildingsConfig";
            public const string GET_AllUsersWithRoles = "GET_AllUsersWithRoles";
            public const string GET_RoleByUserId = "GET_RoleByUserId";
            public const string GET_AllUserBuildingRoles = "GET_AllUserBuildingRoles";
            public const string INS_UserBuildingRole = "INS_UserBuildingRole";
            public const string DEL_UserBuildingRole = "DEL_UserBuildingRole";
            public const string GET_AllBuildingsPublic = "GET_AllBuildingsPublic";
            public const string GET_TemplateBuilding = "GET_TemplateBuilding";
            public const string GET_MixtoParameterCandidates = "GET_MixtoParameterCandidates";
            public const string UPD_PromoteParameterToGlobal = "UPD_PromoteParameterToGlobal";
            public const string UPD_MergeParameterInto = "UPD_MergeParameterInto";
            public const string UPD_UserBuildingApproval = "UPD_UserBuildingApproval";
            public const string INS_Workflow = "INS_Workflow";
            public const string UPD_Workflow = "UPD_Workflow";
            public const string DEL_Workflow = "DEL_Workflow";
            public const string GET_Workflows = "GET_Workflows";
            public const string INS_WorkflowStep = "INS_WorkflowStep";
            public const string UPD_WorkflowStep = "UPD_WorkflowStep";
            public const string DEL_WorkflowStep = "DEL_WorkflowStep";
            public const string GET_WorkflowStepsByWorkflow = "GET_WorkflowStepsByWorkflow";

            // Subscription Procedures (Docs/Design-Subscripcion-Administrador.md)
            public const string INS_Subscription = "INS_Subscription";
            public const string GET_SubscriptionByUser = "GET_SubscriptionByUser";
            public const string GET_AllSubscriptionPlans = "GET_AllSubscriptionPlans";
            public const string UPD_ActivateSubscription = "UPD_ActivateSubscription";

            // Account Procedures (Docs/Design-Account-Facturacion.md)
            public const string INS_Account = "INS_Account";
            public const string INS_AccountUser = "INS_AccountUser";
            public const string GET_AccountByUser = "GET_AccountByUser";
            public const string GET_AccountUsersByAccount = "GET_AccountUsersByAccount";
            public const string INS_AccountInvitation = "INS_AccountInvitation";
            public const string GET_AccountInvitationByCode = "GET_AccountInvitationByCode";
            public const string GET_PendingInvitationsByAccount = "GET_PendingInvitationsByAccount";
            public const string UPD_AccountInvitationStatus = "UPD_AccountInvitationStatus";
            public const string GET_BuildingsByAccount = "GET_BuildingsByAccount";

            // Audit Procedures (ver BDLayout.Audit.cs)
            public const string UPD_BuildingAudit = "UPD_BuildingAudit";
            public const string UPD_OwnerAudit = "UPD_OwnerAudit";
            public const string UPD_BudgetHeaderAudit = "UPD_BudgetHeaderAudit";
            public const string UPD_ExpenseAudit = "UPD_ExpenseAudit";
            public const string UPD_PeriodAudit = "UPD_PeriodAudit";
            public const string UPD_ServiceReadingAudit = "UPD_ServiceReadingAudit";
            public const string UPD_BankAccountAudit = "UPD_BankAccountAudit";
            public const string UPD_UnitAudit = "UPD_UnitAudit";
            public const string UPD_CategoryAudit = "UPD_CategoryAudit";
            public const string UPD_BuildingConfigurationAudit = "UPD_BuildingConfigurationAudit";

            // Workflow Audit Procedures
            public const string INS_WorkflowAuditLog = "INS_WorkflowAuditLog";
            public const string GET_WorkflowAuditLog = "GET_WorkflowAuditLog";

            // Reconciliation Session Procedures -- Docs/Pendientes-Negocio-Conciliacion.md #3
            public const string INS_ReconciliationSession = "INS_ReconciliationSession";
            public const string GET_LastReconciliationSession = "GET_LastReconciliationSession";

            // Expense Template Procedures -- Docs/Pendientes-Negocio-Conciliacion.md #5
            public const string INS_ExpenseTemplate = "INS_ExpenseTemplate";
            public const string UPD_ExpenseTemplate = "UPD_ExpenseTemplate";
            public const string GET_ExpenseTemplatesByBuilding = "GET_ExpenseTemplatesByBuilding";

            // Receipt File Procedures -- Docs/Pendientes-Negocio-Consolidado.md #18b
            public const string INS_ReceiptFile = "INS_ReceiptFile";
            public const string GET_ReceiptFileByInstallment = "GET_ReceiptFileByInstallment";

            // System Log Procedures
            public const string INS_SystemLog = "INS_SystemLog";
            public const string GET_SystemLogSettings = "GET_SystemLogSettings";
            public const string UPD_SystemLogSettings = "UPD_SystemLogSettings";
            public const string GET_SystemLogs_Recent = "GET_SystemLogs_Recent";
            public const string DEL_SystemLogOlderThan = "DEL_SystemLogOlderThan";

            // Incident Procedures
            public const string INS_Incident = "INS_Incident";
            public const string UPD_IncidentStatus = "UPD_IncidentStatus";
            public const string GET_IncidentsByBuilding = "GET_IncidentsByBuilding";
            public const string GET_IncidentsByReporter = "GET_IncidentsByReporter";
            public const string GET_IncidentById = "GET_IncidentById";
            public const string INS_IncidentComment = "INS_IncidentComment";
            public const string GET_IncidentCommentsByIncident = "GET_IncidentCommentsByIncident";

            // Incident Attachment Procedures -- Docs/Pendientes-Negocio-Consolidado.md #18a
            public const string INS_IncidentAttachment = "INS_IncidentAttachment";
            public const string GET_IncidentAttachmentsByIncident = "GET_IncidentAttachmentsByIncident";

            // Announcement Procedures -- Docs/Pendientes-Negocio-Consolidado.md #17
            public const string INS_Announcement = "INS_Announcement";
            public const string GET_AnnouncementsByBuilding = "GET_AnnouncementsByBuilding";
            public const string INS_AnnouncementRecipient = "INS_AnnouncementRecipient";
            public const string GET_AnnouncementRecipientsByAnnouncement = "GET_AnnouncementRecipientsByAnnouncement";
            public const string GET_AnnouncementsParaUsuario = "GET_AnnouncementsParaUsuario";

            // Reservation / Área Común Procedures -- Docs/Pendientes-Negocio-Consolidado.md #21
            public const string INS_CommonArea = "INS_CommonArea";
            public const string UPD_CommonArea = "UPD_CommonArea";
            public const string GET_CommonAreasByBuilding = "GET_CommonAreasByBuilding";
            public const string INS_Reservation = "INS_Reservation";
            public const string UPD_ReservationStatus = "UPD_ReservationStatus";
            public const string UPD_ReservationPago = "UPD_ReservationPago";
            public const string UPD_ReservationFechas = "UPD_ReservationFechas";
            public const string GET_ReservationsByBuilding = "GET_ReservationsByBuilding";
            public const string GET_ReservationById = "GET_ReservationById";
            public const string GET_ReservationsPendientesByBuilding = "GET_ReservationsPendientesByBuilding";
            public const string GET_ReservationsByGroupUnit = "GET_ReservationsByGroupUnit";
            public const string GET_ReservationsConflicto = "GET_ReservationsConflicto";
            public const string GET_ReservationsProximasByCommonArea = "GET_ReservationsProximasByCommonArea";
            public const string INS_ReservationChecklistItem = "INS_ReservationChecklistItem";
            public const string GET_ReservationChecklistItemsByReservation = "GET_ReservationChecklistItemsByReservation";
            public const string INS_ReservationAttachment = "INS_ReservationAttachment";
            public const string GET_ReservationAttachmentsByReservation = "GET_ReservationAttachmentsByReservation";
            public const string INS_CommunityIncome = "INS_CommunityIncome";
            public const string GET_CommunityIncomesByBuilding = "GET_CommunityIncomesByBuilding";

            // Gobernanza / Meetings Procedures -- Docs/Pendientes-Negocio-Consolidado.md #21, Fase 1
            public const string INS_Meeting = "INS_Meeting";
            public const string UPD_Meeting = "UPD_Meeting";
            public const string UPD_MeetingEstado = "UPD_MeetingEstado";
            public const string GET_MeetingsByBuilding = "GET_MeetingsByBuilding";
            public const string GET_MeetingById = "GET_MeetingById";
            public const string INS_AgendaItem = "INS_AgendaItem";
            public const string UPD_AgendaItem = "UPD_AgendaItem";
            public const string UPD_AgendaItemEstado = "UPD_AgendaItemEstado";
            public const string GET_AgendaItemsByMeeting = "GET_AgendaItemsByMeeting";
            public const string GET_AgendaItemById = "GET_AgendaItemById";
            public const string DEL_AgendaItem = "DEL_AgendaItem";
            public const string INS_Attendance = "INS_Attendance";
            public const string GET_AttendancesByMeeting = "GET_AttendancesByMeeting";
            public const string DEL_Attendance = "DEL_Attendance";

            // Gobernanza / Votación Procedures -- Docs/Pendientes-Negocio-Consolidado.md #21, Fase 2
            public const string INS_VotingRound = "INS_VotingRound";
            public const string UPD_VotingRoundCierre = "UPD_VotingRoundCierre";
            public const string GET_VotingRoundsByAgendaItem = "GET_VotingRoundsByAgendaItem";
            public const string GET_VotingRoundById = "GET_VotingRoundById";
            public const string INS_Vote = "INS_Vote";
            public const string DEL_Vote = "DEL_Vote";
            public const string GET_VotesByVotingRound = "GET_VotesByVotingRound";

            // Gobernanza / MeetingMinutes Procedures -- Docs/Pendientes-Negocio-Consolidado.md #21, Fase 3
            public const string INS_MeetingMinutes = "INS_MeetingMinutes";
            public const string UPD_MeetingMinutesContenido = "UPD_MeetingMinutesContenido";
            public const string UPD_MeetingMinutesFirma = "UPD_MeetingMinutesFirma";
            public const string GET_MeetingMinutesByMeeting = "GET_MeetingMinutesByMeeting";

            // Calendar Procedures
            public const string INS_CalendarItem = "INS_CalendarItem";
            public const string UPD_CalendarItem = "UPD_CalendarItem";
            public const string UPD_CalendarItemStatus = "UPD_CalendarItemStatus";
            public const string DEL_CalendarItem = "DEL_CalendarItem";
            public const string GET_CalendarItemsByBuilding = "GET_CalendarItemsByBuilding";
            public const string GET_CalendarItemById = "GET_CalendarItemById";

            // Employee y Payroll -- Fase 1 (Database/Scripts/2026-09-15_108_Employee_Payroll_Fase1.sql)
            public const string INS_Employee = "INS_Employee";
            public const string UPD_Employee = "UPD_Employee";
            public const string GET_EmployeeByAccount = "GET_EmployeeByAccount";
            public const string GET_EmployeeById = "GET_EmployeeById";

            public const string INS_Shift = "INS_Shift";
            public const string UPD_Shift = "UPD_Shift";
            public const string GET_ShiftsByAccount = "GET_ShiftsByAccount";

            public const string INS_EmployeeShiftAssignment = "INS_EmployeeShiftAssignment";
            public const string GET_AsignacionesShiftByEmployee = "GET_AsignacionesShiftByEmployee";

            public const string INS_EmployeeBuildingAssignment = "INS_EmployeeBuildingAssignment";
            public const string UPD_EmployeeBuildingAssignment_Cerrar = "UPD_EmployeeBuildingAssignment_Cerrar";
            public const string GET_AsignacionesEdificioByEmployee = "GET_AsignacionesEdificioByEmployee";
            public const string GET_AsignacionesEdificioByBuilding = "GET_AsignacionesEdificioByBuilding";

            public const string INS_TimeEntry = "INS_TimeEntry";
            public const string UPD_TimeEntry = "UPD_TimeEntry";
            public const string DEL_TimeEntry = "DEL_TimeEntry";
            public const string GET_TimeEntryByEmployee = "GET_TimeEntryByEmployee";

            public const string INS_HolidayConfiguration = "INS_HolidayConfiguration";
            public const string UPD_HolidayConfiguration = "UPD_HolidayConfiguration";
            public const string DEL_HolidayConfiguration = "DEL_HolidayConfiguration";
            public const string GET_FeriadosByAccountAndYear = "GET_FeriadosByAccountAndYear";

            // Employee y Payroll -- Fase 2 (Database/Scripts/2026-09-15_109_Employee_Payroll_Fase2.sql)
            public const string INS_LaborRegimeConfiguration = "INS_LaborRegimeConfiguration";
            public const string GET_LaborRegimeConfigurationVigente = "GET_LaborRegimeConfigurationVigente";
            public const string GET_LaborRegimeConfigurationHistorial = "GET_LaborRegimeConfigurationHistorial";

            public const string INS_LegalParameters = "INS_LegalParameters";
            public const string UPD_LegalParameters = "UPD_LegalParameters";
            public const string GET_LegalParametersByAccountAndYear = "GET_LegalParametersByAccountAndYear";

            public const string INS_Vacation = "INS_Vacation";
            public const string UPD_VacationEstado = "UPD_VacationEstado";
            public const string GET_VacationByEmployee = "GET_VacationByEmployee";
            public const string GET_VacationPendientesByAccount = "GET_VacationPendientesByAccount";
            public const string GET_VacationGozadasByEmployeeAnio = "GET_VacationGozadasByEmployeeAnio";

            public const string INS_Payslip = "INS_Payslip";
            public const string GET_PayslipsByEmployee = "GET_PayslipsByEmployee";
            public const string GET_PayslipById = "GET_PayslipById";
            public const string GET_PayslipsByAccountAndPeriodo = "GET_PayslipsByAccountAndPeriodo";
            public const string INS_PayslipDetail = "INS_PayslipDetail";
            public const string GET_PayslipDetailByPayslip = "GET_PayslipDetailByPayslip";

            // Employee y Payroll -- Fase 3, Permisos y licencias
            // (Database/Scripts/2026-09-15_110_Employee_Payroll_Fase3_Permisos.sql)
            public const string INS_LeaveRequest = "INS_LeaveRequest";
            public const string UPD_LeaveRequestEstado = "UPD_LeaveRequestEstado";
            public const string GET_LeaveRequestByEmployee = "GET_LeaveRequestByEmployee";
            public const string GET_LeaveRequestPendientesByAccount = "GET_LeaveRequestPendientesByAccount";
            public const string GET_LeaveRequestSinGoceDiasByEmployeeMes = "GET_LeaveRequestSinGoceDiasByEmployeeMes";
        }
        #endregion

        #region Helper Methods

        // Cada operación de BDLayout ahora crea su propio SpiderHoodContext de corta vida
        // (ver el constructor), así que la colisión de concurrencia que este reintento
        // mitigaba ya no debería ocurrir DESDE BDLayout. Se deja como red de seguridad:
        // otros servicios del proyecto todavía comparten un SpiderHoodContext inyectado
        // como Scoped y podrían seguir generando el mismo InvalidOperationException hasta
        // que también migren a IDbContextFactory.
        private const int MaxConcurrencyRetries = 3;

        private static bool IsConcurrentDbContextUsage(Exception ex) =>
            ex is InvalidOperationException &&
            ex.Message.Contains("A second operation was started on this context instance", StringComparison.OrdinalIgnoreCase);

        private async Task<T> ExecuteWithErrorHandlingAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            for (int attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
            {
                try
                {
                    //_logger.LogDebug($"Excute operation {operation.ToString()}");
                    return await operation();
                }
                catch (Exception ex) when (IsConcurrentDbContextUsage(ex) && attempt < MaxConcurrencyRetries)
                {
                    await Task.Delay(75 * attempt, cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    // _logger.LogError(ex, "Database update error during {OperationName}: {Message}", operationName, ex.Message);
                    throw new RepositoryException($"Database update failed for {operationName}: {ex.InnerException?.Message ?? ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    //_logger.LogError(ex, "Error during {OperationName}: {Message}", operationName, ex.Message);
                    // Antes el mensaje era sólo "Operation {operationName} failed", sin el
                    // detalle real -- Docs/Pendientes-Negocio-Migracion.md #6.6: un usuario
                    // viendo "Error al cargar transacciones: Operation
                    // GetBankTransactionsNoConciliedAsync failed" en pantalla no tiene forma de
                    // saber si es un timeout, una violación de constraint u otra cosa, y acá no
                    // hay logging real (ver comentarios apagados arriba) para mirarlo del lado
                    // servidor. ex.InnerException es la SqlException/excepción real de ADO.NET;
                    // se antepone ex.Message (el mensaje de más alto nivel) sólo si no hay inner.
                    throw new RepositoryException($"Operation {operationName} failed: {ex.InnerException?.Message ?? ex.Message}", ex);
                }
            }

            throw new RepositoryException($"Operation {operationName} failed after {MaxConcurrencyRetries} attempts");
        }

        private void ValidateEntity<T>(T entity, string entityName) where T : class
        {
            if (entity == null)
            {
                throw new ArgumentNullException(entityName, $"{entityName} cannot be null");
            }
        }

        private async Task<int> ExecuteStoredProcedureAsync(
            string storedProcedureName,
            CancellationToken cancellationToken = default,
            params object[] parameters)
        {
            // Los parámetros van envueltos en SqlParameter (mismo patrón ya usado en
            // ExecuteQuerySingleAsync/ExecuteQueryListAsync más abajo) -- pasar un
            // DBNull.Value "pelado" dentro del object[] directo a ExecuteSqlRawAsync
            // hace que EF intente inferirle un store type mapping y tire
            // "no store type mapping for properties of type 'DBNull'" (visto en vivo
            // con CommonArea -- Docs/Pendientes-Negocio-Consolidado.md #21). Un
            // SqlParameter ya trae su propio tipo ADO.NET, así que EF no necesita
            // inferir nada.
            var paramNames = new List<string>();
            var sqlParams = new List<object>();

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramName = $"@p{i}";
                paramNames.Add(paramName);
                // Un caller puede mandar ya un SqlParameter armado a mano (p.ej.
                // DefaultCategory/WaterReadingDefault en UpdateRecordAsync(BuildingConfiguration),
                // ExpenseApprovalThreshold en UpdateExpenseApprovalThresholdAsync) para tipar
                // explícito un DBNull -- envolverlo de nuevo en "new SqlParameter(paramName,
                // parameters[i])" pone ese SqlParameter como Value de OTRO SqlParameter, y ADO.NET
                // no sabe mapear un SqlParameter como valor ("No mapping exists from object type
                // Microsoft.Data.SqlClient.SqlParameter..."). Si ya es un SqlParameter, se reusa
                // tal cual, sólo renombrado para que coincida con el placeholder posicional.
                if (parameters[i] is SqlParameter existingParam)
                {
                    existingParam.ParameterName = paramName;
                    sqlParams.Add(existingParam);
                }
                else
                {
                    sqlParams.Add(new SqlParameter(paramName, parameters[i] ?? DBNull.Value));
                }
            }

            var sql = $"{storedProcedureName} {string.Join(", ", paramNames)}";

            //_logger.LogDebug("Executing stored procedure: {Sql}", sql);

            var dbContext = await RentContextAsync(cancellationToken);
            try
            {
                return await dbContext.Database.ExecuteSqlRawAsync(sql, sqlParams.ToArray(), cancellationToken);
            }
            finally
            {
                ReturnContext(dbContext);
            }
        }

        private async Task<T?> ExecuteQuerySingleAsync<T>(
            string storedProcedureName,
            params object[] parameters) where T : class
        {
            // Build parameter list for SQL
            var paramNames = new List<string>();
            var sqlParams = new List<object>();

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramName = $"@p{i}";
                paramNames.Add(paramName);

                // Create SqlParameter for better type handling -- si ya viene un SqlParameter
                // armado a mano (para tipar un DBNull explícito), se reusa en vez de envolverlo
                // de nuevo (ver comentario equivalente en ExecuteStoredProcedureAsync).
                if (parameters[i] is SqlParameter existingParam)
                {
                    existingParam.ParameterName = paramName;
                    sqlParams.Add(existingParam);
                }
                else
                {
                    sqlParams.Add(new SqlParameter(paramName, parameters[i] ?? DBNull.Value));
                }
            }

            var sql = $"EXEC {storedProcedureName} {string.Join(", ", paramNames)}";

            var dbContext = await RentContextAsync();
            try
            {
                var item = await dbContext.Set<T>()
                    .FromSqlRaw(sql, sqlParams.ToArray())
                    .AsNoTracking()
                    .ToListAsync();

                return item.FirstOrDefault();
            }
            finally
            {
                ReturnContext(dbContext);
            }
        }

        // FIXED: Use FromSqlRaw with EXEC and call AsEnumerable() for client-side evaluation
        private async Task<List<T>> ExecuteQueryListAsync<T>(
            string storedProcedureName,
            params object[] parameters) where T : class
        {
            // Build parameter list for SQL
            var paramNames = new List<string>();
            var sqlParams = new List<object>();

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramName = $"@p{i}";
                paramNames.Add(paramName);

                // Create SqlParameter for better type handling -- ver comentario equivalente
                // en ExecuteStoredProcedureAsync sobre por qué un SqlParameter ya armado se
                // reusa en vez de envolverlo de nuevo.
                if (parameters[i] is SqlParameter existingParam)
                {
                    existingParam.ParameterName = paramName;
                    sqlParams.Add(existingParam);
                }
                else
                {
                    sqlParams.Add(new SqlParameter(paramName, parameters[i] ?? DBNull.Value));
                }
            }

            var sql = $"EXEC {storedProcedureName} {string.Join(", ", paramNames)}";

            var dbContext = await RentContextAsync();
            try
            {
                return await dbContext.Set<T>()
                    .FromSqlRaw(sql, sqlParams.ToArray())
                    .AsNoTracking()
                    .ToListAsync();
            }
            finally
            {
                ReturnContext(dbContext);
            }
        }

        #endregion
    }
}