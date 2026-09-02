using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Paso 5 del plan (Docs/Design-Defaults-Sistema-Mixto.md §5.3): promoción/fusión
    // de duplicados entre edificios. Deliberadamente separado de ParameterService
    // (que cachea la lista de UN edificio a la vez, ver CurrentBuilding) porque esto
    // opera sobre TODOS los edificios a la vez y sólo lo usa un SysAdmin.
    public interface IParameterPromotionService
    {
        Task<List<ParameterPromotionCandidate>> GetCandidatesAsync();
        Task<OperationResult> PromoteAndMergeAsync(int canonicalIdTabla, List<int> duplicateIdTablas);
    }

    public class ParameterPromotionService : IParameterPromotionService
    {
        private readonly BDLayout ec;

        public ParameterPromotionService(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            ec = new BDLayout(contextFactory);
        }

        public async Task<List<ParameterPromotionCandidate>> GetCandidatesAsync()
        {
            return await ec.GetMixtoParameterCandidatesAsync();
        }

        // canonicalIdTabla puede ser una fila ya existente (se promueve en el mismo
        // movimiento -- PromoteParameterToGlobalAsync sobre una fila que ya está
        // global es un no-op seguro) o una recién creada por el propio SysAdmin ya
        // con IdBuilding = Guid.Empty. duplicateIdTablas son las filas por-edificio
        // que se fusionan hacia ella -- nunca se borran, sólo quedan Inactivo +
        // ReplacedByIdTabla (ver §5.2: ningún Parameter se borra de verdad).
        public async Task<OperationResult> PromoteAndMergeAsync(int canonicalIdTabla, List<int> duplicateIdTablas)
        {
            try
            {
                await ec.PromoteParameterToGlobalAsync(canonicalIdTabla);

                foreach (var oldIdTabla in duplicateIdTablas.Where(id => id != canonicalIdTabla))
                {
                    await ec.MergeParameterIntoAsync(oldIdTabla, canonicalIdTabla);
                }

                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                return OperationResult.Failure($"No se pudo completar la fusión: {DescribeError(ex)}");
            }
        }

        // BDLayout envuelve la excepción real en una RepositoryException genérica --
        // mismo patrón que IBuildingService.DescribeError/CategoryService.DescribeError.
        private static string DescribeError(Exception ex)
        {
            var innermost = ex;
            while (innermost.InnerException != null)
                innermost = innermost.InnerException;
            return innermost.Message;
        }
    }
}
