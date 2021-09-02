using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;

namespace ImageSortCLI
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Listing MTP Devices:");
			IEnumerable<MediaDevices.MediaDevice> devices = MediaDevices.MediaDevice.GetDevices().ToList();
			var deviceList = devices.ToList();
			Dictionary<String, String> gearlist = new Dictionary<string, string>();

			DeviceHandler dh = new DeviceHandler();

			foreach(var device in devices)
			{
				if (!device.IsConnected)
					device.Connect();

				// Skip SD cards...  for now.
				if (device.Model.Trim() == "SD")
					continue;

				//PrintDeviceInfo(device);
				Console.WriteLine("Working: (" + device.Description.Trim() + " - " + device.SerialNumber + ")");
				Device internalDevice = dh.GetDevice(device);

				var files = dh.CopyNewFiles(device);

				//device.EnumerateFiles
				device.Disconnect();
			}
		}

		static void PrintDeviceInfo(MediaDevices.MediaDevice device)
		{
			Console.WriteLine("Device ID: " + device.DeviceId);
			Console.WriteLine("Is Connected: " + device.IsConnected.ToString());
			Console.WriteLine("Description: " + device.Description);
			Console.WriteLine("Friendly Name: " + device.FriendlyName);
			Console.WriteLine("Manufacturer: " + device.Manufacturer);
			if (!device.IsConnected)
				device.Connect();

			Console.WriteLine("Type: " + device.DeviceType.ToString());
			Console.WriteLine("Firmware Version: " + device.FirmwareVersion);

			if (device.FunctionalUniqueId != null)
			{
				Console.WriteLine("Functional Unique ID: " + device.FunctionalUniqueId.ToString());
			}
			Console.WriteLine("Model: " + device.Model);
			if (device.ModelUniqueId != null)
			{
				Console.WriteLine("Model Unique ID: " + device.ModelUniqueId.ToString());
			}
			Console.WriteLine("Protocol: " + device.Protocol);
			Console.WriteLine("Serial Number: " + device.SerialNumber.ToString());
		}

		static void DEBUG([CallerLineNumber] int lineNo = 0)
		{
			Console.WriteLine("    Line " + lineNo + " reached.");
		}
	}
}

/* Table: Device
 *		model
 *		serial
 *		root_dir
 * 
 * */


//\\ ? \swd # wpdbusenum # _??_usbstor # disk & ven_realsil & prod_rtsuerlun0 & rev_1.00        #                    0000 # {53f56307-b6bf-11d0-94f2-00a0c91efb8b}#{6ac27878-a6fa-4155-ba85-f98f491d4f33}

//\\ ? \usb # vid_04e8                        & pid_6860    & ms_comp_mtp     & samsung_android # 6 & 26b44be6 & 0 & 0000 # {6ac27878-a6fa-4155-ba85-f98f491d4f33}
