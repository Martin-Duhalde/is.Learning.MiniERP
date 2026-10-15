using Microsoft.EntityFrameworkCore;
using MiniErp.Core.Data;
using MiniErp.Core.Models;

namespace MiniErp.Core.Services;

public record Totales(decimal Subtotal, decimal Iva, decimal Total);

public class PresupuestoService
{
    private readonly AppDbContext _db;
    private readonly NumeracionService _numeracion;

    public PresupuestoService(AppDbContext db, NumeracionService numeracion)
    {
        _db = db;
        _numeracion = numeracion;
    }

    /// <summary>
    /// Calcula subtotal, IVA y total de un presupuesto. El descuento se aplica
    /// por linea antes del IVA.
    /// </summary>
    public Totales CalcularTotales(Presupuesto presupuesto)
    {
        decimal subtotal = 0m;
        foreach (var item in presupuesto.Items)
        {
            var subtotalLinea = item.Cantidad * item.PrecioUnitario * (1 - item.DescuentoPct / 100m);
            subtotal += subtotalLinea;
        }

        var iva = subtotal * 0.21m;
        var total = subtotal + iva;

        return new Totales(subtotal, iva, total);
    }

    public async Task<Presupuesto> CrearAsync(int clienteId, int validezDias, List<PresupuestoItem> items)
    {
        foreach (var item in items)
        {
            var articulo = await _db.Articulos.FirstOrDefaultAsync(a => a.Id == item.ArticuloId)
                ?? throw new InvalidOperationException($"El articulo {item.ArticuloId} no existe.");

            // Tomamos precio y alicuota actuales del articulo como snapshot.
            item.PrecioUnitario = articulo.PrecioUnitario;
            item.AlicuotaIva = articulo.AlicuotaIva;
        }

        var presupuesto = new Presupuesto
        {
            Numero = await _numeracion.ProximoNumeroPresupuestoAsync(),
            Fecha = DateTime.UtcNow,
            ClienteId = clienteId,
            Estado = EstadoPresupuesto.Borrador,
            ValidezDias = validezDias,
            Items = items
        };

        _db.Presupuestos.Add(presupuesto);
        await _db.SaveChangesAsync();
        return presupuesto;
    }

    public async Task<Presupuesto?> ObtenerAsync(int id)
    {
        return await _db.Presupuestos
            .Include(p => p.Cliente)
            .Include(p => p.Items)
                .ThenInclude(i => i.Articulo)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Presupuesto>> ListarAsync()
    {
        return await _db.Presupuestos
            .Include(p => p.Cliente)
            .Include(p => p.Items)
                .ThenInclude(i => i.Articulo)
            .Where(p => p.Estado != EstadoPresupuesto.Borrador)
            .OrderByDescending(p => p.Numero)
            .ToListAsync();
    }

    public async Task EliminarAsync(int id)
    {
        var presupuesto = await _db.Presupuestos.FindAsync(id);
        if (presupuesto is not null)
        {
            _db.Presupuestos.Remove(presupuesto);
            await _db.SaveChangesAsync();
        }
    }
}
