using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SynPoints
{
    class Program
    {
        private List<Process> _processes = null;
        private ChildResponseCheck _crc;
        /// <summary>
        /// 入口程序
        /// </summary>
        /// <param name="args"></param>
        [STAThread]
        static void Main(string[] args)
        {
            Console.Title = "SynPoints.exe";
            
            Program program = new Program();
            program.Run(args);
        }

        /// <summary>
        /// 启动程序
        /// </summary>
        /// <param name="args">命令行参数</param>
        private void Run(string[] args)
        {
            try
            {
                // 创建进程表
                if (_processes == null)
                {
                    _processes = new List<Process>();
                }

                string[] processesArr = null;
                string processesStr = string.Empty;
                processesStr = ConfigurationManager.AppSettings["processes"];
                if(string.IsNullOrEmpty(processesStr))
                {
                    Console.WriteLine("Can't find 'processes' in the config file, exit!");
                    return;
                }

                processesArr = processesStr.Split(',');
                if(processesArr.Length < 2)
                {
                    Console.WriteLine("At least two processes are required, exit!");
                    return;
                }

                foreach(string process in processesArr)
                {
                    if(!CreateProcess(process))
                    {
                        Console.WriteLine(string.Format("failed to start process: {0}, exit!", process));
                        return;
                    }

                }
                _crc = new ChildResponseCheck(_processes.FirstOrDefault(x => x.ProcessName == "RPoints"), _processes.FirstOrDefault(x => x.ProcessName == "WPoints"));
                _crc.ShowProgress += _crc_ShowProgress;
                _crc.RunWorkerAsync();

                COMMAND:
                string command = string.Empty;
                Console.Write("SynPoints$ ");
                while((command = Console.ReadLine()) != "q" && command != "quit")
                {

                    if (string.IsNullOrEmpty(command))
                    {
                        goto COMMAND;
                    }
                    Console.Clear();
                    // 执行命令
                    switch(command)
                    {
                        case "h":
                        case "help":
                            // 命令帮助
                            break;
                        case "show":
                            show();
                            break;
                        case "stop":
                            stop();
                            break;
                        default:
                            Console.WriteLine("unrecognized command, try again");
                            break;

                    }
                    Thread.Sleep(100);
                    
                    Console.Write("\nSynPoints$ ");
                }
                
                
            }
            catch(ConfigurationErrorsException ex)
            {
                Console.WriteLine("Failed to read config file：" + ex.Message);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Program exception： " + ex.Message);
            }
            


        }

        private void _crc_ShowProgress(object sender, EventArgs e)
        {
            show();
        }

        private void show()
        {
            try
            {
                if (_processes.Count == 0)
                    return;
                Process rpoints = _processes.FirstOrDefault(x => x.ProcessName == "RPoints");
                Console.WriteLine();
                rpoints.StandardInput.WriteLine("show");
                Thread.Sleep(10);
                Process wpoints = _processes.FirstOrDefault(x => x.ProcessName == "WPoints");
                Console.WriteLine();
                wpoints.StandardInput.WriteLine("show");
            
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void stop()
        {
            try
            {
                if (_processes.Count == 0)
                    return;
                Process rpoints = _processes.FirstOrDefault(x => x.ProcessName == "RPoints");
                rpoints.StandardInput.WriteLine("stop");
                Process wpoints = _processes.FirstOrDefault(x => x.ProcessName == "WPoints");
                wpoints.StandardInput.WriteLine("stop");

                _crc.CancelAsync();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// 创建进程
        /// </summary>
        /// <param name="processName"></param>
        public bool CreateProcess(string processName)
        {
            Process process = null;
            if (!string.IsNullOrEmpty(processName))
            {
                string processFullName = Environment.CurrentDirectory + "\\" + processName;

                // 设置进程信息
                ProcessStartInfo psi = new ProcessStartInfo(processFullName, null);
                psi.RedirectStandardError = true;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.CreateNoWindow = false;
                psi.UseShellExecute = false;

                // 创建进程
                process = Process.Start(psi);
                process.EnableRaisingEvents = true;
                process.OutputDataReceived += Process_OutputDataReceived;
                process.ErrorDataReceived += Process_ErrorDataReceived;
                process.Exited += Process_Exited;

                string output = string.Empty;
                bool startSucess = true;

                if (processName == "RPoints")
                {
                    try
                    {
                        string param = string.Empty;
                        string psr = System.Configuration.ConfigurationManager.AppSettings["pts"];
                        string[] psrArr = psr.Split(',');
                        int psr_count = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["pts_count"]);
                        if (psrArr.Length != psr_count)
                        {
                            throw new Exception("point source config error");
                        }
                        READPSR:
                        Console.WriteLine("select the PSR code or they 'Bitwise OR' result\n (other number will be treated as single point source)");
                        for (int i = 0; i < psr_count; ++i)
                        {
                            Console.WriteLine(string.Format("     {0,2}: {1}", Math.Pow(2, i), psrArr[i]));
                        }
                        Console.Write("your select: ");
                        string choiceStr = Console.ReadLine();
                        int choice;
                        if (!Int32.TryParse(choiceStr, out choice))
                        {
                            Console.WriteLine("unrecognized PSR code, try again!");
                            goto READPSR;
                        }
                        if (choice > 1000)
                        {
                            bool choiceValid = false;
                            for (int i = 0; i < psr_count; ++i)
                            {
                                if (choice / 1000 == psrArr[i].Split('-')[0][0] - '0')
                                {
                                    choiceValid = true;
                                    break;
                                }

                            }
                            if (!choiceValid)
                            {
                                Console.WriteLine("custom PSR code over range, try again!");
                                goto READPSR;
                            }
                            param = choice.ToString() + "-" + choice.ToString();

                        }
                        else
                        {
                            for (int i = 0; i < psr_count; ++i)
                            {
                                if ((choice & (int)Math.Pow(2, i)) >= 1)
                                {
                                    param = param + psrArr[0] + ",";
                                }
                            }

                            if (string.IsNullOrEmpty(param))
                            {
                                Console.WriteLine("selected PSR code over range, try again!");
                                goto READPSR;
                            }

                            param = param.TrimEnd(',');
                        }

                        Console.WriteLine("selected PSR: ");
                        Console.WriteLine(param);
                        CHECK:
                        Console.Write("is that OK? (Y/N): ");
                        string checkAsw = Console.ReadLine();
                        if (checkAsw == "Y")
                        {
                            process.StandardInput.WriteLine("start " + param);

                        }
                        else if (checkAsw == "N")
                        {
                            goto READPSR;
                        }
                        else
                        {
                            Console.WriteLine("please input 'Y' or 'N'");
                            goto CHECK;
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                // 获取进程输出
                while (true)
                {
                    StreamReader outputStream = process.StandardOutput;
                    if (!outputStream.EndOfStream)
                    {
                        output = outputStream.ReadLine();
                        if (output == "OK")
                        {
                            break;
                        }
                        if (output == "Error")
                        {
                            startSucess = false;
                            break;
                        }
                        Console.WriteLine(output);

                    }
                }

                if (startSucess)
                {
                    _processes.Add(process);
                    return true;
                }
            }

            return false;
        }
        /// <summary>
        /// 进程退出
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Process_Exited(object sender, EventArgs e)
        {
            Process currentProcess = sender as Process;
            if (currentProcess != null)
            {
                if (currentProcess.HasExited)
                {
                    Console.WriteLine("{0} exited!", currentProcess.ProcessName);
                    if (_processes.Contains(currentProcess))
                        _processes.Remove(currentProcess);
                    currentProcess.Close();
                    currentProcess = null;
                }
            }
        }

        /// <summary>
        /// 进程错误处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        /// <summary>
        /// 进程输出
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }
    }
}
