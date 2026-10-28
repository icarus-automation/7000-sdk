/**
 * 构建一个服务，并以server.conf作为配置文件
 */
//#include <windows.h>



#include <windows.h>
//#pragma comment(linker, "/SUBSYSTEM:WINDOWS")
//#pragma comment(linker, "/ENTRY:mainCRTStartup")
#include <stdlib.h>
#include <string>
#include <assert.h>
#include <iostream>

#include "hax_7k_adapt.h"

typedef hax_error_code (*pfunc_init)();
typedef hax_error_code (*pfunc_uninit)();
typedef hax_error_code (*pfunc_open)(unsigned int dev, hax_dev_type type, int time_out/* = 150*/);
typedef hax_error_code (*pfunc_close)(unsigned int dev);
typedef hax_error_code (*pfunc_get_config)(unsigned int dev, hax_7k_config_st *config);
typedef hax_error_code (*pfunc_set_config)(unsigned int dev, const hax_7k_config_st *config);
typedef hax_error_code (*pfunc_write_data)(unsigned int dev, hax_7k_data data);
typedef hax_error_code (*pfunc_read_data)(unsigned int dev, hax_7k_data *data);
typedef hax_error_code (*pfunc_read_epc_id)(unsigned int dev, epc_id_list *data);
typedef hax_error_code (*pfunc_write_epc_id)(unsigned int dev, unsigned char epc_id[MAX_EPC_ID_LEN]);
typedef hax_error_code (*pfunc_enable_alarm)(unsigned int dev);

int main(int argc, char * argv[]) {
	HMODULE hModule = ::LoadLibraryW(L"card_7k.dll");
	if (hModule) {
		pfunc_open			i_open			= (pfunc_open)::GetProcAddress(hModule, "open");
		pfunc_close			i_close			= (pfunc_close)::GetProcAddress(hModule, "close");
		pfunc_get_config	i_get_config	= (pfunc_get_config)::GetProcAddress(hModule, "get_config");
		pfunc_set_config	i_set_config	= (pfunc_set_config)::GetProcAddress(hModule, "set_config");
		pfunc_write_data	i_write_data	= (pfunc_write_data)::GetProcAddress(hModule, "write_data");
		pfunc_read_data		i_read_data		= (pfunc_read_data)::GetProcAddress(hModule, "read_data");
		pfunc_read_epc_id	i_read_epc_id	= (pfunc_read_epc_id)::GetProcAddress(hModule, "read_epc_id");
		pfunc_write_epc_id	i_write_epc_id	= (pfunc_write_epc_id)::GetProcAddress(hModule, "write_epc_id");
		pfunc_enable_alarm	i_enable_alarm	= (pfunc_enable_alarm)::GetProcAddress(hModule, "enable_alarm");

		assert(i_open);
		assert(i_close);
		assert(i_get_config);
		assert(i_set_config);
		assert(i_write_data);
		assert(i_read_data);
		assert(i_read_epc_id);
		assert(i_write_epc_id);
		assert(i_enable_alarm);

		unsigned int dev = 0;
		std::cout << "input port number:" << std::endl;
		std::cin >> dev;
		//2.open参数传入串口号，同时类型输入hax_com。socket同理，dev=socket，类型输入hax_socket
		hax_error_code ret_code = i_open(dev, hax_com, 150);
		if (ret_code == hax_success) {
			//3.各种api调用，参数均要求输入dev
			hax_7k_config_st config = { 0 };
			ret_code = i_get_config(dev, &config);

			//config.work_freq = 6;
			config.clock[2] = 0x7;
			//config.antenna[0].is_enable = true;
			//config.antenna[0].num = 0;
			//config.antenna[0].readPower = 3100;
			//config.antenna[0].writePower = 3200;
			ret_code = i_set_config(dev, &config);

			hax_7k_data data = { 0 };
			ret_code = i_read_data(dev, &data);
			data.value[0] = 0x12;
			ret_code = i_write_data(dev, data);

			epc_id_list id_list = { 0 };
			ret_code = i_read_epc_id(dev, &id_list);
			unsigned char epc_id[MAX_EPC_ID_LEN] = { 0 };
			epc_id[0] = 0x33;
			ret_code = i_write_epc_id(dev, epc_id);

			i_enable_alarm(dev);

			//4.close关闭对板卡的操作
			i_close(dev);
		}
		else {
			std::cout << "open port error:" << ret_code << std::endl;
		}

		::FreeLibrary(hModule);
	}

	return 0;
}