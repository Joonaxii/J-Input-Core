#include "JInput.h"
#include <cstdio>
#include <process.h>

namespace {
    template<int64_t MinSize, int64_t MaxSize, bool Center>
    struct FloatLUT {
        float buffer[MaxSize - MinSize + 1]{};

        FloatLUT() : buffer{} {
            static constexpr float DIV = MaxSize - MinSize;
            for (int64_t i = 0; i < MaxSize - MinSize + 1; i++) {
                buffer[i] = Center ? ((i / DIV) - 0.5f) * 2.0f : (i / DIV);
            }
        }

        float operator[](int64_t i) const {
            i -= MinSize;
            return i >= (MaxSize - MinSize + 1) || i < 0 ? 0.0f : buffer[i];
        }
    };

    static FloatLUT<0, 255, false> UI8_LUT{};
    static FloatLUT<INT16_MIN, INT16_MAX, true> I16_LUT{};

    typedef DWORD(WINAPI* XInputGetStateFunc)(DWORD dwUserIndex, XINPUT_STATE* pState);
    typedef DWORD(WINAPI* XInputSetStateFunc)(DWORD dwUserIndex, XINPUT_VIBRATION* pVibration);
    typedef DWORD(WINAPI* XInputGetBateryInfoFunc)(DWORD dwUserIndex, BYTE devType, XINPUT_BATTERY_INFORMATION* pBattery);

    enum XInputLoaderState : uint8_t {
        XINPUT_STATE_UNLOADED,
        XINPUT_STATE_LOADED,
        XINPUT_STATE_FAILED,
    };

    class XInputLoader {
    public:
        XInputLoader() : _state(XINPUT_STATE_UNLOADED), _handle(0), getState{ nullptr }, setState{ nullptr }, getBatteryInfo{nullptr} {}
        ~XInputLoader() {}

        void init() {
            if (_state == XINPUT_STATE_UNLOADED) {
                constexpr int32_t TOTAL_XINPUT_RETRIES = 3;
                DWORD errors[TOTAL_XINPUT_RETRIES]{ 0 };
                int32_t current{0};

                do {
                    switch (current)
                    {
                    case 0: // XInput 1_3    
                        _handle = LoadLibraryA("xinput1_3.dll");
                        break;
                    case 1: // XInput 1_4
                        _handle = LoadLibraryA("xinput1_4.dll");
                        break;
                    case 2: // XInput 9_1
                        _handle = LoadLibraryA("xinput9_1_0.dll");
                        break;
                    }
                    errors[current] = GetLastError();
                } while (_handle == NULL && ++current < TOTAL_XINPUT_RETRIES);

                if (_handle != NULL) {
                    _state = XINPUT_STATE_LOADED;
                    getState = (XInputGetStateFunc)GetProcAddress(_handle, (LPCSTR)100);
                    setState = (XInputSetStateFunc)GetProcAddress(_handle, "XInputSetState");
                    getBatteryInfo = (XInputGetBateryInfoFunc)GetProcAddress(_handle, "XInputGetBatteryInformation");
                }
                else {
                    _state = XINPUT_STATE_FAILED;
                    printf_s("[JInput] Failed to load XInput! [0x%08x, 0x%08x, 0x%08x] Has XInput been installed?", errors[0], errors[1], errors[2]);
                }
            }
        }

        uint32_t getStatus() const {
            return _state;
        }

        XInputGetStateFunc getState;
        XInputSetStateFunc setState;
        XInputGetBateryInfoFunc getBatteryInfo;

    private:
        uint8_t _state;
        HMODULE _handle;
    };
    XInputLoader xInputLoader{};

    class WinLoader {
    public:
        WinLoader() : _processId{ MAXDWORD } {}

        void init() {
            _processId = GetCurrentProcessId();
        }

        DWORD getProcessId() const {
            return _processId;
        }

    private:
        DWORD _processId;
    };
    WinLoader winLoader{};
}

DWORD JXInputGetState(DWORD dwUserIndex, GamepadState* pState) {
    xInputLoader.init();

    if (xInputLoader.getState == nullptr) {
        return ERROR_DEVICE_NOT_CONNECTED;
    }

    XINPUT_STATE state{};
    DWORD ret = xInputLoader.getState(dwUserIndex, &state);
    pState->buttons = state.Gamepad.wButtons;

    pState->triggerL = UI8_LUT[state.Gamepad.bLeftTrigger];
    pState->triggerR = UI8_LUT[state.Gamepad.bRightTrigger];
    
    pState->stickLX = I16_LUT[state.Gamepad.sThumbLX];
    pState->stickLY = I16_LUT[state.Gamepad.sThumbLY];
    
    pState->stickRX = I16_LUT[state.Gamepad.sThumbRX];
    pState->stickRY = I16_LUT[state.Gamepad.sThumbRY];

    return ret;
}

void JInputInit() {
    winLoader.init();
    xInputLoader.init();
}
DWORD JXInputGetBatteryInfo(DWORD dwUserIndex, GamepadBatteryInfo* pBattery) {
    if (xInputLoader.getBatteryInfo == nullptr) {
        return ERROR_DEVICE_NOT_CONNECTED;
    }
    XINPUT_BATTERY_INFORMATION batt{};
    DWORD ret = xInputLoader.getBatteryInfo(dwUserIndex, BATTERY_DEVTYPE_GAMEPAD, &batt);
    pBattery->type = batt.BatteryType;
    pBattery->level = batt.BatteryLevel;
    return ret;
}

void JXInputSetState(DWORD dwUserIndex, XINPUT_VIBRATION* vibration) {
    if (xInputLoader.setState == nullptr) {
        return;
    }
    xInputLoader.setState(dwUserIndex, vibration);
}

void JInputGetKeyboardState(BYTE* pState) {
    GetKeyState(0);
    int32_t ret = GetKeyboardState(pState);
}

bool JInputIsFocused() {
    HWND activatedHandle = GetForegroundWindow();
    if (activatedHandle == NULL) {
        return false;
    }

    DWORD foregroundPid;
    if (GetWindowThreadProcessId(activatedHandle, &foregroundPid) == 0) {
        return false;
    }
    return foregroundPid == winLoader.getProcessId();
}