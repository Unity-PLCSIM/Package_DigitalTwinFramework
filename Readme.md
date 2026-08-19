# Digital Twin Framework

> Paquete de Unity para la conexión y simulación con PLC SIM. Proporciona una capa de integración entre Unity y la API de PLC SIM, junto con herramientas HMI y componentes prefabricados listos para usar en entornos de gemelo digital.

---

## Contenido

- [Requisitos](#requisitos)
- [Instalación](#instalación)
- [Cómo funciona](#cómo-funciona)
- [Módulos](#módulos)
  - [HMI](#hmi)
  - [Variador](#variador)
- [Notas de uso](#notas-de-uso)

---

## Requisitos

- **Unity** `6000.3.19f1` o superior

---

## Instalación

### Paso 1 — Importar NativeWebSocket

Abre el Package Manager (`Window > Package Manager`), pulsa **+** y selecciona *Add package from git URL*. Introduce:

```
https://github.com/endel/NativeWebSocket.git#upm
```

### Paso 2 — Importar Digital Twin Framework

Repite el proceso con la URL de este paquete:

```
https://github.com/Unity-PLCSIM/Package_DigitalTwinFramework.git
```

### Paso 3 — Configurar el Player

En `Edit > Project Settings > Player`, aplica los siguientes ajustes:

| Ajuste | Valor |
|--------|-------|
| Allow HTTP | `Always Allowed` |
| Input System | `Both` (Input System + Input Manager) |

### Paso 4 — Añadir Event System

En la **Hierarchy**, añade `UI > Event System`.

---

## Cómo funciona

!!!! añadir explicacion (conexión con instancia PLC SIM, funcionamiento in game, tablas de tags, flujo de comunicación con la API...)

---

## Módulos

### HMI

Pantalla de interfaz hombre-máquina integrada en la escena de Unity.

#### Configuración

**1. Añadir HMI Root**

Añade el prefab **HMI Root** en la Hierarchy.

**2. Prefab de botones**

Añade el prefab de botones como hijo de `Panel HMI` (hijo de `HMI Root`).

- Ajusta la posición en pantalla mediante las coordenadas del `Transform`.
- En el Inspector, asigna el **tag** del PLC asociado a cada botón.

> ⚠️ **Importante:** los botones únicamente soportan toggle de variables booleanas.

!!!! añadir explicacion (tipos de botones disponibles, parámetros del Inspector, ejemplos de uso...)

**3. Prefab de objetos**

!!!! añadir explicacion

#### Uso en editor

> Para continuar modificando la escena, **desactiva `HMI Root`** en la Hierarchy. Asegúrate de **reactivarlo** antes de ejecutar o hacer build de la simulación.

#### Uso en ejecución

| Tecla | Acción |
|-------|--------|
| `H` | Abrir / cerrar la pantalla HMI |

---

### Variador

!!!! añadir explicacion

---

## Notas de uso

- Este paquete ha sido validado con Unity `6000.3.19f1`. No se garantiza compatibilidad con versiones anteriores.
- La conexión con PLC SIM se realiza a través de la API incluida en el paquete.
- Las tablas de tags son modificables directamente desde el editor de Unity.
