using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Dtos;
using MiniErp.Core.Data;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ClientesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<ClienteDto>>> Listar()
    {
        var clientes = await _db.Clientes
            .OrderBy(c => c.RazonSocial)
            .ToListAsync();

        var lista = clientes
            .Select(c => new ClienteDto(c.Id, c.RazonSocial, c.Cuit, c.CondicionIva.ToString()))
            .ToList();

        return Ok(lista);
    }
}
