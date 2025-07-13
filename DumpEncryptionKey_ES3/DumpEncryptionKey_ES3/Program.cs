using System;
using System.Windows.Forms;

namespace KeyFinder
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();   // VS‑шаблон WinForms запускает DPI/visual‑styles
            Application.Run(new Form1());
        }
    }
}
