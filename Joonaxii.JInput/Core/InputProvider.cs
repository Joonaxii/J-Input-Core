using Joonaxii.JInput.Data;

namespace Joonaxii.JInput
{
    public unsafe interface IInputProvider
    {
        void Initialize();

        bool IsFocused();
        bool GetMouse(ref MouseData mouse);
        bool GetKeyboard(byte* keyboard);

        bool GetGamepad(int index, ref Gamepad gamepad);
        bool GetGamepadBatteryInfo(int index, ref Gamepad.BatteryInfo battery);
    }
}
