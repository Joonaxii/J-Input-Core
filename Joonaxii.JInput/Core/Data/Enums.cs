using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joonaxii.JInput
{
    public enum BatteryType : byte
    {
        Disconnected = 0x0,
        Wired = 0x1,
        Alkaline = 0x2,
        NIMH = 0x3,
        Unknown = 0xFF,
    }

    public enum BatteryLevel : byte
    {
        Empty = 0x0,
        Low = 0x1,
        Medium = 0x2,
        Full = 0x3,
    }

    [System.Flags]
    public enum DeviceIndex : byte
    {
        None = 0,

        Keyboard = 0x1,

        One = 0x2,
        Two = 0x4,
        Three = 0x8,
        Four = 0x10,
    }

    [System.Flags]
    public enum GamepadButtons : ushort
    {
        None = 0x00,

        DPadUp = 0x01,
        DPadDown = 0x02,
        DPadLeft = 0x04,
        DPadRight = 0x08,

        Start = 0x10,
        Back = 0x20,

        LStick = 0x40,
        RStick = 0x80,

        LShoulder = 0x100,
        RShoulder = 0x200,

        Guide     = 0x400,

        Button0   = 0x1000,
        Button1   = 0x2000,
        Button2   = 0x4000,
        Button3   = 0x8000,
    }


    public enum InputCode : ushort
    {
        None = 0,
        Mouse0 = 0x1,
        Mouse1 = 0x2,
        Cancel = 0x3,
        Mouse2 = 0x4,
        Mouse3 = 0x5,
        Mouse4 = 0x6,
        Mouse5 = 0x7,

        Backspace = 0x8,
        Tab = 0x9,

        Clear = 0xC,
        Enter = 0xD,

        Shift = 0x10,
        Ctrl = 0x11,
        Alt = 0x12,

        Pause = 0x13,
        CapsLock = 0x14,

        Escape = 0x1B,

        Space = 0x20,
        PageUp = 0x21,
        PageDown = 0x22,
        End = 0x23,
        Home = 0x24,

        Left = 0x25,
        Up = 0x26,
        Right = 0x27,
        Down = 0x28,

        Print = 0x2A,
        Execute = 0x2B,
        PrintScrn = 0x2C,
        Insert = 0x2D,
        Delete = 0x2E,
        Help = 0x2F,

        D0 = 0x30,
        D1 = 0x31,
        D2 = 0x32,
        D3 = 0x33,
        D4 = 0x34,
        D5 = 0x35,
        D6 = 0x36,
        D7 = 0x37,
        D8 = 0x38,
        D9 = 0x39,

        A = 'A',
        B = 'B',
        C = 'C',
        D = 'D',
        E = 'E',
        F = 'F',
        G = 'G',
        H = 'H',
        I = 'I',
        J = 'J',
        K = 'K',
        L = 'L',
        M = 'M',
        N = 'N',
        O = 'O',
        P = 'P',
        Q = 'Q',
        R = 'R',
        S = 'S',
        T = 'T',
        U = 'U',
        V = 'V',
        W = 'W',
        X = 'X',
        Y = 'Y',
        Z = 'Z',

        LSuper = 0x5B,
        RSuper = 0x5C,
        Apps = 0x5D,

        Sleep = 0x5F,

        Num0 = 0x60,
        Num1 = 0x61,
        Num2 = 0x62,
        Num3 = 0x63,
        Num4 = 0x64,
        Num5 = 0x65,
        Num6 = 0x66,
        Num7 = 0x67,
        Num8 = 0x68,
        Num9 = 0x69,

        NumMultiply = 0x6A,
        NumPlus = 0x6B,
        NumSeparator = 0x6C,
        NumSubtract = 0x6D,
        NumDecimal = 0x6E,
        NumDivide = 0x6F,

        F1 = 0x70,
        F2 = 0x71,
        F3 = 0x72,
        F4 = 0x73,
        F5 = 0x74,
        F6 = 0x75,
        F7 = 0x76,
        F8 = 0x77,
        F9 = 0x78,
        F10 = 0x79,
        F11 = 0x7A,
        F12 = 0x7B,
        F13 = 0x7C,
        F14 = 0x7D,
        F15 = 0x7E,
        F16 = 0x7F,
        F17 = 0x80,
        F18 = 0x81,
        F19 = 0x82,
        F20 = 0x83,
        F21 = 0x84,
        F22 = 0x85,
        F23 = 0x86,
        F24 = 0x87,

        NumLock = 0x90,
        ScrollLock = 0x91,

        LShift = 0xA0,
        RShift = 0xA1,
        LCtrl = 0xA2,
        RCtrl = 0xA3,

        LAlt = 0xA4,
        RAlt = 0xA5,

        VolumeMute = 0xAD,
        VolumeDown = 0xAE,
        VolumeUp = 0xAF,

        MediaNext = 0xB0,
        MediaPrev = 0xB1,
        MediaStop = 0xB2,
        MediaPlay = 0xB3,

        Semicolon = 0xBA,
        Plus = 0xBB,
        Comma = 0xBC,
        Minus = 0xBD,
        Period = 0xBE,
        FSlash = 0xBF,
        Tilde = 0xC0,

        LBracket = 0xDB,
        BSlash = 0xDC,
        RBracket = 0xDD,
        Quote = 0xDE,

        Angle = 0xE2,

        //K-Axes
        MDeltaX = 0x100,
        MDeltaY = 0x101,

        MScrollX = 0x102,
        MScrollY = 0x103,

        //K-Axis Buttons
        MouseUp = 0x104,
        MouseLeft = 0x105,
        MouseDown = 0x106,
        MouseRight = 0x107,

        ScrollUp = 0x108,
        ScrollLeft = 0x109,
        ScrollDown = 0x10A,
        ScrollRight = 0x10B,

        //C-Buttons

        DPadUp,
        DPadDown,
        DPadLeft,
        DPadRight,

        StartButton,
        BackButton,

        LStick,
        RStick,

        LShoulder,
        RShoulder,

        GuideButton,

        CButton0 = 280,
        CButton1,
        CButton2,
        CButton3,

        //C-Axes
        TriggerL,
        TriggerR,

        StickLX,
        StickLY,

        StickRX,
        StickRY,

        //C-Axis Buttons
        PressTriggerL,
        PressTriggerR,

        StickLUp,
        StickLLeft,
        StickLDown,
        StickLRight,

        StickRUp,
        StickRLeft,
        StickRDown,
        StickRRight,

        KeyCount,
    }
}
