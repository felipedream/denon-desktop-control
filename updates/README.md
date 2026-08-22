# Carpeta de actualizaciones — DenonRemote

## Qué subir a tu servidor

Sube el contenido de la carpeta `denon/` a:

```
https://haussmed.cl/denon/
```

Tu servidor debe quedar así:

```
https://haussmed.cl/denon/update.json
https://haussmed.cl/denon/DenonRemote-1.0.0.zip
```

## Estructura de archivos

```
denon/
├── update.json              ← Manifiesto de versión (JSON)
└── DenonRemote-1.0.0.zip   ← Aplicación empaquetada (~65 MB)
```

## Cómo funciona

1. La app al iniciar consulta `https://haussmed.cl/denon/update.json`
2. Si la versión remota es mayor que la local, muestra un banner en Configuración
3. El usuario pulsa "Descargar" y se abre el ZIP en el navegador

## Para publicar una nueva versión

1. Incrementa la versión en `DenonRemote.csproj` (`<Version>1.0.1</Version>`)
2. Ejecuta: `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`
3. Comprime la carpeta `publish/` en `DenonRemote-1.0.1.zip`
4. Actualiza `update.json`:
   ```json
   {
     "version": "1.0.1",
     "url": "https://haussmed.cl/denon/DenonRemote-1.0.1.zip",
     "notes": "Correcciones y mejoras de rendimiento."
   }
   ```
5. Sube ambos archivos a `https://haussmed.cl/denon/`

## Requisitos del servidor

- Servir archivos estáticos (cualquier hosting funciona)
- HTTPS habilitado
- Content-Type correcto para .json y .zip (normalmente automático)
