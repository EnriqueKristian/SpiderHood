// Services/DepartamentoService.cs
using SpiderHood.Data;
using SpiderHood.Models;
using SpiderHood.Services;

public class DepartamentoService : IDepartamentoService
{
    public SpiderHoodContext _context = default!;
    private ParameterService ParameterService { get; set; } = default!;

    public DepartamentoService(SpiderHoodContext context)
    {
        _context = context;
    }

    public async Task<List<Departamento>> ObtenerDepartamentosActivosAsync()
    {
        return await ParameterService.ec.GetDptos();
        /*return await _context.Departamentos
            .Where(d => d.Activo)
            .OrderBy(d => d.Nombre)
            .ToListAsync();*/
    }

    public async Task<Departamento> ObtenerDepartamentoPorIdAsync(int id)
    {
        // The original code was returning a List<Departamento> from getDptos(), but the method expects a single Departamento.
        // To fix CS0029, fetch the list and return the Departamento with the matching id.
        var departamentos = await ParameterService.ec.GetDptos();
        return departamentos.FirstOrDefault(d => d.Id == id)!;
    }

    public async Task<Departamento> CrearDepartamentoAsync(Departamento departamento)
    {
        //_context.Departamentos.Add(departamento);
        await _context.SaveChangesAsync();
        return departamento;
    }

    public async Task<bool> ActualizarDepartamentoAsync(Departamento departamento)
    {
        /*var deptoExistente = await _context.Departamentos.FindAsync(departamento.Id);
        if (deptoExistente == null)
            return false;

        _context.Entry(deptoExistente).CurrentValues.SetValues(departamento);*/
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> EliminarDepartamentoAsync(int id)
    {
        /*var departamento = await _context.Departamentos.FindAsync(id);
        if (departamento == null)
            return false;

        // Soft delete
        departamento.Activo = false;*/
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Dictionary<int, decimal>> CalcularPorcentajesAreaAsync()
    {
        var departamentos = await ObtenerDepartamentosActivosAsync();
        var totalArea = departamentos.Sum(d => d.AreaM2);

        var porcentajes = new Dictionary<int, decimal>();

        foreach (var depto in departamentos)
        {
            var porcentaje = totalArea > 0 ? (depto.AreaM2 / totalArea) * 100 : 0;
            porcentajes.Add(depto.Id, Math.Round(porcentaje, 2));
        }

        return porcentajes;
    }

    public async Task<decimal> ObtenerAreaTotalAsync()
    {
        var departamentos = await ObtenerDepartamentosActivosAsync();
        return departamentos.Sum(d => d.AreaM2);
    }
}