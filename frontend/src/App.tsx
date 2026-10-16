import { useState } from 'react'
import { API_URL } from './api/cliente'
import { DetallePresupuesto } from './componentes/DetallePresupuesto'
import { EditorPresupuesto } from './componentes/EditorPresupuesto'
import { ListaPresupuestos } from './componentes/ListaPresupuestos'
import { ReporteTopArticulos } from './componentes/ReporteTopArticulos'
import './App.css'

// Navegación mínima con estado: son cuatro vistas y no hace falta un router.
type Vista =
  | { tipo: 'lista' }
  | { tipo: 'nuevo' }
  | { tipo: 'detalle'; id: number }
  | { tipo: 'reporte' }

function App() {
  const [vista, setVista] = useState<Vista>({ tipo: 'lista' })
  // Cada vuelta a la lista fuerza un refetch, así lo que se acaba de crear o
  // facturar se ve sin apretar F5.
  const [recarga, setRecarga] = useState(0)

  function volverALista() {
    setRecarga((anterior) => anterior + 1)
    setVista({ tipo: 'lista' })
  }

  return (
    <div className="app">
      <header className="app-header">
        <div>
          <h1>Mini ERP</h1>
          <p className="app-subtitulo">Presupuestos, facturación y stock</p>
        </div>
        <div className="acciones">
          {vista.tipo === 'lista' && (
            <button
              type="button"
              className="boton-secundario"
              onClick={() => setVista({ tipo: 'reporte' })}
            >
              Reporte top-N
            </button>
          )}
          {vista.tipo !== 'lista' && (
            <button type="button" className="boton-secundario" onClick={volverALista}>
              ← Volver a la lista
            </button>
          )}
        </div>
      </header>

      <main className="app-main">
        {vista.tipo === 'lista' && (
          <ListaPresupuestos
            recarga={recarga}
            onNuevo={() => setVista({ tipo: 'nuevo' })}
            onVer={(id) => setVista({ tipo: 'detalle', id })}
          />
        )}

        {vista.tipo === 'nuevo' && (
          <EditorPresupuesto onCreado={volverALista} onCancelar={volverALista} />
        )}

        {vista.tipo === 'detalle' && (
          <DetallePresupuesto id={vista.id} onVolver={volverALista} />
        )}

        {vista.tipo === 'reporte' && <ReporteTopArticulos onVolver={volverALista} />}
      </main>

      <footer className="app-footer">
        API: <code>{API_URL}</code>
      </footer>
    </div>
  )
}

export default App
