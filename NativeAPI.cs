using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace HotkeyX
{
    class NativeAPI
    {
        public const uint WM_HOTKEY = 0x312;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        public const uint SW_SHOWNORMAL = 1;
        public const uint SW_SHOW = 5;
        public const uint SW_MINIMIZE = 6;
        public const uint SW_SHOWNA = 8;
        public const uint SW_RESTORE = 9;

        public const uint WINEVENT_OUTOFCONTEXT = 0x0000; // Events are ASYNC
        public const uint WINEVENT_SKIPOWNPROCESS = 0x0002; // Don't call back for events on installer's process
        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint OBJID_WINDOW = 0;
        public const uint CHILDID_SELF = 0;

        public const int TOKEN_QUERY = 0X00000008;

        public const int ERROR_NO_MORE_ITEMS = 259;

        public enum TOKEN_INFORMATION_CLASS
        {
            TokenUser = 1,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics,
            TokenRestrictedSids,
            TokenSessionId
        }


        ////////////////////////////////////////////////////////////////////////////////

        public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        ////////////////////////////////////////////////////////////////////////////////

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate pfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, uint id, uint fsModifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, uint id);

        [DllImport("advapi32")]
        public static extern bool OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, out IntPtr TokenHandle);

        [DllImport("kernel32")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32", CharSet = CharSet.Auto)]
        public static extern bool GetTokenInformation(IntPtr hToken, TOKEN_INFORMATION_CLASS tokenInfoClass, IntPtr TokenInformation, int tokeInfoLength, ref int reqLength);

        [DllImport("kernel32")]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32", CharSet = CharSet.Auto)]
        public static extern bool ConvertSidToStringSid(IntPtr pSID, [In, Out, MarshalAs(UnmanagedType.LPTStr)] ref string pStringSid);
    }

    class API {
        public static string GetWindowText(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(NativeAPI.GetWindowTextLength(hWnd));
            if (NativeAPI.GetWindowText(hWnd, sb, sb.Capacity) <= 0)
                return null;

            return sb.ToString();
        }

        public static bool GetWindowProcessId(IntPtr hWnd, out int pid)
        {
            if(NativeAPI.GetWindowThreadProcessId(hWnd, out pid) > 0)
            {
                return true;
            }

            return false;
        }

        public static string GetCurrentUserSid()
        {
            if(!NativeAPI.OpenProcessToken(NativeAPI.GetCurrentProcess(), NativeAPI.TOKEN_QUERY, out IntPtr TokenHandle))
            {
                return null;
            }

            var winId = new System.Security.Principal.WindowsIdentity(TokenHandle);
            string sid = winId.User.ToString();

            NativeAPI.CloseHandle(TokenHandle);

            return sid;
        }
    }
}
