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

        // Sistema/Mixto (Docs/Design-Defaults-Sistema-Mixto.md, Paso 3): en la BD,
        // IdBuilding admite NULL -- mismo patrón que IdParent ya usa para marcar
        // raíz. GET_AllParameters coalesa el NULL a Guid.Empty (ver
        // ISNULL(IdBuilding, '00000000-...')), así que acá se lee como Guid.Empty,
        // nunca null -- IdBuilding == Guid.Empty identifica un valor de Sistema
        // (global, todos los edificios); cualquier otro guid es un valor Mixto
        // propio de ese edificio. La raíz de un grupo SIEMPRE tiene
        // IdBuilding == Guid.Empty, sea el grupo Sistema o Mixto.
        public Guid IdBuilding { get; set; }

        // Sólo tiene sentido en un hijo de un grupo Mixto: true si vino clonado del
        // Edificio Template al crear el edificio, false si lo agregó el admin a
        // mano. En la RAÍZ de un grupo indica si el grupo entero es Sistema (true)
        // o Mixto (false). Es informativo -- nunca habilita/deshabilita borrado
        // real (ningún Parameter se borra, sólo se inactiva).
        public bool IsSystemDefault { get; set; }

        // Paso 5 (Docs/Design-Defaults-Sistema-Mixto.md §5.3, promoción/fusión de
        // duplicados): apunta al IdTabla del valor que reemplazó a este cuando un
        // SysAdmin lo fusiona con un duplicado promovido a global. Sólo tiene sentido
        // junto con Estado=Inactivo -- nunca se borra la fila vieja (no hay forma
        // barata de saber si está en uso), así que el histórico que la referencia
        // sigue viéndose bien; sólo los reportes que agrupan por Parameter deberían
        // seguir esta cadena en vez de tratarla como un valor más.
        public int? ReplacedByIdTabla { get; set; }

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

    // Paso 5: fila candidata a fusión/promoción -- un hijo Mixto activo de un
    // edificio puntual, con el nombre de su grupo y de su edificio para que un
    // SysAdmin pueda detectar a ojo duplicados entre edificios (GET_MixtoParameterCandidates
    // sólo trae hijos de grupos Mixto, activos y todavía no globales -- ver
    // Docs/Design-Defaults-Sistema-Mixto.md §5.3). Sin FK a ninguna tabla real --
    // es sólo la proyección de una consulta de diagnóstico, nunca se guarda.
    public class ParameterPromotionCandidate
    {
        public int IdTabla { get; set; }
        public string ShortDescription { get; set; } = null!;
        public string Description { get; set; } = null!;
        public int Value { get; set; }
        public int IdParent { get; set; }
        public string GroupName { get; set; } = null!;
        public Guid IdBuilding { get; set; }
        public string BuildingName { get; set; } = null!;
        public int? ReplacedByIdTabla { get; set; }
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