using Joonaxii.JInput.Data;
using System.Collections.Generic;

namespace Joonaxii.JInput
{
    public unsafe class InputCore
    {
        public const int KEYBOARD_STATE_SIZE = 256;

        public const float DEFAULT_DEAD_ZONE = 0.105f;
        public const int MAX_CONTROLLERS = 4;
        public const int KEYBOARD_INPUTS = 0x10C;
        public const int CONTROLLER_INPUTS = (int)InputCode.KeyCount - KEYBOARD_INPUTS;

        public static bool IsValid => Instance != null;
        public static InputCore Instance { get; private set; }

        public MouseData MouseState => _mData;
        public MouseData MouseDelta => _mDelta;

        public bool HasFocus => _provider != null && _provider.IsFocused();

        private InputState[] _inputStates;
  
        private MouseData _mDelta;
        private MouseData _mData;

        private IInputProvider _provider;
        private ulong _tick;

        private float[] _deadzones = new float[MAX_CONTROLLERS + 1] 
        { 
            DEFAULT_DEAD_ZONE, 
            DEFAULT_DEAD_ZONE, 
            DEFAULT_DEAD_ZONE, 
            DEFAULT_DEAD_ZONE,
            DEFAULT_DEAD_ZONE
        };

        public InputCore(IInputProvider provider)
        {
            if (Instance == null)
            {
                Instance = this;
            }

            _tick = 0;
            _inputStates = new InputState[KEYBOARD_INPUTS + CONTROLLER_INPUTS * MAX_CONTROLLERS];

            _provider = provider;
            _provider?.Initialize();
        }

        public void SetDeadZone(DeviceIndex device, float deadzone)
        {
            int dev = FastMath.Log2((ulong)device);
            if (dev < 0 | dev > 4) { return; }
            _deadzones[dev] = deadzone < 0 ? 0.0f : deadzone;
        }

        public void SetDeadZones(params float[] deadzones)
        {
            if(deadzones == null) { return; }
            int len = deadzones.Length < MAX_CONTROLLERS ? deadzones.Length : MAX_CONTROLLERS;
            for (int i = 0; i < len; i++)
            {
                var dead = deadzones[i];
                _deadzones[i] = dead < 0 ? 0.0f : dead;
            }
        }

        public unsafe void GetKeyboardStateMap(uint* bits)
        {
            for (int i = 0; i < KEYBOARD_STATE_SIZE; i += 32) 
            {
                ref uint bitMask = ref bits[i >> 5];
                bitMask = 0;

                for (uint j = 0, m = 1; j < 32; j++, m <<= 1)
                {
                    ref var state = ref _inputStates[i + (int)j];
                    bitMask |= state.IsHeld ? m : 0U;
                }
            }
        }

        public void Clear()
        {
            _tick = 0;
            _mData = default;
            _mDelta = default;

            for (int i = 0; i < KEYBOARD_INPUTS + CONTROLLER_INPUTS * MAX_CONTROLLERS; i++)
            {
                _inputStates[i].Reset();
            }
        }

        public void Update()
        {
            if (_provider == null || !_provider.IsFocused()) { return; }

            unsafe
            {
                byte* keyboard = stackalloc byte[KEYBOARD_STATE_SIZE];

                var prevM = _mData;
                _provider.GetMouse(ref _mData);
                _mDelta = _mData - prevM;

                _provider.GetKeyboard(keyboard);

                bool state;
                for (int i = 0; i < KEYBOARD_STATE_SIZE; i++)
                {
                    ref var iState = ref _inputStates[i];

                    byte b = keyboard[i];
                    state = (b & 0x80) != 0;

                    iState.Update(state, state ? 1.0f : 0.0f, _tick);
                }

                fixed (InputState* st = _inputStates)
                {
                    UpdateMouseAxes(st + KEYBOARD_STATE_SIZE, _mData);
                    Gamepad padState = default;

                    for (int i = 0, j = KEYBOARD_INPUTS; i < MAX_CONTROLLERS; i++, j += CONTROLLER_INPUTS)
                    {
                        if(_provider.GetGamepad(i, ref padState))
                        {
                            UpdateControllerState(&padState, st + j, _deadzones[i + 1]);
                        }
                    }
                }
                _tick++;
            }
        }

        public InputState GetDeviceInputState(DeviceIndex device, InputCode input)
        {
            int dev = (int)device;
            if (!dev.IsPowerOf2()) { return default; }
            int code = (int)input;
            dev = FastMath.Log2(dev);

            if (dev == 0) { return _inputStates[code]; }
            dev--;
            return _inputStates[dev * CONTROLLER_INPUTS + code];
        }

