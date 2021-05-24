using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetDesktopWinForms
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            IntPtr childAppHandle = args.Length > 0 ? (IntPtr)long.Parse(args[0]) : IntPtr.Zero;
            Application.Run(new Form1(childAppHandle));
        }
    }
}
