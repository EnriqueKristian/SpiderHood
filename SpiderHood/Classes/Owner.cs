using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    public class Owner
    {
        public Guid IdGroupOwner { get; set; }
        
        public string GroupName { get; set; } = null!;
        [Precision(18, 2)]
        public decimal AreaTotal { get; set; }
        public Guid IdOwner { get; set; }
        public int TypeOwner { get; set; }
        public string IdNumber { get; set; } = null!;

        [Required(ErrorMessage = "El Nombre es obligatorio")]
        public string Names { get; set; } = null!;
        public string? Surname { get; set; }
        public string Address { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public int IdTypeIdNumber { get; set; }
        public Guid IdBuilding { get; set; }

        public string FullName => $"{Names} {Surname ?? ""}";

    }

    public class OwnerUnit
    {
        public Guid IdGroupOwner { get; set; }
        public string GroupName { get; set; } = null!;
        [Precision(18, 2)]
        public decimal AreaTotal { get; set; }
        public Guid IdOwner { get; set; }
        public int TypeOwner { get; set; }
        // GroupUnit.GroupNumber (int) -- GroupUnit no tiene columna de nombre, sólo
        // este número (ver Database/Scripts/2026-09-03_37_*). Se muestra como
        // "Departamento {N}" en la grilla de /Owners.
        public int GroupNumber { get; set; }

        [Required(ErrorMessage = "El Nombre es obligatorio")]
        public string Names { get; set; } = null!;
        public string? Surname { get; set; }
        public int TypeGroupUnit { get; set; }
        public int IdUnitType { get; set; }
        public string UnitNumber { get; set; }=string.Empty!;
        public string TypeUnit { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Area { get; set; }

        public int Floor { get { return int.Parse(GroupName.Split(' ')[1]) / 100; } }
        public string Estado { get { return "Activo"; } }
        public string FullName => $"{Names} {Surname ?? ""}";
        public string Observaciones => $"{Names} {Surname ?? ""}";
        public DateTime FechaAdquisicion { get { return DateTime.Now; } }
        public int Number { get; set; }

    }

    public class GroupOwner { 
        public Guid IdGroupOwner { get; set; }
        public Guid GroupName { get; set; }
        public int Number { get; set; }
        [Precision(18, 2)]
        public decimal AreaTotal { get; set; }
        [NotMapped]
        public List<Owner>? GroupOwners { get; set; }
        [NotMapped]
        public List<GroupUnit>? GroupUnits { get; set; }
    }


    public class OwnerGroupOwner
    {
        public Guid IdGroupOwner { get; set; }
        public Guid IdOwner { get; set; } 
        public int TypeOwner { get; set; }
    }
}
