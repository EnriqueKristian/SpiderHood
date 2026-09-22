using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    public class Building
    {
        public Guid IdBuilding { get; set; }
        public int Number { get; set; }
        public string Name { get; set; } = "";
        public string Location { get; set; } = "";
        public int Type { get; set; }
        public int Floors { get; set; }
        public int Basements { get; set; }
        public int Apartments { get; set; }
        public int Parkings { get; set; }
        public int Deposits { get; set; }
        public int Others { get; set; }
        [Precision(18, 2)]
        public decimal TotalArea { get; set; }
        public bool IsActive { get; set; } = true;
        // Edificio Template (Docs/Design-Defaults-Sistema-Mixto.md, Paso 2): un
        // Building normal marcado IsTemplate=1 -- lo edita el SysAdmin con las
        // mismas pantallas que cualquier edificio real, y sirve como fuente de los
        // valores default (BuildingConfiguration) que se clonan al crear un
        // edificio nuevo. No hay nada que fuerce que sea único -- se puede tener
        // más de uno marcado (p.ej. varios "demo"); GET_TemplateBuilding trae uno
        // solo (TOP 1) sin ninguna lógica de cuál "gana" entre varios.
        public bool IsTemplate { get; set; }
        // Cuenta de facturación dueña de este edificio (Docs/Design-Account-Facturacion.md)
        // -- de acá sale el conteo de MaxBuildings del plan. Nullable a propósito:
        // edificios creados antes de este feature quedan en NULL (fail-open).
        public Guid? IdAccount { get; set; }

        // --- Campos propios agregados en
        // Database/Scripts/2026-09-22_134_Building_Unit_Owner_Persist_RedesignFields.sql.
        // Todos opcionales (fail-open, mismo criterio que Unit/Owner ExtraFields):
        // edificios cargados antes de este feature quedan sin estos datos hasta que
        // alguien los edite. Contacto propio del edificio (Tab 1/4 de BuildingPage.razor)
        // -- distinto del contacto de facturación de Account (RazonSocial/RucDni/Telefono).
        public int? ConstructionYear { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public int? Elevators { get; set; }
        public string? AdminName { get; set; }
        public string? AdminPhone { get; set; }
        public string? EmergencyPhone { get; set; }
        public string? OfficeHours { get; set; }
        // bool no-nullable (no bool?): Blazor's InputCheckbox<T> (usado en
        // BuildingPage.razor) sólo soporta bool -- por eso estas columnas son BIT
        // NOT NULL DEFAULT(0) en vez de NULL como el resto de los campos fail-open
        // de este mismo script.
        public bool HasPool { get; set; }
        public bool HasGym { get; set; }
        public bool HasBBQ { get; set; }
        public bool HasEventRoom { get; set; }
        public bool HasPetArea { get; set; }
        public bool HasGreenAreas { get; set; }
        public bool Has247Security { get; set; }
        public bool HasPorter { get; set; }
        public bool HasCameras { get; set; }

        [NotMapped]
        public BuildingConfiguration Configuration { get; set; } = new();
        public Building Clone()
        {
            return new Building
            {
                IdBuilding = this.IdBuilding,
                Number = this.Number,
                Name = this.Name,
                Location = this.Location,
                Type = this.Type,
                Floors = this.Floors,
                Basements = this.Basements,
                Apartments = this.Apartments,
                Parkings = this.Parkings,
                Deposits = this.Deposits,
                Others = this.Others,
                TotalArea = this.TotalArea,
                IsActive = this.IsActive,
                IsTemplate = this.IsTemplate,
                IdAccount = this.IdAccount,
                ConstructionYear = this.ConstructionYear,
                Phone = this.Phone,
                Email = this.Email,
                Elevators = this.Elevators,
                AdminName = this.AdminName,
                AdminPhone = this.AdminPhone,
                EmergencyPhone = this.EmergencyPhone,
                OfficeHours = this.OfficeHours,
                HasPool = this.HasPool,
                HasGym = this.HasGym,
                HasBBQ = this.HasBBQ,
                HasEventRoom = this.HasEventRoom,
                HasPetArea = this.HasPetArea,
                HasGreenAreas = this.HasGreenAreas,
                Has247Security = this.Has247Security,
                HasPorter = this.HasPorter,
                HasCameras = this.HasCameras,
                Configuration = this.Configuration.Clone()
            };
        }
    }

    public class BuildingConfiguration
    {
        public Guid IdBuildingConfiguration { get; set; }
        public string Currency { get; set; } = "PEN";
        public List<BankAccount> BankAccounts { get; set; } = [];
        public List<string> PaymentMethods { get; set; } = [];
        public int PaymentPeriod { get; set; }
        public int DueDay { get; set; } = 5;
        [Precision(18, 2)]
        public decimal FineAmount { get; set; } = 10.00m;
        [Precision(18, 2)]
        public decimal MinWaterConsumtion { get; set; } = 12.05m;
        [Precision(18, 2)]
        public decimal DefaultFixedCharge { get; set; } = 5.04m;
        [Precision(18, 2)]
        public decimal LateInterestRate { get; set; } = 2.00m;
        public int InvoiceDay { get; set; } = 1;

        // Días de atraso (desde la fecha de vencimiento) que definen cómo se resalta la
        // deuda de una cuota en el Listado de Cuotas: <= DebtWarningDays sin color,
        // entre DebtWarningDays y DebtCriticalDays en naranja, > DebtCriticalDays en rojo.
        // NOTA: por ahora solo viven en memoria - UPD_BuildingConfiguration todavía no los
        // guarda, falta actualizar el stored procedure/columna en la base de datos.
        public int DebtWarningDays { get; set; } = 30;
        public int DebtCriticalDays { get; set; } = 60;

        // Texto configurable del pie del recibo de mantenimiento (PDF), con placeholders
        // {DPTO}, {Propietario}, {NroCta}, {Banco}, {Titular}, {CCI}, {Administrador},
        // {CorreoADM} resueltos por InstallmentExportService al generar el recibo ({CCI}
        // sale de BankAccount.CCI). Vacío = se omite (el recibo solo muestra "Generado
        // el: ..."). Columna agregada por
        public string ReceiptFooterText { get; set; } = "";

        // Monto a partir del cual un gasto MANUAL (creado desde /expense) requiere
        // aprobación de la Junta antes de quedar activo -- null = nunca requiere
        // aprobación. Los gastos generados desde la conciliación del Estado de Cuenta
        // (ViewExpense.Reconciled = true) están SIEMPRE exentos, sea cual sea el monto,
        // porque reflejan un pago ya efectuado (ver ExpensePage.razor,
        // ResolverEstadoAlGuardar). Se guarda con su propio UPD chico
        // (UPD_BuildingConfiguration_ExpenseThreshold) en vez de sumarse a la lista
        // posicional de UPD_BuildingConfiguration -- ver
        // Database/Scripts/2026-09-11_92_Expense_Approval_Threshold.sql para el porqué.
        [Precision(18, 2)]
        public decimal? ExpenseApprovalThreshold { get; set; }

        [NotMapped]
        public Contact AdminContact { get; set; } = new();
        [NotMapped]
        public Contact RealEstateCompany { get; set; } = new();
        [NotMapped]
        public Contact MaintenanceCompany { get; set; } = new();

        public List<Exoneration> Exonerations { get; set; } = [];
        public Guid IdBuilding { get; set; }
        // Guid? y no Guid: un edificio recién creado no tiene ninguna Category propia
        // todavía (nada se clona al crearlo -- eso es un paso pendiente del plan en
        // Docs/Design-Defaults-Sistema-Mixto.md), así que estos dos quedan NULL en la
        // BD hasta que un admin los elija en "Configuración Rápida". Si esto fuera Guid
        // no-nullable, EF explota con SqlNullValueException al leer GET_AllBuildingsConfig
        // apenas exista un Building con estos campos en NULL -- eso rompía la
        // reconstrucción de sesión entera (bucle de login) para cualquier usuario con
        // acceso a ese edificio, sysadmin incluido.
        public Guid? DefaultCategory { get; set; }
        public Guid? WaterReadingDefault { get; set; }
        public BuildingConfiguration Clone()
        {
            return new BuildingConfiguration
            {
                IdBuildingConfiguration = this.IdBuildingConfiguration,
                Currency = this.Currency,
                BankAccounts = this.BankAccounts.Select(b => b.Clone()).ToList(),
                PaymentMethods = [.. this.PaymentMethods],
                PaymentPeriod = this.PaymentPeriod,
                DueDay = this.DueDay,
                FineAmount = this.FineAmount,
                LateInterestRate = this.LateInterestRate,
                InvoiceDay = this.InvoiceDay,
                DebtWarningDays = this.DebtWarningDays,
                DebtCriticalDays = this.DebtCriticalDays,
                ReceiptFooterText = this.ReceiptFooterText,
                ExpenseApprovalThreshold = this.ExpenseApprovalThreshold,
                AdminContact = this.AdminContact.Clone(),
                RealEstateCompany = this.RealEstateCompany.Clone(),
                MaintenanceCompany = this.MaintenanceCompany.Clone(),
                IdBuilding = this.IdBuilding,
                DefaultCategory = this.DefaultCategory,
                WaterReadingDefault = this.WaterReadingDefault,
                Exonerations = this.Exonerations.Select(e => e.Clone()).ToList()
            };
        }
    }

    public class Contact
    {
        public Guid IdContact { get; set; }
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string Address { get; set; } = "";
        public int TypeContact { get; set; }
        public string OfficePhone { get; set; } = "";
        public string MobilePhone { get; set; } = "";
        public Guid IdRelatedEntity { get; set; }
        public Contact Clone() => (Contact)this.MemberwiseClone();
    }

    public class Currency
    {
        public string Code { get; set; } = "";
        public string Symbol { get; set; } = "";
        public string Name { get; set; } = "";
    }
}