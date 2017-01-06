using System;
using System.Runtime.InteropServices;

namespace Common
{
    public static class NamedPipeNative
    {
        // 管道名称
        public const string PIPE_NAME_SYNPOINTS = @"\\.\pipe\SYNPOINTS";

        public const uint PIPE_ACCESS_DUPLEX = 0x00000003;

        public const uint PIPE_READMODE_MESSAGE = 0x00000002;

        public const uint PIPE_TYPE_MESSAGE = 0x00000004;

        public const uint PIPE_WAIT = 0x00000000;

        public const uint PIPE_UNLIMITED_INSTANCES = 255;

        public const int INVALID_HANDLE_VALUE = -1;

        public const ulong ERROR_PIPE_CONNECTED = 535;

        public const uint GENERIC_WRITE = (0x40000000);

        public const uint GENERIC_READ = (0x80000000);

        public const uint OPEN_EXISTING = 3;

        public const ulong ERROR_PIPE_BUSY = 231;

        public const ulong ERROR_NO_DATA = 232;

        public const ulong ERROR_CANNOT_CONNECT_TO_PIPE = 2;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateFile(
            String lpFileName,						  // file name
            uint dwDesiredAccess,					  // access mode
            uint dwShareMode,								// share mode
            SecurityAttributes attr,				// SD
            uint dwCreationDisposition,			// how to create
            uint dwFlagsAndAttributes,			// file attributes
            uint hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateNamedPipe(
            String lpName,									// pipe name
            uint dwOpenMode,								// pipe open mode
            uint dwPipeMode,								// pipe-specific modes
            uint nMaxInstances,							// maximum number of instances
            uint nOutBufferSize,						// output buffer size
            uint nInBufferSize,							// input buffer size
            uint nDefaultTimeOut,						// time-out interval
            IntPtr pipeSecurityDescriptor		// SD
            );


        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ConnectNamedPipe(
            IntPtr hHandle,									// handle to named pipe
            Overlapped lpOverlapped					// overlapped structure
            );


      

        //[DllImport("kernel32.dll", SetLastError = true)]
        //public static extern bool  ReadFile(
        //         IntPtr hFile,
        //         Byte[] lpBuffer,
        //         ulong nNumberOfBytesToRead,
        //         ulong lpNumberOfBytesRead,
        //         ref ulong lpOverlapped
        //        );
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(
            IntPtr hHandle,											// handle to file
            byte[] lpBuffer,								// data buffer
            uint nNumberOfBytesToRead,			// number of bytes to read
            byte[] lpNumberOfBytesRead,			// number of bytes read
            uint lpOverlapped								// overlapped buffer
            );

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteFile(
            IntPtr hHandle,											// handle to file
            byte[] lpBuffer,							  // data buffer
            uint nNumberOfBytesToWrite,			// number of bytes to write
            byte[] lpNumberOfBytesWritten,	// number of bytes written
            uint lpOverlapped								// overlapped buffer
            );

        //[DllImport("kernel32.dll", SetLastError = true)]
        //public static extern BOOL WriteFile(
        //      IntPtr hFile,
        //      Byte[] lpBuffer,
        //      ulong nNumberOfBytesToWrite,
        //      ref ulong lpNumberOfBytesWritten,
        //      ulong lpOverlapped
        //    ); 

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FlushFileBuffers(
            IntPtr hHandle);


        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DisconnectNamedPipe(
            IntPtr hHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetNamedPipeHandleState(
            IntPtr hHandle,
            ref uint mode,
            IntPtr cc,
            IntPtr cd);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WaitNamedPipe(
            String name,
            int timeout);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(
            IntPtr hHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetLastError();

    }

    [StructLayout(LayoutKind.Sequential)]
    public class Overlapped
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    public class SecurityAttributes
    {
    }

}
