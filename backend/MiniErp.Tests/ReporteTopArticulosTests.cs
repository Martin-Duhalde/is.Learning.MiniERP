using Microsoft.EntityFrameworkCore;
using MiniErp.Core.Data;
using MiniErp.Core.Models;
using MiniErp.Core.Services;
using Xunit;

namespace MiniErp.Tests;

/// <summary>
/// Tests propios (Parte C) del reporte top N de articulos por monto facturado en un
/// rango de fechas (stretch 5). No modifican los tests existentes.
/// </summary>
public class ReporteTopArticulosTests
{
    private static (ReporteService reporte, PresupuestoService presupuestos, FacturacionService facturacion, AppDbContext db) Armar()
    {
        var db = TestDbFactory.Create();
        var numeracion = new NumeracionService(db);
        var presupuestos = new PresupuestoService(db, numeracion);
        var facturacion = new FacturacionService(db, presupuestos, numeracion);
        return (new ReporteService(db), presupuestos, facturacion, db);
    }

    private static async Task<(Cliente cliente, Articulo art21, Articulo art105)> SeedAsync(AppDbContext db)
    {
        var cliente = new Cliente
        {
            RazonSocial = "Cliente Test",
            Cuit = "20-00000000-0",
            CondicionIva = CondicionIva.ResponsableInscripto
        };
        var art21 = new Articulo { Codigo = "A21", Descripcion = "Articulo 21", PrecioUnitario = 1000m, StockActual = 1000, AlicuotaIva = 21m };
        var art105 = new Articulo { Codigo = "A105", Descripcion = "Articulo 10.5", PrecioUnitario = 4500m, StockActual = 1000, AlicuotaIva = 10.5m };

        db.Clientes.Add(cliente);
        db.Articulos.AddRange(art21, art105);
        await db.SaveChangesAsync();

        return (cliente, art21, art105);
    }

    private static async Task<Presupuesto> CrearYFacturarAsync(
        PresupuestoService presupuestos,
        FacturacionService facturacion,
        Cliente cliente,
        params (int articuloId, int cantidad, decimal descuentoPct)[] items)
    {
        var presupuesto = await presupuestos.CrearAsync(
            cliente.Id,
            30,
            items.Select(i => new PresupuestoItem
            {
                ArticuloId = i.articuloId,
                Cantidad = i.cantidad,
                DescuentoPct = i.descuentoPct
            }).ToList());

        await facturacion.FacturarAsync(presupuesto.Id);
        return presupuesto;
    }

