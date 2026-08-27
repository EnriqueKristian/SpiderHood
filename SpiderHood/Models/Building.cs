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

        [NotMapped]
        public Contact AdminContact { get; set; } = new();
        [NotMapped]
        public Contact RealEstateCompany { get; set; } = new();
        [NotMapped]
        public Contact MaintenanceCompany { get; set; } = new();

        public List<Exoneration> Exonerations { get; set; } = [];
        public Guid IdBuilding { get; set; }
        public Guid DefaultCategory { get; set; }
        public Guid WaterReadingDefault { get; set; }
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