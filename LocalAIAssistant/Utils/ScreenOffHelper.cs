using System.Runtime.InteropServices;

namespace LocalAIAssistant.Utils
{
    public static class ScreenOffHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MONITORPOWER = 0xF170;
        private const int HWND_BROADCAST = 0xFFFF;

        [DllImport("user32.dll")]
        private static extern bool BlockInput(bool fBlockIt);

        [DllImport("user32.dll")]
        private static extern int SetThreadExecutionState(int esFlags);

        private const int ES_CONTINUOUS = 0x80000000;
        private const int ES_DISPLAY_REQUIRED = 0x00000002;

        public static void TurnOffScreen()
        {
            SendMessage((IntPtr)HWND_BROADCAST, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)2);
        }

        public static void TurnOnScreen()
        {
            SendMessage((IntPtr)HWND_BROADCAST, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)(-1));
        }

        public static void PreventScreenSaver()
        {
            SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED);
        }

        public static void AllowScreenSaver()
        {
            SetThreadExecutionState(ES_CONTINUOUS);
        }
    }
}
