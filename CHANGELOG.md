# Changelog

## v1.5.0 — HEOS Edition (2026-08-23)

### Nuevas funciones

**HEOS completo**
- Navegacion de servicios: Tidal, TuneIn, Local Music, Playlists, History, Favorites
- Busqueda dentro de servicios por Artista, Album, Track y Playlist
- Reproduccion directa desde los resultados de busqueda
- Cola de reproduccion con carátulas — click para saltar, boton limpiar
- Controles de transporte: play/pausa, siguiente, anterior
- **Seek (adelantar/retroceder)** — descubierto que funciona via UPnP AVTransport (no via CLI)
- Barra de progreso interactiva con tiempo actual y duracion
- Login de cuenta HEOS (necesario para Tidal, TuneIn, Deezer)
- Servicios como tarjetas cuadradas con logos
- Fondo blur de la caratula del album (efecto glass)
- Click en artista o album busca automaticamente en el servicio
- Panel de cola colapsable con splitters movibles
- Deteccion de loop infinito en carpetas "My Music" de Tidal (limitacion del firmware)

**Sonido**
- Control de nivel por canal (FL, FR, C, SL, SR) con slider individual
- Perfiles de volumen: guardar, cargar, renombrar, eliminar
- Boton Normalizar (todos los canales a 0 dB)
- Zonas integradas en la pagina de Sonido (ya no hay tab separado)
- Selector de unidad de volumen: absoluto, decibelios o porcentaje

**General**
- Indicador visual de mute (icono cambia + texto rojo "MUTE")
- Localizacion español/ingles automatica segun el idioma de Windows
- Cards clicables en el Dashboard: Source abre selector, Surround abre modos
- Texto completo en las metricas (sin truncar)
- Splitters movibles en HEOS para adaptar el layout
- Widget flotante desde el tray
- Instancia unica (mutex) — no se pueden abrir dos copias
- Auto-update desde haussmed.cl/denon/

### Fixes

- **Zone 2**: ya no se enciende sola. Power On usa `ZMON` (solo Main)
- **Volumen slider**: debounce de 300ms, ya no se dispara
- **HEOS conexion**: lector asíncrono que maneja respuestas "command under process"
- **Canales**: supresion de eco al arrastrar sliders (no rebotan)
- **Dark mode**: brushes de texto con contraste alto (WCAG 4.5:1+)
- **Servicios**: tarjetas cuadradas siempre visibles

### Notas tecnicas sobre el fondo blur

El efecto de caratula difuminada de fondo usa un `Rectangle` con `ImageBrush` +
`BlurEffect` en WPF. Las limitaciones encontradas:

1. WPF no tiene `backdrop-filter` como CSS — el blur se aplica a la imagen,
   no al contenido que esta encima
2. Las cards usan un fondo semi-transparente (`#88141820`) para simular el
   efecto glass. El resultado es sutil pero funcional.
3. El receptor no soporta seek via HEOS CLI (`Command not recognized`) pero
   si lo acepta via UPnP AVTransport SOAP en el puerto 60006. La app oficial
   usa este mismo protocolo para la barra de tiempo.
4. Las carpetas "My Music" y "What's New" de Tidal devuelven la raiz en loop
   a traves del CLI — es un bug del firmware HEOS en modelos S-series. La
   busqueda funciona como workaround.

### Archivos para el servidor de actualizacion

Sube el contenido de `updates/denon/` a `https://haussmed.cl/denon/`:
- `update.json` — manifiesto de version
- `DenonDesktopControl-1.5.0.zip` — ejecutable standalone (~65 MB)
