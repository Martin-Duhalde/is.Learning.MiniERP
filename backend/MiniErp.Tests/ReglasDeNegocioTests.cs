using Microsoft.EntityFrameworkCore;
using MiniErp.Core.Data;
using MiniErp.Core.Models;
using MiniErp.Core.Services;
using Xunit;

namespace MiniErp.Tests;

/// <summary>
/// Tests propios (Parte C, stretch 6) de las 7 reglas de negocio del challenge, una por
/// regla. Incluye los casos que hoy no cubria ningun test: el redondeo del IVA por linea,
/// la numeracion tras borrar y la validez vencida.
/// No modifican los tests existentes.
/// </summary>
public class ReglasDeNegocioTests
{
    private sealed record Contexto(
        AppDbContext Db,
        PresupuestoService Presupuestos,
        FacturacionService Facturacion);

    private static Contexto Armar()
    {
        var db = TestDbFactory.Create();
        var numeracion = new NumeracionService(db);
        var presupuestos = new PresupuestoService(db, numeracion);
        return new Contexto(db, presupuestos, new FacturacionService(db, presupuestos, numeracion));
    }

    private static async Task<(Cliente cliente, Articulo art21, Articulo art105)> SeedAsync(
        AppDbContext db, int stock = 100)
    {
        var cliente = new Cliente
        {
            RazonSocial = "Cliente Test",
            Cuit = "20-00000000-0",
            CondicionIva = CondicionIva.ResponsableInscripto
        };
        var art21 = new Articulo { Codigo = "A21", Descripcion = "Articulo 21%", PrecioUnitario = 1000m, StockActual = stock, AlicuotaIva = 21m };
        var art105 = new Articulo { Codigo = "A105", Descripcion = "Articulo 10,5%", PrecioUnitario = 4500m, StockActual = stock, AlicuotaIva = 10.5m };

        db.Clientes.Add(cliente);
        db.Articulos.AddRange(art21, art105);
        await db.SaveChangesAsync();

        return (cliente, art21, art105);
    }

    private static Presupuesto PresupuestoCon(params PresupuestoItem[] items) =>
        new() { Items = items.ToList() };

    private static List<PresupuestoItem> UnItem(int articuloId, int cantidad = 1, decimal descuento = 0m) =>
        new() { new PresupuestoItem { ArticuloId = articuloId, Cantidad = cantidad, DescuentoPct = descuento } };

    // ---- Regla 1: subtotal = cantidad x precio x (1 - descuento), y el descuento va antes del IVA ----

    [Fact]
    public void Regla1_ElDescuentoSeAplicaAntesDelIva()
    {
        var c = Armar();
        var presupuesto = PresupuestoCon(
            new PresupuestoItem { Cantidad = 10, PrecioUnitario = 100m, DescuentoPct = 10m, AlicuotaIva = 21m });

        var totales = c.Presupuestos.CalcularTotales(presupuesto);

        // 10 x 100 = 1000; -10% => 900; IVA 21% sobre 900 => 189.
        Assert.Equal(900m, totales.Subtotal);
        Assert.Equal(189m, totales.Iva);
        Assert.Equal(1089m, totales.Total);
    }

    // ---- Regla 2: IVA por linea segun su alicuota, redondeado a 2 decimales por linea, despues se suma ----

    [Fact]
    public void Regla2_UsaLaAlicuotaDeCadaLinea()
    {
        var c = Armar();
        var presupuesto = PresupuestoCon(
            new PresupuestoItem { Cantidad = 1, PrecioUnitario = 35000m, DescuentoPct = 0m, AlicuotaIva = 21m },
            new PresupuestoItem { Cantidad = 1, PrecioUnitario = 4500m, DescuentoPct = 0m, AlicuotaIva = 10.5m });

        var totales = c.Presupuestos.CalcularTotales(presupuesto);

        // Si el IVA se calculara con una sola alicuota, estos numeros no darian.
        Assert.Equal(39500m, totales.Subtotal);
        Assert.Equal(7822.5m, totales.Iva);
        Assert.Equal(47322.5m, totales.Total);
    }

    [Fact]
    public void Regla2_RedondeaA2DecimalesPorLinea()
    {
        var c = Armar();
        var presupuesto = PresupuestoCon(
            new PresupuestoItem { Cantidad = 1, PrecioUnitario = 4500m, DescuentoPct = 1m, AlicuotaIva = 10.5m });

        // 4.455 al 10,5% = 467,775 => 467,78
        var totales = c.Presupuestos.CalcularTotales(presupuesto);

        Assert.Equal(4455m, totales.Subtotal);
        Assert.Equal(467.78m, totales.Iva);
        Assert.Equal(4922.78m, totales.Total);
    }

