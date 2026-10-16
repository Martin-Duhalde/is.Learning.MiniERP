using Microsoft.EntityFrameworkCore;
using MiniErp.Core.Data;
using MiniErp.Core.Models;
using MiniErp.Core.Services;
using Xunit;

namespace MiniErp.Tests;

/// <summary>
/// Tests propios (Parte C) para "Duplicar presupuesto" (regla de negocio 6 y stretch 4):
/// numero nuevo, fecha de hoy y precios refrescados desde el precio actual del articulo.
/// No modifican los tests existentes.
/// </summary>
public class DuplicarPresupuestoTests
{
    private static (PresupuestoService service, AppDbContext db) Armar()
    {
        var db = TestDbFactory.Create();
        return (new PresupuestoService(db, new NumeracionService(db)), db);
    }

    private static async Task<(Cliente cliente, Articulo art1, Articulo art2)> SeedAsync(AppDbContext db)
    {
        var cliente = new Cliente
        {
            RazonSocial = "Cliente Test",
            Cuit = "20-00000000-0",
            CondicionIva = CondicionIva.ResponsableInscripto
        };
        var art1 = new Articulo { Codigo = "A1", Descripcion = "Articulo 1", PrecioUnitario = 100m, StockActual = 10, AlicuotaIva = 21m };
        var art2 = new Articulo { Codigo = "A2", Descripcion = "Articulo 2", PrecioUnitario = 200m, StockActual = 10, AlicuotaIva = 10.5m };

        db.Clientes.Add(cliente);
        db.Articulos.AddRange(art1, art2);
        await db.SaveChangesAsync();

        return (cliente, art1, art2);
    }

    private static async Task<Presupuesto> CrearOriginalAsync(
        PresupuestoService service, Cliente cliente, Articulo art1, Articulo? art2 = null)
    {
        var items = new List<PresupuestoItem>
        {
            new() { ArticuloId = art1.Id, Cantidad = 2, DescuentoPct = 0 }
        };

        if (art2 is not null)
            items.Add(new PresupuestoItem { ArticuloId = art2.Id, Cantidad = 3, DescuentoPct = 5 });

        return await service.CrearAsync(cliente.Id, 15, items);
    }

    [Fact]
    public async Task Duplicar_CopiaItemsClienteYValidez_ConIdNumeroYFechaNuevos()
    {
        var (service, db) = Armar();
        var (cliente, art1, art2) = await SeedAsync(db);
        var original = await CrearOriginalAsync(service, cliente, art1, art2);

        var copia = await service.DuplicarAsync(original.Id);

        Assert.NotEqual(original.Id, copia.Id);
        Assert.NotEqual(original.Numero, copia.Numero);
        Assert.Equal(cliente.Id, copia.ClienteId);
        Assert.Equal(15, copia.ValidezDias);
        Assert.Equal(EstadoPresupuesto.Borrador, copia.Estado);
        Assert.True(copia.Fecha >= original.Fecha, "La copia deberia tener fecha de hoy.");

        var items = await db.PresupuestoItems
            .Where(i => i.PresupuestoId == copia.Id)
            .OrderBy(i => i.ArticuloId)
            .ToListAsync();

        Assert.Equal(2, items.Count);
        Assert.Equal(2, items[0].Cantidad);
        Assert.Equal(0m, items[0].DescuentoPct);
        Assert.Equal(3, items[1].Cantidad);
        Assert.Equal(5m, items[1].DescuentoPct);
    }

    [Fact]
    public async Task Duplicar_UsaLosPreciosActualesDelArticulo_NoLosDelOriginal()
    {
        var (service, db) = Armar();
        var (cliente, art1, _) = await SeedAsync(db);
        var original = await CrearOriginalAsync(service, cliente, art1);

        // El articulo cambia de precio y de alicuota despues de crear el original.
        art1.PrecioUnitario = 150m;
        art1.AlicuotaIva = 10.5m;
        await db.SaveChangesAsync();

        var copia = await service.DuplicarAsync(original.Id);

        var itemOriginal = await db.PresupuestoItems.FirstAsync(i => i.PresupuestoId == original.Id);
        var itemCopia = await db.PresupuestoItems.FirstAsync(i => i.PresupuestoId == copia.Id);

        Assert.Equal(100m, itemOriginal.PrecioUnitario);      // el original conserva su snapshot
        Assert.Equal(21m, itemOriginal.AlicuotaIva);
        Assert.Equal(150m, itemCopia.PrecioUnitario);          // la copia toma el precio de hoy
        Assert.Equal(10.5m, itemCopia.AlicuotaIva);
    }

    [Fact]
    public async Task Duplicar_LaCopiaApareceEnElListado()
    {
        var (service, db) = Armar();
        var (cliente, art1, _) = await SeedAsync(db);
        var original = await CrearOriginalAsync(service, cliente, art1);

        var copia = await service.DuplicarAsync(original.Id);
        var listado = await service.ListarAsync();

        Assert.Contains(listado, p => p.Id == copia.Id);
        Assert.Contains(listado, p => p.Id == original.Id);
    }

    [Fact]
    public async Task Duplicar_DosVeces_LosNumerosNoSeRepiten()
    {
        var (service, db) = Armar();
        var (cliente, art1, _) = await SeedAsync(db);
        var original = await CrearOriginalAsync(service, cliente, art1);

        var copia1 = await service.DuplicarAsync(original.Id);
        var copia2 = await service.DuplicarAsync(original.Id);

        var numeros = new[] { original.Numero, copia1.Numero, copia2.Numero };
        Assert.Equal(numeros.Length, numeros.Distinct().Count());
    }

    [Fact]
    public async Task Duplicar_UnPresupuestoFacturado_EsValido()
    {
        var (service, db) = Armar();
        var (cliente, art1, _) = await SeedAsync(db);
        var original = await CrearOriginalAsync(service, cliente, art1);

        var facturacion = new FacturacionService(db, service, new NumeracionService(db));
        await facturacion.FacturarAsync(original.Id);

        var copia = await service.DuplicarAsync(original.Id);

        Assert.NotEqual(original.Id, copia.Id);
        Assert.Equal(EstadoPresupuesto.Borrador, copia.Estado);
    }

    [Fact]
    public async Task Duplicar_DeUnPresupuestoInexistente_Lanza()
    {
        var (service, _) = Armar();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DuplicarAsync(9999));
    }

    [Fact]
    public async Task Duplicar_ElOriginalQuedaIntacto()
    {
        var (service, db) = Armar();
        var (cliente, art1, art2) = await SeedAsync(db);
        var original = await CrearOriginalAsync(service, cliente, art1, art2);

        var itemsAntes = await db.PresupuestoItems.CountAsync(i => i.PresupuestoId == original.Id);
        var totalesAntes = service.CalcularTotales((await service.ObtenerAsync(original.Id))!);

        await service.DuplicarAsync(original.Id);

        var itemsDespues = await db.PresupuestoItems.CountAsync(i => i.PresupuestoId == original.Id);
        var totalesDespues = service.CalcularTotales((await service.ObtenerAsync(original.Id))!);

        Assert.Equal(itemsAntes, itemsDespues);
        Assert.Equal(totalesAntes.Subtotal, totalesDespues.Subtotal);
        Assert.Equal(totalesAntes.Iva, totalesDespues.Iva);
        Assert.Equal(totalesAntes.Total, totalesDespues.Total);
    }
}
