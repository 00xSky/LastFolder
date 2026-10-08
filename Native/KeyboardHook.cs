using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using LastFolder.Helpers;

namespace LastFolder.Native
{
    /// <summary>
    /// Global low-level keyboard hook.
    /// WH_KEYBOARD_LL is used because RegisterHotKey cannot tell left and right Ctrl apart.
    /// Only "Left Ctrl + Space" (with no other modifier held) is captured and swallowed.
    /// </summary>
    internal sealed class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_SPACE = 0x20;
        private const int VK_SHIFT = 0x10;
        private const int VK_MENU = 0x12;     // Alt (also filters out AltGr, since AltGr = LCtrl + RAlt)
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        // Kept in a field so the delegate is not collected by the GC.
        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hook;
        private bool _leftCtrlDown;
        private bool _spaceSwallowed;

        public event Action? HotkeyPressed;

        public KeyboardHook()
        {
            _proc = HookCallback;
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), Strings.HookFailed);
        }

        private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int msg = wParam.ToInt32();
                bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

                if (data.vkCode == VK_LCONTROL)
                {
                    if (isDown) _leftCtrlDown = true;
                    else if (isUp) _leftCtrlDown = false;
                }
                else if (data.vkCode == VK_SPACE)
                {
                    if (isDown)
                    {
                        // If the key is held, swallow the repeats too, but don't trigger again.
                        if (_spaceSwallowed) return (IntPtr)1;

                        bool leftCtrl = _leftCtrlDown && IsDown(VK_LCONTROL);
                        bool otherModifiers = IsDown(VK_RCONTROL) || IsDown(VK_SHIFT) || IsDown(VK_MENU)
                                              || IsDown(VK_LWIN) || IsDown(VK_RWIN);

                        if (leftCtrl && !otherModifiers)
                        {
                            _spaceSwallowed = true;
                            HotkeyPressed?.Invoke();
                            return (IntPtr)1;
                        }
                    }
                    else if (isUp && _spaceSwallowed)
                    {
                        _spaceSwallowed = false;
                        return (IntPtr)1;
                    }
                }
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }
    }
}
