using System;
using System.Runtime.InteropServices;

namespace LastFolder.Native
{
    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

        [DllImport("shcore.dll")]
        internal static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

        internal const int ASFW_ANY = -1;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;

        /// <summary>
        /// Brings the window to the front, working around Windows' "foreground lock" restriction.
        /// Since the shortcut is caught by a global hook, a plain Activate() is not always enough.
        /// </summary>
        internal static void ForceForeground(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            IntPtr foreground = GetForegroundWindow();
            if (foreground == hwnd) return;

            uint myThread = GetCurrentThreadId();
            uint fgThread = foreground != IntPtr.Zero ? GetWindowThreadProcessId(foreground, out _) : 0;

            if (fgThread != 0 && fgThread != myThread)
            {
                AttachThreadInput(myThread, fgThread, true);
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
                AttachThreadInput(myThread, fgThread, false);
            }
            else
            {
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
            }
        }

        /// <summary>Scale factor of the monitor under the mouse cursor (1.0 = 100%).</summary>
        internal static double GetScaleAt(POINT pt)
        {
            try
            {
                IntPtr monitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0)
                    return dpiX / 96.0;
            }
            catch
            {
                // no shcore.dll (very old Windows): default scale
            }
            return 1.0;
        }
    }
}
