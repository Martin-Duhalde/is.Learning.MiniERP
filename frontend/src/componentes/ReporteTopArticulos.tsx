import { useEffect, useState } from 'react'
import { api } from '../api/cliente'
import type { TopArticuloDto } from '../api/tipos'
import { formatearMoneda, mensajeDe } from '../lib/formato'

interface Props {
  onVolver: () => void
}

/** yyyy-mm-dd en UTC, que es como el backend interpreta el rango de fechas. */
function fechaIso(diasAtras: number): string {
  const fecha = new Date()
  fecha.setUTCDate(fecha.getUTCDate() - diasAtras)
  return fecha.toISOString().slice(0, 10)
}

export function ReporteTopArticulos({ onVolver }: Props) {
  const [formulario, setFormulario] = useState({ desde: fechaIso(30), hasta: fechaIso(0), top: 5 })
  const [consulta, setConsulta] = useState(formulario)
  const [filas, setFilas] = useState<TopArticuloDto[]>([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let activo = true

    api
      .topArticulos(consulta.desde, consulta.hasta, consulta.top)
      .then((lista) => {
        if (activo) {
          setFilas(lista)
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
  }, [consulta])

  function consultar() {
    setError('')
    setCargando(true)
    setConsulta({ ...formulario })
  }

  const totales = filas.reduce(
    (acumulado, fila) => ({
      cantidad: acumulado.cantidad + fila.cantidad,
      montoNeto: acumulado.montoNeto + fila.montoNeto,
      montoIva: acumulado.montoIva + fila.montoIva,
      montoTotal: acumulado.montoTotal + fila.montoTotal,
    }),
    { cantidad: 0, montoNeto: 0, montoIva: 0, montoTotal: 0 },
  )

  return (
    <section className="detalle">
      <h2>Top artículos por monto facturado</h2>
      <p className="aviso">
        Monto neto (sin IVA) de los artículos facturados en el rango. El IVA se calcula por línea
        redondeando a 2 decimales, igual que en los presupuestos, así que la suma coincide con el
        total de las facturas.
      </p>

      <div className="editor-cabecera">
        <div className="campo">
          <label htmlFor="reporte-desde">Desde</label>
          <input
            id="reporte-desde"
            type="date"
            value={formulario.desde}
            onChange={(evento) => setFormulario((anterior) => ({ ...anterior, desde: evento.target.value }))}
          />
        </div>
        <div className="campo">
          <label htmlFor="reporte-hasta">Hasta</label>
          <input
            id="reporte-hasta"
            type="date"
            value={formulario.hasta}
            onChange={(evento) => setFormulario((anterior) => ({ ...anterior, hasta: evento.target.value }))}
          />
        </div>
        <div className="campo">
          <label htmlFor="reporte-top">Top</label>
          <input
            id="reporte-top"
            type="number"
            min={1}
            max={50}
            value={formulario.top}
            onChange={(evento) => setFormulario((anterior) => ({ ...anterior, top: Number(evento.target.value) }))}
          />
        </div>
        <div className="campo">
          <label htmlFor="reporte-consultar">&nbsp;</label>
          <button id="reporte-consultar" type="button" onClick={consultar} disabled={cargando}>
            Consultar
          </button>
        </div>
      </div>

      {error && <p className="aviso aviso-error">{error}</p>}
      {!error && cargando && <p className="aviso">Consultando…</p>}

      {!error && !cargando && filas.length === 0 && (
        <p className="aviso">No hay facturas en ese rango de fechas.</p>
      )}

      {!error && filas.length > 0 && (
        <table className="tabla">
          <thead>
            <tr>
              <th>Código</th>
              <th>Artículo</th>
              <th className="num">Cantidad</th>
              <th className="num">Monto neto</th>
              <th className="num">IVA</th>
              <th className="num">Total</th>
            </tr>
          </thead>
          <tbody>
            {filas.map((fila) => (
              <tr key={fila.articuloId}>
                <td>
                  <span className="resultado-codigo">{fila.codigo}</span>
                </td>
                <td>{fila.descripcion}</td>
                <td className="num">{fila.cantidad}</td>
                <td className="num">{formatearMoneda(fila.montoNeto)}</td>
                <td className="num">{formatearMoneda(fila.montoIva)}</td>
                <td className="num">{formatearMoneda(fila.montoTotal)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <th colSpan={2}>Total del top</th>
              <th className="num">{totales.cantidad}</th>
              <th className="num">{formatearMoneda(totales.montoNeto)}</th>
              <th className="num">{formatearMoneda(totales.montoIva)}</th>
              <th className="num">{formatearMoneda(totales.montoTotal)}</th>
            </tr>
          </tfoot>
        </table>
      )}

      <div className="acciones">
        <button type="button" className="boton-secundario" onClick={onVolver}>
          Volver a la lista
        </button>
      </div>
    </section>
  )
}
