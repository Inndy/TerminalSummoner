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
using Microsoft.Win32;

namespace HotkeyX
{
    public partial class Form1 : Form
    {
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
            foreach (var KeyName in Registry.ClassesRoot.OpenSubKey(@"Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Families").GetSubKeyNames())
            {
                if (KeyName.StartsWith("Microsoft.WindowsTerminal_"))
                {
                    this.txtStartProgarm.Text = string.Format("shell:AppsFolder\\{0}!App", KeyName);
                    this.txtProgramName.Text = "WindowsTerminal";
                    break;
                }
            }
        }

        Process StartProgram(string Program, ProcessWindowStyle WindowStyle = ProcessWindowStyle.Minimized)
        {
            return Process.Start(new ProcessStartInfo(Program)
            {
                WindowStyle = WindowStyle
            });
        }

        IntPtr FindProgram()
        {

            if (LastHwnd != IntPtr.Zero &&
                NativeAPI.GetWindowThreadProcessId(LastHwnd, out int pid) != 0 &&
                pid == LastPid
                )
            {
                return LastHwnd;
            }

            return FindWindowBy(Hwnd =>
                NativeAPI.GetWindowThreadProcessId(Hwnd, out pid) != 0 &&
                Process.GetProcessById(pid).ProcessName == txtProgramName.Text
            );
        }

        IntPtr FindWindowBy(Func<IntPtr, bool> Selector)
        {
            for (IntPtr Current = NativeAPI.FindWindowEx(IntPtr.Zero, IntPtr.Zero, null, null);
                Current != IntPtr.Zero;
                Current = NativeAPI.FindWindowEx(IntPtr.Zero, Current, null, null))
            {
                if (Selector(Current))
                    return Current;
            }
            return IntPtr.Zero;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            this.notifyIcon1.Visible = true;

            // Win-Alt-Space: Toggle existed terminal or start new one
            this.hotkeyManager.Add(1, NativeAPI.MOD_NOREPEAT | NativeAPI.MOD_WIN | NativeAPI.MOD_ALT, Keys.Space, delegate {
                IntPtr Hwnd = FindProgram();
                if (Hwnd == IntPtr.Zero)
                {
                    Debug.WriteLine("[Toggle] Last window not found, start new one");
                    StartProgram(this.txtStartProgarm.Text, ProcessWindowStyle.Normal);
                    return;
                }

                IntPtr Current = NativeAPI.GetForegroundWindow();
                if (Current == Hwnd)
                {
                    Debug.WriteLine("[Toggle] Last window is current foreground window, minimize it");
                    NativeAPI.ShowWindow(Hwnd, NativeAPI.SW_MINIMIZE);
                }
                else
                {
                    Debug.WriteLine("[Toggle] Last window found, bring it to front");
                    NativeAPI.ShowWindow(Hwnd, NativeAPI.SW_SHOWNORMAL);
                    NativeAPI.SetForegroundWindow(Hwnd);
                }
            });

            // Win-Alt-N: Start a new terminal
            this.hotkeyManager.Add(2, NativeAPI.MOD_NOREPEAT | NativeAPI.MOD_WIN | NativeAPI.MOD_ALT, Keys.N, delegate {
                Process.Start(this.txtStartProgarm.Text);
            });

            if (!this.hotkeyManager.RegisterHotkeys())
            {
                MessageBox.Show("Failed to register hotkey", "HotkeyX Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }

            // Start a minimized terminal if there's no any
            if (FindProgram() == IntPtr.Zero)
                StartProgram(this.txtStartProgarm.Text);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeAPI.WM_HOTKEY)
                this.hotkeyManager.Dispatch((uint)m.WParam);
            else
                base.WndProc(ref m);
        }

        // Native form just created, register global event hook
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

        // The global hook handler, we use this to record last actived terminal window
        void HookProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType != NativeAPI.EVENT_SYSTEM_FOREGROUND ||
                idObject != NativeAPI.OBJID_WINDOW ||
                idChild != NativeAPI.CHILDID_SELF ||
                NativeAPI.GetWindowThreadProcessId(hwnd, out int pid) == 0)
                return;

            Debug.WriteLine(string.Format("[Hook] Actived window: {0:x}, Pid: {1}", hwnd.ToInt32(), pid));

            try
            {
                if (Process.GetProcessById(pid).ProcessName == txtProgramName.Text)
                {
                    this.LastPid = pid;
                    this.LastHwnd = hwnd;
                }
            }
            catch { }
        }

        private void NotifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            return; // this may break hotkey, I don't know why :(
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
