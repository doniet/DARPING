using System;
using System.Windows.Forms;

namespace DarPing
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var mainForm = new MainForm();
            mainForm.Hide();
            Application.Run(mainForm);
        }
    }
}