using System;
using System.Windows.Forms;

namespace RoRPC
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.Run(new AppContext());
        }
    }
}
