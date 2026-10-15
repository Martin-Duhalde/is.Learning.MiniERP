namespace MiniErp.Core.Models;

public class PresupuestoItem
{
    public int Id { get; set; }

    public int PresupuestoId { get; set; }
    public Presupuesto? Presupuesto { get; set; }

    public int ArticuloId { get; set; }
    public Articulo? Articulo { get; set; }

    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal DescuentoPct { get; set; }

    /// <summary>Snapshot de la alícuota del artículo al armar el presupuesto.</summary>
    public decimal AlicuotaIva { get; set; }
}
