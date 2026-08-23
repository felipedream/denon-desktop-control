# DENON Desktop Control v1.5.0 — HEOS Edition

Control remoto completo para receptores Denon y Marantz desde Windows, ahora con integracion HEOS completa.

---

## Que hay de nuevo

### HEOS — Navegacion y reproduccion

- Navega TuneIn (radios locales), Tidal, Local Music, Playlists, Favorites
- Busqueda por Artista, Album, Track y Playlist dentro de cada servicio
- Reproduccion directa desde los resultados
- Cola de reproduccion con caratulas — click para saltar a cualquier pista
- Controles de transporte: play/pausa, siguiente, anterior
- **Seek funcional** (adelantar/retroceder arrastrando la barra de tiempo)
- Login de cuenta HEOS para habilitar Tidal, TuneIn, Deezer
- Servicios mostrados como tarjetas cuadradas con logos
- Fondo blur sutil con la caratula del album actual
- Click en el nombre del artista o album busca automaticamente
- Panel de cola colapsable con layout adaptable (splitters)

### Sonido y canales

- Control de nivel individual por canal (FL, FR, C, SL, SR)
- Perfiles de volumen: guardar, cargar, renombrar y eliminar
- Boton Normalizar (todos los canales a 0 dB en un click)
- Zonas integradas en la pagina de Sonido
- Selector de unidad: valor absoluto, dB o porcentaje

### Mejoras generales

- Indicador visual de mute (icono cambia + texto rojo)
- Cards clicables en Inicio: Source y Surround abren selectores rapidos
- Texto completo en las metricas (sin truncar con "...")
- Modos surround: Stereo, Direct, Pure Direct, Multi Ch, Neural:X, y mas
- Widget flotante desde la bandeja del sistema
- Instancia unica — no se pueden abrir dos copias
- Localizacion espanol/ingles segun el idioma de Windows

### Correcciones

- Zone 2 ya no se enciende automaticamente al prender Main
- Slider de volumen con debounce — no se dispara al arrastrar
- Canales no rebotan al mover su trim
- Dark mode con contraste alto en todos los textos

---

## Notas tecnicas

- El seek usa **UPnP AVTransport** (SOAP, puerto 60006), no el CLI de HEOS. Es el mismo protocolo que usa la app oficial de Denon para la barra de tiempo.
- Las carpetas "My Music" y "What's New" de Tidal no funcionan via CLI (bug del firmware HEOS en modelos S-series). La busqueda por nombre de playlist funciona como alternativa.
- El fondo blur de la caratula usa WPF `BlurEffect` sobre `ImageBrush` con cards semi-transparentes para simular el efecto glass.

---

## Descargas

| Archivo | Descripcion |
|---------|-------------|
| `DenonDesktopControl-1.5.0.zip` | Portable — extrae y ejecuta, no requiere .NET instalado |
| `DenonDesktopControl-1.5.0-Setup.exe` | Instalador con acceso directo en escritorio y opcion de iniciar con Windows |

**Requisitos:** Windows 10/11 x64

---

## Compatibilidad

- Cualquier receptor Denon o Marantz con puerto de red (AVR-X, AVR-S, Cinema, NR, SR series)
- Probado con: Denon AVR-S970H
- Protocolo: Telnet (puerto 23) + HTTP goform (puerto 8080) + HEOS CLI (puerto 1255) + UPnP AVTransport (puerto 60006)

---

## Autor

**Felipe** — Buin, Santiago de Chile

- Telegram: [@felipedream](https://t.me/felipedream)
- Email: felipedream@gmail.com
- Donar: [PayPal](https://www.paypal.com/donate/?business=felipedream@gmail.com&currency_code=USD)

---

## Codigo fuente

MIT License — [github.com/felipedream/denon-desktop-control](https://github.com/felipedream/denon-desktop-control)
