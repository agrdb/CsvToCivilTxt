# CsvToCivilTxt

**Convierte archivos CSV de puntos —de cualquier origen— a TXT listos para importar en Autodesk Civil 3D.**

App de escritorio para Windows (C# · WinForms · .NET Framework 4.8). Es un `.exe` independiente: no necesita AutoCAD ni Civil 3D instalados para convertir, y se distribuye copiando un solo archivo.

![Plataforma](https://img.shields.io/badge/plataforma-Windows-0078D6)
![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4)
![Licencia](https://img.shields.io/badge/licencia-MIT-green)

---

## El problema

Los levantamientos llegan en CSV de programas muy distintos (Excel, estación total, GNSS, QGIS, software de colectora...). Cada uno usa su propio orden de columnas, separador (`;` o `,`), coma o punto decimal, y a veces coordenadas geográficas en vez de planas. Civil 3D solo importa los formatos fijos de **Point File Formats** (PENZD, PNEZD, ENZ...), así que cada archivo había que acomodarlo a mano en Excel.

## Qué hace

La app tiene **dos métodos**, cada uno en su pestaña:

### 1 · CSV → TXT (coordenadas planas)
- Carga varios CSV a la vez (botón o arrastrar y soltar).
- Detecta automáticamente el **separador** y si hay **encabezado**.
- Reconoce las columnas por su nombre (`Norte`, `Northing`, `Y`, `Este (m)`, `Cota`, `Point ID`, `Code`...) y permite corregir el mapeo.
- Acepta `1234,56`, `1.234,56` y `1234.56`; siempre escribe punto decimal.
- En lote, **cada archivo se mapea por separado**, así que se pueden mezclar CSV de distintos programas.

### 2 · Geográficas (lat/long) → planas → TXT
- Lee ángulos en decimal (`4.6097`, `-74,0817`) o en GMS (`4°36'35"N`, `74 04 54.1 W`, `O` = Oeste).
- Proyecta a:
  - **MAGNA-SIRGAS / Origen Nacional – CTM12 (EPSG:9377)**
  - MAGNA-SIRGAS Colombia Bogotá, Oeste, Oeste Oeste, Este Central y Este Este (EPSG:3114–3118)
  - WGS 84 / UTM 17N, 18N y 19N
- Proyección Transverse Mercator con **series de Krüger hasta n⁶**, implementada sin librerías externas y **verificada contra PROJ** (la librería de QGIS/ArcGIS) en los 9 sistemas: diferencia < 0.1 mm desde San Andrés hasta Leticia.
- Opción para pasar de altura elipsoidal a cota restando la ondulación geoidal *N* del sitio.

### Salida
- Formatos PENZD, PNEZD, PENZ, PNEZ, ENZ y NEZ, *comma* o *space delimited* — los mismos nombres que en Civil 3D.
- Vista previa de cada línea del TXT antes de exportar y registro de filas omitidas con el motivo.
- Numeración automática si el CSV no trae número de punto; descripciones con espacios protegidas (`POZO 1` → `POZO_1`) en formato *space delimited*.

## Descargar

Ve a [**Releases**](../../releases) y descarga `CsvToCivilTxt.exe`. Windows 10/11 ya trae .NET Framework 4.8, así que no hay que instalar nada.

> La primera vez Windows SmartScreen puede advertir que la app no está firmada: *Más información → Ejecutar de todas formas*.

## Uso en Civil 3D

1. Convierte el CSV en la app y fíjate en el formato que indica la barra inferior (p. ej. `PENZD (comma delimited)`).
2. En Civil 3D: **Insert → Points from File** (o *Points → Point Creation Tools → Import Points*).
3. Elige el TXT y **el mismo formato**.
4. Si usaste la pestaña de geográficas, asigna al dibujo el mismo sistema de coordenadas (*Drawing Settings → Units and Zone*).

## Compilar desde el código

```bash
git clone https://github.com/agrdb/CsvToCivilTxt.git
cd CsvToCivilTxt
dotnet build src/CsvToCivilTxt/CsvToCivilTxt.csproj -c Release
```

O abre `src/CsvToCivilTxt/CsvToCivilTxt.csproj` en Visual Studio 2022. El ejecutable queda en `src/CsvToCivilTxt/bin/Release/net48/`.

Cada push se compila automáticamente en GitHub Actions; al crear un tag `v*` se publica el `.exe` en Releases.

## Estructura

```
src/CsvToCivilTxt/
├── Program.cs          Punto de entrada
├── MainForm.cs         Ventana principal y pestaña 1 (coordenadas planas)
├── GeoPanel.cs         Pestaña 2 (geográficas → planas)
├── CsvPointReader.cs   Lectura de CSV, detección de separador/columnas, formateo y escritura del TXT
└── Projection.cs       Sistemas MAGNA-SIRGAS/UTM y Transverse Mercator (Krüger)
ejemplos/               CSV de prueba para cada pestaña
```

## Autor

**AGRDB — AGR Digital Building** · BIM · GIS · automatización para infraestructura
[agrdb.github.io](https://agrdb.github.io)

## Licencia

[MIT](LICENSE)
