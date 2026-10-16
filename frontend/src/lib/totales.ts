import type { LineaNueva, PresupuestoItemDto } from '../api/tipos'

// Estas funciones replican la aritmética de PresupuestoService.CalcularTotales
// (reglas de negocio 1 y 2 del challenge), que es la fuente de verdad:
//
//   1. subtotal de línea = cantidad x precioUnitario x (1 - descuento%). El descuento
//      se aplica ANTES del IVA.
//   2. el IVA se calcula por línea según su alícuota, se redondea a 2 decimales POR
//      LÍNEA y recién después se suma.
//
// El subtotal se acumula sin redondear (igual que el backend) y el total es
// subtotal + iva. El redondeo es sólo de presentación, en formatearMoneda.

export interface Totales {
  subtotal: number
  iva: number
  total: number
}

/** Campos de una línea ya resueltos contra el precio y la alícuota del artículo. */
interface LineaCalculable {
  cantidad: number
  precioUnitario: number
  descuentoPct: number
  alicuotaIva: number
}

/**
 * Redondeo a 2 decimales "half away from zero", equivalente al
 * Math.Round(x, 2, MidpointRounding.AwayFromZero) del backend.
 *
 * El epsilon compensa que los decimal de C# son exactos y los double de JS no:
 * 467.775 en binario es 467.77499999999998, así que Math.round lo mandaría a 467.77
 * cuando el backend devuelve 467.78.
 */
export function redondear2(n: number): number {
  const signo = n < 0 ? -1 : 1
  return (Math.round(Math.abs(n) * 100 + 1e-6) * signo) / 100
}

/** Subtotal de una línea, sin redondear. */
export function subtotalLinea(linea: LineaCalculable): number {
  return linea.cantidad * linea.precioUnitario * (1 - linea.descuentoPct / 100)
}

/** IVA de una línea, redondeado a 2 decimales. */
export function ivaLinea(linea: LineaCalculable): number {
  return redondear2((subtotalLinea(linea) * linea.alicuotaIva) / 100)
}

export function calcularTotales(lineas: LineaCalculable[]): Totales {
  let subtotal = 0
  let iva = 0

  for (const linea of lineas) {
    subtotal += subtotalLinea(linea)
    iva += ivaLinea(linea)
  }

  return { subtotal, iva, total: subtotal + iva }
}

/** Adapta una línea en edición a la forma que consumen los cálculos. */
export function camposDeLinea(linea: LineaNueva): LineaCalculable {
  return {
    cantidad: linea.cantidad,
    precioUnitario: linea.articulo.precioUnitario,
    descuentoPct: linea.descuentoPct,
    alicuotaIva: linea.articulo.alicuotaIva,
  }
}

/** Adapta un item ya guardado a la forma que consumen los cálculos. */
export function camposDeItem(item: PresupuestoItemDto): LineaCalculable {
  return {
    cantidad: item.cantidad,
    precioUnitario: item.precioUnitario,
    descuentoPct: item.descuentoPct,
    alicuotaIva: item.alicuotaIva,
  }
}

export function subtotalDeLinea(linea: LineaNueva): number {
  return subtotalLinea(camposDeLinea(linea))
}

/** Total en vivo del editor. */
export function totalesDeLineas(lineas: LineaNueva[]): Totales {
  return calcularTotales(lineas.map(camposDeLinea))
}

/** Mismos totales, pero recalculados sobre lo que devolvió la API. */
export function totalesDeItems(items: PresupuestoItemDto[]): Totales {
  return calcularTotales(items.map(camposDeItem))
}
