const formatoMoneda = new Intl.NumberFormat('es-AR', {
  style: 'currency',
  currency: 'ARS',
  minimumFractionDigits: 2,
})

const formatoFecha = new Intl.DateTimeFormat('es-AR', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})

export const formatearMoneda = (n: number): string => formatoMoneda.format(n)

export const formatearFecha = (iso: string): string => formatoFecha.format(new Date(iso))

/**
 * Vencimiento = fecha + validezDias, en UTC.
 * El backend compara contra DateTime.UtcNow, así que acá también se usa UTC para
 * que el badge "Vencido" coincida con lo que va a responder la API al facturar.
 */
export function fechaVencimiento(iso: string, validezDias: number): Date {
  const fecha = new Date(iso)
  fecha.setUTCDate(fecha.getUTCDate() + validezDias)
  return fecha
}

export function estaVencido(iso: string, validezDias: number): boolean {
  return Date.now() > fechaVencimiento(iso, validezDias).getTime()
}

/** Mensaje legible para cualquier cosa que se haya lanzado en un catch. */
export function mensajeDe(error: unknown): string {
  if (error instanceof Error && error.message) return error.message
  return 'Ocurrió un error inesperado.'
}
