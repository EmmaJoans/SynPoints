
using Common;
using Lunz.PI.GPSServer;
using Lunz.PI.GPSServer.Component;
using Lunz.PI.GPSServer.Component.Writer;
using Lunz.Services.CLog;
using Lunz.Services.LBS.Interface.DataModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WPoints
{
    public class DataWriter : BackgroundWorker
    {
        private string _serverName;
        private string _uid;
        private string _pwd;
        private string[] _memcached;

        private volatile int _writeCount = 0;
        public int WriteCount { get { return _writeCount; } }

        private DataListener _dataListener;

        // PI 操作对象
        PIGPSServer _piGPSServer;
        PIWriter _writer;

        ILog _log;
        public DataWriter(string serverName, string uid, string pwd, string[] memcached)
        {
            _serverName = serverName;
            _uid = uid;
            _pwd = pwd;
            _memcached = memcached;

            _dataListener = new DataListener(NamedPipeNative.PIPE_NAME_SYNPOINTS);
            _dataListener.DataReveived += _dataListener_DataReveived;

            _piGPSServer = new PIGPSServer("WPoints", memcached, "WPoints.config", "WPoints.log");
            _writer = new PIWriter();

            _log = LogManager.GetLogger(Assembly.GetExecutingAssembly().GetName().Name);

            WorkerSupportsCancellation = true;
        }

        private void _dataListener_DataReveived(object sender, EventArgs e)
        {
            if (sender == null)
            {
                _log.Error("解析后的数据为空");
                return;
            }
            try
            {
                PointAttrFull attrFull = (PointAttrFull)sender;
                string error = string.Empty;
                if (_piGPSServer == null)
                    return;
                // 创建测点
                if (!(_piGPSServer.PIPointLib as PIGPSPointLib).CreatePoints(
                    attrFull.IMEI,
                    attrFull.PointSource.ToString(),
                    attrFull.AccessPort,
                    attrFull.DeivcePattern,
                    attrFull.DeviceModel,
                    out error))
                {
                    _log.Error("测点创建失败: " + error);
                    return;
                }

                if (!string.IsNullOrEmpty(attrFull.PA_TIM.SNValue))
                {
                    ZRGPSInformationData gpsData = new ZRGPSInformationData();
                    gpsData.Device = new ZRGPSDevice();
                    gpsData.Device.GpsDeviceNo = attrFull.IMEI;
                    gpsData.Device.GpsDeviceModel = (EnumZRGPSDeviceModel)(attrFull.DeviceModel);
                    gpsData.Device.GpsDevicePort = attrFull.AccessPort;
                    gpsData.Device.GpsDeviceType = (EnumZRGPSDeviceTypes)(attrFull.DeivcePattern);
                    // TIM
                    gpsData.LastCommunicationTime = Convert.ToDateTime(attrFull.PA_TIM.SNValue);
                    // TIG
                    if (!string.IsNullOrEmpty(attrFull.PA_TIG.SNValue))
                        gpsData.LastCommunicationTime = Convert.ToDateTime(attrFull.PA_TIG.SNValue);
                    // SPD
                    if (!string.IsNullOrEmpty(attrFull.PA_SPD.SNValue))
                        gpsData.Speed = (float)Convert.ToDouble(attrFull.PA_SPD.SNValue);
                    // ORI
                    if (!string.IsNullOrEmpty(attrFull.PA_ORI.SNValue))
                        gpsData.DirectionAngle = (float)Convert.ToDouble(attrFull.PA_ORI.SNValue);
                    // LNG
                    if (!string.IsNullOrEmpty(attrFull.PA_LNG.SNValue))
                        gpsData.Lng = Convert.ToDouble(attrFull.PA_LNG.SNValue);
                    // LBT
                    string locationMode = attrFull.PA_LBT.SNValue;
                    if (!string.IsNullOrEmpty(locationMode))
                    {
                        if (locationMode == "GPS")
                            gpsData.LocateMode = EnumZRGPSDataLocateMode.GPSDataLocateMode_GPS;
                        else
                            gpsData.LocateMode = EnumZRGPSDataLocateMode.GPSDataLocateMode_Base;
                    }
                    // LAT
                    if (!string.IsNullOrEmpty(attrFull.PA_LAT.SNValue))
                        gpsData.Lat = Convert.ToDouble(attrFull.PA_LAT.SNValue);
                    // BAT
                    if (!string.IsNullOrEmpty(attrFull.PA_BAT.SNValue))
                        gpsData.Battery = Convert.ToDouble(attrFull.PA_BAT.SNValue);

                    if(!_writer.WriteGPSDataToPIServer(gpsData))
                    {
                        _log.Error("快照写入失败");
                    }

                    _writeCount++;
                }

            }
            catch (Exception ex)
            {
                _log.Error(ex.InnerException == null ? ex.Message : ex.InnerException.Message);
            }
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            base.OnDoWork(e);

            try
            {
                if (!_dataListener.Open())
                {
                    _log.Error("管道打开失败!");
                    return;
                }

                _piGPSServer.ConnectPIServer(_serverName, _uid, _pwd, 5450);
                PIGPSPointLib piGPSPointLib = new PIGPSPointLib();
                piGPSPointLib.ParentServer = _piGPSServer;
                _piGPSServer.PIPointLib = piGPSPointLib;
                _writer.ParentServer = _piGPSServer;

                _dataListener.StartListener();
            }
            catch (Exception ex)
            {
                _log.Error(ex.InnerException == null ? ex.Message : ex.InnerException.Message);
            }
        }

        public void CancelWrite()
        {
            _dataListener.CancleListen();
        }

        protected override void OnRunWorkerCompleted(RunWorkerCompletedEventArgs e)
        {
            base.OnRunWorkerCompleted(e);
            try
            {
                _piGPSServer.Disconnect(_serverName);
                _piGPSServer = null;
                _dataListener.Close();
            }
            catch
            { }
        }
    }
}
