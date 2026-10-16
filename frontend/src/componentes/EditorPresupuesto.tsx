import { useEffect, useState } from 'react'
import { api } from '../api/cliente'
import type { ArticuloDto, ClienteDto, LineaNueva } from '../api/tipos'
import { formatearMoneda, mensajeDe } from '../lib/formato'
import { subtotalDeLinea, totalesDeLineas } from '../lib/totales'
import { BuscadorArticulos } from './BuscadorArticulos'

interface Props {
  onCreado: () => void
  onCancelar: () => void
}

const VALIDEZ_DEFAULT = 15

/**
 * Mismas validaciones que hace el backend, para no llegar al 400 por cosas que
 * se pueden ver en pantalla (regla de negocio 7).
 */
function errorDeLinea(linea: LineaNueva): string {
  if (!Number.isInteger(linea.cantidad) || linea.cantidad <= 0) {
    return 'La cantidad debe ser un entero mayor a cero.'
  }
  if (linea.descuentoPct < 0 || linea.descuentoPct > 100) {
    return 'El descuento debe estar entre 0% y 100%.'
  }
  return ''
}

export function EditorPresupuesto({ onCreado, onCancelar }: Props) {
  const [clientes, setClientes] = useState<ClienteDto[]>([])
  const [errorClientes, setErrorClientes] = useState('')
  const [clienteId, setClienteId] = useState<number | ''>('')
  const [validezDias, setValidezDias] = useState(VALIDEZ_DEFAULT)
  const [lineas, setLineas] = useState<LineaNueva[]>([])
  const [enviando, setEnviando] = useState(false)
  const [errorApi, setErrorApi] = useState('')

  useEffect(() => {
    let activo = true
    api
      .clientes()
      .then((lista) => {
        if (activo) setClientes(lista)
      })
      .catch((e: unknown) => {
        if (activo) setErrorClientes(mensajeDe(e))
      })
    return () => {
      activo = false
    }
  }, [])

  function agregarArticulo(articulo: ArticuloDto) {
    setLineas((previas) => {
      // Si el artículo ya está en el presupuesto, sumamos cantidad en vez de
      // duplicar la fila.
      const yaEsta = previas.some((linea) => linea.articulo.id === articulo.id)
      if (yaEsta) {
        return previas.map((linea) =>
          linea.articulo.id === articulo.id ? { ...linea, cantidad: linea.cantidad + 1 } : linea,
        )
      }
      return [...previas, { articulo, cantidad: 1, descuentoPct: 0 }]
    })
  }

  function cambiarCantidad(articuloId: number, cantidad: number) {
    setLineas((previas) =>
      previas.map((linea) => (linea.articulo.id === articuloId ? { ...linea, cantidad } : linea)),
    )
  }

  function cambiarDescuento(articuloId: number, descuentoPct: number) {
    setLineas((previas) =>
      previas.map((linea) => (linea.articulo.id === articuloId ? { ...linea, descuentoPct } : linea)),
    )
  }

  function quitarLinea(articuloId: number) {
    setLineas((previas) => previas.filter((linea) => linea.articulo.id !== articuloId))
  }

  const totales = totalesDeLineas(lineas)

  const errores = [
    clienteId === '' ? 'Elegí un cliente.' : '',
    !Number.isInteger(validezDias) || validezDias <= 0
      ? 'La validez debe ser un entero mayor a cero.'
      : '',
    lineas.length === 0 ? 'Agregá al menos un artículo.' : '',
    ...lineas.map(errorDeLinea),
  ].filter((mensaje) => mensaje !== '')

  async function guardar() {
    if (errores.length > 0 || enviando) return

    setEnviando(true)
    setErrorApi('')
    try {
      await api.crearPresupuesto({
        clienteId: Number(clienteId),
        validezDias,
        items: lineas.map((linea) => ({
          articuloId: linea.articulo.id,
          cantidad: linea.cantidad,
          descuentoPct: linea.descuentoPct,
        })),
      })
      onCreado()
    } catch (e: unknown) {
      setErrorApi(mensajeDe(e))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <section className="editor">
      <h2>Nuevo presupuesto</h2>

      {errorApi && <p className="aviso aviso-error">{errorApi}</p>}
      {errorClientes && <p className="aviso aviso-error">{errorClientes}</p>}

      <div className="editor-cabecera">
        <div className="campo">
          <label htmlFor="cliente">Cliente</label>
          <select
            id="cliente"
            value={clienteId}
            onChange={(evento) =>
              setClienteId(evento.target.value === '' ? '' : Number(evento.target.value))
            }
          >
            <option value="">— Elegí un cliente —</option>
            {clientes.map((cliente) => (
              <option key={cliente.id} value={cliente.id}>
                {cliente.razonSocial} ({cliente.cuit})
              </option>
            ))}
          </select>
        </div>

        <div className="campo">
          <label htmlFor="validez">Validez (días)</label>
          <input
            id="validez"
            type="number"
            min={1}
            step={1}
            value={validezDias}
            onChange={(evento) => setValidezDias(Number(evento.target.value))}
          />
        </div>
      </div>

      <BuscadorArticulos onAgregar={agregarArticulo} />

      <table className="tabla">
        <thead>
          <tr>
            <th>Artículo</th>
            <th className="num">Precio unit.</th>
            <th className="num">IVA</th>
            <th className="num">Cantidad</th>
            <th className="num">Descuento %</th>
            <th className="num">Subtotal</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {lineas.length === 0 && (
            <tr>
              <td colSpan={7} className="vacio">
                Todavía no agregaste artículos. Buscá uno arriba y hacé click para agregarlo.
              </td>
            </tr>
          )}

          {lineas.map((linea) => {
            const problema = errorDeLinea(linea)
            return (
              <tr key={linea.articulo.id}>
                <td>
                  <span className="resultado-codigo">{linea.articulo.codigo}</span>{' '}
                  {linea.articulo.descripcion}
                </td>
                <td className="num">{formatearMoneda(linea.articulo.precioUnitario)}</td>
                <td className="num">{linea.articulo.alicuotaIva}%</td>
                <td className="num">
                  <input
                    type="number"
                    min={1}
                    step={1}
                    value={linea.cantidad}
                    onChange={(evento) => cambiarCantidad(linea.articulo.id, Number(evento.target.value))}
                    aria-invalid={problema !== ''}
                  />
                </td>
                <td className="num">
                  <input
                    type="number"
                    min={0}
                    max={100}
                    step={0.5}
                    value={linea.descuentoPct}
                    onChange={(evento) => cambiarDescuento(linea.articulo.id, Number(evento.target.value))}
                  />
                </td>
                <td className="num">{formatearMoneda(subtotalDeLinea(linea))}</td>
                <td>
                  <button type="button" className="boton-secundario" onClick={() => quitarLinea(linea.articulo.id)}>
                    Quitar
                  </button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>

      {errores.length > 0 && (
        <ul className="aviso aviso-error lista-errores">
          {errores.map((mensaje, indice) => (
            <li key={`${indice}-${mensaje}`}>{mensaje}</li>
          ))}
        </ul>
      )}

      <div className="totales">
        <div className="totales-fila">
          <span>Subtotal</span>
          <strong>{formatearMoneda(totales.subtotal)}</strong>
        </div>
        <div className="totales-fila">
          <span>IVA (por línea, según alícuota)</span>
          <strong>{formatearMoneda(totales.iva)}</strong>
        </div>
        <div className="totales-fila totales-total">
          <span>Total</span>
          <strong>{formatearMoneda(totales.total)}</strong>
        </div>
      </div>

      <div className="acciones">
        <button type="button" onClick={guardar} disabled={errores.length > 0 || enviando}>
          {enviando ? 'Guardando…' : 'Crear presupuesto'}
        </button>
        <button type="button" className="boton-secundario" onClick={onCancelar} disabled={enviando}>
          Cancelar
        </button>
      </div>
    </section>
  )
}
