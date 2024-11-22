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

            if (nCode >= 0 && wParam == (IntPtr)WM_RBUTTONDOWN)
            {
                mouseTrack.Clear();
                MouseRightDown = true;
            }
            if (nCode >= 0 && wParam == (IntPtr)WM_RBUTTONUP)
            {
                MouseRightDown = false;
                Debug.WriteLine($"mouseTrack length = {mouseTrack.Count}");
                if (mouseTrack.Count >= 15)
                {
                    var degree = GetDegrees(mouseTrack[0], mouseTrack[^1]);
                    var track = GetTrack(degree);
                    Debug.WriteLine($"mouseTrack degree = {degree}, track = {track}");
                }
                return (IntPtr)1;
            }
            if (MouseRightDown && nCode >= 0 && wParam == (IntPtr)WM_MOUSEMOVE)
            {
                // 阻止右键事件
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                mouseTrack.Add(hookStruct.pt);
                //Debug.WriteLine($"x={hookStruct.pt.x},y={hookStruct.pt.y}");
            }

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

        private static MOUSE_TRACK GetTrack(double degree)
        {
            if (degree > 360 - 7.5 || degree < 7.5)
            {
                return MOUSE_TRACK.RIGHT;
            }
            else if (degree > 45 - 7.5 && degree < 45 + 7.5)
            {
                return MOUSE_TRACK.RIGHT_DOWN;
            }
            else if (degree > 90 - 7.5 && degree < 90 + 7.5)
            {
                return MOUSE_TRACK.DOWN;
            }
            else if (degree > 135 - 7.5 && degree < 135 + 7.5)
            {
                return MOUSE_TRACK.LEFT_DOWN;
            }
            else if (degree > 180 - 7.5 && degree < 180 + 7.5)
            {
                return MOUSE_TRACK.LEFT;
            }
            else if (degree > 225 - 7.5 && degree < 225 + 7.5)
            {
                return MOUSE_TRACK.LEFT_UP;
            }
            else if (degree > 270 - 7.5 && degree < 270 + 7.5)
            {
                return MOUSE_TRACK.UP;
            }
            else if (degree > 315 - 7.5 && degree < 315 + 7.5)
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

        #endregion
    }
}
