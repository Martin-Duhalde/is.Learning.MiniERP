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
import { camposDeItem, subtotalLinea, totalesDeItems } from '../lib/totales'

interface Props {
  id: number
  onVolver: () => void
}

/**
 * Detalle de un presupuesto. Además de mostrar lo que devuelve la API, recalcula
 * los totales con la regla de negocio y avisa si no coinciden: sirve para detectar
 * diferencias de redondeo entre el backend y el cliente.
 */
export function DetallePresupuesto({ id, onVolver }: Props) {
  const [presupuesto, setPresupuesto] = useState<PresupuestoDto | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let activo = true

    api
      .presupuesto(id)
      .then((dato) => {
        if (activo) setPresupuesto(dato)
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
  }, [id])

  if (cargando) return <p className="aviso">Cargando presupuesto…</p>

  if (error) {
    return (
      <section className="detalle">
        <p className="aviso aviso-error">{error}</p>
        <button type="button" className="boton-secundario" onClick={onVolver}>
          Volver
        </button>
      </section>
    )
  }

  if (!presupuesto) return null

  const locales = totalesDeItems(presupuesto.items)
  const desvioIva = Math.abs(locales.iva - presupuesto.iva) > 0.005
  const desvioTotal = Math.abs(locales.total - presupuesto.total) > 0.005
  const hayDesvio = desvioIva || desvioTotal
  const vencido = estaVencido(presupuesto.fecha, presupuesto.validezDias)

  return (
    <section className="detalle">
      <h2>
        Presupuesto N° {presupuesto.numero}{' '}
        <span className={`chip chip-${presupuesto.estado.toLowerCase()}`}>{presupuesto.estado}</span>
      </h2>

      <dl className="datos">
        <div>
          <dt>Cliente</dt>
          <dd>{presupuesto.clienteRazonSocial}</dd>
        </div>
        <div>
          <dt>Fecha</dt>
          <dd>{formatearFecha(presupuesto.fecha)}</dd>
        </div>
        <div>
          <dt>Vence</dt>
          <dd>
            {formatearFecha(fechaVencimiento(presupuesto.fecha, presupuesto.validezDias).toISOString())}
            {vencido && <span className="chip chip-vencido">Vencido</span>}
          </dd>
        </div>
        <div>
          <dt>Validez</dt>
          <dd>{presupuesto.validezDias} días</dd>
        </div>
      </dl>

      <table className="tabla">
        <thead>
          <tr>
            <th>Artículo</th>
            <th className="num">Cantidad</th>
            <th className="num">Precio unit.</th>
            <th className="num">Desc. %</th>
            <th className="num">IVA</th>
            <th className="num">Subtotal línea</th>
          </tr>
        </thead>
        <tbody>
          {presupuesto.items.map((item) => (
            <tr key={item.articuloId}>
              <td>
                <span className="resultado-codigo">{item.articuloCodigo}</span>{' '}
                {item.articuloDescripcion}
              </td>
              <td className="num">{item.cantidad}</td>
              <td className="num">{formatearMoneda(item.precioUnitario)}</td>
              <td className="num">{item.descuentoPct}%</td>
              <td className="num">{item.alicuotaIva}%</td>
              <td className="num">{formatearMoneda(subtotalLinea(camposDeItem(item)))}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="totales">
        <div className="totales-fila">
          <span>Subtotal</span>
          <strong>{formatearMoneda(presupuesto.subtotal)}</strong>
        </div>
        <div className="totales-fila">
          <span>IVA</span>
          <strong>{formatearMoneda(presupuesto.iva)}</strong>
        </div>
        <div className="totales-fila totales-total">
          <span>Total</span>
          <strong>{formatearMoneda(presupuesto.total)}</strong>
        </div>
      </div>

      {hayDesvio && (
        <p className="aviso aviso-error">
          Los totales que devuelve la API no coinciden con la regla de negocio (IVA redondeado por línea
          antes de sumar). API: {formatearMoneda(presupuesto.subtotal)} / {formatearMoneda(presupuesto.iva)} /{' '}
          {formatearMoneda(presupuesto.total)} — cálculo local: {formatearMoneda(locales.subtotal)} /{' '}
          {formatearMoneda(locales.iva)} / {formatearMoneda(locales.total)}.
        </p>
      )}

      <div className="acciones">
        <button type="button" className="boton-secundario" onClick={onVolver}>
          Volver a la lista
        </button>
      </div>
    </section>
  )
}
