using MiniErp.Core.Models;
using MiniErp.Core.Services;
using Xunit;

namespace MiniErp.Tests;

// NO MODIFICAR ESTOS TESTS.
public class CalcularTotalesTests
{
    private static PresupuestoService NuevoService()
    {
        var db = TestDbFactory.Create();
        return new PresupuestoService(db, new NumeracionService(db));
    }

    private static Presupuesto PresupuestoCon(params PresupuestoItem[] items)
        => new Presupuesto { Items = items.ToList() };

    [Fact]
    public void CalcularTotales_TodoAl21SinDescuento_DaBien()
    {
        var service = NuevoService();
        var p = PresupuestoCon(
            new PresupuestoItem { Cantidad = 10, PrecioUnitario = 100m, DescuentoPct = 0m, AlicuotaIva = 21m },
            new PresupuestoItem { Cantidad = 2, PrecioUnitario = 50m, DescuentoPct = 0m, AlicuotaIva = 21m }
        );

        var t = service.CalcularTotales(p);

        Assert.Equal(1100m, t.Subtotal);
        Assert.Equal(231m, t.Iva);
        Assert.Equal(1331m, t.Total);
    }

    [Fact]
    public void CalcularTotales_ConDescuento_AplicaAntesDeIva()
    {
        var service = NuevoService();
        var p = PresupuestoCon(
            new PresupuestoItem { Cantidad = 10, PrecioUnitario = 100m, DescuentoPct = 10m, AlicuotaIva = 21m }
        );

        var t = service.CalcularTotales(p);

        // 10 * 100 = 1000; -10% => 900; IVA 21% => 189; total 1089.
        Assert.Equal(900m, t.Subtotal);
        Assert.Equal(189m, t.Iva);
        Assert.Equal(1089m, t.Total);
    }

    [Fact]
    public void CalcularTotales_ConAlicuotasMixtas_CalculaIvaPorLinea()
    {
        var service = NuevoService();
        var p = PresupuestoCon(
            new PresupuestoItem { Cantidad = 10, PrecioUnitario = 100m, DescuentoPct = 0m, AlicuotaIva = 21m },  // sub 1000, iva 210
            new PresupuestoItem { Cantidad = 4, PrecioUnitario = 50m, DescuentoPct = 0m, AlicuotaIva = 10.5m }   // sub 200,  iva 21
        );

        var t = service.CalcularTotales(p);

        Assert.Equal(1200m, t.Subtotal);
        Assert.Equal(231m, t.Iva);    // 210 + 21
        Assert.Equal(1431m, t.Total);
    }
}
