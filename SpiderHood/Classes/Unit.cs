
using DocumentFormat.OpenXml.Wordprocessing;
using Humanizer.Localisation;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    public enum GroupUnitType { Individual = 1, Shared = 0 }
    public enum OwnerType { NaturalPerson = 1, LegalEntity = 2 }

    // OJO al leer esta clase: mezcla dos cosas de origen distinto, heredado de cómo
    // se armó GetUnitsByBuildingAsync (BDLayout.Get.cs) -- un JOIN de Unit contra
    // Owner/GroupOwner que devuelve UNA FILA POR CADA DUEÑO de cada unidad. NO se
    // separaron en dos clases todavía porque eso implicaría tocar los ~6 call
    // sites que hoy dependen de esta forma (UnitGroups.razor, AssignUnits.razor,
    // Owners.razor, Home.razor.cs, ModalUnit.razor) -- queda pendiente para
    // cuando haya forma de probarlo en vivo antes de tocar tantos archivos a la
    // vez. Por ahora, la sección de abajo dice qué es qué:
    //
    //   SECCIÓN 1 (real, editable): columnas físicas de dbo.RealEstateUnit.
    //   Son las únicas que persisten INS_Unit/UPD_Unit -- lo que se edita desde
    //   ModalUnit.razor.
    //
    //   SECCIÓN 2 (denormalizada, sólo lectura): NO son columnas de
    //   RealEstateUnit -- vienen del JOIN contra ApartmentOwner/GroupOwner en
    //   GET_UnitsByBuilding. Sólo tienen datos cuando el objeto salió de
    //   GetUnitsByBuildingAsync; en cualquier otro contexto (p.ej. un
    //   RealEstateUnit recién creado en memoria) quedan en su valor default.
    //   Escribirles algo acá NO actualiza ni a Owner ni a GroupOwner -- para eso
    //   están los métodos dedicados (AddOwnerUnitAsync, etc.).
    public class RealEstateUnit
    {
        // ===== SECCIÓN 1: columnas reales de dbo.RealEstateUnit =====

        public Guid IdUnit { get; set; }
        public string UnitNumber { get; set; } = string.Empty;

        [Precision(18, 2)]
        public decimal Area { get; set; }

        public Guid IdBuilding { get; set; }

        public int TypeUnit { get; set; }

        public int Number { get; set; }

        public bool IsAvailable { get; set; }

        // --- Campos propios agregados en
        // Database/Scripts/2026-09-04_49_Unit_ExtraFields.sql. Todos opcionales:
        // unidades cargadas antes de este feature quedan sin estos datos hasta que
        // alguien las edite (fail-open, mismo criterio que el resto de la app).

        // Ubicación física
        public int? Floor { get; set; }
        public string? Tower { get; set; }
        // Texto libre referencial -- ej. "S1", "SS", "Torre A-3" -- para ubicar en
        // qué sótano/nivel/torre está la unidad sin tener que modelar de antemano
        // la estructura de sótanos/torres del edificio.
        public string? LocationCode { get; set; }

        // Sólo DPTO/OFICINA (TypeUnit 1/4, ver ModalUnit.razor)
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        [Precision(18, 2)]
        public decimal? BuiltArea { get; set; }

        // Sólo ESTACIONAMIENTO (TypeUnit 2)
        public bool? IsCovered { get; set; }
        public bool? IsForDisabled { get; set; }
        public string? VehicleType { get; set; }

        // Sólo DEPOSITO (TypeUnit 3)
        [Precision(18, 2)]
        public decimal? Height { get; set; }
        public bool? HasVentilation { get; set; }
        public bool? HasElectricity { get; set; }

        public string? Notes { get; set; }

        // ===== SECCIÓN 2: denormalizado del JOIN con Owner/GroupOwner (sólo
        // lectura, ver comentario de la clase) =====

        public GroupUnitType TypeGroupUnit { get; set; }
        public Guid IdGroupOwner { get; set; }
        public string GroupName { get; set; } = string.Empty;

        [Precision(18, 2)]
        public decimal AreaTotal { get; set; }

        public OwnerType TypeOwner { get; set; }
        public string Names { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public Guid IdOwner { get; set; }

        // Nombre del edificio (no confundir con IdBuilding, de arriba) -- también
        // viene del JOIN, no de dbo.RealEstateUnit.
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
