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
        var presupuesto = await _db.Presupuestos
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == presupuestoId)
            ?? throw new InvalidOperationException("El presupuesto no existe.");

        var vencimiento = presupuesto.Fecha.AddDays(presupuesto.ValidezDias);
        if (DateTime.UtcNow > vencimiento)
            throw new InvalidOperationException("El presupuesto esta vencido y no se puede facturar.");

        foreach (var item in presupuesto.Items)
        {
            var articulo = await _db.Articulos.FirstAsync(a => a.Id == item.ArticuloId);
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
        return factura;
    }
}
