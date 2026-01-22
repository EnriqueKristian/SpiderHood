using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using System;
using System.ComponentModel.DataAnnotations;

namespace SpiderHood.Models
{

    public class Parameter : IValidatableObject
    {
        public int IdTabla { get; set; }

        [Required(ErrorMessage = "La Descripción es obligatoria")]
        public string Description { get; set; } = null!;

        [Required(ErrorMessage = "La Descripción corta es obligatoria")]
        public string ShortDescription { get; set; } = null!;

        [Required(ErrorMessage = "El Valor es obligatorio")]
        public int Value { get; set; }

        public int Sort { get; set; } = 0;
        public int IdParent { get; set; }

        [Required(ErrorMessage = "El Estado es obligatorio")]
        public ParameterEstado Estado { get; set; }
        public Guid IdBuilding { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Description == ShortDescription)
            {
                yield return new ValidationResult(
                    "La descripción y la descripción corta no pueden ser iguales",
                    new[] { nameof(Description), nameof(ShortDescription) }
                );
            }
        }
    }

    public class NotDefaultAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value is string strValue)
            {
                return !strValue.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase);
            }
            return true; // Si no es string, no aplica la validación
        }
    }


    public enum ParameterEstado
    {
        Activo = 1,
        Inactivo = 2
    }

    public enum OpenType
    {
        ReadOnly = 1,
        Create = 2,
        Update = 3,
        Delete = 4
    }

    public enum TypeDistribution
    {
        Distribution = 8,
        Fija = 1,
        Proporcional = 2
    }

    public enum ParamParent
    {
        State = 1,
        UnitType = 4,
        ExpenseDistribution = 8,
        DocumentType = 11
    }
}