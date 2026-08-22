# Contribuir a DENON Desktop Control

¡Gracias por tu interés! Este es un proyecto personal pero acepto contribuciones.

## Cómo contribuir

1. Fork el repositorio
2. Crea una rama: `git checkout -b feature/mi-mejora`
3. Haz tus cambios y commitea: `git commit -m "Agrega mi mejora"`
4. Push a tu fork: `git push origin feature/mi-mejora`
5. Abre un Pull Request

## Reportar bugs

Abre un [Issue](https://github.com/felipedream/denon-desktop-control/issues) con:
- Tu modelo de receptor
- Versión de Windows
- Pasos para reproducir el problema
- Captura de pantalla si aplica

## Desarrollo local

```bash
# Clonar
git clone https://github.com/felipedream/denon-desktop-control.git
cd denon-desktop-control/src/DenonRemote

# Compilar y ejecutar
dotnet run

# Publicar release
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Estructura del proyecto

```
src/DenonRemote/
├── Denon/          # Protocolo Denon: telnet, HTTP, parser de eventos
├── Discovery/      # SSDP + escaneo de subred
├── Services/       # Orquestador, settings, auto-update, localización
├── ViewModels/     # MVVM con CommunityToolkit
├── Views/          # XAML (Fluent/Mica con WPF-UI)
│   └── Pages/      # Páginas de NavigationView
├── Controls/       # Controles custom (ChannelChip)
├── Converters/     # Value converters para bindings
└── Assets/         # Iconos
```

## Contacto

- Telegram: [@felipedream](https://t.me/felipedream)
- Email: felipedream@gmail.com
