using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace BadUI
{
    /// <summary>
    /// Allows all new windows to be suppressed on the calling thread.
    /// </summary>
    public static class WindowBlocker
    {
        private static readonly Dictionary<uint, HookInfo> currentHooks = new Dictionary<uint, HookInfo>();

        /// <summary>
        /// Occurs when the a window is shown on the current thread.
        /// </summary>
        public static event EventHandler<ShowWindowEventArgs> ShowWindow;

        /// <summary>
        /// Disables creation of new windows on the current thread.
        /// </summary>
        public static void DisableWindowCreation()
        {
            lock (currentHooks)
            {
                var threadId = NativeMethods.GetCurrentThreadId();
                if (currentHooks.ContainsKey(threadId))
                    return;

                currentHooks.Add(threadId, new HookInfo(threadId));
            }
        }
        /// <summary>
        /// Enables creation of new windows on the current thread.
        /// </summary>
        public static void EnableWindowCreation()
        {
            lock (currentHooks)
            {
                var threadId = NativeMethods.GetCurrentThreadId();
                HookInfo info;
                if (currentHooks.TryGetValue(threadId, out info))
                {
                    info.Unhook();
                    currentHooks.Remove(threadId);
                }
            }
        }

        /// <summary>
        /// Raises the <see cref="E:ShowWindow"/> event.
        /// </summary>
        /// <param name="e">The <see cref="BadUI.ShowWindowEventArgs"/> instance containing the event data.</param>
        private static void OnShowWindow(ShowWindowEventArgs e)
        {
            var handler = ShowWindow;
            if (handler != null)
                handler(null, e);
        }

        /// <summary>
        /// Contains information about a hook.
        /// </summary>
        private sealed class HookInfo
        {
            private readonly NativeMethods.HookProc hookProc;
            private readonly NativeMethods.WindowHookProc wndHookProc;
            private readonly IntPtr hookHandle;
            private IntPtr nextWindowProc;

            /// <summary>
            /// Initializes a new instance of the <see cref="HookInfo"/> class.
            /// </summary>
            /// <param name="threadId">The hook thread id.</param>
            public HookInfo(uint threadId)
            {
                this.hookProc = new NativeMethods.HookProc(CBTHook);
                this.wndHookProc = new NativeMethods.WindowHookProc(WindowHook);

                var result = NativeMethods.SetWindowsHookEx(NativeMethods.Hook.CBT, hookProc, IntPtr.Zero, threadId);
                if (result == IntPtr.Zero)
                    throw new Win32Exception();

                this.hookHandle = result;
            }

            /// <summary>
            /// Unhooks the hook described by this instance.
            /// </summary>
            public void Unhook()
            {
                NativeMethods.UnhookWindowsHookEx(this.hookHandle);
            }

            /// <summary>
            /// Gets the children of a window handle.
            /// </summary>
            /// <param name="hWndParent">The parent window.</param>
            /// <returns>List of child windows sorted by position from top-left.</returns>
            private static List<ChildWindowInfo> GetChildWindows(IntPtr hWndParent)
            {
                var handles = new List<ChildWindowInfo>();

                NativeMethods.EnumChildProc proc = (w, l) => { handles.Add(new ChildWindowInfo(w)); return true; };
                NativeMethods.EnumChildWindows(hWndParent, proc, IntPtr.Zero);

                handles.Sort();

                return handles;
            }
            /// <summary>
            /// Gets the text associated with a window handle.
            /// </summary>
            /// <param name="hWnd">The window handle.</param>
            /// <returns>String containing the text associated with the handle.</returns>
            private static string GetWindowText(IntPtr hWnd)
            {
                int length = NativeMethods.GetWindowTextLength(hWnd);
                var buffer = new StringBuilder(length + 1);

                NativeMethods.GetWindowText(hWnd, buffer, buffer.Capacity);
                return buffer.ToString();
            }

            /// <summary>
            /// Method invoked on a CBT event.
            /// </summary>
            /// <param name="code">CBT event code.</param>
            /// <param name="wParam">Event parameter 1.</param>
            /// <param name="lParam">Event parameter 2.</param>
            /// <returns>CBT return value.</returns>
            private int CBTHook(int code, IntPtr wParam, IntPtr lParam)
            {
                if (code < 0)
                    return NativeMethods.CallNextHookEx(IntPtr.Zero, 0, wParam, lParam);

                switch ((NativeMethods.CBTCode)code)
                {
                    case NativeMethods.CBTCode.CreateWindow:
                        var createWnd = Marshal.ReadIntPtr(lParam);
                        var createStruct = (NativeMethods.CREATESTRUCT)Marshal.PtrToStructure(createWnd, typeof(NativeMethods.CREATESTRUCT));
                        if (createStruct.hwndParent == IntPtr.Zero)
                            this.nextWindowProc = NativeMethods.SetWindowLongPtr(wParam, -4, Marshal.GetFunctionPointerForDelegate(this.wndHookProc));
                        break;
                }

                return NativeMethods.CallNextHookEx(IntPtr.Zero, 0, wParam, lParam);
            }
            /// <summary>
            /// Hooked window procedure.
            /// </summary>
            /// <param name="hwnd">The hooked window handle.</param>
            /// <param name="msg">The message.</param>
            /// <param name="wParam">Parameter 1.</param>
            /// <param name="lParam">Parameter 2.</param>
            /// <returns>Window message procedure return value.</returns>
            private int WindowHook(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
            {
                switch (msg)
                {
                    case NativeMethods.WM_SHOWWINDOW:
                        var handles = GetChildWindows(hwnd);
                        var text = string.Empty;
                        if(handles.Count > 0)
                            text = handles[0].Text;
                        var args = new ShowWindowEventArgs(GetWindowText(hwnd), text);
                        OnShowWindow(args);
                        if(!args.Allow)
                            NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        break;
                }

                return NativeMethods.CallWindowProc(this.nextWindowProc, hwnd, msg, wParam, lParam);
            }

            /// <summary>
            /// Stores information about a child window.
            /// </summary>
            private sealed class ChildWindowInfo : IComparable<ChildWindowInfo>
            {
                /// <summary>
                /// Initializes a new instance of the <see cref="ChildWindowInfo"/> class.
                /// </summary>
                /// <param name="hWnd">The child window handle.</param>
                public ChildWindowInfo(IntPtr hWnd)
                {
                    this.Text = GetWindowText(hWnd);

                    var rect = new NativeMethods.RECT();
                    NativeMethods.GetWindowRect(hWnd, out rect);
                    this.Position = rect;
                }

                /// <summary>
                /// Gets the window text.
                /// </summary>
                public string Text { get; private set; }
                /// <summary>
                /// Gets the window position and size in screen coordinates.
                /// </summary>
                public NativeMethods.RECT Position { get; private set; }

                /// <summary>
                /// Compare this to another instance.
                /// </summary>
                /// <param name="other">The other instance.</param>
                /// <returns>Relative difference between instances.</returns>
                public int CompareTo(ChildWindowInfo other)
                {
                    if (other == null)
                        return 1;

                    if (this.Position.top != other.Position.top)
                        return this.Position.top.CompareTo(other.Position.top);

                    return this.Position.left.CompareTo(other.Position.left);
                }
            }
        }
    }

    /// <summary>
    /// Contains information about an attempt to show a window.
    /// </summary>
    public sealed class ShowWindowEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ShowWindowEventArgs"/> class.
        /// </summary>
        /// <param name="title">The window title.</param>
        /// <param name="message">The window message.</param>
        public ShowWindowEventArgs(string title, string message)
        {
            this.Title = title;
            this.Message = message;
        }

        /// <summary>
        /// Gets the title of the window.
        /// </summary>
        public string Title { get; private set; }
        /// <summary>
        /// Gets the message displayed in the window.
        /// </summary>
        public string Message { get; private set; }
        /// <summary>
        /// Gets or sets a value indicating whether displaying the window should be permitted.
        /// </summary>
        public bool Allow { get; set; }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("{0}: {1}", this.Title, this.Message);
        }
    }
}
