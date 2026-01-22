using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace SpiderHood.Models
{
    public class Category
    {
        public Guid IdCategory { get; set; }

        [Required(ErrorMessage = "La descripción es obligatorio")]
        public string Description { get; set; } = null!;

        [Required(ErrorMessage = "La Descripción Corta es obligatorio")]
        public string ShortDescript { get; set; } = null!;
        //public Guid IdOwner { get; set; }
        public Guid IdBuilding { get; set; }
        public Guid ParentId { get; set; }

        [Required(ErrorMessage = "Selecciona un icono.")]
        public string Icon { get; set; } = null!;

        [Required(ErrorMessage = "Selecciona un color.")]
        [RegularExpression("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$", ErrorMessage = "Color inválido.")]
        public string Color { get; set; } = null!;

        public string Ruta { get; set; } = null!;
        public string ParentName { get; set; } = null!;
        public int Nivel { get; set; }
        public TypeDistribution Distribution { get; set; } = TypeDistribution.Fija;
    }


    public interface IRepository<T> where T : class
    {
        Task AddAsync(T entity);
        Task<T> GetByIdAsync(Guid id);
        Task<IEnumerable<T>> GetAllAsync();
        void Update(T entity);
        void Remove(T entity);
    }

    public interface IUnitOfWork : IDisposable
    {
        IRepository<Category> Categories { get; }
        IRepository<BudgetHeader> BudgetHeaders { get; }
        IRepository<BudgetDetail> ExpenseItems { get; }
        Task<int> CompleteAsync();
    }


    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly DbContext _context;
        private readonly DbSet<T> _dbSet;

        public Repository(DbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public async Task AddAsync(T entity) => await _dbSet.AddAsync(entity);
        public async Task<T> GetByIdAsync(Guid id) => await _dbSet.FindAsync(id);
        public async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.ToListAsync();
        public void Update(T entity) => _dbSet.Update(entity);
        public void Remove(T entity) => _dbSet.Remove(entity);
    }


    public class UnitOfWork : IUnitOfWork
    {
        private readonly DbContext _context;

        public IRepository<Category> Categories { get; }
        public IRepository<BudgetHeader> BudgetHeaders { get; }
        public IRepository<BudgetDetail> ExpenseItems { get; }

        public UnitOfWork(DbContext context)
        {
            _context = context;
            Categories = new Repository<Category>(context);
            BudgetHeaders = new Repository<BudgetHeader>(context);
            ExpenseItems = new Repository<BudgetDetail>(context);
        }

        public async Task<int> CompleteAsync() => await _context.SaveChangesAsync();

        public void Dispose() => _context.Dispose();
    }


}
