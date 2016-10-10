using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Podkolzin.GlobalShortcuts
{
    public class Shortcut : IDisposable
    {
        private class KeyEventArgs
        {
            public Key Key { get; private set; }
            public bool StopPropagation { get; set; }

            public KeyEventArgs(Key key)
            {
                Key = key;
            }
        }

        private static class GlobalShortcuts
        {
            private const int WH_KEYBOARD_LL = 13;
            private const int WM_KEYDOWN = 0x0100;
            private const int WM_KEYUP = 0x0101;
            private static LowLevelKeyboardProc _proc = HookCallback;
            private static IntPtr _hookID = IntPtr.Zero;

            static GlobalShortcuts()
            {
                SetHook();
            }

            private static IntPtr SetHook()
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    return SetWindowsHookEx(WH_KEYBOARD_LL, _proc,
                        GetModuleHandle(curModule.ModuleName), 0);
                }
            }

            private delegate IntPtr LowLevelKeyboardProc(
                int nCode, IntPtr wParam, IntPtr lParam);

            private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
            {
                KeyEventArgs args = null;
                if (nCode >= 0)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    if (wParam == (IntPtr)WM_KEYDOWN)
                        KeyDown?.Invoke(null, args = new KeyEventArgs(KeyInterop.KeyFromVirtualKey(vkCode)));
                    else if (wParam == (IntPtr)WM_KEYUP)
                        KeyUp?.Invoke(null, args = new KeyEventArgs(KeyInterop.KeyFromVirtualKey(vkCode)));
                }
                return (args != null && args.StopPropagation) ? (IntPtr)1 : CallNextHookEx(_hookID, nCode, wParam, lParam);
            }

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr SetWindowsHookEx(int idHook,
                LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
                IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr GetModuleHandle(string lpModuleName);

            public static event EventHandler<KeyEventArgs> KeyDown;
            public static event EventHandler<KeyEventArgs> KeyUp;
        }

        private Key[] keys;
        private bool[] state;

        public Shortcut(params Key[] keys)
        {
            this.keys = keys;
            this.state = new bool[keys.Length];
            GlobalShortcuts.KeyDown += GlobalShortcuts_KeyDown;
            GlobalShortcuts.KeyUp += GlobalShortcuts_KeyUp;
        }

        public void Dispose()
        {
            GlobalShortcuts.KeyDown -= GlobalShortcuts_KeyDown;
            GlobalShortcuts.KeyUp -= GlobalShortcuts_KeyUp;
        }

        private void GlobalShortcuts_KeyUp(object sender, KeyEventArgs e)
        {
            int index = Array.IndexOf(keys, e.Key);
            if (index >= 0)
            {
                if (state.All(f => f))
                    Fired?.Invoke(this, EventArgs.Empty);
                state[index] = false;
            }
                
        }

        private void GlobalShortcuts_KeyDown(object sender, KeyEventArgs e)
        {
            int index = Array.IndexOf(keys, e.Key);
            if (index >= 0)
                state[index] = true;
        }

        public event EventHandler Fired;
    }
}
