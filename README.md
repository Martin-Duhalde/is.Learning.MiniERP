# Challenge Técnico - Mini ERP (Backend + Cliente React)

Bienvenido/a al challenge. Heredás el **backend** de un módulo de un mini-ERP que
gestiona **presupuestos**, **facturación**, **artículos** y **stock**. El código fue
empezado por otra persona: **funciona a medias y tiene bugs**. Además, **todavía no
tiene un cliente**: parte del challenge es que lo construyas vos.

Tu trabajo es el de un dev que entra a un proyecto en marcha: arreglar lo que está mal,
y construir el cliente que falta consumiendo la API.

> **Tiempo sugerido: 3 horas.** Es por confianza. Hacé *commits frecuentes*: nos
> interesa tu proceso, no sólo el resultado final.

---

## Stack

- **Backend (incluido):** .NET 8 (Web API) + Entity Framework Core + SQLite. Tests con xUnit.
- **Cliente (lo construís vos):** React + TypeScript. Podés usar la herramienta que prefieras.

La base de datos es un archivo SQLite que **se crea y se seedea solo** al arrancar la API
(artículos y clientes de ejemplo). No tenés que instalar ningún motor.

---

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/) (para el cliente React)

## Cómo levantar el backend

```bash
cd backend/MiniErp.Api
dotnet run
```

- API: `http://localhost:5080`
- **Swagger (úsalo para explorar y probar la API): `http://localhost:5080/swagger`**

La API ya tiene **CORS habilitado para `http://localhost:5173`** (el puerto por defecto de
Vite), así que un cliente Vite pega contra ella sin configurar nada extra.

## Cómo correr los tests (backend)

```bash
cd backend
dotnet test
```

---

## ⚠️ Sobre los tests (importante)

- **No modifiques los tests.** Están para guiarte.
- Al entregar, **todos los tests tienen que pasar** (verde).
- Algunos tests **hoy fallan a propósito**: te están marcando un bug. Cuando arregles el
  bug, el test pasa. Esa es la idea.
- **Ojo:** no todos los bugs tienen un test que los delate. Algunos los vas a encontrar
  explorando la API (Swagger), leyendo el código, o cuando armes el cliente.

---

## La API que tenés que consumir

El backend ya expone todo lo necesario (probalo en Swagger):

| Método | Endpoint | Qué hace |
|---|---|---|
| GET | `/api/clientes` | Lista de clientes |
| GET | `/api/articulos?busqueda=teclado` | Busca artículos por código o descripción |
| GET | `/api/presupuestos` | Lista de presupuestos |
| GET | `/api/presupuestos/{id}` | Un presupuesto con sus ítems y totales |
| POST | `/api/presupuestos` | Crea un presupuesto |
| DELETE | `/api/presupuestos/{id}` | Elimina un presupuesto |
| GET | `/api/facturas` | Lista de facturas |
| POST | `/api/facturas/facturar/{presupuestoId}` | Factura un presupuesto |

Body de `POST /api/presupuestos`:

```json
{
  "clienteId": 1,
  "validezDias": 15,
  "items": [
    { "articuloId": 1, "cantidad": 2, "descuentoPct": 0 },
    { "articuloId": 4, "cantidad": 10, "descuentoPct": 5 }
  ]
}
```

---

## La consigna

### Parte A - Encontrá y arreglá los bugs (backend)

El código del backend tiene **varios bugs plantados** (entre 3 y 6). Algunos son de
corrección de negocio, otros de datos. Encontralos, arreglalos y **documentá cada uno**
en el `NOTES.md` (qué pasaba, por qué, cómo lo resolviste).

### Parte B - Construí el cliente React (must-have)

Armá un cliente en **React + TypeScript** que consuma la API, con como mínimo:

1. **Lista de presupuestos** (grilla).
2. **Alta de presupuesto:** elegir cliente, **buscar y agregar artículos**, setear
   **cantidad** y **descuento por línea**, y mostrar los **totales en vivo** (subtotal,
   IVA, total) — que tienen que ser **correctos** según las reglas de negocio.
3. **Facturar** un presupuesto desde la grilla, mostrando el error si no se puede
   (sin stock, vencido, o ya facturado).

