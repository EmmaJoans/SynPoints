using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;

namespace Common
{
    /// <summary>
    /// 点值信息
    /// </summary>
    public enum ENUMPointDataType
    {
        pttypNull = 0,
        pttypInt16 = 6,
        pttypInt32 = 8,
        pttypFloat16 = 11,
        pttypFloat32 = 12,
        pttypFloat64 = 13,
        pttypDigital = 101,
        pttypBlob = 102,
        pttypTimestamp = 104,
        pttypString = 105
    }

    /// <summary>
    /// 设备类型 有线/无线
    /// </summary>
    public enum ENUMDeviceMode
    {
        Wire = 1,
        Wireless = 2
    }

    /// <summary>
    /// 设备型号
    /// </summary>
    public enum EnumDevicePattern
    {
        GPSDeviceType_T = 1,
        GPSDeviceType_ZR300 = 2,
        GPSDeviceType_ZRA5C = 3,
        GPSDeviceType_GT02D = 5,
        GPSDeviceType_ZR300N = 6,
        GPSDeviceType_Y901 = 7,
        GPSDeviceType_Mini = 8
    }

    /// <summary>
    /// 点属性信息
    /// </summary>
    public class PointAttr
    {
        // 快照值
        public string SNValue { get; set; }

    }

    /// <summary>
    /// 一个IMEI下的所有测点
    /// </summary>
    public class PointAttrFull
    {
        // IMEI
        public string IMEI { get; set; }
        // 点源
        public int PointSource { get; set; }
        // 接入服务端口号
        public int AccessPort { get; set; }
        // 设备型号
        public int DeivcePattern { get; set; }
        // 设备类型 有线 无线
        public int DeviceModel { get; set; }

        #region 快照值
        public PointAttr PA_BAT { get; set; }
        public PointAttr PA_LAT { get; set; }
        public PointAttr PA_LNG { get; set; }
        public PointAttr PA_ORI { get; set; }
        public PointAttr PA_SPD { get; set; }
        public PointAttr PA_LBT { get; set; }
        public PointAttr PA_TIG { get; set; }
        public PointAttr PA_TIM { get; set; }
        #endregion
    }

    /// <summary>
    /// 数据发送器
    /// </summary>
    public class DataTransmitter
    {
        // 管道名称
        string _pipeName;
        // 管道句柄
        IntPtr _fileHandle = IntPtr.Zero;
        // 默认管道打开等待时常
        const int DEFAULT_TIMEOUT = 100;

        public DataTransmitter(string pipeName)
        {
            _pipeName = pipeName;
        }


