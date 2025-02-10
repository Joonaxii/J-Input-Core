#pragma once
#include "Core.h"

#include <Windows.h>
#include <Xinput.h>
#include <cstdint>

extern "C" {

#pragma pack(push, 1)
    struct GamepadState {
        uint16_t buttons {0};
        float triggerL{0.0f};
        float triggerR{0.0f};
        
        float stickLX{ 0.0f };
        float stickLY{ 0.0f };

        float stickRX{ 0.0f };
        float stickRY{ 0.0f };
    };

    struct GamepadBatteryInfo {
        uint8_t type{ 0 };
        uint8_t level{ 0 };
    };

#pragma pack(pop)

    JINPUT_EXPORT void JInputInit();

    JINPUT_EXPORT DWORD JXInputGetState(DWORD dwUserIndex, GamepadState* pState);
    JINPUT_EXPORT DWORD JXInputGetBatteryInfo(DWORD dwUserIndex, GamepadBatteryInfo* pBattery);
    JINPUT_EXPORT void JXInputSetState(DWORD dwUserIndex, XINPUT_VIBRATION* vibration);

    JINPUT_EXPORT void JInputGetKeyboardState(BYTE* pState);
    JINPUT_EXPORT bool JInputIsFocused();
}