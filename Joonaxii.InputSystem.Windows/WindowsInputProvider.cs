using Joonaxii.JInput;
using Joonaxii.JInput.Data;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Joonaxii.JInput.Windows
{
    public unsafe class WindowsInputProvider : IInputProvider
    {
        [DllImport("JInput.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void JInputInit();

        [DllImport("JInput.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool JInputIsFocused();
        
        [DllImport("JInput.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void JInputGetKeyboardState(byte* lpKeyState);

        [DllImport("JInput.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint JXInputGetState(uint dwUserIndex, Gamepad* pState);

        [DllImport("JInput.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint JXInputGetBatteryInfo(uint dwUserIndex, Gamepad.BatteryInfo* pBatteryInfo);

        public virtual bool IsFocused() => JInputIsFocused();
        public virtual bool GetMouse(ref MouseData mouse)
        {
            return false;
        }

        public virtual unsafe bool GetKeyboard(byte* keyboard)
        {
            JInputGetKeyboardState(keyboard);
            return true;
        }

        public virtual bool GetGamepad(int index, ref Gamepad gamepad)
        {
            if(index < 0 || index >= 4) { return false; }
            fixed (Gamepad* pad = &gamepad)
            {
                JXInputGetState((uint)index, pad);
                //
                //ref var xPad = ref state.gamePad;
                //Gamepad.Stick stickL = new Gamepad.Stick(
                //    xPad.sThumbLX.Normalize(),
                //    xPad.sThumbLY.Normalize());
                //Gamepad.Stick stickR = new Gamepad.Stick(
                //    xPad.sThumbRX.Normalize(),
                //    xPad.sThumbRY.Normalize());
                //
                //  gamepad.Update(xPad.wButtons, xPad.bLTrigger.Normalize(), xPad.bRTrigger.Normalize(), in stickL, in stickR);
                return true;
            }
        }

        public virtual bool GetGamepadBatteryInfo(int index, ref Gamepad.BatteryInfo battery)
        {
            if (index < 0 || index >= 4) { return false; }
            fixed (Gamepad.BatteryInfo* batt = &battery)
            {
                JXInputGetBatteryInfo((uint)index, batt);
            }
            return true;
        }

        public void Initialize() => JInputInit();

        //[StructLayout(LayoutKind.Sequential, Size = 16)]
        //private struct XInputState
        //{
        //    public uint dwPacketNumber;
        //    public XInputGamepad gamePad;
        //}
        //
        //[StructLayout(LayoutKind.Sequential, Size = 2)]
        //private struct XInputBatteryInfo
        //{
        //    public byte type;
        //    public byte level;
        //}
        //
        //[StructLayout(LayoutKind.Sequential, Size = 12)]
        //private struct XInputGamepad
        //{
        //    public ushort wButtons;
        //
        //    public byte bLTrigger;
        //    public byte bRTrigger;
        //
        //    public short sThumbLX;
        //    public short sThumbLY;
        //
        //    public short sThumbRX;
        //    public short sThumbRY;
        //}
    }
}
