using Microsoft.EntityFrameworkCore;
using MiniErp.Core.Data;

namespace MiniErp.Core.Services;

public class NumeracionService
{
    private readonly AppDbContext _db;

    public NumeracionService(AppDbContext db) => _db = db;

    public async Task<int> ProximoNumeroPresupuestoAsync()
    {
        // El proximo numero se calcula a partir de la cantidad de presupuestos existentes.
        var cantidad = await _db.Presupuestos.CountAsync();
        return cantidad + 1;
    }

    public async Task<int> ProximoNumeroFacturaAsync()
    {
        var max = await _db.Facturas.MaxAsync(f => (int?)f.Numero) ?? 0;
        return max + 1;
    }
}
