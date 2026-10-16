using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Dtos;
using MiniErp.Core.Services;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportesController : ControllerBase
{
    private const int TopPorDefecto = 5;
    private const int TopMaximo = 50;
    private const int DiasPorDefecto = 30;

    private readonly ReporteService _service;

    public ReportesController(ReporteService service) => _service = service;

    /// <summary>
    /// GET /api/reportes/top-articulos?desde=2026-01-01&amp;hasta=2026-01-31&amp;top=5
    /// El rango se interpreta por dia completo en UTC, que es como se guardan las fechas.
    /// Sin parametros: ultimos 30 dias y top 5.
    /// </summary>
    [HttpGet("top-articulos")]
    public async Task<ActionResult<List<TopArticuloDto>>> TopArticulos(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int? top)
    {
        var hastaDia = (hasta ?? DateTime.UtcNow).Date;
        var desdeDia = (desde ?? hastaDia.AddDays(-DiasPorDefecto)).Date;

        var desdeUtc = DateTime.SpecifyKind(desdeDia, DateTimeKind.Utc);
        var hastaUtc = DateTime.SpecifyKind(hastaDia.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var cantidad = top ?? TopPorDefecto;
        if (cantidad <= 0 || cantidad > TopMaximo)
            return BadRequest(new { error = $"El top debe estar entre 1 y {TopMaximo}." });

        try
        {
            var resultado = await _service.TopArticulosAsync(desdeUtc, hastaUtc, cantidad);

            return Ok(resultado
                .Select(a => new TopArticuloDto(
                    a.ArticuloId, a.Codigo, a.Descripcion, a.Cantidad, a.MontoNeto, a.MontoIva, a.MontoTotal))
                .ToList());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