        public bool GetBatteryInfo(DeviceIndex index, out Gamepad.BatteryInfo battery)
        {
            battery = default;
            int dev = (int)index;
            if (!dev.IsPowerOf2()) { return false; }
            return GetBatteryInfo(FastMath.Log2((ulong)index) - 1, out battery);
        }
        public bool GetBatteryInfo(int index, out Gamepad.BatteryInfo battery)
        {
            battery = default;
            if(index < 0 || index > 3 || _provider == null) { return false; }
            return _provider.GetGamepadBatteryInfo(index, ref battery) && battery.Type != BatteryType.Disconnected;
        }

        public bool AnyDown(DeviceIndex devices, out InputResult res) => Any(0, devices, out res, null);
        public bool AnyHeld(DeviceIndex devices, out InputResult res) => Any(1, devices, out res, null);
        public bool AnyUp(DeviceIndex devices, out InputResult res) => Any(2, devices, out res, null);
        public bool AnyToggled(DeviceIndex devices, out InputResult res) => Any(3, devices, out res, null);

        public bool AnyDown(DeviceIndex devices, out InputResult res, IList<InputCode> ignored) => Any(0, devices, out res, ignored);
        public bool AnyHeld(DeviceIndex devices, out InputResult res, IList<InputCode> ignored) => Any(1, devices, out res, ignored);
        public bool AnyUp(DeviceIndex devices, out InputResult res, IList<InputCode> ignored) => Any(2, devices, out res, ignored);
        public bool AnyToggled(DeviceIndex devices, out InputResult res, IList<InputCode> ignored) => Any(3, devices, out res, ignored);

        public bool IsUp(InputCode code, DeviceIndex index)
        {
            if (_provider == null || !_provider.IsFocused()) { return false; }

            InputState* tmp = stackalloc InputState[5] { default, default, default, default, default };
            int states = GetInputState(code, index, tmp);
            for (int i = 0; i < states; i++)
            {
                if (tmp[i].IsUp) { return true; }
            }
            return false;
        }
        public bool IsDown(InputCode code, DeviceIndex index)
        {
            if (_provider == null || !_provider.IsFocused()) { return false; }

            InputState* tmp = stackalloc InputState[5] { default, default, default, default, default };
            int states = GetInputState(code, index, tmp);
            for (int i = 0; i < states; i++)
            {
                if (tmp[i].IsDown) { return true; }
            }
            return false;
        }
        public bool IsHeld(InputCode code, DeviceIndex index)
        {
            if (_provider == null || !_provider.IsFocused()) { return false; }

            InputState* tmp = stackalloc InputState[5] { default, default, default, default, default };
            int states = GetInputState(code, index, tmp);
            for (int i = 0; i < states; i++)
            {
                if (tmp[i].IsHeld) { return true; }
            }
            return false;
        }
        public bool IsToggled(InputCode code, DeviceIndex index)
        {
            InputState* tmp = stackalloc InputState[5] { default, default, default, default, default };
            int states = GetInputState(code, index, tmp);
            for (int i = 0; i < states; i++)
            {
                if (tmp[i].IsToggled) { return true; }
            }
            return false;
        }

        public float GetValue(InputCode code, DeviceIndex index, bool rawValue = false)
        {
            if (_provider == null || !_provider.IsFocused()) { return 0; }

            InputState* tmp = stackalloc InputState[5] { default, default, default, default, default };
            int states = GetInputState(code, index, tmp);

            float value = 0;
            for (int i = 0; i < states; i++)
            {
                value += tmp[i].Value;
            }
            return rawValue | (states < 2) ? value : value / states;
        }

        public int GetInputState(InputCode code, DeviceIndex index, InputState* state)
        {
            *state = default;
            int kCode = (int)code;
            int dIndex = (int)index;

            int sInd = 0;
            int curSize = KEYBOARD_INPUTS;
            for (int i = 0, j = 1, k = 0; i < MAX_CONTROLLERS + 1; i++, j <<= 1)
            {
                if ((j & dIndex) != 0 && (kCode < curSize))
                {
                    state[sInd++] = _inputStates[k + kCode];
                }

                if(i == 0)
                {
                    kCode -= KEYBOARD_INPUTS;
                    curSize = CONTROLLER_INPUTS;
                    if(kCode < 0) { break; }
                    k += KEYBOARD_INPUTS;
                    continue;
                }
                k += CONTROLLER_INPUTS;
            }
            return sInd;
        }

        private InputState GetControllerState(int code, int index) => _inputStates[index * CONTROLLER_INPUTS + code];

