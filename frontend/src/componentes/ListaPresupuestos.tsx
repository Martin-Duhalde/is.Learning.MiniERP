import { useEffect, useState } from 'react'
import { api } from '../api/cliente'
import type { PresupuestoDto } from '../api/tipos'
import {
  estaVencido,
  fechaVencimiento,
  formatearFecha,
  formatearMoneda,
  mensajeDe,
} from '../lib/formato'

interface Props {
  /** Cambia cuando el padre quiere forzar un refetch (por ejemplo al volver del editor). */
  recarga: number
  onNuevo: () => void
  onVer: (id: number) => void
}

interface Mensaje {
  tipo: 'ok' | 'error'
  texto: string
}

export function ListaPresupuestos({ recarga, onNuevo, onVer }: Props) {
  const [presupuestos, setPresupuestos] = useState<PresupuestoDto[]>([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const [version, setVersion] = useState(0)
  const [idEnProceso, setIdEnProceso] = useState<number | null>(null)
  const [mensajes, setMensajes] = useState<Record<number, Mensaje>>({})

  useEffect(() => {
    let activo = true

    api
      .presupuestos()
      .then((lista) => {
        if (activo) {
          setPresupuestos(lista)
          setError('')
        }
      })
      .catch((e: unknown) => {
        if (activo) setError(mensajeDe(e))
      })
      .finally(() => {
        if (activo) setCargando(false)
      })

    return () => {
      activo = false
    }
  }, [recarga, version])

  // Los reseteos van acá y no dentro del efecto: llamar setState sincrónicamente
  // en el cuerpo de un efecto dispara renders en cascada (react-hooks/set-state-in-effect).
  const refrescar = () => {
    setError('')
    setCargando(true)
    setVersion((anterior) => anterior + 1)
  }

  function avisar(id: number, mensaje: Mensaje) {
    setMensajes((previos) => ({ ...previos, [id]: mensaje }))
  }

  async function facturar(presupuesto: PresupuestoDto) {
    setIdEnProceso(presupuesto.id)
    try {
      const factura = await api.facturar(presupuesto.id)
      avisar(presupuesto.id, { tipo: 'ok', texto: `Facturado. Factura N° ${factura.numero}.` })
      refrescar()
    } catch (e: unknown) {
      // Acá cae el 400 del backend: sin stock, vencido o ya facturado.
      avisar(presupuesto.id, { tipo: 'error', texto: mensajeDe(e) })
    } finally {
      setIdEnProceso(null)
    }
  }

  async function duplicar(presupuesto: PresupuestoDto) {
    setIdEnProceso(presupuesto.id)
    try {
      const copia = await api.duplicarPresupuesto(presupuesto.id)
      avisar(presupuesto.id, { tipo: 'ok', texto: `Duplicado. Nuevo presupuesto N° ${copia.numero}.` })
      refrescar()
    } catch (e: unknown) {
      avisar(presupuesto.id, { tipo: 'error', texto: mensajeDe(e) })
    } finally {
      setIdEnProceso(null)
    }
  }

  async function eliminar(presupuesto: PresupuestoDto) {
    if (!window.confirm(`¿Eliminar el presupuesto N° ${presupuesto.numero}?`)) return

    setIdEnProceso(presupuesto.id)
    try {
      await api.eliminarPresupuesto(presupuesto.id)
      refrescar()
    } catch (e: unknown) {
      avisar(presupuesto.id, { tipo: 'error', texto: mensajeDe(e) })
    } finally {
      setIdEnProceso(null)
    }
  }

  return (
    <section className="lista">
      <div className="lista-cabecera">
        <h2>Presupuestos</h2>
        <div>
          <button type="button" className="boton-secundario" onClick={refrescar} disabled={cargando}>
            Refrescar
          </button>{' '}
          <button type="button" onClick={onNuevo}>
            Nuevo presupuesto
          </button>
        </div>
      </div>

      {cargando && <p className="aviso">Cargando presupuestos…</p>}

      {!cargando && error && (
        <p className="aviso aviso-error">
          {error}{' '}
          <button type="button" className="boton-secundario" onClick={refrescar}>
            Reintentar
          </button>
        </p>
      )}

      {!cargando && !error && presupuestos.length === 0 && (
        <p className="aviso">
          Todavía no hay presupuestos. Creá el primero con “Nuevo presupuesto”.
        </p>
      )}

      {!cargando && !error && presupuestos.length > 0 && (
        <table className="tabla">
          <thead>
            <tr>
              <th>N°</th>
              <th>Fecha</th>
              <th>Cliente</th>
              <th>Estado</th>
              <th className="num">Ítems</th>
              <th className="num">Subtotal</th>
              <th className="num">IVA</th>
              <th className="num">Total</th>
              <th>Vence</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {presupuestos.map((presupuesto) => {
              const vencido = estaVencido(presupuesto.fecha, presupuesto.validezDias)
              const enProceso = idEnProceso === presupuesto.id
              const yaFacturado = presupuesto.estado === 'Facturado'
              const mensaje = mensajes[presupuesto.id]

              return (
                <tr key={presupuesto.id}>
                  <td>{presupuesto.numero}</td>
                  <td>{formatearFecha(presupuesto.fecha)}</td>
                  <td>{presupuesto.clienteRazonSocial}</td>
                  <td>
                    <span className={`chip chip-${presupuesto.estado.toLowerCase()}`}>
                      {presupuesto.estado}
                    </span>
                  </td>
                  <td className="num">{presupuesto.items.length}</td>
                  <td className="num">{formatearMoneda(presupuesto.subtotal)}</td>
                  <td className="num">{formatearMoneda(presupuesto.iva)}</td>
                  <td className="num">{formatearMoneda(presupuesto.total)}</td>
                  <td>
                    {formatearFecha(fechaVencimiento(presupuesto.fecha, presupuesto.validezDias).toISOString())}
                    {vencido && !yaFacturado && <span className="chip chip-vencido">Vencido</span>}
                  </td>
                  <td className="acciones-celda">
                    <button
                      type="button"
                      onClick={() => facturar(presupuesto)}
                      disabled={yaFacturado || enProceso}
                      title={yaFacturado ? 'Este presupuesto ya fue facturado' : 'Facturar'}
                    >
                      {enProceso ? '…' : 'Facturar'}
                    </button>
                    <button type="button" className="boton-secundario" onClick={() => onVer(presupuesto.id)}>
                      Ver
                    </button>
                    <button
                      type="button"
                      className="boton-secundario"
                      onClick={() => duplicar(presupuesto)}
                      disabled={enProceso}
                      title="Clona el presupuesto con número nuevo y precios actuales"
                    >
                      Duplicar
                    </button>
                    <button
                      type="button"
                      className="boton-secundario"
                      onClick={() => eliminar(presupuesto)}
                      disabled={enProceso}
                    >
                      Eliminar
                    </button>

                    {mensaje && (
                      <p className={mensaje.tipo === 'ok' ? 'aviso aviso-ok' : 'aviso aviso-error'}>
                        {mensaje.texto}
                      </p>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}
    </section>
  )
}
