using Microsoft.EntityFrameworkCore;
using MiniErp.Core.Data;

namespace MiniErp.Core.Services;

public class NumeracionService
{
    private readonly AppDbContext _db;

    public NumeracionService(AppDbContext db) => _db = db;

    public async Task<int> ProximoNumeroPresupuestoAsync()
    {
        // Se calcula a partir del maximo existente, igual que el de facturas:
        // contar filas repite numeros despues de eliminar un presupuesto.
        var max = await _db.Presupuestos.MaxAsync(p => (int?)p.Numero) ?? 0;
        return max + 1;
    }

    public async Task<int> ProximoNumeroFacturaAsync()
    {
        var max = await _db.Facturas.MaxAsync(f => (int?)f.Numero) ?? 0;
        return max + 1;
    }
}