        public bool Open()
        {
            // 创建管道
            try
            {
                if (!NamedPipeNative.WaitNamedPipe(_pipeName, DEFAULT_TIMEOUT))
                {
                    return false;
                }

                _fileHandle = NamedPipeNative.CreateFile(_pipeName, NamedPipeNative.GENERIC_READ | NamedPipeNative.GENERIC_WRITE,
                    0, null, NamedPipeNative.OPEN_EXISTING, 0, 0);
                if (_fileHandle.ToInt32() == NamedPipeNative.INVALID_HANDLE_VALUE)
                {
                    return false;
                }

                uint pipeMode = NamedPipeNative.PIPE_READMODE_MESSAGE;
                // 设置管道属性
                if (!NamedPipeNative.SetNamedPipeHandleState(_fileHandle, ref pipeMode, IntPtr.Zero, IntPtr.Zero))
                {
                    return false;
                }

                return true;

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void Close()
        {
            try
            {
                if (_fileHandle != IntPtr.Zero)
                    NamedPipeNative.CloseHandle(_fileHandle);
            }
            catch
            {

            }
        }

        /// <summary>
        /// 发送下信息
        /// </summary>
        /// <param name="message"></param>
        public void SendMessage(string message)
        {
            try
            {
                byte[] msgBuffer = Encoding.UTF8.GetBytes(message);

                uint msgLen = (uint)msgBuffer.Length;
                byte[] numReadWritten = new byte[4];
                if (!NamedPipeNative.WriteFile(_fileHandle, BitConverter.GetBytes(msgLen), 4, numReadWritten, 0))
                {
                    throw new Exception("数据长度写入失败!");
                }
                if (!NamedPipeNative.WriteFile(_fileHandle, msgBuffer, msgLen, numReadWritten, 0))
                {
                    throw new Exception("数据发送失败!");
                }

            }
            catch (Exception ex)
            {
                throw (ex);
            }

        }


    }

    /// <summary>
    /// 数据接收器
    /// </summary>
    public class DataListener
    {
        string _pipeName;
        IntPtr _pipeHandle = IntPtr.Zero;
        bool _pipeIsBreak = false;

        public event EventHandler DataReveived;
        public DataListener(string pipeName)
        {
            _pipeName = pipeName;
        }

        /// <summary>
        /// 链接管道
        /// </summary>
        /// <returns></returns>
        public bool Open()
        {
            try
            {
                _pipeHandle = NamedPipeNative.CreateNamedPipe(
                    _pipeName,
                    NamedPipeNative.PIPE_ACCESS_DUPLEX,
                    NamedPipeNative.PIPE_TYPE_MESSAGE | NamedPipeNative.PIPE_READMODE_MESSAGE,
                    NamedPipeNative.PIPE_UNLIMITED_INSTANCES,
                    1024,
                    1024,
                    0,
                    IntPtr.Zero);
                if (_pipeHandle == IntPtr.Zero || _pipeHandle.ToInt32() == NamedPipeNative.INVALID_HANDLE_VALUE)
                {
                    return false;
                }


                if (!NamedPipeNative.ConnectNamedPipe(_pipeHandle, null))
                {
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 关闭管道
        /// </summary>
        public void Close()
        {
            if (_pipeHandle != IntPtr.Zero)
                NamedPipeNative.CloseHandle(_pipeHandle);
        }

        /// <summary>
        /// 开启数据监听
        /// </summary>
        public void StartListener()
        {
            string messageRecv = string.Empty;
            try
            {
                while (!_pipeIsBreak && NamedPipeNative.GetLastError() != NamedPipeNative.ERROR_PIPE_CONNECTED)
                {
                    messageRecv = ReadMessage(_pipeHandle);
                    NamedPipeNative.FlushFileBuffers(_pipeHandle);

                    if (!string.IsNullOrEmpty(messageRecv))
                    {
                        ThreadPool.QueueUserWorkItem(new WaitCallback(CB_RecvMessage), messageRecv);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        public void CancleListen()
        {
            _pipeIsBreak = true;
        }

        private void CB_RecvMessage(object state)
        {
            if (DataReveived != null)
            {
                string message = state.ToString();
                PointAttrFull attr = DataConverter.PaseData(message);
                DataReveived(attr, null);
            }
        }

        private string ReadMessage(IntPtr pipeHandle)
        {
            string message = string.Empty;
            uint errorCode = 0;
            try
            {
                uint len = 0;
                byte[] numReadWritten = new byte[4];
                byte[] intBytes = new byte[4];

                // 读取数据数据包长度
                if (!NamedPipeNative.ReadFile(pipeHandle, intBytes, 4, numReadWritten, 0))
                {
                    if ((errorCode = NamedPipeNative.GetLastError()) == 0)
                        return message;
                    //_log.Error("数据包包长读取失败, 错误码: " + NamedPipeNative.GetLastError());
                }

                len = BitConverter.ToUInt32(intBytes, 0);
                byte[] bufferRecv = new byte[len];
                if (!NamedPipeNative.ReadFile(pipeHandle, bufferRecv, len, numReadWritten, 0))
                {
                    if ((errorCode = NamedPipeNative.GetLastError()) == 0)
                        return message;
                    //throw new Exception("数据读取失败, 错误码：" + NamedPipeNative.GetLastError());
                }
                message = Encoding.UTF8.GetString(bufferRecv);
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return message;
        }
    }

    /// <summary>
    /// 数据转换器
    /// </summary>
    public class DataConverter
    {
        /// <summary>
        /// 数据封装
        /// </summary>
        /// <param name="attr">点属性</param>
        /// <returns></returns>
        public static string AssembleData(PointAttrFull attr)
        {
            string message = string.Empty;
            try
            {
                message = JsonConvert.SerializeObject(attr);
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return message;
        }

        /// <summary>
        /// 数据解析
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static PointAttrFull PaseData(string message)
        {
            PointAttrFull attr = null;

            try
            {
                attr = JsonConvert.DeserializeObject<PointAttrFull>(message);
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return attr;
        }

    }
}
