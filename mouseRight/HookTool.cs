using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace mouseRight
{
    public class HookTool
    {
        private static IntPtr _hookId = IntPtr.Zero;
        private Thread _hookThread;

        public void Start()
        {
            if (_hookThread != null && _hookThread.IsAlive)
                return;

            _hookThread = new Thread(() =>
            {
                _hookId = SetHook(HookCallback);
                System.Windows.Threading.Dispatcher.Run(); // 保持线程消息循环
            })
            {
                IsBackground = true // 设置为后台线程
            };
            _hookThread.Start();
        }

        public void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }

            if (_hookThread != null && _hookThread.IsAlive)
            {
                _hookThread.Abort(); // 终止线程
                _hookThread = null;
            }
        }

        private static IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            Debug.WriteLine($"HookCallback1 nCode = {nCode}, wParam = {wParam}, lParam = {lParam}");
            if (nCode >= 0 && wParam == (IntPtr)WM_RBUTTONDOWN)
            {
                mouseTrack.Clear();
                MouseRightDown = true;
                return (IntPtr)1;
            }
            else if (nCode >= 0 && wParam == (IntPtr)WM_RBUTTONUP)
            {
                MouseRightDown = false;
                //Debug.WriteLine($"mouseTrack length = {mouseTrack.Count}");
                try
                {
                    if (mouseTrack.Count >= 15)
                    {
                        var degree = GetDegrees(mouseTrack[0], mouseTrack[^1]);
                        var track = GetTrack(degree);
                        if (track != MOUSE_TRACK.UNKOWN)
                        {
                            Debug.WriteLine($"mouseTrack degree = {degree}, track = {track}");
                            ExecuteTrace(track);
                            return (IntPtr)1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
                finally
                {
                    mouseTrack.Clear();
                }
            }
            else if (MouseRightDown && nCode >= 0 && wParam == (IntPtr)WM_MOUSEMOVE)
            {
                // 阻止右键事件
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                mouseTrack.Add(hookStruct.pt);
                //Debug.WriteLine($"x={hookStruct.pt.x},y={hookStruct.pt.y}");
            }

            Debug.WriteLine($"HookCallback2 nCode = {nCode}, wParam = {wParam}, lParam = {lParam}");
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private static bool MouseRightDown = false;
        private static List<POINT> mouseTrack = new List<POINT>();

        private static double GetDegrees(POINT start, POINT end)
        {
            double radians = Math.Atan2(end.y - start.y, end.x - start.x);
            double degree = radians * (180 / Math.PI);

            // 调整到 0-360 范围
            if (degree < 0)
            {
                degree += 360;
            }
            return degree;
        }

        private const double deviation = 15;
        private static MOUSE_TRACK GetTrack(double degree)
        {
            if (degree > 360 - deviation || degree < deviation)
            {
                return MOUSE_TRACK.RIGHT;
            }
            else if (degree > 45 - deviation && degree < 45 + deviation)
            {
                return MOUSE_TRACK.RIGHT_DOWN;
            }
            else if (degree > 90 - deviation && degree < 90 + deviation)
            {
                return MOUSE_TRACK.DOWN;
            }
            else if (degree > 135 - deviation && degree < 135 + deviation)
            {
                return MOUSE_TRACK.LEFT_DOWN;
            }
            else if (degree > 180 - deviation && degree < 180 + deviation)
            {
                return MOUSE_TRACK.LEFT;
            }
            else if (degree > 225 - deviation && degree < 225 + deviation)
            {
                return MOUSE_TRACK.LEFT_UP;
            }
            else if (degree > 270 - deviation && degree < 270 + deviation)
            {
                return MOUSE_TRACK.UP;
            }
            else if (degree > 315 - deviation && degree < 315 + deviation)
            {
                return MOUSE_TRACK.RIGHT_UP;
            }
            else
            {
                return MOUSE_TRACK.UNKOWN;
            }
        }

        private enum MOUSE_TRACK
        {
            UNKOWN,
            RIGHT,
            RIGHT_DOWN,
            DOWN,
            LEFT_DOWN,
            LEFT,
            LEFT_UP,
            UP,
            RIGHT_UP
        }

        private static void ExecuteTrace(MOUSE_TRACK track)
        {
            switch (track)
            {
                case MOUSE_TRACK.RIGHT:
                    Debug.WriteLine("Right");
                    SimulateKey(VK_END);
                    break;
                case MOUSE_TRACK.RIGHT_DOWN:
                    Debug.WriteLine("Right Down");
                    break;
                case MOUSE_TRACK.DOWN:
                    Debug.WriteLine("Down");
                    break;
                case MOUSE_TRACK.LEFT_DOWN:
                    Debug.WriteLine("Left Down");
                    break;
                case MOUSE_TRACK.LEFT:
                    Debug.WriteLine("Left");
                    SimulateKey(VK_HOME);
                    break;
                case MOUSE_TRACK.LEFT_UP:
                    Debug.WriteLine("Left Up");
                    break;
                case MOUSE_TRACK.UP:
                    Debug.WriteLine("Up");
                    break;
                case MOUSE_TRACK.RIGHT_UP:
                    Debug.WriteLine("Right Up");
                    break;
                default:
                    Debug.WriteLine("Unkown");
                    break;
            }
        }

        static void SimulateKey(ushort keyCode)
        {
            INPUT input = new INPUT();
            input.type = INPUT_KEYBOARD;
            input.u.ki.wVk = keyCode;
            input.u.ki.dwFlags = KEYEVENTF_KEYUP;
            SendInput(1, ref input, Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(10);
            input.u.ki.dwFlags = KEYEVENTF_KEYDOWN;
            SendInput(1, ref input, Marshal.SizeOf(typeof(INPUT)));
        }

        #region WinAPI Imports
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const int WH_MOUSE_LL = 14;
        const int WM_MOUSEMOVE = 0x0200;
        const int WM_RBUTTONDOWN = 0x0204;
        const int WM_RBUTTONUP = 0x0205;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYDOWN = 0x0000;
        const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);

        // 定义虚拟键码
        const ushort VK_HOME = 0x24;
        const ushort VK_END = 0x23;


        #endregion
    }
}
