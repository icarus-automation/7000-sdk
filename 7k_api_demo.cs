using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Net.Sockets;
using System.Net;

/// <summary>
/// 
/// </summary>
namespace hax_soft
{
    public enum hax_error_code
    {
        hax_success = 0,    //成功
        hax_failed,         //失败
        hax_param_error,    //参数错误
        hax_dev_error,      //请检查串口或者socket是否正确
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct rfid_info_st
    {
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 12)]
        public byte[] serial_num;       //模块的序列号，无法修改
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 20)]
        public byte[] version;          //模块的版本号，无法修改
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct epc_id_st
    {
        [MarshalAs(UnmanagedType.U2)]
        public ushort len;                  //标签长度
        [MarshalAs(UnmanagedType.U2)]
        public ushort pc;                   //PC值
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 12)]
        public byte[] id;                   //EPC ID
        [MarshalAs(UnmanagedType.U2)]
        public ushort crc;                  //CRC
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct epc_id_list
    {
        [MarshalAs(UnmanagedType.Struct)]
        public rfid_info_st sn;                     //参考rfid_info_st
        [MarshalAs(UnmanagedType.I4)]
        public int size;                               //识别到的标签数量
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 32)]
        public epc_id_st[] epc_id;     //参考epc_id_st
    };

    [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
    public delegate void func_port_connecting(string connID);

    [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
    public delegate void func_port_closing(string connID);

    [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
    public delegate void func_output_tags(string connID, epc_id_list data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct IAsynchronousMessage
    {
        [MarshalAs(UnmanagedType.SysInt)]
        public IntPtr port_connecting_;
        [MarshalAs(UnmanagedType.SysInt)]
        public IntPtr port_closing_;
        [MarshalAs(UnmanagedType.SysInt)]
        public IntPtr output_tags_;
    };

    public class Card7kAdapter
    {
        /*************************************************
         Function:			open_tcp_server
         Description:		打开TCP服务器

         Return:				返回0成功，其它为错误码，在enum hax_error_code中定义
         *************************************************/
        [DllImport("card_7k.dll", EntryPoint = "open_tcp_server", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern hax_error_code open_tcp_server(string ip, uint port, IAsynchronousMessage log);

        /*************************************************
	    Function:			close_tcp_server
	    Description:		关闭tcp服务器
	    *************************************************/
        [DllImport("card_7k.dll", EntryPoint = "close_tcp_server", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void close_tcp_server();

        /*************************************************
	    Function:			start_auto_read
	    Description:		开启自动读取EPCID

	    Input:
	    int time,			查询间隔，单位：毫秒
	    *************************************************/
        [DllImport("card_7k.dll", EntryPoint = "start_auto_read", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void start_auto_read(uint time = 200);

        /*************************************************
         Function:			stop_auto_read
         Description:		关闭自动读取EPCID
         *************************************************/
        [DllImport("card_7k.dll", EntryPoint = "stop_auto_read", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void stop_auto_read();

        /*************************************************
	    Function:			read_epc_id
	    Description:		读epc id

	    Ouput:
	    epc_id_list data,	epc id，可参考epc_id_list

	    Return:				返回0成功，其它为错误码，在enum hax_error_code中定义
	    *************************************************/
        [DllImport("card_7k.dll", EntryPoint = "read_epc_id", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern hax_error_code read_epc_id(string connID, ref epc_id_list data);

        /*************************************************
        Function:			enable_alarm
        Description:		主板报警使能

        Return:				返回0成功，其它为错误码，在enum hax_error_code中定义
        *************************************************/
        [DllImport("card_7k.dll", EntryPoint = "enable_alarm", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern hax_error_code enable_alarm(string connID);
    }
}

namespace main_
{
    class Program
    {
        public static string m_connID;
        public static hax_soft.IAsynchronousMessage m_log;

        public static string format_str(byte[] src, int size)
        {
            string strFormat = "";
            for (int i = 0; i < size; ++i)
            {
                strFormat += string.Format("{0:x}", (int)src[i]);
            }
            return strFormat;
        }
        public static void port_connecting(string connID)
        {
            Console.WriteLine("port_connecting:{0}", connID);
            m_connID = connID;
        }

        public static void port_closing(string connID)
        {
            Console.WriteLine("port_closing:{0}", connID);
        }

        public static void output_tags(string connID, hax_soft.epc_id_list data)
        {
            Console.WriteLine("==========================================================================");
            Console.WriteLine("output_tags:{0} time[{1}]", connID, DateTime.Now.TimeOfDay.ToString());
            Console.WriteLine("epc_id_list.sn.serial_num:{0}", format_str(data.sn.serial_num, 12));
            Console.WriteLine("epc_id_list.sn.version:{0}", format_str(data.sn.version, 20));
            Console.WriteLine("epc_id_list.size:{0}", data.size);
            for (int i = 0; i < data.size && i < 32; ++i)
            {

                Console.WriteLine("data.epc_id[{0}]:len={1} pc={2} crc={3} value={4}",
                    i + 1,
                    data.epc_id[i].len,
                    data.epc_id[i].pc,
                    data.epc_id[i].crc,
                    format_str(data.epc_id[i].id, 12));
            }
            Console.WriteLine("==========================================================================");
        }

        static void Main(string[] args)
        {
            m_connID = "";
            //open_tcp_server
            //hax_soft.IAsynchronousMessage log = new hax_soft.IAsynchronousMessage();
            m_log= new hax_soft.IAsynchronousMessage();
            m_log.port_connecting_ = Marshal.GetFunctionPointerForDelegate((Delegate)(hax_soft.func_port_connecting)Program.port_connecting);
            m_log.port_closing_ = Marshal.GetFunctionPointerForDelegate((Delegate)(hax_soft.func_port_closing)Program.port_closing);
            m_log.output_tags_ = Marshal.GetFunctionPointerForDelegate((Delegate)(hax_soft.func_output_tags)Program.output_tags);
            
            hax_soft.hax_error_code ret = hax_soft.Card7kAdapter.open_tcp_server("10.0.0.6", 8810, m_log);
            Console.WriteLine("open_tcp_server {0}",
                ret == hax_soft.hax_error_code.hax_success ? "ok.waiting for client to connect..." : "failed.");

            GC.KeepAlive(m_log.output_tags_);
            GC.KeepAlive(m_log.port_connecting_);
            GC.KeepAlive(m_log.port_closing_);
            if (ret == hax_soft.hax_error_code.hax_success)
            {
                hax_soft.Card7kAdapter.start_auto_read(500);

                while (true)
                {
                    Thread.Sleep(1000);
                }
            }


            ////read_epc_id 
            //hax_soft.epc_id_st epc_id;
            //epc_id.len = 0;
            //epc_id.pc = 0;
            //epc_id.id = new byte[12];
            //epc_id.crc = 0;

            //hax_soft.epc_id_list epc_list;
            //epc_list.epc_id = Enumerable.Repeat((hax_soft.epc_id_st)epc_id, 32).ToArray();
            //epc_list.size = 0;
            //epc_list.sn.serial_num = new byte[12];
            //epc_list.sn.version = new byte[20];

            //ret = hax_soft.Card7kAdapter.read_epc_id(m_connID, ref epc_list);

            ////enable_alarm
            //ret = hax_soft.Card7kAdapter.enable_alarm(m_connID);

            Console.ReadLine();
            hax_soft.Card7kAdapter.close_tcp_server();
        }
    }
}
