// Tipos que espejan los DTO del backend (backend/MiniErp.Api/Dtos/Dtos.cs).
// System.Text.Json los serializa en camelCase, y los decimal de C# llegan como number.

export type EstadoPresupuesto = 'Borrador' | 'Aprobado' | 'Rechazado' | 'Facturado'

export interface ClienteDto {
  id: number
  razonSocial: string
  cuit: string
  condicionIva: string
}

export interface ArticuloDto {
  id: number
  codigo: string
  descripcion: string
  precioUnitario: number
  stockActual: number
  alicuotaIva: number
}

export interface PresupuestoItemDto {
  articuloId: number
  articuloCodigo: string
  articuloDescripcion: string
  cantidad: number
  precioUnitario: number
  descuentoPct: number
  alicuotaIva: number
  subtotalLinea: number
}

export interface PresupuestoDto {
  id: number
  numero: number
  fecha: string
  clienteId: number
  clienteRazonSocial: string
  estado: EstadoPresupuesto
  validezDias: number
  items: PresupuestoItemDto[]
  subtotal: number
  iva: number
  total: number
}

export interface FacturaDto {
  id: number
  numero: number
  fecha: string
  presupuestoId: number
  subtotal: number
  iva: number
  total: number
}

// ---- Body de POST /api/presupuestos ----

export interface CrearPresupuestoItemDto {
  articuloId: number
  cantidad: number
  descuentoPct: number
}

export interface CrearPresupuestoRequest {
  clienteId: number
  validezDias: number
  items: CrearPresupuestoItemDto[]
}

/** Línea que se está editando en el cliente, todavía sin persistir. */
export interface LineaNueva {
  articulo: ArticuloDto
  cantidad: number
  descuentoPct: number
}

/** Fila del reporte top-N de artículos por monto facturado. */
export interface TopArticuloDto {
  articuloId: number
  codigo: string
  descripcion: string
  cantidad: number
  montoNeto: number
  montoIva: number
  montoTotal: number
}
