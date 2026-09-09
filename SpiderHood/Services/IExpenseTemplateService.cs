using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Docs/Pendientes-Negocio-Conciliacion.md #5 -- "Guardar como plantilla para
    // transacciones similares" en CreateExpenseFromTransactionModal.razor.
    public interface IExpenseTemplateService
    {
        // Trae la plantilla cuyo DescriptionPattern es prefijo de `descripcion` (case-
        // insensitive) -- si hay más de una que matchee, la más larga/específica gana. Null
        // si ninguna aplica. Sólo para PRE-LLENAR el formulario -- nunca crea ni concilia
        // nada por su cuenta.
        Task<ExpenseTemplate?> BuscarPlantillaAsync(Guid idBuilding, string descripcion);

        // Upsert por (IdBuilding, DescriptionPattern) case-insensitive -- volver a guardar
        // la misma descripción con otra categoría/distribución actualiza la plantilla
        // existente en vez de duplicarla (no hay pantalla de gestión en v1, ver doc).
        Task GuardarPlantillaAsync(Guid idBuilding, string descripcion, Guid idCategory, TypeDistribution distribution, string? supplier, string performedBy);
    }

    public class ExpenseTemplateService : IExpenseTemplateService
    {
        private BDLayout ec { get; set; }

        public ExpenseTemplateService(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            ec = new BDLayout(contextFactory);
        }

        public async Task<ExpenseTemplate?> BuscarPlantillaAsync(Guid idBuilding, string descripcion)
        {
            if (string.IsNullOrWhiteSpace(descripcion)) return null;

            var plantillas = await ec.GetExpenseTemplatesByBuildingAsync(idBuilding);
            return plantillas
                .Where(p => descripcion.StartsWith(p.DescriptionPattern, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.DescriptionPattern.Length)
                .FirstOrDefault();
        }

        public async Task GuardarPlantillaAsync(Guid idBuilding, string descripcion, Guid idCategory, TypeDistribution distribution, string? supplier, string performedBy)
        {
            var descripcionNormalizada = descripcion.Trim();
            if (string.IsNullOrWhiteSpace(descripcionNormalizada)) return;

            var existentes = await ec.GetExpenseTemplatesByBuildingAsync(idBuilding);
            var existente = existentes.FirstOrDefault(p =>
                p.DescriptionPattern.Equals(descripcionNormalizada, StringComparison.OrdinalIgnoreCase));

            if (existente != null)
            {
                existente.IdCategory = idCategory;
                existente.Distribution = distribution;
                existente.Supplier = supplier;
                existente.ModifiedBy = performedBy;
                await ec.UpdateRecordAsync(existente);
            }
            else
            {
                await ec.AddNewRecordAsync(new ExpenseTemplate
                {
                    IdBuilding = idBuilding,
                    DescriptionPattern = descripcionNormalizada,
                    IdCategory = idCategory,
                    Distribution = distribution,
                    Supplier = supplier,
                    CreatedBy = performedBy
                });
            }
        }
    }
}
