using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace HotkeyX
{
    public partial class Form1 : Form
    {
        const string Program = @"C:\Users\Inndy\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Ubuntu.lnk";
        const string ProgramName = "mintty";
        const string ProgramTitle = "Ubuntu";

        private HotkeyManager hotkeyManager;
        private IntPtr hook;
        private int LastPid;
        private IntPtr LastHwnd;
        private NativeAPI.WinEventDelegate WinEventHandler;

        public static string GetWindowText(IntPtr hWnd)
        {
            StringBuilder sb = new StringBuilder(1024);
            if (NativeAPI.GetWindowText(hWnd, sb, sb.Capacity) <= 0)
                return null;
            return sb.ToString();
        }

        public Form1()
        {
            InitializeComponent();
            this.hotkeyManager = new HotkeyManager(this.Handle);
        }

        Process StartProgram(ProcessWindowStyle WindowStyle = ProcessWindowStyle.Minimized)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo(Program);
            processStartInfo.WindowStyle = WindowStyle;
            return Process.Start(processStartInfo);
        }

        IntPtr FindProgram()
        {
            if(LastHwnd != IntPtr.Zero)
            {
                if (NativeAPI.GetWindowThreadProcessId(LastHwnd, out int pid) != 0)
                {
                    if (pid == LastPid)
                    {
                        return LastHwnd;
                    }
                }
            }
            return FindWindowBy(WindowTitle => WindowTitle == ProgramTitle);
        }

        IntPtr FindWindowBy(Func<string, bool> Selector)
        {
            for (IntPtr Current = NativeAPI.FindWindowEx(IntPtr.Zero, IntPtr.Zero, null, null);
                Current != IntPtr.Zero;
                Current = NativeAPI.FindWindowEx(IntPtr.Zero, Current, null, null))
            {
                string title = GetWindowText(Current);
                if (title == null)
                    continue;

                if (Selector(title))
                    return Current;
            }
            return IntPtr.Zero;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            this.notifyIcon1.Visible = true;

            this.hotkeyManager.Add(1, NativeAPI.MOD_NOREPEAT | NativeAPI.MOD_WIN | NativeAPI.MOD_ALT, Keys.Space, delegate {
                IntPtr Hwnd = FindProgram();
                if (Hwnd == IntPtr.Zero)
                {
                    StartProgram(ProcessWindowStyle.Normal);
                    return;
                }

                IntPtr Current = NativeAPI.GetForegroundWindow();
                if (Current == Hwnd)
                {
                    NativeAPI.ShowWindow(Hwnd, NativeAPI.SW_MINIMIZE);
                }
                else
                {
                    NativeAPI.ShowWindow(Hwnd, NativeAPI.SW_SHOWNORMAL);
                    NativeAPI.SetForegroundWindow(Hwnd);
                }
            });

            this.hotkeyManager.Add(2, NativeAPI.MOD_NOREPEAT | NativeAPI.MOD_WIN | NativeAPI.MOD_ALT, Keys.N, delegate {
                Process.Start(Program);
            });

            if (!this.hotkeyManager.RegisterHotkeys())
            {
                MessageBox.Show("Failed to register hotkey", "HotkeyX Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }

            if (FindProgram() == IntPtr.Zero)
                StartProgram();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == NativeAPI.WM_HOTKEY)
                this.hotkeyManager.Dispatch((uint)m.WParam);
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            this.Visible = false;
            this.WinEventHandler = new NativeAPI.WinEventDelegate(this.HookProc);
            this.hook = NativeAPI.SetWinEventHook(NativeAPI.EVENT_SYSTEM_FOREGROUND, NativeAPI.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, WinEventHandler, 0, 0, NativeAPI.WINEVENT_OUTOFCONTEXT | NativeAPI.WINEVENT_SKIPOWNPROCESS);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.notifyIcon1.Visible = false;
            this.hotkeyManager.UnregisterHotkeys();
            NativeAPI.UnhookWinEvent(this.hook);
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        void HookProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType != NativeAPI.EVENT_SYSTEM_FOREGROUND ||
                idObject != NativeAPI.OBJID_WINDOW ||
                idChild != NativeAPI.CHILDID_SELF ||
                NativeAPI.GetWindowThreadProcessId(hwnd, out int pid) == 0)
                return;

            try
            {
                if (Process.GetProcessById(pid).ProcessName == ProgramName)
                {
                    this.LastPid = pid;
                    this.LastHwnd = hwnd;
                }
            }
            catch { }
        }

        private void NotifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            this.Visible = !this.Visible;
            this.ShowInTaskbar = this.Visible;
            if(this.Visible)
            {
                this.WindowState = FormWindowState.Normal;
                this.BringToFront();
            }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if(this.WindowState == FormWindowState.Minimized)
            {
                this.Visible = false;
                this.ShowInTaskbar = false;
            }
        }
    }
}
