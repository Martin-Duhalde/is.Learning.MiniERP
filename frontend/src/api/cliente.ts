import type {
  ArticuloDto,
  ClienteDto,
  CrearPresupuestoRequest,
  FacturaDto,
  PresupuestoDto,
} from './tipos'

/**
 * Base de la API. Se puede sobrescribir con VITE_API_URL en un .env si algún día
 * la API no vive en localhost:5080. El backend tiene CORS habilitado para
 * http://localhost:5173, que es el puerto por defecto de Vite.
 */
export const API_URL: string = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

/** Error con el mensaje de negocio que devuelve la API en { "error": "..." }. */
export class ApiError extends Error {
  constructor(mensaje: string) {
    super(mensaje)
    this.name = 'ApiError'
  }
}

async function mensajeDeRespuesta(respuesta: Response): Promise<string> {
  try {
    const cuerpo = (await respuesta.json()) as { error?: string }
    if (cuerpo.error) return cuerpo.error
  } catch {
    // El cuerpo no era JSON: nos quedamos con el status.
  }
  return `Error HTTP ${respuesta.status}`
}

async function pedir<T>(ruta: string, init?: RequestInit): Promise<T> {
  const opciones: RequestInit = { ...init }

  // El Content-Type sólo va cuando hay body: mandarlo en un GET dispara un
  // preflight de CORS innecesario.
  if (opciones.body !== undefined) {
    opciones.headers = { 'Content-Type': 'application/json', ...opciones.headers }
  }

  let respuesta: Response
  try {
    respuesta = await fetch(`${API_URL}${ruta}`, opciones)
  } catch (error) {
    // El abort de una búsqueda cancelada tiene que seguir siendo un AbortError,
    // no un falso "no se pudo conectar".
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError(`No se pudo conectar con la API en ${API_URL}. ¿Está corriendo "dotnet run"?`)
  }

  if (respuesta.status === 204) return undefined as unknown as T

  if (!respuesta.ok) throw new ApiError(await mensajeDeRespuesta(respuesta))

  return (await respuesta.json()) as T
}

export const api = {
  clientes: () => pedir<ClienteDto[]>('/api/clientes'),

  articulos: (busqueda: string, signal?: AbortSignal) => {
    const termino = busqueda.trim()
    const query = termino ? `?busqueda=${encodeURIComponent(termino)}` : ''
    return pedir<ArticuloDto[]>(`/api/articulos${query}`, { signal })
  },

  presupuestos: () => pedir<PresupuestoDto[]>('/api/presupuestos'),

  presupuesto: (id: number) => pedir<PresupuestoDto>(`/api/presupuestos/${id}`),

  crearPresupuesto: (body: CrearPresupuestoRequest) =>
    pedir<PresupuestoDto>('/api/presupuestos', { method: 'POST', body: JSON.stringify(body) }),

  eliminarPresupuesto: (id: number) =>
    pedir<void>(`/api/presupuestos/${id}`, { method: 'DELETE' }),

  facturar: (presupuestoId: number) =>
    pedir<FacturaDto>(`/api/facturas/facturar/${presupuestoId}`, { method: 'POST' }),
}
