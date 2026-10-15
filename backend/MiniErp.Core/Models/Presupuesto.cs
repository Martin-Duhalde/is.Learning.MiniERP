namespace MiniErp.Core.Models;

public class Presupuesto
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public DateTime Fecha { get; set; }

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public EstadoPresupuesto Estado { get; set; }
    public int ValidezDias { get; set; }

    public List<PresupuestoItem> Items { get; set; } = new();
}
