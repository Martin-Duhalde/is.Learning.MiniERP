using Microsoft.EntityFrameworkCore;

using MiniErp.Core.Data;
using MiniErp.Core.Models;

namespace MiniErp.Core.Services;

public class FacturacionService
{
    private readonly AppDbContext _db;
    private readonly PresupuestoService _presupuestos;
    private readonly NumeracionService _numeracion;

    public FacturacionService(AppDbContext db, PresupuestoService presupuestos, NumeracionService numeracion)
    {
        _db = db;
        _presupuestos = presupuestos;
        _numeracion = numeracion;
    }

    public async Task<Factura> FacturarAsync(int presupuestoId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            if (await _db.Facturas.AnyAsync(c => c.PresupuestoId == presupuestoId))
                throw new InvalidOperationException("El presupuesto ya fue facturado.");

            var presupuesto = await _db.Presupuestos
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == presupuestoId)
                ?? throw new InvalidOperationException("El presupuesto no existe.");

            var vencimiento = presupuesto.Fecha.AddDays(presupuesto.ValidezDias);
            if (DateTime.UtcNow > vencimiento)
                throw new InvalidOperationException("El presupuesto esta vencido y no se puede facturar.");

            /// selecciono los ids de los articulos.
            var idsArticulos = presupuesto.Items.Select(i => i.ArticuloId).Distinct().ToList();

            /// Traigo los artículos así no hago una consulta a la base de datos por cada item del presupuesto.
            var articulos = await _db.Articulos.Where(a => idsArticulos.Contains(a.Id)).ToListAsync();

            foreach (var item in presupuesto.Items)
            {
                var articulo = articulos.FirstOrDefault(a => a.Id == item.ArticuloId)
                    ?? throw new InvalidOperationException($"El artículo con id {item.ArticuloId} no existe.");

                if (articulo.StockActual < item.Cantidad)
                    throw new InvalidOperationException($"No hay suficiente stock del artículo {articulo.Codigo} para facturar el presupuesto. Stock Actual: {articulo.StockActual}");

                articulo.StockActual -= item.Cantidad;
            }

            var totales = _presupuestos.CalcularTotales(presupuesto);

            var factura = new Factura
            {
                Numero = await _numeracion.ProximoNumeroFacturaAsync(),
                Fecha = DateTime.UtcNow,
                PresupuestoId = presupuesto.Id,
                Subtotal = totales.Subtotal,
                Iva = totales.Iva,
                Total = totales.Total
            };

            presupuesto.Estado = EstadoPresupuesto.Facturado;
            _db.Facturas.Add(factura);
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
            return factura;

        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
