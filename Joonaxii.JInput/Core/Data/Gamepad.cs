
using System.Runtime.InteropServices;

namespace Joonaxii.JInput.Data
{
    [StructLayout(LayoutKind.Explicit, Pack=1, Size = 26)]
    public struct Gamepad
    {
        public bool this[InputCode code]
        {
            get
            {
                unsafe
                {
                    return this[*(ushort*)&code - 0x10C];
                }
            }
        }
        public bool this[int index]
        {
            get
            {
                if (index < 0 | index > 0x8000) { return false; }
                return ((int)_buttons & index) != 0;
            }
        }

        public GamepadButtons Buttons => _buttons.Reinterpret<ushort, GamepadButtons>();
        internal ushort ButtonsUI => _buttons;

        public float TriggerL => _triggerL;
        public float TriggerR => _triggerR;

        public Stick StickL => _stickL;
        public Stick StickR => _stickR;

        [FieldOffset(0)] internal ushort _buttons;

        [FieldOffset(2)] internal float _triggerL;
        [FieldOffset(6)] internal float _triggerR;

        [FieldOffset(10)] internal Stick _stickL;
        [FieldOffset(18)] internal Stick _stickR;

        public void Update(ushort buttons, float triggerL, float triggerR, in Stick stickL, in Stick stickR)
        {
            _buttons = buttons;

            _triggerL = triggerL;
            _triggerR = triggerR;

            _stickL = stickL;
            _stickR = stickR;
        }

        [StructLayout(LayoutKind.Sequential, Size=8)]
        public struct Stick
        {
            public float x;
            public float y;

            public Stick(float x, float y)
            {
                this.x = x;
                this.y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 2)]
        public struct BatteryInfo
        {
            public BatteryType Type => _type;
            public BatteryLevel Level => _level;

            private BatteryType _type;
            private BatteryLevel _level;

            public BatteryInfo(BatteryType type, BatteryLevel level)
            {
                _type = type;
                _level = level;
            }

            public BatteryInfo(int type, int level)
            {
                _type = (BatteryType)type;
                _level = (BatteryLevel)level;
            }
        }
    }
}
