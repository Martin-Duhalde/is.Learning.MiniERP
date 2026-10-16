import { useEffect, useState } from 'react'
import { api } from '../api/cliente'
import type { ArticuloDto } from '../api/tipos'
import { formatearMoneda, mensajeDe } from '../lib/formato'

interface Props {
  onAgregar: (articulo: ArticuloDto) => void
}

/** Búsqueda de artículos con debounce contra GET /api/articulos?busqueda=... */
export function BuscadorArticulos({ onAgregar }: Props) {
  const [busqueda, setBusqueda] = useState('')
  const [resultados, setResultados] = useState<ArticuloDto[]>([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    const controlador = new AbortController()

    const temporizador = setTimeout(() => {
      setCargando(true)
      api
        .articulos(busqueda, controlador.signal)
        .then((lista) => {
          setResultados(lista)
          setError('')
          setCargando(false)
        })
        .catch((e: unknown) => {
          // Una búsqueda cancelada por el debounce no es un error para mostrar.
          if (e instanceof DOMException && e.name === 'AbortError') return
          setError(mensajeDe(e))
          setCargando(false)
        })
    }, 300)

    return () => {
      clearTimeout(temporizador)
      controlador.abort()
    }
  }, [busqueda])

  return (
    <section className="buscador">
      <label htmlFor="busqueda-articulo">Buscar artículo</label>
      <input
        id="busqueda-articulo"
        type="search"
        autoComplete="off"
        placeholder="Código o descripción (ej.: teclado)"
        value={busqueda}
        onChange={(evento) => setBusqueda(evento.target.value)}
      />

      {error && <p className="aviso aviso-error">{error}</p>}
      {!error && cargando && <p className="aviso">Buscando…</p>}

      {!error && !cargando && resultados.length === 0 && (
        <p className="aviso">No se encontraron artículos para “{busqueda}”.</p>
      )}

      {!error && resultados.length > 0 && (
        <ul className="resultados">
          {resultados.map((articulo) => (
            <li key={articulo.id}>
              <button
                type="button"
                className="resultado"
                onClick={() => onAgregar(articulo)}
                title="Agregar al presupuesto"
              >
                <span className="resultado-codigo">{articulo.codigo}</span>
                <span className="resultado-descripcion">{articulo.descripcion}</span>
                <span className="resultado-precio">{formatearMoneda(articulo.precioUnitario)}</span>
                <span className="resultado-alicuota">IVA {articulo.alicuotaIva}%</span>
                <span className={articulo.stockActual > 0 ? 'resultado-stock' : 'resultado-stock sin-stock'}>
                  Stock {articulo.stockActual}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
