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
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;
using System.Security.Principal;
using System.DirectoryServices.AccountManagement;
using System.Security.Cryptography;

namespace TerminalSummoner
{
    public partial class Form1 : Form
    {
        private HotkeyManager hotkeyManager;
        private IntPtr hook;
        private int LastPid;
        private IntPtr LastHwnd;
        private NativeAPI.WinEventDelegate WinEventHandler;
        private Dictionary<int, Process> ProcessCache;
        private string WindowClassName;

        public Form1()
        {
            InitializeComponent();
            this.hotkeyManager = new HotkeyManager(this.Handle);
            this.ProcessCache = new Dictionary<int, Process>();
            this.WindowClassName = null;
            this.LastHwnd = IntPtr.Zero;

            foreach(var package in new PackageManager().FindPackagesForUser(API.GetCurrentUserSid()))
            {
                if(package.Id.Name == "Microsoft.WindowsTerminal")
                {
                    Debug.WriteLine(string.Format("WindowsTerminal found: {0}", package.Id.FamilyName), "WindowsTerminal");
                    this.txtStartProgarm.Text = string.Format("shell:AppsFolder\\{0}!App", package.Id.FamilyName);
                    this.txtProgramName.Text = "WindowsTerminal";
                    this.WindowClassName = "CASCADIA_HOSTING_WINDOW_CLASS";
                    break;
                }
            }
        }

        Process GetProcessByIdWithCache(int pid)
        {
            try
            {
                if (!ProcessCache.TryGetValue(pid, out Process proc) || proc.HasExited)
                {
                    return ProcessCache[pid] = Process.GetProcessById(pid);
                } else
                {
                    return proc;
                }
            }
            catch (Exception)
            {
                return ProcessCache[pid] = null;
            }
        }

        string GetProcessNameById(int pid)
        {
            Process proc = GetProcessByIdWithCache(pid);
            if (proc == null) return null;
            return proc.ProcessName;
        }

        IntPtr FindProgram()
        {
            if (LastHwnd != IntPtr.Zero && API.GetWindowProcessId(LastHwnd, out int pid) && pid == LastPid)
                return LastHwnd;

            LastHwnd = FindWindowBy(Hwnd =>
                API.GetWindowProcessId(Hwnd, out pid) &&
                GetProcessNameById(pid) == txtProgramName.Text
            );

            return LastHwnd;
        }

        IntPtr FindWindowBy(Func<IntPtr, bool> Selector)
        {
            for (IntPtr Current = NativeAPI.FindWindowEx(IntPtr.Zero, IntPtr.Zero, this.WindowClassName, null);
                Current != IntPtr.Zero;
                Current = NativeAPI.FindWindowEx(IntPtr.Zero, Current, this.WindowClassName, null))
            {
                Debug.WriteLine(string.Format("Found hwnd: {0:x}", Current), "FindWindowBy");
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
                    Debug.WriteLine("Last window not found, start new one", "Toggle");
                    Process.Start(this.txtStartProgarm.Text);
                    return;
                }

                IntPtr Current = NativeAPI.GetForegroundWindow();
                if (Current == Hwnd)
                {
                    Debug.WriteLine("Last window is current foreground window, minimize it", "Toggle");
                    NativeAPI.ShowWindow(Hwnd, NativeAPI.SW_MINIMIZE);
                }
                else
                {
                    Debug.WriteLine("Last window found, bring it to front", "Toggle");
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
                MessageBox.Show("Failed to register hotkey", "TerminalSummoner Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }

            Debug.WriteLine("Hotkey registered", "Init");

            // Start a minimized terminal if there's no any
            if (FindProgram() == IntPtr.Zero)
                Process.Start(this.txtStartProgarm.Text);
            else
                Debug.WriteLine(string.Format("Existing terminal found: {0:x}", this.LastHwnd), "Init");
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
                API.GetWindowProcessId(hwnd, out int pid))
                return;

            Debug.WriteLine(string.Format("Actived window: {0:x}, Pid: {1}", hwnd.ToInt32(), pid), "Hook");

            try
            {
                if (GetProcessNameById(pid) == txtProgramName.Text)
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
