using Microsoft.EntityFrameworkCore;
using MiniErp.Core.Data;

namespace MiniErp.Core.Services;

/// <summary>Monto facturado de un articulo en un rango de fechas.</summary>
public record TopArticulo(
    int ArticuloId,
    string Codigo,
    string Descripcion,
    int Cantidad,
    decimal MontoNeto,
    decimal MontoIva,
    decimal MontoTotal);

public class ReporteService
{
    private readonly AppDbContext _db;

    public ReporteService(AppDbContext db) => _db = db;

    /// <summary>
    /// Top N de articulos por monto facturado (neto, sin IVA) en un rango de fechas inclusive.
    /// Solo mira presupuestos que tienen factura en el rango. El IVA se recalcula por linea
    /// redondeando a 2 decimales (regla de negocio 2) para que la suma por articulo coincida
    /// exactamente con los totales de las facturas.
    /// </summary>
    public async Task<List<TopArticulo>> TopArticulosAsync(DateTime desde, DateTime hasta, int top)
    {
        if (top <= 0)
            throw new InvalidOperationException("El top debe ser mayor a cero.");

        if (hasta < desde)
            throw new InvalidOperationException("La fecha 'hasta' no puede ser anterior a 'desde'.");

        var lineas = await _db.Facturas
            .Where(f => f.Fecha >= desde && f.Fecha <= hasta)
            .SelectMany(f => f.Presupuesto!.Items.Select(i => new
            {
                i.ArticuloId,
                Codigo = i.Articulo!.Codigo,
                Descripcion = i.Articulo!.Descripcion,
                i.Cantidad,
                i.PrecioUnitario,
                i.DescuentoPct,
                i.AlicuotaIva
            }))
            .ToListAsync();

        // La agrupacion y el redondeo por linea se hacen en memoria: es la unica forma de
        // garantizar el mismo redondeo que CalcularTotales.
        return lineas
            .GroupBy(l => new { l.ArticuloId, l.Codigo, l.Descripcion })
            .Select(g =>
            {
                decimal neto = 0m;
                decimal iva = 0m;
                var cantidad = 0;

                foreach (var linea in g)
                {
                    var subtotalLinea = linea.Cantidad * linea.PrecioUnitario * (1 - linea.DescuentoPct / 100m);

                    neto += subtotalLinea;
                    iva += Math.Round(subtotalLinea * linea.AlicuotaIva / 100m, 2, MidpointRounding.AwayFromZero);
                    cantidad += linea.Cantidad;
                }

                return new TopArticulo(g.Key.ArticuloId, g.Key.Codigo, g.Key.Descripcion, cantidad, neto, iva, neto + iva);
            })
            .OrderByDescending(a => a.MontoNeto)
            .ThenByDescending(a => a.Cantidad)
            .ThenBy(a => a.Codigo)
            .Take(top)
            .ToList();
    }
}
