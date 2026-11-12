namespace Dynasland.Modules.Common;

/// <summary>
/// Ícones vetoriais próprios. Onde o contrato é uma string de glifo (ex.: <c>IslandShortcut.Glyph</c>, Tag do menu),
/// usa-se a chave "vector:..." e o <see cref="GlyphContentConverter"/> troca a chave pelo <see cref="VectorIcon"/>.
/// </summary>
public static class VectorIcons
{
    public const string Prefix = "vector:";

    /// <summary>Gota d'água com reflexo (módulo Água).</summary>
    public const string Water = Prefix + "water";

    public const string Music = Prefix + "music";

    public const string Volume = Prefix + "volume";

    public const string Brightness = Prefix + "brightness";

    public const string Timer = Prefix + "timer";

    public const string Weather = Prefix + "weather";

    public const string Calendar = Prefix + "calendar";

    public const string Download = Prefix + "download";

    public const string Bluetooth = Prefix + "bluetooth";

    public const string Battery = Prefix + "battery";

    public const string Camera = Prefix + "camera";

    public const string Clipboard = Prefix + "clipboard";

    public const string Bell = Prefix + "bell";

    public const string Pulse = Prefix + "pulse";

    public const string Game = Prefix + "game";

    public const string Clock = Prefix + "clock";

    public const string Refresh = Prefix + "refresh";

    public const string Focus = Prefix + "focus";

    public const string Capture = Prefix + "capture";

    public const string ChevronLeft = Prefix + "chevronleft";

    public const string ChevronRight = Prefix + "chevronright";

    public const string Close = Prefix + "close";

    public const string Gear = Prefix + "gear";

    public const string Home = Prefix + "home";

    public const string Palette = Prefix + "palette";

    public const string Sparkles = Prefix + "sparkles";

    public const string Sliders = Prefix + "sliders";

    public const string Monitor = Prefix + "monitor";

    public const string Modules = Prefix + "modules";

    public const string Lock = Prefix + "lock";

    public const string Keyboard = Prefix + "keyboard";

    /// <summary>Linha de alta com seta (módulo Cotações).</summary>
    public const string Trend = Prefix + "trend";

    /// <summary>Bola de basquete (módulo Placar).</summary>
    public const string Ball = Prefix + "ball";

    public const string Storage = Prefix + "storage";

    public const string Info = Prefix + "info";

    public const string Play = Prefix + "play";

    public const string Pause = Prefix + "pause";

    public const string Previous = Prefix + "previous";

    public const string Next = Prefix + "next";

    public const string Stop = Prefix + "stop";

    public const string Add = Prefix + "add";

    public const string Copy = Prefix + "copy";

    public const string Document = Prefix + "document";

    public const string Warning = Prefix + "warning";

    public const string Accept = Prefix + "accept";

    public const string Flag = Prefix + "flag";

    public const string History = Prefix + "history";

    public const string Search = Prefix + "search";

    public const string Trash = Prefix + "trash";

    public const string Star = Prefix + "star";

    public const string StarFill = Prefix + "starfill";

    public const string Record = Prefix + "record";

    public const string Microphone = Prefix + "microphone";

    public const string Mute = Prefix + "mute";

    public const string VolumeLow = Prefix + "volumelow";

    public const string VolumeMedium = Prefix + "volumemedium";

    public const string FolderOpen = Prefix + "folderopen";

    public const string OpenFile = Prefix + "openfile";

    public const string Pin = Prefix + "pin";

    public const string PinOff = Prefix + "pinoff";

    public const string Bolt = Prefix + "bolt";

    public const string Crop = Prefix + "crop";

    public const string Window = Prefix + "window";

    public const string MicrophoneOff = Prefix + "microphoneoff";

    public const string ChevronDown = Prefix + "chevrondown";

    public const string Pencil = Prefix + "pencil";

    public const string Undo = Prefix + "undo";

    public const string MapPin = Prefix + "mappin";

