# MegaDatos

MegaDatos es una aplicación de escritorio multiplataforma desarrollada con **Avalonia UI** y **.NET 10**. Su objetivo principal es facilitar el procesamiento por lotes de imágenes, la conversión de formatos y la edición avanzada de metadatos (EXIF, IPTC, GPS).

## Arquitectura y Tecnologías
*   **Framework de UI:** [Avalonia UI](https://avaloniaui.net/) (Patrón MVVM).
*   **Gestión de Estado:** `CommunityToolkit.Mvvm` (Source Generators para propiedades observables y comandos).
*   **Procesamiento de Imágenes:** [Magick.NET](https://github.com/dlemstra/Magick.NET) (`Magick.NET-Q16-AnyCPU`).
    *   *Nota Histórica:* Inicialmente se consideró `SixLabors.ImageSharp`, pero fue reemplazado por Magick.NET debido a que ImageSharp no permitía preservar con precisión el método de compresión DCT (Progressive vs Baseline) de las imágenes JPEG originales durante la conversión/redimensión. Magick.NET ofrece control granular a través de `image.Interlace` y `MagickFormat.Pjpeg`.

## Características Principales

### 1. Explorador de Archivos (Sidebar Izquierdo)
*   **Árbol de Directorios:** Permite navegar por las carpetas del sistema.
*   **Listado de Archivos:** Muestra los archivos del directorio actual con detección inteligente de categorías (Imagen, Raw, Video, Audio, Documento).
*   **Selección por Lotes:** Checkboxes para seleccionar múltiples archivos. La interfaz bloquea inteligentemente la selección de formatos incompatibles (ej. mezclar imágenes con documentos) para el procesamiento por lotes.
*   **Columnas Dinámicas:** Se ajustan según la vista seleccionada (Por defecto, Imágenes, Coordenadas).

### 2. Inspector y Editor de Metadatos (Panel Derecho)
Extrae y muestra metadatos de los archivos seleccionados.
*   **EXIF Extendido:**
    *   Cámara (Marca y Modelo), Lente, Apertura (`FNumber`), Velocidad de obturación (`ExposureTime`), Distancia Focal (`FocalLength`), ISO, Flash, Modo de Exposición, Balance de Blancos y Fecha de Captura.
    *   *Detalle Técnico:* Los campos físicos (Apertura, Velocidad, Distancia Focal) se extraen como tipos `Rational` (numerador/denominador) y se calculan a decimales para su visualización.
*   **IPTC / XMP:**
    *   Título (`Title`), Autor (`Byline`), Copyright (`CopyrightNotice`), Descripción (`Caption`) y Palabras Clave (`Keyword` - soporta múltiples valores).
*   **GPS:**
    *   Extracción precisa de coordenadas de latitud y longitud.
    *   Soporte para cambiar la visualización entre formato Decimal y DMS (Grados, Minutos, Segundos) en tiempo real.
*   **Autoría:** Edición directa de campos de autor, copyright, software y calificación.

### 3. Herramientas de Procesamiento por Lotes y Metadatos Avanzados
*   **Plantillas de Metadatos (`Templates` - `📋`):**
    *   Verificación, diagnóstico y aplicación de perfiles estándar de aplicaciones y programas:
        *   **Adobe Photoshop:** Metadatos de software, espacio de color sRGB, resolución 72/300 DPI y sincronización de fechas.
        *   **WhatsApp:** Purga total de bloques EXIF/XMP/IPTC idéntica a la compresión y limpieza que aplica WhatsApp.
        *   **Timestamp Camera:** Firma de aplicación móvil, datos ópticos de smartphone y sincronización de fecha de captura a partir de nombres de archivo `TimePhoto_YYYYMMDD_HHMMSS`.
        *   **Captura de Pantalla:** Limpieza de datos ópticos de cámara y asignación de resolución de pantalla nativa.
        *   **⚪ (Ninguna):** Modo neutral para navegar sin validación de plantillas.
    *   **Vista Exclusiva:** Vista dedicada *"Verificación de Plantilla"* que muestra únicamente las columnas relevantes (Nombre, Estado Plantilla y Diagnóstico detallado).
*   **Datos de Teléfono / Celular (`Phone` - `📱`):**
    *   Incrustación de hardware de smartphones reales (Apple iPhone 15 Pro, Samsung Galaxy S24 Ultra, Xiaomi 14 Leica, Google Pixel 8 Pro, Motorola Edge 50 Pro).
    *   Inyecta Make, Model, Software de SO, apertura real (`FNumber`), distancia focal equivalente (`FocalLengthIn35mmFilm`), ISO, modo de medición y sincronización de fechas.
    *   Modos: *Rellenar solo datos faltantes* vs. *Reemplazar todos los datos de cámara*, con opción para conservar GPS original y generar copias `.bak`.
*   **Generador de Coordenadas GPS con Radio (`GpsGen` - `📍`):**
    *   Genera coordenadas aleatorias uniformemente distribuidas en un círculo alrededor de un punto medio.
    *   Radio configurable en metros (m) o kilómetros (km) con botones de acceso rápido (`100m`, `500m`, `1km`, `5km`).
    *   Botón *"Copiar de Foto"* para usar la ubicación de cualquier imagen seleccionada.
    *   Modos de asignación: *Dispersión aleatoria por cada foto* vs. *Misma coordenada para todo el lote*.
    *   Inyección estricta en formato EXIF racional (DMS, `GPSLatitudeRef`, `GPSLongitudeRef`, `GPSAltitude`, `GPSVersionID`).
    *   Tarjeta de simulación en vivo en formato Decimal y DMS.
*   **Limpieza de Metadatos (`Clean` - `🧹`):**
    *   Limpieza total (`image.Strip()`) o selectiva (GPS, EXIF de cámara, Autoría/XMP, IPTC).
*   **Redimensión (`Resize` - `📏`):**
    *   Ajuste de dimensiones en píxeles con opción de mantener relación de aspecto automática.
*   **Conversión de Formato (`Format` - `🔄`):**
    *   Conversión entre formatos (JPG, PNG, WEBP, BMP, TIFF) preservando el tipo de entrelazado (Progressive/Baseline).
*   **Casilla de Selección Masiva en Cabecera:**
    *   CheckBox maestro en el encabezado de la primera columna para seleccionar o deseleccionar todos los archivos en un solo clic, con sincronización tri-state en tiempo real.

---

## 📌 Lista de Tareas Pendientes / Roadmap de Metadatos Forenses y Profundos

Para garantizar una minuciosidad forense absoluta, libre de discrepancias en análisis avanzados de metadatos (ej. ExifTool, FotoForensics), se tienen programadas las siguientes mejoras de bajo nivel:

1. **Perfiles de Color ICC Reales (APP2):**
   * [ ] Inyección de perfiles de color binarios embebidos: **Display P3** (específico de iPhone/iPad/Mac) y **sRGB IEC61966-2.1** (estándar Android/Windows).
   * [ ] Corrección de coincidencia entre el tag `ColorSpace` (EXIF) y el perfil ICC real adjunto.

2. **Husos Horarios y Desfases Temporales (`OffsetTime`):**
   * [ ] Soporte para etiquetas EXIF 2.31+: `OffsetTime`, `OffsetTimeOriginal` y `OffsetTimeDigitized` (ej. `-05:00`, `+01:00`) para reflejar con exactitud la zona horaria del dispositivo de captura.

3. **Temporización de Alta Precisión (`SubSecTime`):**
   * [ ] Generación e inyección de `SubSecTime`, `SubSecTimeOriginal` y `SubSecTimeDigitized` (milisegundos/microsegundos del momento exacto del disparo, crucial para fotos tomadas en ráfaga).

4. **Tratamiento y Purga de MakerNotes Foráneos:**
   * [ ] Detección y eliminación automática de bloques binarios MakerNotes incompatibles al convertir o aplicar perfiles móviles (evitar que una foto marcada como iPhone conserve un MakerNote residual de Canon, Nikon o Sony).

5. **Sincronización y Purga de la Miniatura Oculta (IFD1 Thumbnail):**
   * [ ] Regenerar o eliminar la miniatura JPEG incrustada de baja resolución en el segundo IFD (`IFD1`) al redimensionar, limpiar o modificar una imagen, evitando que software forense detecte la versión previa sin editar.

6. **Árbol de Linaje e Historial XMP (`xmpMM`):**
   * [ ] Generación coherente de UUIDs para `xmpMM:DocumentID`, `xmpMM:InstanceID` y secuencias de eventos `xmpMM:History` en la plantilla de Photoshop para simular historiales de guardado legítimos.

7. **Metadatos de Disparo Derivados (Valores APEX):**
   * [ ] Cálculo matemático e inyección de `BrightnessValue`, `ShutterSpeedValue` y `ApertureValue` coherentes con los valores de `ExposureTime`, `FNumber` e `ISO`.

8. **Bloques de Recursos 8BIM de Photoshop (APP13):**
   * [ ] Soporte para incrustar bloques de recursos binarios de Photoshop (`0x03ed` para resolución física DPI, flags de impresión y registros IPTC-NAA extendidos).

---

## Convenciones Críticas de Desarrollo (¡NO ROMPER!)

Si eres un desarrollador continuando el trabajo en este proyecto, por favor ten en cuenta las siguientes reglas críticas que ya han sido implementadas y solucionadas:

1.  **Sincronización de UI en Avalonia (ObservableCollections):**
    *   Al recargar metadatos en `MainViewModel.cs` (método `LoadDemoMetadata`), **NO** reasignes las colecciones instanciando un nuevo objeto (ej. `ExifData = new ObservableCollection...`). Esto rompe los bindings del `ItemsControl` en Avalonia, causando que los TextBoxes no se actualicen al cambiar de imagen.
    *   **Solución implementada:** Usa `.Clear()` seguido de múltiples `.Add(...)` sobre la misma instancia de la colección.

2.  **Manejo de Datos EXIF con Magick.NET:**
    *   El método `GetValue(ExifTag.X)` puede devolver tipos anulables (ej. `IExifValue<Rational?>`).
    *   **SIEMPRE** usa `.HasValue` y `.Value` antes de intentar acceder a `.Numerator` o `.Denominator`. No asumas que los metadatos existen.

3.  **Preservación de JPEG Progresivo (Progressive DCT):**
    *   La propiedad `Interlace` es de solo lectura en `MagickImage`.
    *   Para forzar un guardado progresivo en un archivo JPG modificado, debes asignar explícitamente el formato a Pjpeg:
        ```csharp
        if (originalInterlace == Interlace.Jpeg) image.Format = MagickFormat.Pjpeg;
        ```

4.  **Fecha de Creación vs Fecha de Modificación:**
    *   Las imágenes usan `File.GetCreationTime()` para la fecha de creación en disco, y `DateTimeOriginal` del perfil EXIF para la fecha real de captura de la foto. No confunda estos campos.

## Requisitos de Compilación
*   **SDK:** .NET 10.0 (o superior soportado por el proyecto).
*   **IDE:** Visual Studio 2022 o IDE compatible con Avalonia (JetBrains Rider, VS Code con extensión Avalonia).
*   **Comando de compilación rápida:** `dotnet build`

## Estructura del Proyecto
*   `/Models`: Contiene modelos de datos como `FileItem`, `FolderNode`, `MetadataEntry`, perfiles de dispositivos (`/Models/Devices`) y plantillas de metadatos (`/Models/Templates`).
*   `/Services`: Servicios auxiliares como `AppStateService` (persistencia) y `GpsGeneratorService` (cálculo y dispersión aleatoria geográfica).
*   `/ViewModels`: Lógica de presentación y negocio. `MainViewModel.cs` es el controlador principal (Fat ViewModel) para todas las operaciones actuales.
*   `/Views`: Interfaces de usuario en XAML (`MainWindow.axaml`). Utiliza estilos dinámicos definidos en Avalonia.
