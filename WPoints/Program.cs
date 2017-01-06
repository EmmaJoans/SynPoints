using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPoints
{
    class Program
    {
        string _serverName;
        string _uid;
        string _pwd;

        string[] _memchached;

        DataWriter writer;
        [STAThread]
        static void Main(string[] args)
        {
            Program pgm = new Program();
            pgm.Run(args);
        }


        private void Run(string[] args)
        {
            _serverName = System.Configuration.ConfigurationManager.AppSettings["server_name"];
            _uid = System.Configuration.ConfigurationManager.AppSettings["uid"];
            _pwd = System.Configuration.ConfigurationManager.AppSettings["pwd"];
            _memchached = System.Configuration.ConfigurationManager.AppSettings["memcached"].Split(',');

            writer = new DataWriter(_serverName, _uid, _pwd, _memchached);
            writer.RunWorkerAsync();
            Console.WriteLine("OK");
            while (true)
            {
                string command = Console.ReadLine();
                string[] commandArr = command.Split(' ');
                string option = commandArr[0];
                switch (option)
                {
                    case "stop":
                        stop();
                        break;
                    case "show":
                        show();
                        break;
                }
            }
        }

        private void stop()
        {
            writer.CancelAsync();
        }

        private void show()
        {
            Console.WriteLine("--------------------------------------------------------------");
            Console.WriteLine("IMEI count has writtened: " + writer.WriteCount);
        }
    }
}
