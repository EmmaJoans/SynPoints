using Lunz.Services.CLog;
using PISDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RPoints
{
    class Program
    {
        private int _currentPSR = 0;
        private int _percent = 0;
        private string _psr = string.Empty;

        DataSender dataSender = null;

        [STAThread]
        static void Main(string[] args)
        {
            Program program = new Program();
         
            program.Run(args);
        }

        /// <summary>
        /// 运行程序
        /// </summary>
        /// <param name="args">命令行参数</param>
        private void Run(string[] args)
        {
            List<PointSourceRange> PSRList = new List<PointSourceRange>();
   
            try
            {
                LogUtils.log4netConfig(LogUtils.DEFAULTFILENAME_LOGMATHERCONFIG, "RPoints.config", "RPoints.log");
                string serverName = System.Configuration.ConfigurationManager.AppSettings["server_name"];
                string uid = System.Configuration.ConfigurationManager.AppSettings["uid"];
                string pwd = System.Configuration.ConfigurationManager.AppSettings["pwd"];
               

                while (true)
                {
                    string command = Console.ReadLine();
                    string[] commandArr = command.Split(' ');
                    string option = commandArr[0];
                    switch (option)
                    {
                        case "start":
                            _psr = commandArr[1];
                            string[] paramsArr = commandArr[1].Split(',');
                            foreach (string param in paramsArr)
                            {
                                PointSourceRange psrTemp = new PointSourceRange
                                {
                                    MiniSource = Convert.ToInt32(param.Split('-')[0]),
                                    MaxSource = Convert.ToInt32(param.Split('-')[1])
                                };
                                PSRList.Add(psrTemp);
                            }
                            _currentPSR = PSRList[0].MiniSource;
                            dataSender = new DataSender(serverName, uid, pwd, PSRList);
                            dataSender.ProgressChanged += DataSender_ProgressChanged;
                            dataSender.RunWorkerAsync();
                            dataSender.ReportProgress(0, _currentPSR);
                            Console.WriteLine("OK");
                            break;
                        case "stop":
                            stop();
                            break;
                        case "show":
                            show();
                            break;
                    }
          
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Error");
            }
        }

        private void show()
        {
            Console.WriteLine("---------------------------------------------------------------");
            Console.WriteLine("Point Source Range: " + _psr);
            Console.WriteLine(string.Format("Current TFP: {0} ------------------ {1}", _currentPSR, _percent + "%"));

        }
        private void stop()
        {
            dataSender.CancelAsync();
        }

        /// <summary>
        /// 进度更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataSender_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            Console.WriteLine("show");
            _percent = e.ProgressPercentage;
            _currentPSR = (int)e.UserState;
        }
    }
}