    private static DateTime DiaUtc(int offsetDias) =>
        DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(offsetDias), DateTimeKind.Utc);

    private static DateTime FinDelDiaUtc(DateTime dia) =>
        DateTime.SpecifyKind(dia.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

    [Fact]
    public async Task Top_AgrupaPorArticuloYOrdenaPorMontoNeto()
    {
        var (reporte, presupuestos, facturacion, db) = Armar();
        var (cliente, art21, art105) = await SeedAsync(db);

        await CrearYFacturarAsync(presupuestos, facturacion, cliente, (art21.Id, 10, 0m));
        await CrearYFacturarAsync(presupuestos, facturacion, cliente, (art105.Id, 2, 0m));

        var top = await reporte.TopArticulosAsync(DiaUtc(0), FinDelDiaUtc(DiaUtc(0)), 10);

        Assert.Equal(2, top.Count);
        Assert.Equal("A21", top[0].Codigo);          // 10.000 neto
        Assert.Equal(10, top[0].Cantidad);
        Assert.Equal(10000m, top[0].MontoNeto);
        Assert.Equal(2100m, top[0].MontoIva);
        Assert.Equal(12100m, top[0].MontoTotal);

        Assert.Equal("A105", top[1].Codigo);         // 9.000 neto
        Assert.Equal(2, top[1].Cantidad);
        Assert.Equal(9000m, top[1].MontoNeto);
        Assert.Equal(945m, top[1].MontoIva);
        Assert.Equal(9945m, top[1].MontoTotal);
    }

    [Fact]
    public async Task Top_RespetaElLimitePedido()
    {
        var (reporte, presupuestos, facturacion, db) = Armar();
        var (cliente, art21, art105) = await SeedAsync(db);

        await CrearYFacturarAsync(presupuestos, facturacion, cliente, (art21.Id, 10, 0m));
        await CrearYFacturarAsync(presupuestos, facturacion, cliente, (art105.Id, 2, 0m));

        var top = await reporte.TopArticulosAsync(DiaUtc(0), FinDelDiaUtc(DiaUtc(0)), 1);

        Assert.Single(top);
        Assert.Equal("A21", top[0].Codigo);
    }

    [Fact]
    public async Task Top_RedondeaElIvaPorLineaAntesDeSumar()
    {
        var (reporte, presupuestos, facturacion, db) = Armar();
        var (cliente, _, art105) = await SeedAsync(db);

        // 4.500 con 1% de descuento => 4.455 neto, IVA 467,775 => 467,78 por linea.
        await CrearYFacturarAsync(presupuestos, facturacion, cliente, (art105.Id, 1, 1m));
        await CrearYFacturarAsync(presupuestos, facturacion, cliente, (art105.Id, 1, 1m));

        var top = await reporte.TopArticulosAsync(DiaUtc(0), FinDelDiaUtc(DiaUtc(0)), 10);

        Assert.Single(top);
        Assert.Equal(2, top[0].Cantidad);
        Assert.Equal(8910m, top[0].MontoNeto);
        // 467,78 x 2 = 935,56. Si se redondeara sobre la suma daria 935,55.
        Assert.Equal(935.56m, top[0].MontoIva);
        Assert.Equal(9845.56m, top[0].MontoTotal);
    }

    [Fact]
    public async Task Top_ExcluyeLosPresupuestosNoFacturados()
    {
        var (reporte, presupuestos, facturacion, db) = Armar();
        var (cliente, art21, _) = await SeedAsync(db);

        // Presupuesto creado pero NO facturado.
        await presupuestos.CrearAsync(cliente.Id, 30, new List<PresupuestoItem>
        {
            new() { ArticuloId = art21.Id, Cantidad = 5, DescuentoPct = 0 }
        });

        var top = await reporte.TopArticulosAsync(DiaUtc(0), FinDelDiaUtc(DiaUtc(0)), 10);

        Assert.Empty(top);
    }

    [Fact]
    public async Task Top_FiltraPorRangoDeFechas()
    {
        var (reporte, presupuestos, facturacion, db) = Armar();
        var (cliente, art21, _) = await SeedAsync(db);

        await CrearYFacturarAsync(presupuestos, facturacion, cliente, (art21.Id, 3, 0m));

        // Mueve la factura 60 dias atras.
        var factura = await db.Facturas.FirstAsync();
        factura.Fecha = DateTime.UtcNow.AddDays(-60);
        await db.SaveChangesAsync();

        var ultimos30 = await reporte.TopArticulosAsync(DiaUtc(-30), FinDelDiaUtc(DiaUtc(0)), 10);
        Assert.Empty(ultimos30);

        var ultimos90 = await reporte.TopArticulosAsync(DiaUtc(-90), FinDelDiaUtc(DiaUtc(0)), 10);
        Assert.Single(ultimos90);
        Assert.Equal(3000m, ultimos90[0].MontoNeto);
    }

    [Fact]
    public async Task Top_ConRangoSinFacturas_DevuelveListaVacia()
    {
        var (reporte, presupuestos, facturacion, db) = Armar();
        var (cliente, art21, _) = await SeedAsync(db);

        await CrearYFacturarAsync(presupuestos, facturacion, cliente, (art21.Id, 1, 0m));

        var futuro = await reporte.TopArticulosAsync(DiaUtc(10), FinDelDiaUtc(DiaUtc(20)), 10);

        Assert.Empty(futuro);
    }

    [Fact]
    public async Task Top_LaSumaPorArticuloCoincideConLasFacturas()
    {
        var (reporte, presupuestos, facturacion, db) = Armar();
        var (cliente, art21, art105) = await SeedAsync(db);

        await CrearYFacturarAsync(presupuestos, facturacion, cliente, (art21.Id, 3, 0m), (art105.Id, 1, 1m));
        await CrearYFacturarAsync(presupuestos, facturacion, cliente, (art105.Id, 2, 5m));

        var desde = DiaUtc(0);
        var hasta = FinDelDiaUtc(desde);

        var totalFacturado = (await db.Facturas.ToListAsync()).Sum(f => f.Total);
        var top = await reporte.TopArticulosAsync(desde, hasta, 50);

        Assert.Equal(totalFacturado, top.Sum(a => a.MontoTotal));
        Assert.Equal((await db.Facturas.ToListAsync()).Sum(f => f.Iva), top.Sum(a => a.MontoIva));
        Assert.Equal((await db.Facturas.ToListAsync()).Sum(f => f.Subtotal), top.Sum(a => a.MontoNeto));
    }

    [Fact]
    public async Task Top_ConTopInvalido_Lanza()
    {
        var (reporte, _, _, _) = Armar();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => reporte.TopArticulosAsync(DiaUtc(-1), FinDelDiaUtc(DiaUtc(0)), 0));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => reporte.TopArticulosAsync(DiaUtc(-1), FinDelDiaUtc(DiaUtc(0)), -5));
    }

    [Fact]
    public async Task Top_ConHastaAnteriorADesde_Lanza()
    {
        var (reporte, _, _, _) = Armar();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => reporte.TopArticulosAsync(DiaUtc(0), FinDelDiaUtc(DiaUtc(-5)), 10));
    }
}
