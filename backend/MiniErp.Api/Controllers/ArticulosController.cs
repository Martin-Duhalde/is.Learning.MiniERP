using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Dtos;
using MiniErp.Core.Data;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArticulosController : ControllerBase
{
    private readonly AppDbContext _db;

    public ArticulosController(AppDbContext db) => _db = db;

    // GET /api/articulos?busqueda=teclado
    [HttpGet]
    public async Task<ActionResult<List<ArticuloDto>>> Buscar([FromQuery] string? busqueda)
    {
        var query = _db.Articulos.AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var b = busqueda.ToLower();
            query = query.Where(a => a.Codigo.ToLower().Contains(b) || a.Descripcion.ToLower().Contains(b));
        }

        var lista = await query
            .OrderBy(a => a.Codigo)
            .Select(a => new ArticuloDto(a.Id, a.Codigo, a.Descripcion, a.PrecioUnitario, a.StockActual, a.AlicuotaIva))
            .ToListAsync();

        return Ok(lista);
    }
}
