# NOTES

> Borrador generado a partir del historial de commits del repo y de la verificación
> contra la API corriendo. Revisalo y ajustá el detalle de cada bug a tu experiencia:
> la sección "Cómo usé IA" está escrita en primera persona y describe esta sesión.

## Bugs encontrados (backend)

Cada entrada: qué pasaba, por qué pasaba y cómo quedó resuelto. Los commits son los del
historial de este repo.

1. **El IVA se calculaba siempre al 21%.** `CalcularTotales` hacía `var iva = subtotal * 0.21m`,
   así que los artículos con alícuota 10,5% (ART-004 y ART-005) pagaban de más. Ahora el IVA
   se calcula **por línea**, con la alícuota del ítem: `ivaLinea = subtotalLinea * item.AlicuotaIva / 100m`.
   (`6389ac3`)
2. **El IVA no se redondeaba por línea.** Se sumaban los IVA de cada línea sin redondear, y la
   regla 2 pide redondear a 2 decimales **por línea** y recién después sumar. Caso testigo:
   1 × ART-004 con 1% de descuento → subtotal 4.455, IVA 467,775 → tiene que dar **467,78**.
   Se agregó `Math.Round(ivaLinea, 2, MidpointRounding.AwayFromZero)` y se acumula el IVA ya
   redondeado. (`5697970`) Verificado contra la API: devuelve 467.78.
3. **Descuento fuera de rango aceptado al calcular totales.** `CalcularTotales` no validaba
   `DescuentoPct`, así que un descuento negativo o mayor a 100 producía totales sin sentido.
   Ahora se valida 0%–100% antes de calcular. (`43ab5ee`)
4. **Cantidad y descuento inválidos aceptados al crear.** `CrearAsync` no validaba nada:
   se podía crear un presupuesto con `cantidad: 0` y total 0, con descuento 150%, con
   `validezDias` 0, con cliente inexistente o sin ítems. Se agregaron esas validaciones
   (y `ArgumentNullException.ThrowIfNull`). (`bcfdd30`, `18b0478`)
5. **No se verificaba el stock al facturar.** Se descontaba `StockActual -= Cantidad` sin
   comprobar disponibilidad, así que el stock podía quedar negativo. Ahora, si no alcanza,
   se lanza el error de negocio y la transacción hace rollback. (`1274a82`)
6. **Se podía facturar dos veces el mismo presupuesto.** Sin control de idempotencia, la
   segunda facturación descontaba stock de nuevo y generaba otra factura. Se agregó el chequeo
   de factura existente por `PresupuestoId` **y** un índice único en la base como red de
   seguridad. (`02245ce`, `0f5360f`)
7. **Faltaba transacción al facturar.** El descuento de stock y la creación de la factura
   podían quedar aplicados a medias si algo fallaba en el medio. Se envolvió todo en una
   transacción con rollback en el `catch`. (`1050d83`)
8. **Consulta por artículo (N+1).** Se hacía una consulta a la base por cada ítem, al crear y
   al facturar. Ahora se traen todos los artículos en una sola consulta (`Contains` +
   `ToDictionaryAsync` / lista) y se busca en memoria. (`685fdb0`, `0c79031`)
9. **Numeración por conteo: números repetidos al borrar.** `NumeracionService.ProximoNumeroPresupuestoAsync`
   devolvía `CountAsync() + 1`. Con dos presupuestos (nº 1 y 2), al borrar el nº 1 y crear otro,
   el nuevo volvía a ser nº 2: colisión. Ahora se calcula con `Max(Numero) + 1`, igual que el de
   facturas. Verificado: a=10, b=11, se borra el 10, el siguiente es 12 y ningún número existente
   se repite. *Limitación conocida:* no es seguro ante dos altas concurrentes (habría que usar
   una secuencia en la base o un índice único sobre `Numero`).
10. **El listado ocultaba los presupuestos recién creados.** `PresupuestoService.ListarAsync`
    filtraba `Estado != Borrador`, y `CrearAsync` guarda con `Estado = Borrador`: un presupuesto
    recién creado no aparecía **nunca** en `GET /api/presupuestos` (es el flujo 1 del README).
    Se quitó el filtro; el estado se sigue mostrando como chip en la grilla.
11. **Acentos corruptos en los mensajes de error.** `PresupuestoService.cs` y
    `FacturacionService.cs` estaban guardados en Windows-1252, así que los mensajes salían con
    caracteres rotos ("El art?culo ... no existe"). Se normalizaron a UTF-8. Verificado en la
    respuesta real de la API: "No hay suficiente stock del artículo ART-006 para facturar el
    presupuesto. Stock Actual: 3".

## Decisiones del cliente React

