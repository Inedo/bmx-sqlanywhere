using System;
using System.Runtime.InteropServices;
using System.Text;

namespace BadUI
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr SetWindowsHookEx(Hook idHook, [MarshalAs(UnmanagedType.FunctionPtr)] HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)]
        public static extern int CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        public static extern uint GetCurrentThreadId();

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhook);

        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            // It appears that on Windows x86 SetWindowLongPtr is a macro that 
            // expands to SetWindowLong. Only on x64 Windows does this function exist.
            return (IntPtr.Size == 4)
                ? _SetWindowLong(hWnd, nIndex, dwNewLong)
                : _SetWindowLongPtr(hWnd, nIndex, dwNewLong);
        }

        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi, EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr _SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi, EntryPoint = "SetWindowLong")]
        public static extern IntPtr _SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)]
        public static extern int CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)]
        public static extern bool EnumChildWindows(IntPtr hWndParent, [MarshalAs(UnmanagedType.FunctionPtr)] EnumChildProc lpEnumFunc, IntPtr lParam);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int HookProc(int code, IntPtr wParam, IntPtr lParam);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int WindowHookProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [return: MarshalAs(UnmanagedType.Bool)]
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        public const uint WM_CLOSE = 0x0010;
        public const uint WM_SHOWWINDOW = 0x0018;

        public enum Hook
        {
            CallWndProc = 4,
            CallWndProcRet = 12,
            CBT = 5,
            Debug = 9,
            ForegroundIdle = 11,
            GetMessage = 3,
            JournalPlayback = 1,
            JournalRecord = 0,
            Keyboard = 2,
            KeyboardLowLevel = 13,
            Mouse = 7,
            MouseLowLevel = 14,
            MsgFilter = -1,
            Shell = 10,
            SysMsgFilter = 6
        }

        public enum CBTCode
        {
            Activate = 5,
            ClickSkipped = 6,
            CreateWindow = 3,
            DestroyWindow = 4,
            KeySkipped = 7,
            MinMax = 1,
            MoveSize = 0,
            QueueSync = 2,
            SetFocus = 9,
            SysCommand = 8
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CREATESTRUCT
        {
            public IntPtr lpCreateParams;
            public IntPtr hInstance;
            public IntPtr hMenu;
            public IntPtr hwndParent;
            public int cy;
            public int cx;
            public int y;
            public int x;
            public int style;
            public IntPtr lpszName;
            public IntPtr lpszClass;
            public uint dwExStyle;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
    }
}
