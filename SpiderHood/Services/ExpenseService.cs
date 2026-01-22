using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IExpenseService
    {
        Task<List<ViewExpense>> ObtenerGastosPendientesConciliacionAsync(BDLayout ec, Guid IdBuilding, DateTime desde, DateTime hasta);
        Task<List<CategoriaGasto>> ObtenerCategoriasAsync();
        Task<ViewExpense> CrearGastoAsync(BDLayout ec, ViewExpense gasto);
        Task MarcarGastoComoConciliadoAsync(Guid gastoId, Guid transaccionId);
        Task DesconciliarGastoAsync(Guid gastoId);
    }

    public class ExpenseService : IExpenseService
    {
        private List<ViewExpense> gastos = new();
        private List<CategoriaGasto> categorias = new();

        public ExpenseService()
        {
            InicializarDatos();
        }

        private void InicializarDatos()
        {
            // Inicializar categorías
            categorias = new List<CategoriaGasto>
            {
                new CategoriaGasto { Id = 1, Nombre = "Servicios", Descripcion = "Agua, luz, gas, internet" },
                new CategoriaGasto { Id = 2, Nombre = "Mantenimiento", Descripcion = "Reparaciones y mejoras" },
                new CategoriaGasto { Id = 3, Nombre = "Limpieza", Descripcion = "Limpieza de áreas comunes" },
                new CategoriaGasto { Id = 4, Nombre = "Seguridad", Descripcion = "Vigilancia y sistemas de seguridad" },
                new CategoriaGasto { Id = 5, Nombre = "Personal", Descripcion = "Sueldos y honorarios" },
                new CategoriaGasto { Id = 6, Nombre = "Suministros", Descripcion = "Materiales y provisiones" },
                new CategoriaGasto { Id = 7, Nombre = "Seguros", Descripcion = "Pólizas de seguro" },
                new CategoriaGasto { Id = 8, Nombre = "Impuestos", Descripcion = "Impuestos y contribuciones" },
                new CategoriaGasto { Id = 9, Nombre = "Varios", Descripcion = "Otros gastos no categorizados" }
            };

            // Inicializar gastos de ejemplo
            gastos = new List<ViewExpense>
            {
                new ViewExpense
                {
                    IdExpense = new Guid(),
                    Description = "Pago Agua",
                    Amount = 1154.70m,
                    ExpenseDate = DateTime.Now.AddDays(-15),
                    Category = "Servicios",
                    Supplier = "CFE",
                    Status = StatusExpense.Approved,
                    Reconciled  = false,
                    ReconciledTransactionId  =new Guid()
                },
                new ViewExpense
                {
                    IdExpense = new Guid(),
                    Description = "Pago Luz",
                    Amount = 1150.20m,
                    ExpenseDate = DateTime.Now.AddDays(-15),
                    Category = "Servicios",
                    Supplier = "CFE",
                    Status =StatusExpense.Approved,
                    Reconciled = false,
                    ReconciledTransactionId = new Guid()
                },
                new ViewExpense
                {
                    IdExpense = new Guid(),
                    Description = "Mantenimiento Ascensor",
                    Amount = 8500.00m,
                    ExpenseDate = DateTime.Now.AddDays(-10),
                    Category = "Mantenimiento",
                    Supplier = "Elevadores MX",
                    Status = StatusExpense.Approved,
                    Reconciled = false
                },
                new ViewExpense
                {
                    IdExpense = new Guid(),
                    Description = "Mantenimiento Puerta",
                    Amount = 8500.00m,
                    ExpenseDate = DateTime.Now.AddDays(-10),
                    Category = "Mantenimiento",
                    Supplier = "Don Jorge Cerrajero",
                    Status =StatusExpense.Approved,
                    Reconciled = false
                },
                new ViewExpense
                {
                    IdExpense = new Guid(),
                    Description = "Limpieza Semanal",
                    Amount = 3200.00m,
                    ExpenseDate = DateTime.Now.AddDays(-5),
                    Category = "Limpieza",
                    Supplier = "Limpieza Total",
                    Status = StatusExpense.Approved,
                    Reconciled = false
                }
            };
        }

        public async Task<List<ViewExpense>> ObtenerGastosPendientesConciliacionAsync(BDLayout ec, Guid IdBuilding, DateTime desde, DateTime hasta)
        {
            return await ec.getPendingConciliationExpenses(IdBuilding, desde, hasta);
            /*return gastos
                .Where(g => !g.Conciliado &&
                           g.Fecha >= desde &&
                           g.Fecha <= hasta &&
                           g.Estado == "Aprobado")
                .ToList();*/
        }

        public async Task<List<CategoriaGasto>> ObtenerCategoriasAsync()
        {
            return categorias;
        }

        public async Task<ViewExpense> CrearGastoAsync(BDLayout ec, ViewExpense gasto)
        {

            gasto.IdExpense = Guid.NewGuid(); //gastos.Max(g => g.Id) + 1;
            await ec.AddNewRecordAsync(gasto);
            gasto.ExpenseDate = DateTime.Now;
            gastos.Add(gasto);

            return gasto;
        }

        public async Task MarcarGastoComoConciliadoAsync(Guid gastoId, Guid transaccionId)
        {
            var gasto = gastos.FirstOrDefault(g => g.IdExpense == gastoId);
            if (gasto != null)
            {
                gasto.Reconciled = true;
                gasto.ReconciledTransactionId = new Guid(); //transaccionId;
            }
        }
        
        public async Task DesconciliarGastoAsync(Guid gastoId)
        {
            var gasto = gastos.FirstOrDefault(g => g.IdExpense == gastoId);
            if (gasto != null)
            {
                gasto.Reconciled = false;
                gasto.ReconciledTransactionId = Guid.Empty;
            }
        }
    }
}