using Microsoft.EntityFrameworkCore;
using MiniErp.Core.Data;
using MiniErp.Core.Models;
using MiniErp.Core.Services;
using Xunit;

namespace MiniErp.Tests;

// NO MODIFICAR ESTOS TESTS.
public class FacturacionTests
{
    private static (FacturacionService fact, AppDbContext db) Armar()
    {
        var db = TestDbFactory.Create();
        var numeracion = new NumeracionService(db);
        var presupuestos = new PresupuestoService(db, numeracion);
        var fact = new FacturacionService(db, presupuestos, numeracion);
        return (fact, db);
    }

    private static async Task<(Articulo art, Presupuesto pres)> SeedPresupuesto(
        AppDbContext db, int stock, int cantidad, decimal precio = 100m)
    {
        var art = new Articulo { Codigo = "A1", Descripcion = "Articulo", PrecioUnitario = precio, StockActual = stock, AlicuotaIva = 21m };
        db.Articulos.Add(art);

        var cliente = new Cliente { RazonSocial = "Cliente Test", Cuit = "20-00000000-0", CondicionIva = CondicionIva.ResponsableInscripto };
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var pres = new Presupuesto
        {
            Numero = 1,
            Fecha = DateTime.UtcNow,
            ClienteId = cliente.Id,
            ValidezDias = 30,
            Estado = EstadoPresupuesto.Aprobado,
            Items = new List<PresupuestoItem>
            {
                new PresupuestoItem { ArticuloId = art.Id, Cantidad = cantidad, PrecioUnitario = precio, AlicuotaIva = 21m }
            }
        };
        db.Presupuestos.Add(pres);
        await db.SaveChangesAsync();

        return (art, pres);
    }

    [Fact]
    public async Task Facturar_ConStock_DescuentaStockYGeneraFactura()
    {
        var (fact, db) = Armar();
        var (art, pres) = await SeedPresupuesto(db, stock: 100, cantidad: 10);

        var factura = await fact.FacturarAsync(pres.Id);

        var artActualizado = await db.Articulos.FirstAsync(a => a.Id == art.Id);
        Assert.Equal(90, artActualizado.StockActual);
        Assert.True(factura.Total > 0);
    }

    [Fact]
    public async Task Facturar_SinStockSuficiente_LanzaYNoDejaStockNegativo()
    {
        var (fact, db) = Armar();
        var (art, pres) = await SeedPresupuesto(db, stock: 5, cantidad: 10);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fact.FacturarAsync(pres.Id));

        var artActualizado = await db.Articulos.FirstAsync(a => a.Id == art.Id);
        Assert.True(artActualizado.StockActual >= 0, "El stock no deberia quedar negativo.");
    }

    [Fact]
    public async Task Facturar_DosVeces_LanzaYNoDescuentaDeMas()
    {
        var (fact, db) = Armar();
        var (art, pres) = await SeedPresupuesto(db, stock: 100, cantidad: 10);

        await fact.FacturarAsync(pres.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fact.FacturarAsync(pres.Id));

        var artActualizado = await db.Articulos.FirstAsync(a => a.Id == art.Id);
        Assert.Equal(90, artActualizado.StockActual);  // descuento una sola vez
    }
}
