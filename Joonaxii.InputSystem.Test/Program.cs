
using Joonaxii.JInput.Windows;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Joonaxii.JInput.Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            InputCore core = new InputCore(new WindowsInputProvider());
            DeviceIndex devices = DeviceIndex.One | DeviceIndex.Two | DeviceIndex.Three | DeviceIndex.Four | DeviceIndex.Keyboard;
            Console.WriteLine($"Checking: [{devices}]");
            char[] temp = new char[32];
            for (int i = 0; i < temp.Length; i++)
            {
                temp[i] = ' ';
            }
            int yP = Console.CursorTop;
            InputState state;
            InputCode[] ignored = new InputCode[] { InputCode.StickLX, InputCode.StickLY, InputCode.StickRX, InputCode.StickRY, };
            bool written = true;

            unsafe
            {
                uint* keyboard = stackalloc uint[8] { 0, 0, 0, 0, 0, 0, 0, 0 };
                char[] mask = new char[32];
                long focusedFor = 0;
                long unfocusedFor = 0;
                while (true)
                {
                    int yyP = yP;
                    core.Update();
                    core.GetKeyboardStateMap(keyboard);

                    for (int i = 0; i < 8; i++)
                    {
                        uint maskUI = keyboard[i];

                        for (uint j = 0, k = 1; j < 32; j++, k <<= 1)
                        {
                            mask[j] = ((maskUI & k) != 0 ? '#' : '.');
                        }
                        Console.SetCursorPosition(0, yyP);
                        Console.Out.Write(mask);
                        ++yyP;
                    }

                    if (core.HasFocus)
                    {
                        ++focusedFor;
                    }
                    else
                    {
                        ++unfocusedFor;
                    }

                    ++yyP;

                    Console.SetCursorPosition(0, yyP);
                    Console.Write($"Focused for. {focusedFor, -16} ticks..");
                    ++yyP;
                    Console.SetCursorPosition(0, yyP);
                    Console.Write($"Unfocused for. {unfocusedFor, -16} ticks..");

                    ++yyP;

                    for (int i = 0; i < 4; i++)
                    {
                        if (core.GetBatteryInfo(i, out var battery))
                        {
                            Console.SetCursorPosition(0, yyP);
                            Console.Write($"Controller '{i}' {battery.Type}, {battery.Level}".PadRight(48, ' '));
                            written = true;
                        }
                        else
                        {
                            Console.SetCursorPosition(0, yyP);
                            Console.Write($"Controller '{i}' Disconnected...".PadRight(48, ' '));
                        }
                        ++yyP;
                    }
                    ++yyP;

                    //if (core.AnyToggled(devices, out var res, ignored))
                    //{
                    //    Console.SetCursorPosition(0, yyP);
                    //    Console.Write($"Result: {res.device} [{res.code}, {res.tick}]".PadRight(32, ' '));
                    //    written = true;
                    //}
                    //else
                    //{
                    //    if (written)
                    //    {
                    //        Console.SetCursorPosition(0, yyP);
                    //        Console.Write($"".PadRight(32, ' '));
                    //        written = false;
                    //    }
                    //}

                    //++yyP;

                    //int xx = 0;
                    //for (int j = 0; j < 2; j++)
                    //{
                    //    DeviceIndex dev = (DeviceIndex)(1 << (j + 1));
                    //    int yy = yyP;
                    //    Console.SetCursorPosition(xx, yy++);
                    //    Console.Write($"Controller: {dev}");

                    //    Console.SetCursorPosition(xx, yy++);
                    //    Console.Write("[Controller Buttons]");
                    //    for (int i = (int)InputCode.DPadUp; i < (int)InputCode.TriggerL; i++)
                    //    {
                    //        state = core.GetDeviceInputState(dev, (InputCode)i);
                    //        Console.SetCursorPosition(xx, yy++);
                    //        Console.Write($"{(InputCode)i,-14}, {$"{state.Value:0.###}",-6} |{state.IsHeld,-5}|".PadRight(32, ' '));
                    //    }
                    //    yy++;

                    //    Console.SetCursorPosition(xx, yy++);
                    //    Console.Write("[Controller Axes]");
                    //    for (int i = (int)InputCode.TriggerL; i < (int)InputCode.PressTriggerL; i++)
                    //    {
                    //        state = core.GetDeviceInputState(dev, (InputCode)i);
                    //        Console.SetCursorPosition(xx, yy++);
                    //        Console.Write($"{(InputCode)i,-14}, {$"{state.Value:0.###}",-6} |{state.IsHeld,-5}|".PadRight(32, ' '));
                    //    }
                    //    yy++;

                    //    Console.SetCursorPosition(xx, yy++);
                    //    Console.Write("[Controller Axis Buttons]");
                    //    for (int i = (int)InputCode.PressTriggerL; i < (int)InputCode.KeyCount; i++)
                    //    {
                    //        state = core.GetDeviceInputState(dev, (InputCode)i);
                    //        Console.SetCursorPosition(xx, yy++);
                    //        Console.Write($"{(InputCode)i,-14}, {$"{state.Value:0.###}",-6} |{state.IsHeld,-5}|".PadRight(32, ' '));
                    //    }
                    //    yy++;
                    //    xx += 36;
                    //}
                }
            }
        }
    }
}
