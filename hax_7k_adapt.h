#ifndef HAX_7K_ADAPT
#define HAX_7K_ADAPT

#ifdef _WIN64
#error 64 bit programs are not supported
#endif

#ifdef hax_adapt_EXPORTS
#define HAX_ADAPT_FOR_IMPORTS_API  __declspec(dllexport)
#else
#define HAX_ADAPT_FOR_IMPORTS_API 
#endif

extern "C" {

	#define MAX_7k_DATA_LEN			8
	#define MAX_EPC_ID_LEN			12
	#define MAX_EPC_ID_COUNT		32
	#define MAX_ANTENNA_COUNT		4
	#define MAX_CLOCK_COUNT			7
	#define MAX_SN_COUNT			32

	#pragma pack(1)
	enum hax_error_code
	{
		hax_success = 0,	//成功
		hax_failed,			//失败
		hax_param_error,	//参数错误
		hax_dev_error,		//请检查串口或者socket是否正确
	};

	enum hax_dev_type {
		hax_com,		//串口链接
		hax_socket,		//socket链接
	};

	struct antenna_st {
		unsigned char	num;				//天线端口
		bool			is_enable;			//工作状态，天线端口是否启用
		unsigned short	readPower;			//读发射功率
		unsigned short	writePower;			//写发射功率
	};

	struct rfid_info_st {
		unsigned char serial_num[12];		//模块的序列号，无法修改
		unsigned char version[20];			//模块的版本号，无法修改
	};

	struct hax_7k_config_st {
		unsigned char		work_freq;					//工作频率区域，1：美国 6：中国 8：欧洲 10：中国 255:全频段
		antenna_st			antenna[MAX_ANTENNA_COUNT];	//天线参数
		unsigned char		clock[MAX_CLOCK_COUNT];		//时钟，长度7字节
		rfid_info_st		sn;							//参考rfid_info_st
	};

	struct hax_7k_data {
		unsigned char value[MAX_7k_DATA_LEN];			//标签存储区数据，长度8字节
	};

	struct epc_id_st {
		unsigned short len;					//标签长度
		unsigned short pc;					//PC值
		unsigned char  id[MAX_EPC_ID_LEN];	//EPC ID
		unsigned short crc;					//CRC
	};

	struct epc_id_list {
		rfid_info_st sn;
		int size;								//识别到的标签数量
		epc_id_st epc_id[MAX_EPC_ID_COUNT];		//参考epc_id_st
	};

	typedef void(*port_connecting)(const char *connID);
	typedef void(*port_closing)(const char *connID);
	typedef void(*output_tags)(const char *connID, epc_id_list data);
	struct IAsynchronousMessage {
		port_connecting func_port_connecting;
		port_closing	func_port_closing;
		output_tags		func_output_tags;
	};

	#pragma pack()
	/*************************************************
	Function:			open_tcp_server
	Description:		打开TCP服务器

	Return:				返回0成功，其它为错误码，在enum hax_error_code中定义
	*************************************************/
	HAX_ADAPT_FOR_IMPORTS_API hax_error_code open_tcp_server(const char *ip, unsigned int port, IAsynchronousMessage log);

	/*************************************************
	Function:			close_tcp_server
	Description:		关闭tcp服务器
	*************************************************/
	HAX_ADAPT_FOR_IMPORTS_API void close_tcp_server();

	/*************************************************
	Function:			start_auto_read
	Description:		开启自动读取EPCID

	Input:
	int time,			查询间隔，单位：毫秒
	*************************************************/
	HAX_ADAPT_FOR_IMPORTS_API void start_auto_read(int time = 200);

	/*************************************************
	Function:			stop_auto_read
	Description:		关闭自动读取EPCID
	*************************************************/
	HAX_ADAPT_FOR_IMPORTS_API void stop_auto_read();

	/*************************************************
	Function:			open_socket
	Description:		连接主板

	Input:
	unsigned int dev,	端口号或者socket
	int time_out,		超时时间
	hax_dev_type type,  链接类型，参考hax_dev_type
	Return:				返回0成功，其它为错误码，在enum hax_error_code中定义
	*************************************************/
	HAX_ADAPT_FOR_IMPORTS_API hax_error_code open(const char *connID, unsigned int socket, int time_out = 150);

	/*************************************************
	Function:			close
	Description:		关闭主板连接
	Input:
	int dev,			端口号或者socket
	Return:				返回0成功，其它为错误码，在enum hax_error_code中定义
	*************************************************/
	HAX_ADAPT_FOR_IMPORTS_API hax_error_code close(const char *connID);

	/*************************************************
	Function:			get_config
	Description:		获取主板参数

	Ouput:
	hax_7k_config_st *config,	主板参数，可参考hax_7k_config_st

	Return:				返回0成功，其它为错误码，在enum hax_error_code中定义
	*************************************************/
	HAX_ADAPT_FOR_IMPORTS_API hax_error_code get_config(const char *connID, hax_7k_config_st *config);

	/*************************************************
	Function:			set_config
	Description:		设置主板参数

	Iuput:
	const hax_7k_config_st *config,	主板参数，可参考hax_7k_config_st

	Return:				返回0成功，其它为错误码，在enum hax_error_code中定义
	*************************************************/
	HAX_ADAPT_FOR_IMPORTS_API hax_error_code set_config(const char *connID, const hax_7k_config_st *config);

	/*************************************************
	Function:			read_data
	Description:		读标签存储区

	Ouput:
	hax_7k_data data,	标签存储区的数据，可参考hax_7k_data

	Return:				返回0成功，其它为错误码，在enum hax_error_code中定义
	*************************************************/
	HAX_ADAPT_FOR_IMPORTS_API hax_error_code read_data(const char *connID, hax_7k_data *data);

	/*************************************************
	Function:			write_data
	Description:		写标签存储区

	Iuput:
	hax_7k_data data,	待写入的数据，可参考hax_7k_data

	Return:				返回0成功，其它为错误码，在enum hax_error_code中定义
	*************************************************/
	HAX_ADAPT_FOR_IMPORTS_API hax_error_code write_data(const char *connID, hax_7k_data data);

	/*************************************************
	Function:			read_epc_id
	Description:		读epc id

	Ouput:
	epc_id_list data,	epc id，可参考epc_id_list

	Return:				返回0成功，其它为错误码，在enum hax_error_code中定义
	*************************************************/
	HAX_ADAPT_FOR_IMPORTS_API hax_error_code read_epc_id(const char *connID, epc_id_list *data);

	/*************************************************
	Function:			write_epc_id
	Description:		写epc id

	Iuput:
	const unsigned char epc_id[MAX_EPC_ID_LEN],	待写入的epc id

	Return:				返回0成功，其它为错误码，在enum hax_error_code中定义
	*************************************************/
	HAX_ADAPT_FOR_IMPORTS_API hax_error_code write_epc_id(const char *connID, unsigned char epc_id[MAX_EPC_ID_LEN]);

	/*************************************************
	Function:			enable_alarm
	Description:		主板报警使能

	Return:				返回0成功，其它为错误码，在enum hax_error_code中定义
	*************************************************/
	HAX_ADAPT_FOR_IMPORTS_API hax_error_code enable_alarm(const char *connID);
}

#endif