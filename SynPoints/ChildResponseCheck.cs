using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SynPoints
{

    public class ChildResponseCheck : BackgroundWorker
    {

        private Process _rp, _wp;
        public event EventHandler ShowProgress;

        public ChildResponseCheck(Process rp, Process wp)
        {
            _rp = rp;
            _wp = wp;
            this.WorkerSupportsCancellation = true;

        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            base.OnDoWork(e);
            while (true)
            {
                if (CancellationPending)
                {
                    return;
                }
                System.IO.StreamReader rop = _rp.StandardOutput;
                if (!rop.EndOfStream)
                {
                    string command = rop.ReadLine();
                  
                    if (command == "show")
                    {
                        // ShowProgress(null, null);
                        //Stream inputStream = Console.OpenStandardInput();
                        //inputStream.Read(Encoding.ASCII.GetBytes("show"), 0, 4);
                        //string a = Console.ReadLine();
                        //SendKeys.SendWait("{s}");
                        //SendKeys.SendWait("{h}");
                        //SendKeys.SendWait("{o}");
                        //SendKeys.SendWait("{w}");
                        //SendKeys.SendWait("{ENTER}");
                        SendKeys.SendWait("show\n");
                    }
                    else
                    {
                        command = command + "\n" + rop.ReadLine();
                        command = command + "\n" + rop.ReadLine();
                        Console.WriteLine(command + "\n");
                    }

                }

                System.IO.StreamReader wop = _wp.StandardOutput;
                if (!wop.EndOfStream)
                {
                    string command = wop.ReadLine();
                    command = command + "\n" + wop.ReadLine();
                    Console.WriteLine(command + "\n");

                }
            }
        }
    }
}
