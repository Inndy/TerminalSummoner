using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HotkeyX
{
    public class Hotkey
    {
        private bool _Enabled;
        public bool Enabled {
            get => _Enabled;
            private set => _Enabled = value;
        }

        public readonly IntPtr Hwnd;
        public readonly uint Id;
        public readonly uint Mod;
        public readonly Keys Key;
        public readonly Action Action;

        public Hotkey(IntPtr hwnd, uint id, uint mod, Keys key, Action action)
        {
            this.Hwnd = hwnd;
            this.Id = id;
            this.Mod = mod;
            this.Key = key;
            this.Action = action;
        }

        public bool Enable()
        {
            if(!this.Enabled && NativeAPI.RegisterHotKey(this.Hwnd, this.Id, this.Mod, (uint)this.Key)) {
                this.Enabled = true;
            }

            return this.Enabled;
        }

        public bool Disable()
        {
           if(this.Enabled && NativeAPI.UnregisterHotKey(this.Hwnd, this.Id))
            {
                this.Enabled = false;
            }

            return this.Enabled == false;
        }
    }

    public class HotkeyManager
    {

        private readonly List<Hotkey> Hotkeys;
        private readonly Dictionary<uint, Hotkey> HotkeysCollection;
        private readonly IntPtr Hwnd;

        public HotkeyManager(IntPtr hwnd)
        {
            this.Hotkeys = new List<Hotkey>();
            this.HotkeysCollection = new Dictionary<uint, Hotkey>();
            this.Hwnd = hwnd;
        }

        public void Add(uint id, uint mod, Keys key, Action action)
        {
            var hotkey = new Hotkey(this.Hwnd, id, mod, key, action);
            this.Hotkeys.Add(hotkey);
            this.HotkeysCollection[hotkey.Id] = hotkey;
        }

        public void Dispatch(uint id)
        {
            this.HotkeysCollection[id].Action();
        }

        public bool RegisterHotkeys()
        {
            foreach (var hotkey in Hotkeys)
                if (!hotkey.Enable())
                    return false;

            return true;
        }

        public bool UnregisterHotkeys()
        {
            foreach (var hotkey in Hotkeys)
                if (!hotkey.Disable())
                    return false;

            return true;
        }
    }
}
