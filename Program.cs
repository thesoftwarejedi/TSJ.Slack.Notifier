﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TSJ.Slack.Notifier
{
    static class Program
    {

        /**
         * Taking the liberty to put this up top, because it's the actual "do something" part
         * This method fires every POLL_INTERVAL milliseconds
         * */
        const short POLL_INTERVAL = 10000;
        static void t_Tick(object sender, EventArgs e)
        {
            if (!Properties.Settings.Default.ShowBubbles && !Properties.Settings.Default.FlashWindow) return;

            //find all the chromes for r2integrated slack
            var pids = Process.GetProcessesByName("chrome").Select(a => (uint)a.Id).ToArray();
            if (pids == null || pids.Length == 0) return;            
            //get pointers to the chrome windows (lazy enum)
            var hWnds = GetRootWindowsOfProcesses(pids);
            //inspect them all
            foreach (var hWnd in hWnds)
            {
                if (!GetWindowText(hWnd).Contains("| R2Integrated Slack")) continue; 
                if (hWnd == IntPtr.Zero) continue;
                if (_s_slackWin == IntPtr.Zero)
                {
                    _s_slackWin = hWnd;
                }
                //get the icon for the window
                byte[] bs = IconToByteArray(GetWindowIcon(hWnd));

                //HACK FIX
                var match = true;
                for (int i = 0; i < bs.Length; i++)
                {
                    if (bs[i] != _s_messageIcon[i])
                    {
                        Debug.Write(i);
                        if (i != 9)
                        {
                            match = false;
                            break;
                        }
                    }
                }

                //if it matches, flash the window
                //if (memcmp(bs, _s_messageIcon, bs.Length) == 0)
                if (match)
                {
                    _s_slackWin = hWnd;
                    if (Properties.Settings.Default.FlashWindow)
                    {
                        FlashWindow(hWnd, true);
                    }
                    if (Properties.Settings.Default.ShowBubbles)
                    {
                        _s_icon.ShowBalloonTip(1000, "Check Slack", "Hey slacker, you have unread messages!", ToolTipIcon.Info);
                    }
                }
            }
        }

        static NotifyIcon _s_icon;
        //the byte[] for the message icon
        static byte[] _s_messageIcon;
        //save the slack window for setting active
        static IntPtr _s_slackWin;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
            Application.ApplicationExit += Application_ApplicationExit;

            _s_icon = new NotifyIcon();
            //load the icon
            using (Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("TSJ.Slack.Notifier.Slack alt.ico"))
            {
                _s_icon.Icon = new Icon(s);
            }
            //load the icon
            using (Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("TSJ.Slack.Notifier.slack_message.ico"))
            {
                _s_messageIcon = IconToByteArray(new Icon(s));
            }

            MenuItem[] items = new MenuItem[3];
            items[2] = new MenuItem("Exit");
            items[2].Click += Exit_Click;
            items[1] = new MenuItem("About");
            items[1].Click += About_Click;
            items[0] = new MenuItem("Settings");
            items[0].Click += Settings_Click;
            _s_icon.ContextMenu = new ContextMenu(items);
            _s_icon.Visible = true;
            _s_icon.DoubleClick += _s_icon_DoubleClick;
            var t = new System.Windows.Forms.Timer();
            t.Interval = POLL_INTERVAL;
            t.Tick += t_Tick;
            t.Start();
            Application.Run();
        }

        private static void Settings_Click(object sender, EventArgs e)
        {
            new SettingsForm().ShowDialog();
        }

        private static void Application_ApplicationExit(object sender, EventArgs e)
        {
            Properties.Settings.Default.Save();
        }

        static void _s_icon_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                ShowWindow(_s_slackWin, SW.SHOWDEFAULT);
                SetForegroundWindow(_s_slackWin);
            }
            catch { }
        }

        static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            _s_icon.Dispose();
            Application.Exit();
        }

        static void About_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/thesoftwarejedi/TSJ.Slack.Notifier");
        }

        static void Exit_Click(object sender, EventArgs e)
        {
            _s_icon.Dispose();
            Application.Exit();
        }

        /**
         * These helpers use the pinvoke to do fun stuff
         * */

        const int _s_charCount = 512;
        static string GetWindowText(IntPtr a)
        {
            var sb = new StringBuilder(_s_charCount);
            GetWindowText(a, sb, _s_charCount);
            return sb.ToString();
        }

        static IEnumerable<IntPtr> GetRootWindowsOfProcesses(uint[] pids)
        {
            List<IntPtr> rootWindows = GetChildWindows(IntPtr.Zero);
            foreach (IntPtr hWnd in rootWindows)
            {
                uint lpdwProcessId;
                GetWindowThreadProcessId(hWnd, out lpdwProcessId);
                if (pids.Contains(lpdwProcessId)) yield return hWnd;
            }
        }

        static List<IntPtr> GetChildWindows(IntPtr parent)
        {
            List<IntPtr> result = new List<IntPtr>();
            GCHandle listHandle = GCHandle.Alloc(result); //get a pointer to the GC heap for the list
            try
            {
                Win32Callback childProc = new Win32Callback(EnumWindow);
                EnumChildWindows(parent, childProc, GCHandle.ToIntPtr(listHandle)); //pass the list pointer for our delegate
            }
            finally
            {
                if (listHandle.IsAllocated)
                    listHandle.Free();
            }
            return result;
        }

        static bool EnumWindow(IntPtr handle, IntPtr pointer)
        {
            GCHandle gch = GCHandle.FromIntPtr(pointer); //lookup the pointer in the heap
            List<IntPtr> list = gch.Target as List<IntPtr>;
            list.Add(handle);
            return true;
        }

        static Icon GetWindowIcon(IntPtr hwnd)
        {
            IntPtr iconHandle = SendMessage(hwnd, WM_GETICON, ICON_SMALL2, 0);
            if (iconHandle == IntPtr.Zero)
                iconHandle = SendMessage(hwnd, WM_GETICON, ICON_SMALL, 0);
            if (iconHandle == IntPtr.Zero)
                iconHandle = SendMessage(hwnd, WM_GETICON, ICON_BIG, 0);
            if (iconHandle == IntPtr.Zero)
                iconHandle = GetClassLongPtr(hwnd, GCL_HICON);
            if (iconHandle == IntPtr.Zero)
                iconHandle = GetClassLongPtr(hwnd, GCL_HICONSM);

            if (iconHandle == IntPtr.Zero)
                return null;

            Icon icn = Icon.FromHandle(iconHandle);

            return icn;
        }

        static byte[] IconToByteArray(Icon i)
        {
            byte[] byteArray;
            using (MemoryStream stream = new MemoryStream())
            {
                i.Save(stream);
                byteArray = stream.ToArray();
            }
            return byteArray;
        }

        /***
         * 
         *  All the pinvoke goodness, standard user32 stuff
         *  straight from pinvoke.net.  too lazy to even install 
         *  the vs plugin.
         *  Also threw in memcmp for the byte array.  Just want it to not
         *  have a shot in hell at clogging up resources doing the simple
         *  byte array comparison
         * 
         * */

        [DllImport("user32.dll")]
        public static extern IntPtr SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, SW cmd);
        public enum SW
        {
            HIDE = 0,
            SHOWNORMAL = 1,
            SHOWMINIMIZED = 2,
            SHOWMAXIMIZED = 3,
            SHOWNOACTIVATE = 4,
            SHOW = 5,
            MINIMIZE = 6,
            SHOWMINNOACTIVE = 7,
            SHOWNA = 8,
            RESTORE = 9,
            SHOWDEFAULT = 10
        }

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        delegate bool Win32Callback(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.Dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumChildWindows(IntPtr parentHandle, Win32Callback callback, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

        //here's an oldy but goody!
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);

        public const int GCL_HICONSM = -34;
        public const int GCL_HICON = -14;

        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;
        public const int ICON_SMALL2 = 2;

        public const int WM_GETICON = 0x7F;

        public static IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size > 4)
                return GetClassLongPtr64(hWnd, nIndex);
            else
                return new IntPtr(GetClassLongPtr32(hWnd, nIndex));
        }

        [DllImport("user32.dll", EntryPoint = "GetClassLong")]
        public static extern uint GetClassLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtr")]
        public static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    }
}
