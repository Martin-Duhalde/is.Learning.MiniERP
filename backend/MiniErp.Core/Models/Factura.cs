namespace MiniErp.Core.Models;

public class Factura
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public DateTime Fecha { get; set; }

    public int PresupuestoId { get; set; }
    public Presupuesto? Presupuesto { get; set; }

    // Totales congelados al momento de facturar.
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }
}
