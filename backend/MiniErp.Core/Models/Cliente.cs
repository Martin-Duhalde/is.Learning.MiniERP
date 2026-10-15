namespace MiniErp.Core.Models;

public class Cliente
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = "";
    public string Cuit { get; set; } = "";
    public CondicionIva CondicionIva { get; set; }
}
