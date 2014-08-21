using System;
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

        static NotifyIcon _s_icon;
        //the byte[] for the message icon
        static byte[] _s_messageIcon;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);

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

            MenuItem[] items = new MenuItem[2];
            items[1] = new MenuItem("Exit");
            items[1].Click += new EventHandler(Exit_Click);
            items[0] = new MenuItem("About");
            items[0].Click += new EventHandler(About_Click);
            _s_icon.ContextMenu = new ContextMenu(items);
            _s_icon.Visible = true;
            var t = new System.Windows.Forms.Timer();
            t.Interval = 10000;
            t.Tick += t_Tick;
            t.Start();
            Application.Run();
        }

        static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            _s_icon.Dispose();
            Application.Exit();
        }

        static void t_Tick(object sender, EventArgs e)
        {
            //find all the chromes
            var pids = Process.GetProcesses().Where(a => { try { return a.MainModule.ModuleName == "chrome.exe"; } catch { return false; } }).Select(a => (uint)a.Id).ToArray();
            if (pids == null || pids.Length == 0) return;
            //get pointers to the chrome windows (lazy enum)
            var hWnds = GetRootWindowsOfProcesses(pids);
            //find the first window with the text we're looking for
            var hWnd = hWnds.FirstOrDefault(a => GetWindowText(a).Contains("| R2Integrated Slack"));
            if (hWnd == IntPtr.Zero) return;
            //get the icon for the window
            byte[] bs = IconToByteArray(GetWindowIcon(hWnd));
            //if it matches, flash the window
            if (memcmp(bs, _s_messageIcon, bs.Length) == 0)
            {
                FlashWindow(hWnd, true);
                _s_icon.ShowBalloonTip(1000, "Check Slack", "Hey slacker, you have unread messages!", ToolTipIcon.Info);
            }
        }

        static void About_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Causes the windows running slack to flash when you have an unread message.\r\nhttp://github.com/softwarejedi", "Software Jedi Slack Notifier");
        }

        static void Exit_Click(object sender, EventArgs e)
        {
            _s_icon.Dispose();
            Application.Exit();
        }

        const int _s_charCount = 256;
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
