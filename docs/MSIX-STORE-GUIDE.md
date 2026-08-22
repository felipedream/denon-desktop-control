# Publicar DenonRemote en Microsoft Store

## Requisitos previos

1. **Cuenta de desarrollador de Microsoft** (~$19 USD una vez)
   - Regístrate en: https://developer.microsoft.com/en-us/microsoft-store/register/
   - Usa tu email: felipedream@gmail.com

2. **Windows App SDK** o **empaquetado MSIX** del proyecto
   - Ya tenemos WPF + .NET 8, que es compatible con MSIX

3. **Certificado de firma** (Microsoft lo provee al publicar por Partner Center)

## Pasos para publicar

### 1. Crear la identidad de la app en Partner Center

1. Ve a https://partner.microsoft.com/dashboard
2. Click "Apps and games" → "New product" → "MSIX or PWA app"
3. Reserva el nombre: **DenonRemote** (o "Denon Remote Control")
4. Anota el **Package Identity Name**, **Publisher** y **Publisher Display Name**

### 2. Generar el paquete MSIX

Desde la raíz del proyecto, ejecuta:

```powershell
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=false --self-contained
```

Luego empaqueta con la herramienta de Windows:

```powershell
# Instalar MSIX Packaging Tool (gratis en Microsoft Store)
# O usar makeappx.exe del Windows SDK:

& "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe" pack `
    /d "src\DenonRemote\publish" `
    /p "DenonRemote_1.0.0.0_x64.msix"
```

### 3. Firmar el paquete (para testing local)

Para pruebas locales sin Store:
```powershell
# Crear certificado autofirmado (solo desarrollo)
New-SelfSignedCertificate -Type Custom -Subject "CN=FelipeDream" `
    -KeyUsage DigitalSignature -FriendlyName "DenonRemote Dev" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")

# Exportar y firmar
& signtool.exe sign /fd SHA256 /a /f cert.pfx /p password DenonRemote_1.0.0.0_x64.msix
```

### 4. Subir a Microsoft Store

1. En Partner Center → tu app → "Packages"
2. Sube el `.msix` o `.msixupload`
3. Completa la información:
   - **Categoría**: Utilities & Tools
   - **Descripción**: "Control remoto para receptores Denon y Marantz. 
     Descubrimiento automático, control de volumen, fuentes, zonas, 
     ecualizador y más."
   - **Capturas de pantalla**: al menos 1 de 1366x768 o mayor
   - **Icono**: 300x300 PNG (usa el rojo Denon que ya tenemos)
   - **Precio**: Gratis (con donaciones opcionales vía PayPal)
   - **Idiomas**: Español, English
   - **Privacidad**: "Esta app no recopila datos personales"
   - **Requisitos**: Windows 10 v1903+, x64

4. Submit for certification (tarda 1-3 días hábiles)

### 5. Actualizaciones automáticas vía Store

Una vez publicada en Store, las actualizaciones son automáticas:
- Incrementa `<Version>` en el .csproj
- Genera nuevo MSIX
- Sube a Partner Center → nueva submission

Microsoft Store maneja la distribución y auto-update.

## Modelo de negocio sugerido

- **Versión gratuita** (la actual): todas las funciones
- **Versión Pro** (futura): HEOS control, ecualizador avanzado, múltiples dispositivos
- Usar la API de Microsoft Store para in-app purchases, o simplemente
  mantener PayPal donations como modelo principal

## Alternativa: distribución directa (sin Store)

Tu sistema actual con `haussmed.cl/denon/update.json` funciona perfectamente
para distribución directa. Puedes mantener ambos canales:
- Microsoft Store para alcance y credibilidad
- Descarga directa desde tu web para usuarios que prefieren no usar Store

## Archivos necesarios para MSIX completo

Para un MSIX apropiado necesitas un `Package.appxmanifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
         xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
         xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">
  <Identity Name="FelipeDream.DenonRemote"
            Publisher="CN=FelipeDream"
            Version="1.0.0.0"
            ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>Denon Remote</DisplayName>
    <PublisherDisplayName>Felipe</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>
  <Resources>
    <Resource Language="es-cl" />
    <Resource Language="en-us" />
  </Resources>
  <Applications>
    <Application Id="App" Executable="DenonRemote.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="Denon Remote"
                          Description="Remote control for Denon/Marantz AVR"
                          BackgroundColor="#E63946"
                          Square150x150Logo="Assets\Square150x150Logo.png"
                          Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" />
      </uap:VisualElements>
    </Application>
  </Applications>
  <Capabilities>
    <Capability Name="internetClient" />
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
```

Genera los logos PNG en los tamaños requeridos (44x44, 150x150, 310x150, 
300x300 para Store) usando el mismo estilo del icono rojo actual.
