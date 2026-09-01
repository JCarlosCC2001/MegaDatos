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

### 3. Herramientas de Procesamiento por Lotes
*   **Redimensión (`Resize`):**
    *   Permite especificar Ancho (`TargetWidth`) y Alto (`TargetHeight`).
    *   **Mantener Relación de Aspecto:** Al activarse, recalcula automáticamente la dimensión opuesta mientras el usuario ajusta un valor (`OnKeepAspectRatioChanged`).
*   **Conversión de Formato (`Format`):**
    *   Convierte entre formatos populares (JPG, PNG, WEBP, BMP, TIFF) manteniendo los metadatos y el tipo de entrelazado original (Progressive/Baseline).
*   **Diálogo de Confirmación (Crear Copia vs. Reemplazar):**
    *   Antes de ejecutar cualquier lote, la UI muestra un diálogo (`IsConfirmDialogOpen`) que da a elegir entre generar archivos nuevos (con sufijos como `_convertido` o `_WxH`) o sobrescribir irreversiblemente los originales.

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
*   `/Models`: Contiene modelos de datos como `FileItem`, `FolderNode` y `MetadataEntry`. El modelo `MetadataEntry` contiene la lógica compleja de conversión bidireccional entre coordenadas GPS Decimales y DMS.
*   `/ViewModels`: Lógica de presentación y negocio. `MainViewModel.cs` es el controlador principal (Fat ViewModel) para todas las operaciones actuales.
*   `/Views`: Interfaces de usuario en XAML (`MainWindow.axaml`). Utiliza estilos dinámicos definidos en Avalonia.
