namespace MiniErp.Api.Dtos;

// ---- Requests ----
public record CrearPresupuestoItemDto(int ArticuloId, int Cantidad, decimal DescuentoPct);

public record CrearPresupuestoRequest(int ClienteId, int ValidezDias, List<CrearPresupuestoItemDto> Items);

// ---- Responses ----
public record ArticuloDto(
    int Id,
    string Codigo,
    string Descripcion,
    decimal PrecioUnitario,
    int StockActual,
    decimal AlicuotaIva);

public record PresupuestoItemDto(
    int ArticuloId,
    string ArticuloCodigo,
    string ArticuloDescripcion,
    int Cantidad,
    decimal PrecioUnitario,
    decimal DescuentoPct,
    decimal AlicuotaIva,
    decimal SubtotalLinea);

public record PresupuestoDto(
    int Id,
    int Numero,
    DateTime Fecha,
    int ClienteId,
    string ClienteRazonSocial,
    string Estado,
    int ValidezDias,
    List<PresupuestoItemDto> Items,
    decimal Subtotal,
    decimal Iva,
    decimal Total);

public record FacturaDto(
    int Id,
    int Numero,
    DateTime Fecha,
    int PresupuestoId,
    decimal Subtotal,
    decimal Iva,
    decimal Total);

public record ClienteDto(
    int Id,
    string RazonSocial,
    string Cuit,
    string CondicionIva);