    [Fact]
    public void Regla2_RedondeaPorLineaYNoSobreLaSuma()
    {
        var c = Armar();
        var linea = new PresupuestoItem { Cantidad = 1, PrecioUnitario = 4500m, DescuentoPct = 1m, AlicuotaIva = 10.5m };
        var presupuesto = PresupuestoCon(linea, linea, linea);

        var totales = c.Presupuestos.CalcularTotales(presupuesto);

        // 3 x 467,78 = 1.403,34. Redondeando sobre la suma daria 1.403,325 => 1.403,33.
        Assert.Equal(13365m, totales.Subtotal);
        Assert.Equal(1403.34m, totales.Iva);
        Assert.Equal(14768.34m, totales.Total);
    }

    // ---- Regla 3: al facturar se descuenta stock y no puede quedar negativo ----

    [Fact]
    public async Task Regla3_FacturarDescuentaElStock()
    {
        var c = Armar();
        var (cliente, art21, _) = await SeedAsync(c.Db, stock: 10);
        var presupuesto = await c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(art21.Id, cantidad: 4));

        await c.Facturacion.FacturarAsync(presupuesto.Id);

        Assert.Equal(6, (await c.Db.Articulos.FirstAsync(a => a.Id == art21.Id)).StockActual);
    }

    [Fact]
    public async Task Regla3_SinStockSeRechazaYElStockNoQuedaNegativo()
    {
        var c = Armar();
        var (cliente, art21, _) = await SeedAsync(c.Db, stock: 3);
        var presupuesto = await c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(art21.Id, cantidad: 5));

        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Facturacion.FacturarAsync(presupuesto.Id));

        var articulo = await c.Db.Articulos.FirstAsync(a => a.Id == art21.Id);
        Assert.Equal(3, articulo.StockActual);
        Assert.True(articulo.StockActual >= 0);
        Assert.Empty(await c.Db.Facturas.ToListAsync());
    }

    // ---- Regla 4: idempotencia, un presupuesto se factura una sola vez ----

    [Fact]
    public async Task Regla4_UnPresupuestoSeFacturaUnaSolaVez()
    {
        var c = Armar();
        var (cliente, art21, _) = await SeedAsync(c.Db, stock: 10);
        var presupuesto = await c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(art21.Id, cantidad: 2));

        await c.Facturacion.FacturarAsync(presupuesto.Id);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Facturacion.FacturarAsync(presupuesto.Id));

        Assert.Contains("ya fue facturado", ex.Message);
        Assert.Single(await c.Db.Facturas.ToListAsync());
        Assert.Equal(8, (await c.Db.Articulos.FirstAsync(a => a.Id == art21.Id)).StockActual);
    }

    // ---- Regla 5: un presupuesto vencido no se puede facturar ----

    [Fact]
    public async Task Regla5_NoSePuedeFacturarUnPresupuestoVencido()
    {
        var c = Armar();
        var (cliente, art21, _) = await SeedAsync(c.Db, stock: 10);
        var presupuesto = await c.Presupuestos.CrearAsync(cliente.Id, 1, UnItem(art21.Id));

        presupuesto.Fecha = DateTime.UtcNow.AddDays(-2);
        await c.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Facturacion.FacturarAsync(presupuesto.Id));

        Assert.Contains("vencido", ex.Message);
        Assert.Empty(await c.Db.Facturas.ToListAsync());
        Assert.Equal(10, (await c.Db.Articulos.FirstAsync(a => a.Id == art21.Id)).StockActual);
    }

    [Fact]
    public async Task Regla5_UnPresupuestoVigenteSePuedeFacturar()
    {
        var c = Armar();
        var (cliente, art21, _) = await SeedAsync(c.Db, stock: 10);
        var presupuesto = await c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(art21.Id));

        await c.Facturacion.FacturarAsync(presupuesto.Id);

        Assert.Single(await c.Db.Facturas.ToListAsync());
    }

    // ---- Regla 6: numeracion secuencial y sin colisiones ----

    [Fact]
    public async Task Regla6_LosNumerosDePresupuestoSonSecuenciales()
    {
        var c = Armar();
        var (cliente, art21, _) = await SeedAsync(c.Db);

        var p1 = await c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(art21.Id));
        var p2 = await c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(art21.Id));
        var p3 = await c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(art21.Id));

        Assert.Equal(new[] { 1, 2, 3 }, new[] { p1.Numero, p2.Numero, p3.Numero });
    }

    [Fact]
    public async Task Regla6_AlBorrarNoSeRepiteNingunNumeroExistente()
    {
        var c = Armar();
        var (cliente, art21, _) = await SeedAsync(c.Db);

        var p1 = await c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(art21.Id));
        var p2 = await c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(art21.Id));

        // Borra el PRIMERO: con la numeracion vieja (Count + 1) el siguiente repetia el 2.
        await c.Presupuestos.EliminarAsync(p1.Id);

        var p3 = await c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(art21.Id));
        var numerosExistentes = (await c.Presupuestos.ListarAsync())
            .Where(p => p.Id != p3.Id)
            .Select(p => p.Numero)
            .ToList();

        Assert.DoesNotContain(p3.Numero, numerosExistentes);
        Assert.Contains(p2.Numero, numerosExistentes);
    }

    [Fact]
    public async Task Regla6_LosNumerosDeFacturaSonSecuenciales()
    {
        var c = Armar();
        var (cliente, art21, _) = await SeedAsync(c.Db, stock: 100);

        var p1 = await c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(art21.Id));
        var p2 = await c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(art21.Id));

        var f1 = await c.Facturacion.FacturarAsync(p1.Id);
        var f2 = await c.Facturacion.FacturarAsync(p2.Id);

        Assert.Equal(1, f1.Numero);
        Assert.Equal(2, f2.Numero);
    }

    // ---- Regla 7: datos validos ----

    [Fact]
    public async Task Regla7_RechazaCantidadCeroONegativa()
    {
        var c = Armar();
        var (cliente, art21, _) = await SeedAsync(c.Db);

        foreach (var cantidad in new[] { 0, -1 })
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(art21.Id, cantidad)));

            Assert.Contains("cantidad", ex.Message);
        }

        Assert.Empty(await c.Db.Presupuestos.ToListAsync());
    }

    [Fact]
    public async Task Regla7_RechazaDescuentosFueraDeRango()
    {
        var c = Armar();
        var (cliente, art21, _) = await SeedAsync(c.Db);

        foreach (var descuento in new[] { -0.01m, 100.01m })
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(art21.Id, descuento: descuento)));

            Assert.Contains("descuento", ex.Message);
        }

        Assert.Empty(await c.Db.Presupuestos.ToListAsync());
    }

    [Fact]
    public async Task Regla7_RechazaValidezInvalidaClienteInexistenteYPresupuestoSinItems()
    {
        var c = Armar();
        var (cliente, art21, _) = await SeedAsync(c.Db);

        var exValidez = await Assert.ThrowsAsync<InvalidOperationException>(
            () => c.Presupuestos.CrearAsync(cliente.Id, 0, UnItem(art21.Id)));
        Assert.Contains("validez", exValidez.Message);

        var exCliente = await Assert.ThrowsAsync<InvalidOperationException>(
            () => c.Presupuestos.CrearAsync(9999, 30, UnItem(art21.Id)));
        Assert.Contains("no existe", exCliente.Message);

        var exVacio = await Assert.ThrowsAsync<InvalidOperationException>(
            () => c.Presupuestos.CrearAsync(cliente.Id, 30, new List<PresupuestoItem>()));
        Assert.Contains("al menos un item", exVacio.Message);
    }

    [Fact]
    public async Task Regla7_RechazaArticuloInexistente()
    {
        var c = Armar();
        var (cliente, _, _) = await SeedAsync(c.Db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => c.Presupuestos.CrearAsync(cliente.Id, 30, UnItem(9999)));

        Assert.Contains("no existe", ex.Message);
    }

    [Fact]
    public void Regla7_CalcularTotalesRechazaDatosInvalidos()
    {
        var c = Armar();

        var exCantidad = Assert.Throws<InvalidOperationException>(() => c.Presupuestos.CalcularTotales(
            PresupuestoCon(new PresupuestoItem { Cantidad = 0, PrecioUnitario = 100m, DescuentoPct = 0m, AlicuotaIva = 21m })));
        Assert.Contains("cantidad", exCantidad.Message);

        var exDescuento = Assert.Throws<InvalidOperationException>(() => c.Presupuestos.CalcularTotales(
            PresupuestoCon(new PresupuestoItem { Cantidad = 1, PrecioUnitario = 100m, DescuentoPct = 101m, AlicuotaIva = 21m })));
        Assert.Contains("descuento", exDescuento.Message);

        var exVacio = Assert.Throws<InvalidOperationException>(() => c.Presupuestos.CalcularTotales(
            PresupuestoCon(Array.Empty<PresupuestoItem>())));
        Assert.Contains("al menos un item", exVacio.Message);
    }
}
