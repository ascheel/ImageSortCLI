using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageSortCLI
{
	public class Device
	{
		public string device_id { get; set; }
		public string serial { get; set; }
		public string name { get; set; }
		public string local_path { get; set; }
		public DateTime added { get; set; }
		public bool ignore { get; set; }
	}

	public class DeviceFile
	{
		public int device_rowid { get; set; }
		public string path_camera { get; set; }
		public string path_local { get; set; }
		public string sha256sum { get; set; }
		public int size { get; set; }
		public DateTime created { get; set; }
	}
}