**Ubicación y stack.** El cliente vive en `frontend/` (el scaffold de Vite que ya estaba en el
repo). React 19 + TypeScript, **sin dependencias nuevas**: `fetch` para la API y
`useState`/`useEffect` para el estado. Nada de router, axios ni react-query: son tres vistas y la
navegación se resuelve con estado (`type Vista = {tipo:'lista'} | {tipo:'nuevo'} | {tipo:'detalle'; id:number}`).
Ventaja: funciona sin instalar nada y sin riesgo de versiones con React 19 / Vite 8.

**Estructura**

```
frontend/src/
  api/cliente.ts        # única puerta a la API: fetch + ApiError con el { error } del backend
  api/tipos.ts          # tipos que espejan los DTO del backend (import type)
  lib/totales.ts        # reglas de negocio de subtotal e IVA (funciones puras)
  lib/formato.ts        # moneda/fecha es-AR, vencimiento en UTC, mensajeDe(error)
  componentes/ListaPresupuestos.tsx    # grilla + facturar + eliminar
  componentes/EditorPresupuesto.tsx    # alta + totales en vivo + validaciones
  componentes/BuscadorArticulos.tsx    # búsqueda con debounce y AbortController
  componentes/DetallePresupuesto.tsx   # detalle + control cruzado de totales
  App.tsx               # navegación entre las tres vistas
```

**Cómo se calculan los totales en vivo** (lo importante). `lib/totales.ts` replica
`PresupuestoService.CalcularTotales`, que es la fuente de verdad:

- `subtotalLinea = cantidad × precioUnitario × (1 − descuentoPct / 100)` — el descuento va
  **antes** del IVA.
- El **subtotal se acumula sin redondear** y el **IVA se redondea por línea** (`redondear2`)
  y recién después se suma.
- `total = subtotal + iva`. El redondeo es sólo de presentación, con
  `Intl.NumberFormat('es-AR', { style:'currency', currency:'ARS' })`.

`redondear2` implementa el *half away from zero* del backend con un detalle: los `decimal` de C#
son exactos y los `double` de JS no (467,775 se representa 467,77499999999998), así que
`Math.round(x * 100)` daría 467,77 cuando el backend devuelve 467,78. Se compensa sumando `1e-6`
antes de redondear. Es un truco numérico explícito y documentado en el código; la alternativa
"prolija" es llevar todo a centavos enteros.

Verificado ejecutando el módulo real con Node: el caso del README (1 × ART-001 21% + 1 × ART-004
10,5%) da 39.500 / 7.822,50 / 47.322,50 y el caso de redondeo da 467,78; los dos coinciden con lo
que responde la API.

**Otras decisiones**

- **Los errores de negocio se muestran tal cual los devuelve el backend.** `api/cliente.ts`
  lanza `ApiError` con el campo `error` de los 400 (sin stock, vencido, ya facturado), y el
  componente lo muestra en la fila o arriba del formulario.
- **Validaciones en el cliente** espejando la regla 7 (cliente elegido, validez > 0, al menos un
  ítem, cantidad entera > 0, descuento 0–100) para no llegar al 400 por algo visible en pantalla.
  Igual, la fuente de verdad sigue siendo el backend y su mensaje se muestra si rechaza.
- **Vista de detalle con control cruzado:** recalcula los totales con la regla de negocio y avisa
  si no coinciden con lo que devolvió la API. Sirve para detectar diferencias de redondeo del
  backend sin escribir tests.
- **Vencimiento en UTC**: `fecha + validezDias` se calcula con `setUTCDate` porque el backend
  compara contra `DateTime.UtcNow`; si se calculara en hora local el badge "Vencido" podría no
  coincidir con lo que responde la API al facturar.
- **Facturar** queda deshabilitado si el presupuesto ya está `Facturado` y la fila se bloquea
  mientras el POST está en vuelo (la idempotencia real la garantiza el backend).
- **Agregar un artículo ya presente incrementa la cantidad** en vez de duplicar la fila.
- **Búsqueda con debounce de 300 ms y `AbortController`** para que una respuesta vieja no pise a
  la nueva. Un abort no se muestra como error.
- **Sin librería de UI**: CSS propio y chico en `App.css` / `index.css`, con variables y soporte
  de tema claro/oscuro.

**Cómo correrlo:** API en `http://localhost:5080` (`cd backend/MiniErp.Api && dotnet run`) y
cliente en `http://localhost:5173` (`cd frontend && npm run dev`). El puerto importa: el backend
tiene CORS habilitado sólo para `localhost:5173`. La URL de la API se puede cambiar con
`VITE_API_URL`.

## Qué hice y qué dejé afuera

**Hice (y verifiqué contra la API corriendo):**

- Los tres must-have: grilla de presupuestos, alta con búsqueda de artículos, cantidad,
  descuento por línea y totales en vivo correctos, y facturar desde la grilla mostrando el error
  de negocio (sin stock, vencido, ya facturado) sin recargar.
- Vista de detalle del presupuesto y eliminación desde la grilla.
- Los bugs 9, 10 y 11 de la lista de arriba (numeración, listado que ocultaba los borradores y
  el encoding de los mensajes), porque sin ellos el cliente no se podía ver funcionando.
