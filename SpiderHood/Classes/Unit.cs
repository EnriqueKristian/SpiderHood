
using DocumentFormat.OpenXml.Wordprocessing;
using Humanizer.Localisation;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    public enum GroupUnitType { Individual = 1, Shared = 0 }
    public enum OwnerType { NaturalPerson = 1, LegalEntity = 2 }

    public class RealEstateUnit
    {
        public Guid IdUnit { get; set; }
        public string UnitNumber { get; set; } = string.Empty;

        [Precision(18, 2)]
        public decimal Area { get; set; }

        public GroupUnitType TypeGroupUnit { get; set; }

        public Guid IdGroupOwner { get; set; }

        public string GroupName { get; set; } = string.Empty;

        [Precision(18, 2)]
        public decimal AreaTotal { get; set; }

        public OwnerType TypeOwner { get; set; }

        public string Names { get; set; } = string.Empty;

        public string Surname { get; set; } = string.Empty;

        public Guid IdBuilding { get; set; }

        public int TypeUnit { get; set; }

        public Guid IdOwner { get; set; }
        
        public int Number { get; set; }

        public bool IsAvailable { get; set; }
        
        public string Building { get; set; } = string.Empty;
    }

    public class UnitView
    {
        public Guid IdUnit { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public Guid IdBuilding { get; set; }
        [Precision(18, 2)]
        public decimal Area { get; set; }
        public int TypeUnit { get; set; }
        public int Number { get; set; } 
        public bool IsAvailable { get; set; }
        public Guid IdGroupUnit { get; set; }


    }
    public class GroupUnit
    {
        public Guid IdUnit { get; set; }
        public Guid IdGroupOwner { get; set; }
        public GroupUnitType TypeGroupUnit { get; set; }
    }

    public class OwnerUnitView
    {
        public Guid IdGroupUnit { get; set; }
        [Precision(18, 2)]
        public decimal TotalArea { get; set; }
        public int GroupNumber { get; set; }
        public Guid IdUnit { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Area { get; set; }
        public int TypeUnit { get; set; }
        public int Number { get; set; }
        public bool IsAvailable { get; set; } = true;
        public Guid IdGroupOwnerRol { get; set; }
        public int Role { get; set; }
        public Guid IdOwner { get; set; }
        public string IdentityDocument { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public Guid IdBuilding { get; set; }
        // Referencia a Parameter.Value del grupo "Tipo de Documento" -- requiere que
        // GET_OwnerByBuilding la traiga en el SELECT (ver
        // Database/Scripts/2026-09-03_34_ApartmentOwner_IdTypeIdNumber.sql).
        public int IdTypeIdNumber { get; set; }
    }
}