        private void UpdateMouseAxes(InputState* states, MouseData mData)
        {
            int stateX;
            int stateY;

            float x;
            float y;
            float* mDataF = (float*)&mData;

            float dead = _deadzones[0];
            for (int i = 0, j = 0; i < 2; i++, j += 2, states += 2)
            {
                x = mDataF[j];
                y = mDataF[j + 1];
                stateX = x > dead ? 1 : x < -dead ? -1 : 0;
                stateY = y > dead ? 1 : y < -dead ? -1 : 0;

                states->Update(stateX != 0, x, _tick);
                (states + 1)->Update(stateY != 0, y, _tick);

                (states + 4 + j)->Update(stateY == 1, y, _tick);
                (states + 5 + j)->Update(stateX == -1, -x, _tick);

                (states + 6 + j)->Update(stateY == -1, -y, _tick);
                (states + 7 + j)->Update(stateX == 1, x, _tick);
            }
        }

        private void UpdateControllerState(Gamepad* padState, InputState* states, float deadzone)
        {
            var og = states;
            float * tempF;
            Gamepad.Stick* tempS;
            bool state;

            //Buttons
            for (int i = 0, j = 1; i < 16; i++, states++, j <<= 1)
            {
                state = (padState->ButtonsUI & j) != 0;
                states->Update(state, state ? 1.0f : 0.0f, _tick);
            }
    
            //Triggers
            tempF = &padState->_triggerL;
            for (int i = 0; i < 2; i++, states++)
            {
                float val = *tempF++;
                state = val > deadzone;

                states->Update(state, val, _tick);
                (states + 6)->Update(state, state ? 1.0f : 0.0f, _tick);
            }

            int stateA;
            int stateB;

            //Sticks
            tempS = &padState->_stickL;

            var stickBtn = states + 6;
            for (int i = 0, j = 0; i < 2; i++, states += 2, stickBtn+=4, j += 4)
            {
                ref Gamepad.Stick val = ref *tempS;
                //Stick XY
                stateA = val.x > deadzone ? 1 : val.x < -deadzone ? -1 : 0;
                stateB = val.y > deadzone ? 1 : val.y < -deadzone ? -1 : 0;

                state = stateA != 0;
                states->Update(state, state ? val.x : 0, _tick);

                state = stateB != 0;
                (states + 1)->Update(state, state ? val.y : 0, _tick);

                //Axis buttons U->L->D->R
                state = stateB == 1;
                (stickBtn + 0)->Update(state, state ? val.y : 0, _tick);

                state = stateA == -1;
                (stickBtn + 1)->Update(state, state ? -val.x : 0, _tick);

                state = stateB == -1;
                (stickBtn + 2)->Update(state, state ? -val.y : 0, _tick);

                state = stateA == 1;
                (stickBtn + 3)->Update(state, state ? val.x : 0, _tick);
            }
        }

        private bool Any(int mode, DeviceIndex devices, out InputResult res, IList<InputCode> ignored)
        {
            int devs = (int)devices;
            res = default;

            res.tick = 0;
            InputState state;

            int curSize = KEYBOARD_INPUTS;
            int offset = 0;
            ulong tick;
            bool result;
            for (int i = 0, j = 1, k = 0; i < MAX_CONTROLLERS + 1; i++, j <<= 1)
            {
                if ((j & devs) != 0)
                {
                    for (int l = 0; l < curSize; l++)
                    {
                        if(i == 0)
                        {
                            switch (l)
                            {
                                case (ushort)InputCode.Shift:
                                case (ushort)InputCode.Ctrl:
                                case (ushort)InputCode.Alt: continue;
                            }
                        }

                        state = _inputStates[k + l];
                        tick = state.Tick;
                        switch (mode)
                        {
                            default:
                                result = state.IsDown;
                                break;
                            case 1:
                                result = state.IsHeld;
                                break;
                            case 2:                    
                                result = state.IsUp;
                                break;
                            case 3:                    
                                result = state.IsToggled;
                                break;
                        }

                        if (result)
                        {
                            var iCode = (InputCode)(offset + l);

                            if(ignored != null && ignored.BinarySearch(iCode) > -1) { continue; }
                            if (res.tick >= tick) { continue; } 

                            res.device = (DeviceIndex)j;
                            res.code = iCode;
                            res.tick = tick;
                        }
                    }
                }

                if (i == 0)
                {
                    offset = KEYBOARD_INPUTS;
                    curSize = CONTROLLER_INPUTS;
                    k += KEYBOARD_INPUTS;
                    continue;
                }
                k += CONTROLLER_INPUTS;
            }
            return res.device != 0;
        }
    }
}
