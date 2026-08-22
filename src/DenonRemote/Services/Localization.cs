// DENON Desktop Control
// Copyright (c) 2026 Felipe (@felipedream) - Buin, Santiago de Chile
// Licensed under MIT License
// https://github.com/felipedream/denon-desktop-control

using System.Globalization;

namespace DenonRemote.Services;

/// <summary>
/// Lightweight i18n: ES / EN. Detects from Windows UI language at startup.
/// </summary>
public static class L
{
    public static bool IsSpanish { get; }

    static L()
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        IsSpanish = lang == "es";
    }

    // -- About --
    public static string AboutTitle => IsSpanish ? "Acerca de" : "About";
    public static string AboutCreatedBy => IsSpanish ? "CREADO POR" : "CREATED BY";
    public static string AboutLocation => "Buin, Santiago de Chile";
    public static string AboutDonate => IsSpanish ? "Donar con PayPal" : "Donate with PayPal";
    public static string AboutTelegram => IsSpanish ? "Telegram @felipedream" : "Telegram @felipedream";
    public static string AboutFreeVersion => IsSpanish
        ? "Version gratuita - Si te gusta, dona un cafe"
        : "Free version - If you like it, buy me a coffee";

    // -- Settings --
    public static string SettingsTitle => IsSpanish ? "Configuracion" : "Settings";
    public static string SettingsAutoConnect => IsSpanish ? "Conectar automaticamente" : "Connect automatically";
    public static string SettingsAutoConnectDesc => IsSpanish
        ? "Reconecta al ultimo dispositivo al iniciar."
        : "Reconnects to the last known device at startup.";
    public static string SettingsCloseToTray => IsSpanish ? "Cerrar a la bandeja" : "Close to tray";
    public static string SettingsCloseToTrayDesc => IsSpanish
        ? "Cerrar la ventana la minimiza a la bandeja del sistema."
        : "Closing the window minimizes to the system tray instead of quitting.";
    public static string SettingsStartMinimized => IsSpanish ? "Iniciar minimizado" : "Start minimized";
    public static string SettingsStartMinimizedDesc => IsSpanish
        ? "Lanza la app a la bandeja para que no robe el foco."
        : "Launches the app to the tray so it doesn't steal focus.";
    public static string SettingsAutoUpdate => IsSpanish ? "Actualizaciones automaticas" : "Auto-update";
    public static string SettingsAutoUpdateDesc => IsSpanish
        ? "Busca nuevas versiones al iniciar desde haussmed.cl."
        : "Checks for new versions at startup from haussmed.cl.";

    // -- Navigation --
    public static string Devices => IsSpanish ? "Dispositivos" : "Devices";
    public static string Sources => IsSpanish ? "Fuentes" : "Sources";
    public static string Sound => IsSpanish ? "Sonido" : "Sound";
    public static string Zones => IsSpanish ? "Zonas" : "Zones";
    public static string Dashboard => IsSpanish ? "Inicio" : "Home";
    public static string Settings => IsSpanish ? "Configuracion" : "Settings";
    public static string Recent => IsSpanish ? "RECIENTES" : "RECENT";

    // -- Connect page --
    public static string AddReceiver => IsSpanish ? "Agregar receptor" : "Add a receiver";
    public static string AddReceiverDesc => IsSpanish
        ? "Cualquier Denon o Marantz en la misma red."
        : "Any Denon or Marantz on the same network.";
    public static string DiscoveredDevices => IsSpanish ? "Dispositivos encontrados" : "Discovered devices";
    public static string Rescan => IsSpanish ? "Buscar" : "Rescan";
    public static string Connect => IsSpanish ? "Conectar" : "Connect";
    public static string AddByIp => IsSpanish ? "Agregar por IP" : "Add by IP";
    public static string AddByIpDesc => IsSpanish
        ? "Escribe la IP del receptor si no aparece arriba."
        : "Type the receiver's IP if it didn't appear above.";
    public static string AddAndConnect => IsSpanish ? "Agregar y conectar" : "Add and connect";
    public static string DoesntShowUp => IsSpanish ? "No aparece?" : "Doesn't show up?";
    public static string DoesntShowUpDesc => IsSpanish
        ? "Asegurate de que Network Standby esta habilitado en el receptor y que tu PC y el AVR estan en la misma subred."
        : "Make sure Network Standby is enabled on the receiver, and that your PC and the AVR are on the same subnet. UPnP / SSDP is used for discovery.";

    // -- Dashboard --
    public static string Volume => IsSpanish ? "VOLUMEN" : "VOLUME";
    public static string Source => IsSpanish ? "FUENTE" : "SOURCE";
    public static string Surround => IsSpanish ? "SURROUND" : "SURROUND";
    public static string Format => IsSpanish ? "FORMATO" : "FORMAT";
    public static string MasterVolume => IsSpanish ? "VOLUMEN PRINCIPAL" : "MASTER VOLUME";
    public static string InputSignal => IsSpanish ? "SENAL DE ENTRADA" : "INPUT SIGNAL";
    public static string ActiveSpeakers => IsSpanish ? "ALTAVOCES ACTIVOS" : "ACTIVE SPEAKERS";
    public static string ClickSpeakerTrim => IsSpanish
        ? "Click en un altavoz para ajustar su nivel"
        : "Click a speaker to adjust its trim";

    // -- Sources page --
    public static string SourcesTitle => IsSpanish ? "Fuentes" : "Sources";
    public static string SourcesDesc => IsSpanish
        ? "Selecciona una fuente para la zona principal."
        : "Tap a tile to switch the main zone to that input.";
    public static string SurroundMode => IsSpanish ? "Modo Surround" : "Surround Mode";
    public static string SurroundModeDesc => IsSpanish
        ? "Selecciona el modo de procesamiento de audio."
        : "Select the audio processing mode.";
    public static string CurrentMode => IsSpanish ? "MODO ACTUAL" : "CURRENT MODE";

    // -- Sound page --
    public static string SoundTitle => IsSpanish ? "Sonido" : "Sound";
    public static string SoundDesc => IsSpanish
        ? "Control de tono, subwoofer y DSP."
        : "Tone control, subwoofer trim and DSP.";
    public static string ToneControl => IsSpanish ? "Control de tono" : "Tone control";
    public static string SubwooferTrim => IsSpanish ? "Ajuste de subwoofer" : "Subwoofer trim";
    public static string SubwooferTrimDesc => IsSpanish
        ? "Ajusta la salida del subwoofer en pasos de 0.5 dB."
        : "Adjusts the subwoofer output in 0.5 dB steps.";

    // -- Zones page --
    public static string ZonesTitle => IsSpanish ? "Zonas" : "Zones";
    public static string ZonesDesc => IsSpanish
        ? "Audio independiente para otras habitaciones."
        : "Independent audio for other rooms.";
}