    private static readonly Dictionary<string, VectorIcon> Icons = new(StringComparer.Ordinal)
    {
        [Water] = new VectorIcon(
            Outline: "M12,2.25 C12,2.25 5.25,9.75 5.25,15 A6.75,6.75 0 0 0 18.75,15 C18.75,9.75 12,2.25 12,2.25 Z",
            Detail: "M9,15 A3,3 0 0 0 12,18"),
        [Music] = new VectorIcon(
            Outline: "M9,18 V5 L21,3 V16",
            Detail: "M9,18 A3,3 0 1 1 3,18 A3,3 0 1 1 9,18 M21,16 A3,3 0 1 1 15,16 A3,3 0 1 1 21,16"),
        [Volume] = new VectorIcon(
            Outline: "M11,5 L6,9 H2 V15 H6 L11,19 Z",
            Detail: "M15.5,8.5 A5,5 0 0 1 15.5,15.5 M19,5 A10,10 0 0 1 19,19"),
        [Brightness] = new VectorIcon(
            Outline: "M16,12 A4,4 0 1 1 8,12 A4,4 0 1 1 16,12",
            Detail: "M12,2 V4 M12,20 V22 M4.9,4.9 L6.3,6.3 M17.7,17.7 L19.1,19.1 M2,12 H4 M20,12 H22 M4.9,19.1 L6.3,17.7 M17.7,6.3 L19.1,4.9"),
        [Timer] = new VectorIcon(
            Outline: "M20,14 A8,8 0 1 1 4,14 A8,8 0 1 1 20,14",
            Detail: "M12,14 L15,11 M9,2 H15"),
        [Weather] = new VectorIcon(
            Outline: "M17.5,19 H9 A7,7 0 1 1 15.71,10 H17.5 A4.5,4.5 0 1 1 17.5,19 Z",
            Detail: ""),
        [Calendar] = new VectorIcon(
            Outline: "M5,4 H19 A2,2 0 0 1 21,6 V20 A2,2 0 0 1 19,22 H5 A2,2 0 0 1 3,20 V6 A2,2 0 0 1 5,4 Z",
            Detail: "M8,2 V6 M16,2 V6 M3,10 H21"),
        [Download] = new VectorIcon(
            Outline: "M21,15 V19 A2,2 0 0 1 19,21 H5 A2,2 0 0 1 3,19 V15",
            Detail: "M7,10 L12,15 L17,10 M12,15 V3"),
        [Bluetooth] = new VectorIcon(
            Outline: "M7,7 L17,17 L12,22 V2 L17,7 L7,17",
            Detail: ""),
        [Battery] = new VectorIcon(
            Outline: "M4,7 H17 A2,2 0 0 1 19,9 V15 A2,2 0 0 1 17,17 H4 A2,2 0 0 1 2,15 V9 A2,2 0 0 1 4,7 Z",
            Detail: "M22,11 V13 M6,10.5 V13.5"),
        [Camera] = new VectorIcon(
            Outline: "M22,19 A2,2 0 0 1 20,21 H4 A2,2 0 0 1 2,19 V8 A2,2 0 0 1 4,6 H7 L9,3 H15 L17,6 H20 A2,2 0 0 1 22,8 Z",
            Detail: "M16,13 A4,4 0 1 1 8,13 A4,4 0 1 1 16,13"),
        [Clipboard] = new VectorIcon(
            Outline: "M9,2 H15 A1,1 0 0 1 16,3 V5 A1,1 0 0 1 15,6 H9 A1,1 0 0 1 8,5 V3 A1,1 0 0 1 9,2 Z",
            Detail: "M16,4 H18 A2,2 0 0 1 20,6 V20 A2,2 0 0 1 18,22 H6 A2,2 0 0 1 4,20 V6 A2,2 0 0 1 6,4 H8"),
        [Bell] = new VectorIcon(
            Outline: "M6,8 A6,6 0 0 1 18,8 C18,15 21,17 21,17 H3 C3,17 6,15 6,8 Z",
            Detail: "M10.3,21 A2,2 0 0 0 13.7,21"),
        [Pulse] = new VectorIcon(
            Outline: "M22,12 H18 L15,21 L9,3 L6,12 H2",
            Detail: ""),
        [Game] = new VectorIcon(
            Outline: "M7,6 H17 A5,5 0 0 1 22,11 V13 A3,3 0 0 1 16.2,14 L15.5,12.5 H8.5 L7.8,14 A3,3 0 0 1 2,13 V11 A5,5 0 0 1 7,6 Z",
            Detail: "M7,9.5 V12.5 M5.5,11 H8.5 M16,10 H16.01 M18,12 H18.01"),
        [Clock] = new VectorIcon(
            Outline: "M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12",
            Detail: "M12,6 V12 L16,14"),
        [Refresh] = new VectorIcon(
            Outline: "M21,12 A9,9 0 0 0 6,5.3 L3,8",
            Detail: "M3,3 V8 H8 M3,12 A9,9 0 0 0 18,18.7 L21,16 M16,16 H21 V21"),
        [Focus] = new VectorIcon(
            Outline: "M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12",
            Detail: "M18,12 A6,6 0 1 1 6,12 A6,6 0 1 1 18,12 M13,12 A1,1 0 1 1 11,12 A1,1 0 1 1 13,12"),
        [Capture] = new VectorIcon(
            Outline: "M5,3 H19 A2,2 0 0 1 21,5 V19 A2,2 0 0 1 19,21 H5 A2,2 0 0 1 3,19 V5 A2,2 0 0 1 5,3 Z",
            Detail: "M21,15 L16,10 L5,21 M9,8 H9.01"),
        [ChevronLeft] = new VectorIcon(
            Outline: "M15,18 L9,12 L15,6",
            Detail: ""),
        [ChevronRight] = new VectorIcon(
            Outline: "M9,18 L15,12 L9,6",
            Detail: ""),
        [Close] = new VectorIcon(
            Outline: "M18,6 L6,18",
            Detail: "M6,6 L18,18"),
        [Gear] = new VectorIcon(
            Outline: "M12.22,2 H11.78 A2,2 0 0 0 9.78,4 V4.18 A2,2 0 0 1 8.78,5.91 L8.35,6.16 A2,2 0 0 1 6.35,6.16 L6.2,6.08 A2,2 0 0 0 3.47,6.81 L3.25,7.19 A2,2 0 0 0 3.98,9.92 L4.13,10.02 A2,2 0 0 1 5.13,11.74 V12.25 A2,2 0 0 1 4.13,13.99 L3.98,14.08 A2,2 0 0 0 3.25,16.81 L3.47,17.19 A2,2 0 0 0 6.2,17.92 L6.35,17.84 A2,2 0 0 1 8.35,17.84 L8.78,18.09 A2,2 0 0 1 9.78,19.82 V20 A2,2 0 0 0 11.78,22 H12.22 A2,2 0 0 0 14.22,20 V19.82 A2,2 0 0 1 15.22,18.09 L15.65,17.84 A2,2 0 0 1 17.65,17.84 L17.8,17.92 A2,2 0 0 0 20.53,17.19 L20.75,16.8 A2,2 0 0 0 20.02,14.07 L19.87,13.99 A2,2 0 0 1 18.87,12.25 V11.75 A2,2 0 0 1 19.87,10.01 L20.02,9.92 A2,2 0 0 0 20.75,7.19 L20.53,6.81 A2,2 0 0 0 17.8,6.08 L17.65,6.16 A2,2 0 0 1 15.65,6.16 L15.22,5.91 A2,2 0 0 1 14.22,4.18 V4 A2,2 0 0 0 12.22,2 Z",
            Detail: "M15,12 A3,3 0 1 1 9,12 A3,3 0 1 1 15,12"),
        [Home] = new VectorIcon(
            Outline: "M3,10 L12,3 L21,10 V20 A1,1 0 0 1 20,21 H4 A1,1 0 0 1 3,20 Z",
            Detail: "M9,21 V12 H15 V21"),
        [Palette] = new VectorIcon(
            Outline: "M12,22 A10,10 0 1 1 22,12 A5,5 0 0 1 17,17 H15.5 A2,2 0 0 0 14,20.3 A1.5,1.5 0 0 1 12,22 Z",
            Detail: "M7.5,11 H7.51 M10,7 H10.01 M15,7 H15.01 M17.5,11 H17.51"),
        [Sparkles] = new VectorIcon(
            Outline: "M12,3 L14,9 L20,11 L14,13 L12,19 L10,13 L4,11 L10,9 Z",
            Detail: "M19,3 V7 M17,5 H21"),
        [Sliders] = new VectorIcon(
            Outline: "M4,21 V14 M4,10 V3 M12,21 V12 M12,8 V3 M20,21 V16 M20,12 V3",
            Detail: "M1,14 H7 M9,8 H15 M17,16 H23"),
        [Monitor] = new VectorIcon(
            Outline: "M4,3 H20 A2,2 0 0 1 22,5 V15 A2,2 0 0 1 20,17 H4 A2,2 0 0 1 2,15 V5 A2,2 0 0 1 4,3 Z",
            Detail: "M8,21 H16 M12,17 V21"),
        [Modules] = new VectorIcon(
            Outline: "M4,3 H9 A1,1 0 0 1 10,4 V9 A1,1 0 0 1 9,10 H4 A1,1 0 0 1 3,9 V4 A1,1 0 0 1 4,3 Z M15,3 H20 A1,1 0 0 1 21,4 V9 A1,1 0 0 1 20,10 H15 A1,1 0 0 1 14,9 V4 A1,1 0 0 1 15,3 Z",
            Detail: "M4,14 H9 A1,1 0 0 1 10,15 V20 A1,1 0 0 1 9,21 H4 A1,1 0 0 1 3,20 V15 A1,1 0 0 1 4,14 Z M15,14 H20 A1,1 0 0 1 21,15 V20 A1,1 0 0 1 20,21 H15 A1,1 0 0 1 14,20 V15 A1,1 0 0 1 15,14 Z"),
        [Lock] = new VectorIcon(
            Outline: "M5,11 H19 A2,2 0 0 1 21,13 V20 A2,2 0 0 1 19,22 H5 A2,2 0 0 1 3,20 V13 A2,2 0 0 1 5,11 Z",
            Detail: "M7,11 V7 A5,5 0 0 1 17,7 V11"),
        [Trend] = new VectorIcon(
            Outline: "M3,17 L9,11 L13,15 L21,7",
            Detail: "M15,7 H21 V13"),
        [Ball] = new VectorIcon(
            Outline: "M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12",
            Detail: "M2,12 H22 M12,2 V22 M5,5 C9,9 9,15 5,19 M19,5 C15,9 15,15 19,19"),
        [Keyboard] = new VectorIcon(
            Outline: "M4,5 H20 A2,2 0 0 1 22,7 V17 A2,2 0 0 1 20,19 H4 A2,2 0 0 1 2,17 V7 A2,2 0 0 1 4,5 Z",
            Detail: "M6,9 H6.01 M10,9 H10.01 M14,9 H14.01 M18,9 H18.01 M8,15 H16"),
        [Storage] = new VectorIcon(
            Outline: "M21,5 A9,3 0 1 1 3,5 A9,3 0 1 1 21,5 Z",
            Detail: "M3,5 V19 A9,3 0 0 0 21,19 V5 M3,12 A9,3 0 0 0 21,12"),
        [Info] = new VectorIcon(
            Outline: "M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12",
            Detail: "M12,16 V12 M12,8 H12.01"),
        [Play] = new VectorIcon(
            Outline: "M7,4 L19,12 L7,20 Z",
            Detail: ""),
        [Pause] = new VectorIcon(
            Outline: "M7,4 H10 V20 H7 Z",
            Detail: "M14,4 H17 V20 H14 Z"),
        [Previous] = new VectorIcon(
            Outline: "M19,19 L9,12 L19,5 Z",
            Detail: "M5,19 V5"),
        [Next] = new VectorIcon(
            Outline: "M5,19 L15,12 L5,5 Z",
            Detail: "M19,19 V5"),
        [Stop] = new VectorIcon(
            Outline: "M7,7 H17 V17 H7 Z",
            Detail: ""),
        [Add] = new VectorIcon(
            Outline: "M12,5 V19",
            Detail: "M5,12 H19"),
        [Copy] = new VectorIcon(
            Outline: "M11,8 H19 A2,2 0 0 1 21,10 V19 A2,2 0 0 1 19,21 H11 A2,2 0 0 1 9,19 V10 A2,2 0 0 1 11,8 Z",
            Detail: "M5,16 H4 A2,2 0 0 1 2,14 V5 A2,2 0 0 1 4,3 H13 A2,2 0 0 1 15,5 V6"),
        [Document] = new VectorIcon(
            Outline: "M14,2 H6 A2,2 0 0 0 4,4 V20 A2,2 0 0 0 6,22 H18 A2,2 0 0 0 20,20 V8 Z",
            Detail: "M14,2 V8 H20 M8,13 H16 M8,17 H13"),
        [Warning] = new VectorIcon(
            Outline: "M10.3,3.9 L1.8,18 A2,2 0 0 0 3.5,21 H20.5 A2,2 0 0 0 22.2,18 L13.7,3.9 A2,2 0 0 0 10.3,3.9 Z",
            Detail: "M12,9 V13 M12,17 H12.01"),
        [Accept] = new VectorIcon(
            Outline: "M20,6 L9,17 L4,12",
            Detail: ""),
        [Flag] = new VectorIcon(
            Outline: "M4,15 C4,15 5,14 8,14 C11,14 13,16 16,16 C19,16 20,15 20,15 V3 C20,3 19,4 16,4 C13,4 11,2 8,2 C5,2 4,3 4,3 Z",
            Detail: "M4,22 V15"),
        [History] = new VectorIcon(
            Outline: "M3,12 A9,9 0 1 0 6,5.3 L3,8",
            Detail: "M3,3 V8 H8 M12,7 V12 L16,14"),
        [Search] = new VectorIcon(
            Outline: "M19,11 A8,8 0 1 1 3,11 A8,8 0 1 1 19,11",
            Detail: "M21,21 L16.65,16.65"),
        [Trash] = new VectorIcon(
            Outline: "M3,6 H21",
            Detail: "M19,6 V20 A2,2 0 0 1 17,22 H7 A2,2 0 0 1 5,20 V6 M8,6 V4 A2,2 0 0 1 10,2 H14 A2,2 0 0 1 16,4 V6"),
        [Star] = new VectorIcon(
            Outline: "M12,2 L15.09,8.26 L22,9.27 L17,14.14 L18.18,21.02 L12,17.77 L5.82,21.02 L7,14.14 L2,9.27 L8.91,8.26 Z",
            Detail: ""),
        [StarFill] = new VectorIcon(
            Outline: "M12,2 L15.09,8.26 L22,9.27 L17,14.14 L18.18,21.02 L12,17.77 L5.82,21.02 L7,14.14 L2,9.27 L8.91,8.26 Z",
            Detail: "",
            Filled: true),
        [Record] = new VectorIcon(
            Outline: "M18,12 A6,6 0 1 1 6,12 A6,6 0 1 1 18,12",
            Detail: "",
            Filled: true),
        [Microphone] = new VectorIcon(
            Outline: "M12,2 A3,3 0 0 0 9,5 V12 A3,3 0 0 0 15,12 V5 A3,3 0 0 0 12,2 Z",
            Detail: "M19,10 V12 A7,7 0 0 1 5,12 V10 M12,19 V22"),
        [Mute] = new VectorIcon(
            Outline: "M11,5 L6,9 H2 V15 H6 L11,19 Z",
            Detail: "M22,9 L16,15 M16,9 L22,15"),
        [VolumeLow] = new VectorIcon(
            Outline: "M11,5 L6,9 H2 V15 H6 L11,19 Z",
            Detail: "M15.5,9.5 A3.5,3.5 0 0 1 15.5,14.5"),
        [VolumeMedium] = new VectorIcon(
            Outline: "M11,5 L6,9 H2 V15 H6 L11,19 Z",
            Detail: "M15.5,8.5 A5,5 0 0 1 15.5,15.5"),
        [FolderOpen] = new VectorIcon(
            Outline: "M4,20 H18 A2,2 0 0 0 20,18 V9 A2,2 0 0 0 18,7 H11 L9,5 H4 A2,2 0 0 0 2,7 V18 A2,2 0 0 0 4,20 Z",
            Detail: ""),
        [OpenFile] = new VectorIcon(
            Outline: "M18,13 V19 A2,2 0 0 1 16,21 H5 A2,2 0 0 1 3,19 V8 A2,2 0 0 1 5,6 H11",
            Detail: "M15,3 H21 V9 M10,14 L21,3"),
        [Pin] = new VectorIcon(
            Outline: "M5,17 H19 V15.2 A2,2 0 0 0 18,13.5 L16,12.5 V5 H17 A1,1 0 0 0 17,3 H7 A1,1 0 0 0 7,5 H8 V12.5 L6,13.5 A2,2 0 0 0 5,15.2 Z",
            Detail: "M12,17 V22"),
        [PinOff] = new VectorIcon(
            Outline: "M5,17 H19 V15.2 A2,2 0 0 0 18,13.5 L16,12.5 V5 H17 A1,1 0 0 0 17,3 H7 A1,1 0 0 0 7,5 H8 V12.5 L6,13.5 A2,2 0 0 0 5,15.2 Z",
            Detail: "M12,17 V22 M3,3 L21,21"),
        [Bolt] = new VectorIcon(
            Outline: "M13,2 L3,14 H12 L11,22 L21,10 H12 Z",
            Detail: ""),
        [Crop] = new VectorIcon(
            Outline: "M6,2 V16 A2,2 0 0 0 8,18 H22",
            Detail: "M2,6 H16 A2,2 0 0 1 18,8 V22"),
        [Window] = new VectorIcon(
            Outline: "M5,4 H19 A2,2 0 0 1 21,6 V18 A2,2 0 0 1 19,20 H5 A2,2 0 0 1 3,18 V6 A2,2 0 0 1 5,4 Z",
            Detail: "M3,9 H21"),
        [MicrophoneOff] = new VectorIcon(
            Outline: "M12,2 A3,3 0 0 0 9,5 V12 A3,3 0 0 0 15,12 V5 A3,3 0 0 0 12,2 Z",
            Detail: "M19,10 V12 A7,7 0 0 1 5,12 V10 M12,19 V22 M3,3 L21,21"),
        [ChevronDown] = new VectorIcon(
            Outline: "M6,9 L12,15 L18,9",
            Detail: ""),
        [Pencil] = new VectorIcon(
            Outline: "M17,3 A2.1,2.1 0 0 1 21,7 L7.5,20.5 L2,22 L3.5,16.5 Z",
            Detail: "M15,5 L19,9"),
        [Undo] = new VectorIcon(
            Outline: "M3,12 A9,9 0 1 1 6,18.7 L3,16",
            Detail: "M3,21 V16 H8"),
        [MapPin] = new VectorIcon(
            Outline: "M20,10 C20,16 12,22 12,22 C12,22 4,16 4,10 A8,8 0 0 1 20,10 Z",
            Detail: "M15,10 A3,3 0 1 1 9,10 A3,3 0 1 1 15,10"),
    };

    /// <summary>O ícone da chave, ou null se a chave não for de um ícone vetorial conhecido.</summary>
    public static VectorIcon? Get(string? key) => key is not null && Icons.TryGetValue(key, out var icon) ? icon : null;
}
