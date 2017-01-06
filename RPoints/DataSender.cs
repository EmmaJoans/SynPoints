using System;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel;

using PISDK;
using Lunz.Services.CLog;

using Common;
using System.Runtime.InteropServices;

namespace RPoints
{
    public class DataSender : BackgroundWorker
    {
        [DllImport(@"C:\Windows\SysWOW64\piapi32.dll")]
        public static extern void pitm_secint(
            Int32 timedate,
            int[] timearray);

        // PI 服务器信息
        private string _serverName;
        private string _uid;
        private string _pwd;

        PISDK.PISDK _sdk = null;
        Server _server = null;
        // 点源
        private List<PointSourceRange> _PSRangeList;

        // 管道传输 对象
        DataTransmitter _dataTransmitter;

        // 日志对象
        ILog _log;
        public DataSender(string serverName, string uid, string pwd, List<PointSourceRange> PSRangeList)
        {
            // pi 服务配置
            _serverName = serverName;
            _uid = uid;
            _pwd = pwd;
            _PSRangeList = PSRangeList;

            _dataTransmitter = new DataTransmitter(NamedPipeNative.PIPE_NAME_SYNPOINTS);
            // 初始化日志类
            _log = LogManager.GetLogger(Assembly.GetExecutingAssembly().GetName().Name);

            WorkerSupportsCancellation = true;
            WorkerReportsProgress = true;
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            base.OnDoWork(e);
            string IMEI = string.Empty;
            try
            {
                if (!_dataTransmitter.Open())
                {
                    _log.Error("管道打开失败");
                    return;
                }
                ConnectToPI(_serverName, _uid, _pwd);
                if (_PSRangeList.Count == 0)
                {
                    _log.Error("点源列表为空！");
                    return;
                }

                // 根据点源查找到的测点列表
                PointList pointList = null;
                // 根据IMEI查找到的测点列表
                PointList pointAttrList = null;

                // 设备号与测点标记后缀
                IMEI = string.Empty;
                string tag = string.Empty;

                // 点源类型枚举
                ENUMPointSourceType pst;
                // 设备信息枚举
                ENUMDeviceMode deviceMode;
                EnumDevicePattern devicePattern;

                // 接入服务端口号
                int accessPort = 0;

                // 测点传输集合
                PointAttrFull attrFull = null;

                foreach (PointSourceRange psr in _PSRangeList)
                {
                    for (int i = psr.MiniSource; i <= psr.MaxSource; ++i)
                    {
                        if (!CancellationPending)
                        {
                            pst = (ENUMPointSourceType)(i / PointSourceRange.PS_RANGE);
                            accessPort = GetAccessPort(pst);
                            deviceMode = GetDeviceMode(pst);
                            devicePattern = GetDevicePattern(pst);

                            pointList = _server.GetPoints(string.Format("tag=\"*_tim\" and pointsource=\"{0}\"", i));
                            if (pointList == null || pointList.Count == 0)
                            {
                                _log.Error(string.Format("点源 {0} 下没有找到测点或测点查询失败", i));
                                continue;
                            }
                            _log.Debug(string.Format("{0} IMEIs got", pointList.Count));
                            int transcatedCount = 0;
                            int oldTranscatedCount = 0;
                            foreach (PIPoint point in pointList)
                            {
                                IMEI = point.PointAttributes["tag"].Value.ToString();
                                IMEI = IMEI.Split('_')[0];

                                pointAttrList = _server.GetPoints(string.Format("tag=\"{0}_*\"", IMEI));
                                if (pointAttrList == null || pointAttrList.Count == 0)
                                    _log.Error(string.Format("设备号 {0} 下没有找到测点或测点查询失败", IMEI));

                                // 填充测点
                                if (!SetPointAttrFull(out attrFull, pointAttrList,
                                    IMEI, i, accessPort,
                                    devicePattern, deviceMode))
                                {
                                    _log.Error(string.Format("设备号 {0} 下没有找到 tim 测点", IMEI));
                                    continue;
                                }

                                #region 发送数据
                                string msgSend = DataConverter.AssembleData(attrFull);
                                if (string.IsNullOrEmpty(msgSend))
                                {
                                    _log.Error("json 序列化结果为空！");
                                    continue;
                                }
                                _dataTransmitter.SendMessage(msgSend);
                                _log.Debug(string.Format("have sent message: {0}", msgSend));
                                transcatedCount++;
                                if (transcatedCount - oldTranscatedCount > 30)
                                {
                                    ReportProgress(transcatedCount / pointList.Count, i);
                                    oldTranscatedCount = transcatedCount;
                                }



                                #endregion
                            }


                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error((ex.InnerException == null ? ex.Message : ex.InnerException.Message) 
                    + "\n IMEI: " + IMEI);
            }
            finally
            {
                if (_server != null && _server.Connected)
                    _server.Close();
                _dataTransmitter.Close();
            }
        }

        /// <summary>
        /// 设置带发送测点信息集合
        /// </summary>
        /// <param name="attrFull"></param>
        /// <param name="pointList"></param>
        /// <returns></returns>
        private bool SetPointAttrFull(out PointAttrFull attrFull, PointList pointList,
            string IMEI, int pointSource, int accessPort,
            EnumDevicePattern devicePattern, ENUMDeviceMode deviceMode)
        {
            attrFull = new PointAttrFull();
            PIPoint point = null;
            // 测点值操作对象
            PIValue pivalue;
            try
            {
                point = pointList[string.Format("\\\\{0}\\{1}", _serverName, IMEI + "_tim")];
                if (point == null)
                    return false;
                attrFull.IMEI = IMEI;
                attrFull.PointSource = pointSource;
                attrFull.AccessPort = accessPort;
                attrFull.DeivcePattern = (int)devicePattern;
                attrFull.DeviceModel = (int)deviceMode;

                #region 快照
                pivalue = point.Data.Snapshot;
                if (pivalue.IsGood())
                {
                    int[] pastime = new int[6];
                    pastime[0] = 0;
                    pastime[1] = 0;
                    pastime[2] = 0;
                    pastime[3] = 0;
                    pastime[4] = 0;
                    pastime[5] = 0;
                    pitm_secint(pivalue.Value, pastime);
                    DateTime dt = new DateTime(pastime[2], pastime[0], pastime[1], pastime[3], pastime[4], pastime[5]);

                    #region TIM 测点
                    attrFull.PA_TIM = new PointAttr
                    {
                        SNValue = dt.ToString("yyyy-MM-dd HH:mm:ss")
                    };
                    #endregion

                    #region BAT 测点
                    string batteryPercent = "100.00";
                    if (devicePattern != EnumDevicePattern.GPSDeviceType_T)
                    {
                        point = pointList[string.Format("\\\\{0}\\{1}", _serverName, IMEI + "_bat")];
                        pivalue = point.Data.Snapshot;
                        if (pivalue.IsGood())
                            batteryPercent = pivalue.Value.ToString();
                        else
                            batteryPercent = string.Empty;
                    }

                    attrFull.PA_BAT = new PointAttr
                    {
                        SNValue = batteryPercent
                    };
                    #endregion

                    #region LAT 测点
                    point = pointList[string.Format("\\\\{0}\\{1}", _serverName, IMEI + "_lat")];
                    pivalue = point.Data.Snapshot;
                    attrFull.PA_LAT = new PointAttr
                    {
                        SNValue = pivalue.IsGood() ? pivalue.Value.ToString() : string.Empty
                    };
                    #endregion

                    #region LBT 测点
                    string lbt = string.Empty;
                    point = pointList[string.Format("\\\\{0}\\{1}", _serverName, IMEI + "_lbt")];
                    pivalue = point.Data.Snapshot;
                    if (pivalue.IsGood())
                        lbt = ((DigitalState)(pivalue.Value)).Name;
                    attrFull.PA_LBT = new PointAttr
                    {
                        SNValue = lbt
                    };
                    #endregion

                    #region LNG 测点
                    point = pointList[string.Format("\\\\{0}\\{1}", _serverName, IMEI + "_lng")];
                    pivalue = point.Data.Snapshot;
                    attrFull.PA_LNG = new PointAttr
                    {
                        SNValue = pivalue.IsGood() ? pivalue.Value.ToString() : string.Empty
                    };
                    #endregion

                    #region ORI 测点
                    point = pointList[string.Format("\\\\{0}\\{1}", _serverName, IMEI + "_ori")];
                    pivalue = point.Data.Snapshot;
                    attrFull.PA_ORI = new PointAttr
                    {
                        SNValue = pivalue.IsGood() ? pivalue.Value.ToString() : string.Empty
                    };
                    #endregion

                    #region SPD 测点
                    point = pointList[string.Format("\\\\{0}\\{1}", _serverName, IMEI + "_spd")];
                    pivalue = point.Data.Snapshot;
                    attrFull.PA_SPD = new PointAttr
                    {
                        SNValue = pivalue.IsGood() ? pivalue.Value.ToString() : string.Empty
                    };
                    #endregion

                    #region TIG 测点
                    point = pointList[string.Format("\\\\{0}\\{1}", _serverName, IMEI + "_tig")];
                    pivalue = point.Data.Snapshot;
                    attrFull.PA_TIG = new PointAttr
                    {
                        SNValue = pivalue.IsGood() ? Convert.ToDateTime(((PITimeServer.PITime)(pivalue.Value)).LocalDate).ToString("yyyy-MM-dd HH:mm:ss")
                        : string.Empty
                    };
                    #endregion

                    return true;
                }
                attrFull.PA_TIM = new PointAttr
                {
                    SNValue = string.Empty
                };
                attrFull.PA_BAT = new PointAttr
                {
                    SNValue = string.Empty
                };
                attrFull.PA_LAT = new PointAttr
                {
                    SNValue = string.Empty
                };
                attrFull.PA_LBT = new PointAttr
                {
                    SNValue = string.Empty
                };
                attrFull.PA_LNG = new PointAttr
                {
                    SNValue = string.Empty
                };
                attrFull.PA_ORI = new PointAttr
                {
                    SNValue = string.Empty
                };
                attrFull.PA_SPD = new PointAttr
                {
                    SNValue = string.Empty
                };
                attrFull.PA_TIG = new PointAttr
                {
                    SNValue = string.Empty
                };
                #endregion

            }
            catch(Exception ex)
            {
                _log.Error(ex.Message + " | IMEI: " + IMEI);
                return false;
            }


            return true;
        }

        /// <summary>
        /// 获取设备接入端口号
        /// </summary>
        /// <param name="pst">点源类型</param>
        /// <returns></returns>
        public static int GetAccessPort(ENUMPointSourceType pst)
        {
            switch (pst)
            {
                case ENUMPointSourceType.ZRT02:
                    return 8821;
                case ENUMPointSourceType.ZR300:
                    return 8861;
                case ENUMPointSourceType.ZRA5:
                    return 6661;
                case ENUMPointSourceType.ZR300N:
                    return 8862;
                case ENUMPointSourceType.ZRT02D:
                    return 8851;
                default: return 0;
            }
        }

        /// <summary>
        /// 获取设备类型
        /// </summary>
        /// <param name="pst">点源类型</param>
        /// <returns></returns>
        public static ENUMDeviceMode GetDeviceMode(ENUMPointSourceType pst)
        {
            switch (pst)
            {
                case ENUMPointSourceType.ZRT02:
                case ENUMPointSourceType.ZRT02D:
                    return ENUMDeviceMode.Wire;
                default:
                    return ENUMDeviceMode.Wireless;
            }
        }

        /// <summary>
        /// 获取设备型号
        /// </summary>
        /// <param name="pst"></param>
        /// <returns></returns>
        public static EnumDevicePattern GetDevicePattern(ENUMPointSourceType pst)
        {
            switch (pst)
            {
                case ENUMPointSourceType.ZRT02:
                    return EnumDevicePattern.GPSDeviceType_T;
                case ENUMPointSourceType.ZR300:
                    return EnumDevicePattern.GPSDeviceType_ZR300;
                case ENUMPointSourceType.ZRA5:
                    return EnumDevicePattern.GPSDeviceType_ZRA5C;
                case ENUMPointSourceType.ZR300N:
                    return EnumDevicePattern.GPSDeviceType_ZR300N;
                case ENUMPointSourceType.ZRT02D:
                    return EnumDevicePattern.GPSDeviceType_GT02D;
                default: return 0;
            }
        }

        /// <summary>
        /// 连接至PI服务器
        /// </summary>
        /// <param name="serverName"></param>
        /// <param name="uid"></param>
        /// <param name="pwd"></param>
        /// <returns></returns>
        private void ConnectToPI(string serverName, string uid, string pwd)
        {
            _sdk = new PISDK.PISDK();
            try
            {
                Servers servers = _sdk.Servers;
                /* 若没有在KST中读取到任何服务信息，则将要链接
               * 的服务信息 加载到服务列表中 */
                if (servers.Count == 0)
                {

                    string connectString = string.Format("UID={0};PATH={1};PORT=5450;DEFAULT=1",
                        uid, serverName);
                    servers.Add(serverName, connectString, "PI3");

                }
                // 获取并链接服务器
                _server = servers[serverName];

                string openStr = string.Format("UID={0};PWD={1};SERVERROLE=Any", uid, pwd);
                if (!_server.Connected)
                    _server.Open(openStr);
            }
            catch (Exception ex)
            {
                throw new Exception(_sdk.GetErrorString((PISDKErrorConstants)ex.HResult));
            }
        }
    }

    /// <summary>
    /// 点源范围列表
    /// </summary>
    public struct PointSourceRange
    {
        public const int PS_RANGE = 1000;
        public int MiniSource { get; set; }
        public int MaxSource { get; set; }
    }

    public enum ENUMPointSourceType
    {
        ZRT02 = 1,
        ZR300 = 2,
        ZRA5 = 3,
        ZRT02D = 5,
        ZR300N = 6
    }



}
