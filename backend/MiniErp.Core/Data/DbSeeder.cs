using MiniErp.Core.Models;

namespace MiniErp.Core.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        db.Database.EnsureCreated();
        if (db.Articulos.Any()) return;

        db.Clientes.AddRange(
            new Cliente { RazonSocial = "Distribuidora del Sur S.A.", Cuit = "30-71234567-9", CondicionIva = CondicionIva.ResponsableInscripto },
            new Cliente { RazonSocial = "Kiosco Dona Rosa", Cuit = "27-30111222-3", CondicionIva = CondicionIva.Monotributo },
            new Cliente { RazonSocial = "Consumidor Final", Cuit = "00-00000000-0", CondicionIva = CondicionIva.ConsumidorFinal }
        );

        db.Articulos.AddRange(
            new Articulo { Codigo = "ART-001", Descripcion = "Teclado mecanico", PrecioUnitario = 35000m, StockActual = 50, AlicuotaIva = 21m },
            new Articulo { Codigo = "ART-002", Descripcion = "Mouse inalambrico", PrecioUnitario = 18000m, StockActual = 80, AlicuotaIva = 21m },
            new Articulo { Codigo = "ART-003", Descripcion = "Monitor 24 pulgadas", PrecioUnitario = 220000m, StockActual = 15, AlicuotaIva = 21m },
            new Articulo { Codigo = "ART-004", Descripcion = "Resma de papel A4", PrecioUnitario = 4500m, StockActual = 200, AlicuotaIva = 10.5m },
            new Articulo { Codigo = "ART-005", Descripcion = "Cuaderno tapa dura", PrecioUnitario = 3200m, StockActual = 120, AlicuotaIva = 10.5m },
            new Articulo { Codigo = "ART-006", Descripcion = "Auriculares", PrecioUnitario = 27000m, StockActual = 3, AlicuotaIva = 21m }
        );

        db.SaveChanges();
    }
}
