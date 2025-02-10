namespace Joonaxii.JInput
{
    public struct InputState
    {
        public bool IsDown      => (_flags & 0x1) != 0;
        public bool IsHeld      => (_flags & 0x2) != 0;
        public bool IsUp        => (_flags & 0x4) != 0;
        public bool IsToggled   => (_flags & 0x8) != 0;

        public float Value => _axisVal;
        public ulong Tick => _tick;

        private byte _flags;
        private float _axisVal;
        private ulong _tick;

        internal InputState(ulong tick) : this()
        {
            _tick = tick;
        }


        public void Reset()
        {
            _flags = 0;
            _axisVal = 0;
            _tick = 0;
        }

        public void Update(bool state, float value, ulong tick)
        {
            _axisVal = value;
            bool down = IsDown;
            bool held = IsHeld;
            bool toggled = IsToggled;

            _flags = 0;

            if(held != state && !toggled)
            {
                _flags |= 0x8;
            }

            if (down && !state) { _flags |= 0x4; }
            if(!held && state) 
            { 
                _flags |= 0x1;
                _tick = tick;
            }
            if(state) { _flags |= 0x2; }
        }
    }
}