- `npm run lint` y `npm run build` en verde.
- Verificación end-to-end: 25 chequeos contra la API (totales con alícuotas mixtas, redondeo por
  línea, validaciones, stock, idempotencia, numeración tras borrar, 404) y CORS/preflight para el
  origen 5173.

**Dejé afuera, a propósito:**

- **Parte C** (duplicar presupuesto con precios refrescados, reporte top-N por rango de fechas).
  Es lo que el README marca como stretch y preferí asegurar los must-have.
- **Tests propios del cliente.** No hay vitest instalado y sumarlo implicaba un `npm install`
  nuevo; la lógica pura de `lib/totales.ts` se validó ejecutándola con Node y comparándola contra
  la API. Con más tiempo, ese módulo es el primer candidato a test unitario.
- **Router / deep links.** La navegación por estado alcanza para tres vistas, pero no se puede
  linkear un presupuesto puntual.
- Paginación, orden por columna, edición de un presupuesto ya creado y aviso temprano de stock
  insuficiente al armar el presupuesto (hoy el error aparece al facturar, que es cuando el
  backend lo valida).

**Cómo prioricé:** primero los must-have verificables contra la API real, después los bugs de
backend que los bloqueaban, y recién al final lo cosmético (estilos, detalle).

## Cómo usé IA

Usé un agente de IA para el análisis y para escribir el cliente, y verifiqué cada paso contra la
API corriendo en vez de confiar en el resultado.

- **Para qué la usé:** leer el README y el contrato real de la API (DTOs, códigos de estado,
  mensajes de error), proponer la estructura del cliente, escribir los componentes y el módulo de
  totales, armar los scripts de verificación end-to-end, y revisar el historial de commits del
  backend para documentar los bugs.
- **Qué acepté:** la estructura general (capa `api/`, lógica pura en `lib/totales.ts`, componentes
  por vista), el uso de `fetch` sin dependencias nuevas, y el hecho de que los totales en vivo
  repliquen exactamente el orden de cálculo del backend (subtotal sin redondear, IVA redondeado
  por línea).
- **Qué descarté o corregí:** el agente asumió que el bug de redondeo del IVA seguía presente y
  propuso arreglarlo; el historial mostró que ya estaba resuelto (`5697970`), así que saqué ese
  cambio. También propuso agregar un router y un framework de tests: los descarté por tiempo y
  porque no eran must-have.
- **Dónde se equivocó** (lo dejé registrado porque es parte del proceso):
  1. Escribió los scripts de verificación para PowerShell 7 y esta máquina tiene Windows
     PowerShell 5.1; además usó `Invoke-WebRequest` sin desactivar la barra de progreso, y el
     error de consola que eso produce lo interpretó como "no me pude conectar a la API". Eso
     generó una tanda de falsos negativos ("FALLA" en cosas que en realidad funcionaban) hasta
     que se aisló la causa real.
  2. El primer lint del cliente falló con `react-hooks/set-state-in-effect` porque reseteaba
     estado dentro del cuerpo del `useEffect`; hubo que mover esos reseteos al manejador del
     evento.
  3. Dio por hecho que podía compilar y correr todo dentro del confinamiento; las escrituras en
     subcarpetas del repo estaban bloqueadas y hubo que pedir permisos explícitos para el build.
- **Qué revisé yo:** que los números del cliente coincidan con los de la API (incluido el caso de
  redondeo 467,775 → 467,78 y el de "redondear por línea, no sobre la suma"), que el error de
  stock no deje el stock negativo, que la segunda facturación falle y que la numeración no se
  repita después de un DELETE.

## Qué haría con más tiempo / qué falta para producción

- **Tests**: unitarios de `lib/totales.ts` (vitest) con los casos de redondeo y alícuotas mixtas,
  de componentes con testing-library, y un test de integración que corra la API y valide los
  flujos completos.
- **Numeración a prueba de concurrencia**: `Max(Numero) + 1` arregla la colisión tras borrar pero
  no dos altas simultáneas. Lo correcto es una secuencia en la base o un índice único sobre
  `Numero` con reintento.
- **Aritmética de dinero**: reemplazar el `double` del cliente por centavos enteros (o una
  librería decimal) para eliminar el `1e-6` de `redondear2`.
- **Parte C**: duplicar presupuesto con precios refrescados y reporte top-N de artículos por monto
  facturado en un rango de fechas (endpoint + UI).
- **UX/producto**: router con deep links, paginación y orden en la grilla, edición de
  presupuestos, aviso de stock insuficiente mientras se arma el presupuesto, export a PDF,
  manejo de sesión/usuarios y auditoría de quién facturó qué.
- **Configuración**: mover la URL de la API y los orígenes de CORS a configuración por ambiente en
  lugar de tenerlos fijos en el código y en `Program.cs`.
