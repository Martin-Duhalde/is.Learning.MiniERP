namespace MiniErp.Core.Models;

public class Articulo
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal PrecioUnitario { get; set; }
    public int StockActual { get; set; }

    /// <summary>Alícuota de IVA del artículo (por ejemplo 21 o 10.5).</summary>
    public decimal AlicuotaIva { get; set; }
}