> Poné el cliente en una carpeta del repo (por ejemplo `frontend/`).

### Parte C - Stretch (si te sobra tiempo)

4. **Duplicar presupuesto:** clonar uno con número nuevo, fecha de hoy y **precios
   refrescados** desde el precio actual de cada artículo (endpoint + UI).
5. **Reporte:** top N artículos por monto facturado en un rango de fechas (endpoint).
6. **Tests propios** (backend y/o cliente) y/o validaciones y manejo de errores en el front.

> No esperamos que termines todo. **Priorizá** y dejá claro en el `NOTES.md` qué hiciste,
> qué dejaste afuera y por qué.

---

## Reglas de negocio

1. **Subtotal por línea:** `cantidad × precioUnitario × (1 − descuento%)`. El descuento se
   aplica **antes** del IVA.
2. **IVA:** cada artículo tiene su **propia alícuota** (21% o 10,5%). El IVA se calcula
   **por línea según su alícuota**, redondeando a 2 decimales por línea, y recién después
   se suma. (Ojo con esto, tanto en el backend como en el total en vivo del cliente.)
3. **Stock:** al facturar se descuenta el stock de cada artículo. El stock **no puede
   quedar negativo**.
4. **Idempotencia:** un presupuesto se puede facturar **una sola vez**.
5. **Validez:** un presupuesto vencido (pasada su `validezDias` desde la fecha) **no se
   puede facturar**.
6. **Numeración:** los números de presupuesto deben ser **secuenciales y sin colisiones**.
7. **Datos válidos:** un presupuesto no debería aceptar cantidades inválidas (cero o
   negativas) ni descuentos fuera de rango.

> Nota: el **backend es la fuente de verdad** de los totales que se guardan. El total en
> vivo del editor del cliente es una ayuda visual y debería coincidir con lo que calcula
> el backend.

---

## Flujos que deben funcionar

Para cada uno te decimos **qué deberías obtener si todo está bien**. Compará con lo que pasa en realidad y sacá tus conclusiones.

1. **Crear y listar.** Hacé `POST /api/presupuestos` y después `GET /api/presupuestos`.
   *Deberías ver el presupuesto que acabás de crear en la lista.*

2. **IVA con alícuotas mezcladas.** Creá un presupuesto con una unidad de `ART-001` (21%) y
   una de `ART-004` (10,5%), sin descuento, y hacé `GET /api/presupuestos/{id}`.
   *Con los precios del seed, deberías obtener: subtotal 39.500, IVA 7.822,50 y total
   47.322,50.*

3. **Numeración tras borrar.** Creá dos presupuestos, `DELETE` el último y creá un tercero.
   *El tercero debería recibir un número nuevo, sin repetir ninguno existente.*

4. **Datos inválidos.** Hacé `POST /api/presupuestos` con `cantidad: 0` (o negativa).
   *La API debería rechazarlo con un error de validación.*

5. **Facturación.**
   - Facturá un presupuesto de `ART-006` (stock 3) con cantidad mayor al stock
     (`POST /api/facturas/facturar/{id}`). *Debería rechazarlo, y el stock no debería quedar
     negativo.*
   - Facturá un presupuesto válido y volvé a facturar el mismo. *La segunda vez debería
     fallar, y el stock debería haberse descontado una sola vez.*

---

## Qué entregar

1. El repositorio con tus cambios (backend arreglado + cliente React), como PR a la rama
   que te indiquemos o un `.zip` que incluya la carpeta `.git` para ver el historial.
2. Un archivo **`NOTES.md`** con:
   - **Bugs encontrados:** qué pasaba, por qué y cómo lo arreglaste.
   - **Decisiones del cliente React** (estructura, librerías, cómo calculás los totales en vivo).
   - **Qué hiciste y qué dejaste afuera**, y cómo priorizaste.
   - **Cómo usaste IA:** para qué partes, qué le pediste, qué aceptaste y qué descartaste, y
     dónde se equivocó. (Usar IA está **permitido y lo valoramos**. Lo que evaluamos es tu
     *criterio* al usarla.)
   - **Qué harías con más tiempo** / qué le falta para producción.

¡Éxitos!
