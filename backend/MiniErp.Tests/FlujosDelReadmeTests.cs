using Microsoft.EntityFrameworkCore;
using MiniErp.Core.Data;
using MiniErp.Core.Models;
using MiniErp.Core.Services;
using Xunit;

namespace MiniErp.Tests;

/// <summary>
/// Tests propios (Parte C, stretch 6) de los 5 flujos de "Flujos que deben funcionar" del
/// README. Usan los datos del seed (los mismos que crea la API al arrancar), asi que los
/// valores esperados son los que documenta el challenge.
/// No modifican los tests existentes.
/// </summary>
public class FlujosDelReadmeTests
{
    private sealed record Contexto(
        AppDbContext Db,
        PresupuestoService Presupuestos,
        FacturacionService Facturacion);

    private static Contexto Armar()
    {
        var db = TestDbFactory.Create();
        DbSeeder.Seed(db);

        var numeracion = new NumeracionService(db);
        var presupuestos = new PresupuestoService(db, numeracion);
        return new Contexto(db, presupuestos, new FacturacionService(db, presupuestos, numeracion));
    }

    private static async Task<int> IdArticuloAsync(AppDbContext db, string codigo) =>
        (await db.Articulos.FirstAsync(a => a.Codigo == codigo)).Id;

    private static async Task<int> PrimerClienteIdAsync(AppDbContext db) =>
        (await db.Clientes.OrderBy(c => c.Id).FirstAsync()).Id;

    private static List<PresupuestoItem> UnItem(int articuloId, int cantidad = 1, decimal descuento = 0m) =>
        new() { new PresupuestoItem { ArticuloId = articuloId, Cantidad = cantidad, DescuentoPct = descuento } };

    [Fact]
    public async Task Flujo1_CrearYListar_ElPresupuestoRecienCreadoApareceEnLaLista()
    {
        var c = Armar();
        var clienteId = await PrimerClienteIdAsync(c.Db);
        var articuloId = await IdArticuloAsync(c.Db, "ART-001");

        var creado = await c.Presupuestos.CrearAsync(clienteId, 15, UnItem(articuloId));
        var listado = await c.Presupuestos.ListarAsync();

        Assert.Contains(listado, p => p.Id == creado.Id);
    }

    [Fact]
    public async Task Flujo2_IvaConAlicuotasMezcladas_DaLosNumerosDelReadme()
    {
        var c = Armar();
        var clienteId = await PrimerClienteIdAsync(c.Db);
        var art001 = await IdArticuloAsync(c.Db, "ART-001");   // 35.000 al 21%
        var art004 = await IdArticuloAsync(c.Db, "ART-004");   // 4.500 al 10,5%

        var creado = await c.Presupuestos.CrearAsync(clienteId, 15, new List<PresupuestoItem>
        {
            new() { ArticuloId = art001, Cantidad = 1, DescuentoPct = 0 },
            new() { ArticuloId = art004, Cantidad = 1, DescuentoPct = 0 }
        });

        var completo = await c.Presupuestos.ObtenerAsync(creado.Id);
        var totales = c.Presupuestos.CalcularTotales(completo!);

        Assert.Equal(39500m, totales.Subtotal);
        Assert.Equal(7822.5m, totales.Iva);
        Assert.Equal(47322.5m, totales.Total);

        // Cada item conserva la alicuota de su articulo como snapshot.
        var items = completo!.Items.OrderBy(i => i.ArticuloId).ToList();
        Assert.Equal(21m, items[0].AlicuotaIva);
        Assert.Equal(10.5m, items[1].AlicuotaIva);
    }

    [Fact]
    public async Task Flujo3_NumeracionDespuesDeBorrar_ElTerceroNoRepiteNumerosExistentes()
    {
        var c = Armar();
        var clienteId = await PrimerClienteIdAsync(c.Db);
        var articuloId = await IdArticuloAsync(c.Db, "ART-001");

        var primero = await c.Presupuestos.CrearAsync(clienteId, 15, UnItem(articuloId));
        var segundo = await c.Presupuestos.CrearAsync(clienteId, 15, UnItem(articuloId));

        await c.Presupuestos.EliminarAsync(segundo.Id);

        var tercero = await c.Presupuestos.CrearAsync(clienteId, 15, UnItem(articuloId));

        var numerosExistentes = (await c.Presupuestos.ListarAsync())
            .Where(p => p.Id != tercero.Id)
            .Select(p => p.Numero)
            .ToList();

        Assert.DoesNotContain(tercero.Numero, numerosExistentes);
        Assert.Contains(primero.Numero, numerosExistentes);

        // Nota: borrando el ultimo, la numeracion vieja (Count + 1) tambien daba un numero
        // libre. El caso que rompia era borrar uno que no es el ultimo; ese queda cubierto
        // en ReglasDeNegocioTests.Regla6_AlBorrarNoSeRepiteNingunNumeroExistente.
    }

    [Fact]
    public async Task Flujo4_DatosInvalidos_LaApiLosRechaza()
    {
        var c = Armar();
        var clienteId = await PrimerClienteIdAsync(c.Db);
        var articuloId = await IdArticuloAsync(c.Db, "ART-001");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => c.Presupuestos.CrearAsync(clienteId, 15, UnItem(articuloId, cantidad: 0)));

        Assert.Contains("cantidad", ex.Message);
        Assert.Empty(await c.Db.Presupuestos.ToListAsync());
    }

    [Fact]
    public async Task Flujo5_FacturarMasQueElStock_SeRechazaYElStockNoQuedaNegativo()
    {
        var c = Armar();
        var clienteId = await PrimerClienteIdAsync(c.Db);
        var art006 = await IdArticuloAsync(c.Db, "ART-006");   // stock 3 en el seed

        var presupuesto = await c.Presupuestos.CrearAsync(clienteId, 15, UnItem(art006, cantidad: 5));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Facturacion.FacturarAsync(presupuesto.Id));

        Assert.Contains("stock", ex.Message);
        Assert.Empty(await c.Db.Facturas.ToListAsync());

        var articulo = await c.Db.Articulos.FirstAsync(a => a.Id == art006);
        Assert.Equal(3, articulo.StockActual);
        Assert.True(articulo.StockActual >= 0);
    }

    [Fact]
    public async Task Flujo5_FacturarDosVeces_LaSegundaFallaYElStockBajaUnaSolaVez()
    {
        var c = Armar();
        var clienteId = await PrimerClienteIdAsync(c.Db);
        var art006 = await IdArticuloAsync(c.Db, "ART-006");

        var presupuesto = await c.Presupuestos.CrearAsync(clienteId, 15, UnItem(art006, cantidad: 2));

        var factura = await c.Facturacion.FacturarAsync(presupuesto.Id);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => c.Facturacion.FacturarAsync(presupuesto.Id));

        Assert.Contains("ya fue facturado", ex.Message);
        Assert.Single(await c.Db.Facturas.ToListAsync());
        Assert.Equal(1, (await c.Db.Articulos.FirstAsync(a => a.Id == art006)).StockActual);

        var completo = await c.Presupuestos.ObtenerAsync(presupuesto.Id);
        Assert.Equal(EstadoPresupuesto.Facturado, completo!.Estado);
        Assert.Equal(c.Presupuestos.CalcularTotales(completo).Total, factura.Total);
    }
}
