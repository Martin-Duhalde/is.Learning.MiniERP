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
        decimal iva = 0m;

        foreach (var item in presupuesto.Items)
        {
            if (item.Cantidad <= 0)
                throw new InvalidOperationException("La cantidad debe ser mayor a cero en todos los items del presupuesto.");
            
            if (item.DescuentoPct < 0 || item.DescuentoPct > 100)
                throw new InvalidOperationException($"El descuento debe estar entre 0% y 100%. Valor: {item.DescuentoPct}");

            var subtotalLinea /**/  = item.Cantidad * item.PrecioUnitario * (1 - item.DescuentoPct / 100m);
            var ivaLinea      /**/  = subtotalLinea * item.AlicuotaIva / 100m;
            subtotal          /**/ += subtotalLinea;
            iva               /**/ += Math.Round(ivaLinea, 2, MidpointRounding.AwayFromZero); /// Regla de negocios 2. El IVA se calcula por línea según su alícuota, redondeando a 2 decimales por línea, y recién después se suma.
        }

        decimal total = subtotal + iva;

        return new Totales(subtotal, iva, total);
    }

    public async Task<Presupuesto> CrearAsync(int clienteId, int validezDias, List<PresupuestoItem> items)
    {
        foreach (var item in items)
        {
            if (item.Cantidad <= 0)
                throw new InvalidOperationException("La cantidad debe ser mayor a cero en todos los items del presupuesto.");

            if (item.DescuentoPct < 0 || item.DescuentoPct > 100)
                throw new InvalidOperationException($"El descuento debe estar entre 0% y 100%. Valor: {item.DescuentoPct}");


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
