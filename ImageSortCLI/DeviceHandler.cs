using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace ImageSortCLI
{
	class DeviceHandler
	{
		public IEnumerable<MediaDevices.MediaDevice> devices;
		private string db_filename;
		private SqliteConnection db;
		public string destination;

		public DeviceHandler()
		{
			this.devices = MediaDevices.MediaDevice.GetDevices();

			// Set up the DB
			this.db_filename = @"C:\Users\Art\Documents\ImageSort.db";
			//this.db_filename = ":memory:";
			this.setDB(this.db_filename);
			this.destination = @"C:\Users\Art\Pictures\ImageSort";
		}

		~DeviceHandler()
		{
			this.db.Close();
		}

		private void setDB(string filename)
		{
			var builder = new SqliteConnectionStringBuilder();
			builder.DataSource = filename;
			builder.Mode = SqliteOpenMode.ReadWriteCreate;

			this.db = new SqliteConnection(builder.ConnectionString);
			this.db.Open();
			this.PrepDB();
		}

		private void PrepDB()
		{
			using (var transaction = this.db.BeginTransaction())
			{
				string sql = "";
				sql = "DROP TABLE IF EXISTS device";
				var cmd = this.db.CreateCommand();
				cmd.CommandText = sql;

				cmd.ExecuteNonQuery();

				sql = "DROP TABLE IF EXISTS file";
				cmd = this.db.CreateCommand();
				cmd.CommandText = sql;
				cmd.ExecuteNonQuery();

				sql = @"
				CREATE TABLE IF NOT EXISTS device (
					device_id TEXT UNIQUE,
					serial TEXT,
					name TEXT,
					added DATETIME,
					ignore BOOLEAN,
					PRIMARY KEY (device_id)
				)";
				cmd = this.db.CreateCommand();
				cmd.CommandText = sql;
				cmd.ExecuteNonQuery();

				sql = @"
				CREATE TABLE IF NOT EXISTS file (
					device_rowid INT,
					path_camera TEXT UNIQUE,
					path_local TEXT UNIQUE,
					sha256sum TEXT UNIQUE,
					size INT,
					PRIMARY KEY (device_rowid, path_camera)
				)";
				cmd = this.db.CreateCommand();
				cmd.CommandText = sql;
				cmd.ExecuteNonQuery();
				transaction.Commit();
			}
		}

		public string getSHA256Sum(string input)
		{
			var inputBytes = Encoding.ASCII.GetBytes(input);
			return this.getSHA256Sum(inputBytes);
		}

		public string getSHA256Sum(byte[] inputBytes)
		{
			SHA256 hash = SHA256.Create();
			byte[] hashValue = hash.ComputeHash(inputBytes);
			return BitConverter.ToString(hashValue);
		}

		public MediaDevices.MediaDevice GetMediaDevice(Device internalDevice)
		{
			return GetMediaDevice(internalDevice.device_id, internalDevice.serial);
		}

		public MediaDevices.MediaDevice GetMediaDevice(string device_id, string serial)
		{
			foreach (var device in MediaDevices.MediaDevice.GetDevices())
			{
				if (device.DeviceId == device_id && device.SerialNumber == serial)
					return device;
			}
			throw new IndexOutOfRangeException("DeviceId and SerialNumber do not reference a currently attached device.");
		}

		public Device GetDevice(MediaDevices.MediaDevice device)
		{
			Device internalDevice = new Device();
			internalDevice.device_id= device.DeviceId;
			internalDevice.serial = device.SerialNumber;
			internalDevice.name = device.FriendlyName;
			internalDevice.added = DateTime.Now;
			internalDevice.ignore = false;

			if (this.deviceExistsInDB(internalDevice.device_id, internalDevice.serial))
			{
				string sql = "SELECT ignore FROM device WHERE device_id = @device_id AND serial = @serial";
				var cmd = this.db.CreateCommand();
				cmd.CommandText = sql;
				cmd.Parameters.AddWithValue("@device_id", internalDevice.device_id);
				cmd.Parameters.AddWithValue("@serial", internalDevice.serial);
				var reader = cmd.ExecuteReader();
				reader.Read();
				var value = reader.GetBoolean(0);
				Console.WriteLine("Device is being ignored: " + value);
				// internalDevice.ignore = 
			}
			return internalDevice;
		}

		/// <summary>
		/// Does device exist?  Returns 1 if yes, 0 if no.
		/// </summary>
		/// <param name="device_id">MediaDevices.MediaDevice.DeviceId</param>
		/// <param name="serial">MediaDevices.MediaDevice.SerialNumber</param>
		/// <returns>bool, 1=yes, 0=no</returns>
		public bool deviceExistsInDB(string device_id, string serial)
		{
			// SQL creation
			var cmd = this.db.CreateCommand();

			// Statement
			string sql = "SELECT COUNT(*) FROM device WHERE device_id = @device_id AND serial = @serial";

			// Parameters
			cmd.CommandText = sql;
			cmd.Parameters.AddWithValue("@device_id", device_id);
			cmd.Parameters.AddWithValue("@serial", serial);

			// Execute
			var reader = cmd.ExecuteReader();

			// Read a record
			reader.Read();

			// Get the count
			var count = reader.GetInt32(0);

			// Return 1 if exists, 0 if not
			return (count > 0);
		}

		public bool deviceExistsInDB(MediaDevices.MediaDevice device)
		{
			return this.deviceExistsInDB(device.DeviceId, device.SerialNumber);
		}


		public List<string> CopyNewFiles(Device internalDevice)
		{
			var device = this.GetMediaDevice(internalDevice);
			return this.CopyNewFiles(device);
		}

		public List<string> CopyNewFiles(MediaDevices.MediaDevice device)
		{
			string sql = "SELECT rowid FROM device WHERE device_id = @device_id AND serial = @serial";
			var cmd = this.db.CreateCommand();
			cmd.CommandText = sql;
			cmd.Parameters.AddWithValue("@device_id", device.DeviceId);
			cmd.Parameters.AddWithValue("@serial", device.SerialNumber);
			int rowid = (int)cmd.ExecuteScalar();

			var newFiles = this.GetNewFiles(device);
			List<string> failedToCopy = new List<string>();

			foreach (var file in this.ListImages(device))
			{
				string filebase = Path.GetFileName(file);
				string parent = Path.GetDirectoryName(file);
				string filename = Path.Combine(
					this.destination,
					this.GetLocalPath(device),
					DateTime.Now.ToString("yyyy_MM_dd"),
					filebase
					);
				File fileObj = new File();
				fileObj.path_camera = file;
				fileObj.path_local = filename;
				fileObj.device_row_id = rowid;
				Console.WriteLine(file + " => " + filename);
			}

			return failedToCopy;
		}

		public List<string> GetNewFiles(MediaDevices.MediaDevice device)
		{
			return this.GetNewFiles(device.DeviceId, device.SerialNumber);
		}

		public List<string> GetNewFiles(string device_id, string serial)
		{
			MediaDevices.MediaDevice device = this.GetMediaDevice(device_id, serial);
			MediaDevices.MediaDirectoryInfo root = device.GetRootDirectory();

			List<string> newFiles = new List<string>();

			foreach (var file in ListImages(device, root.FullName))
			{
				var exists = this.FileExistsInDB(device_id, serial, file);
				if (!exists)
					newFiles.Add(file);
			}

			return newFiles;
		}
		
		public bool FileExistsInDB(string device_id, string serial, string device_filename)
		{
			string sql = "SELECT rowid FROM device WHERE device_id = @device_id AND serial = @serial";
			
			var cmd = this.db.CreateCommand();
			cmd.CommandText = sql;
			cmd.Parameters.AddWithValue("@device_id", device_id);
			cmd.Parameters.AddWithValue("@serial", serial);
			var reader = cmd.ExecuteReader();

			if (!reader.HasRows)
				return false;
			reader.Read();
			Console.WriteLine(reader);
			var rowid = reader.GetInt64(0);

			sql = @"SELECT COUNT(*) FROM file WHERE device_row_id = @device_row_id AND path_camera = @path_camera";
			cmd = this.db.CreateCommand();
			cmd.CommandText = sql;
			cmd.Parameters.AddWithValue("@device_row_id", rowid);
			cmd.Parameters.AddWithValue("@path_camera", device_filename);
			reader = cmd.ExecuteReader();
			if (!reader.HasRows)
			{
				return false;
			}
			reader.Read();
			bool exists = reader.GetBoolean(0);
			return exists;
		}

/*		private IEnumerable<string> WalkDevice(MediaDevices.MediaDevice device, string path)
		{
			foreach (var dir in device.GetDirectories(path))
			{
				foreach (var entry in WalkDevice(device, dir))
					yield return entry;
			}
			foreach (var file in device.GetFiles(path))
			{
				yield return file;
			}
		}
*/
		public void AddDevice(MediaDevices.MediaDevice device)
		{
			if (this.deviceExistsInDB(device))
				return;

			using (var transaction = this.db.BeginTransaction())
			{
				var cmd = this.db.CreateCommand();
				cmd.CommandText = @"INSERT INTO device (device_id, serial, name, local_path, added, ignore) VALUES (@device_id, @serial, @name, @local_path, @added, @ignore)";
				cmd.Parameters.AddWithValue("@device_id", device.DeviceId);
				cmd.Parameters.AddWithValue("@serial", device.SerialNumber);
				cmd.Parameters.AddWithValue("@name", device.FriendlyName);
				cmd.Parameters.AddWithValue("@local_path", this.GetLocalPath(device));
				cmd.Parameters.AddWithValue("@added", DateTime.Now);
				cmd.Parameters.AddWithValue("@ignore", false);

				var reader = cmd.ExecuteNonQuery();
				transaction.Commit();
			}
		}

		private string GetLocalPath(MediaDevices.MediaDevice device)
		{
			if (deviceExistsInDB(device))
			{
				string sql = @"SELECT local_path FROM device WHERE device_id = @device_id AND serial = @serial";
				var cmd = this.db.CreateCommand();
				cmd.CommandText = sql;
				cmd.Parameters.AddWithValue("@device_id", device.DeviceId);
				cmd.Parameters.AddWithValue("@serial", device.SerialNumber);
				var value = cmd.ExecuteScalar().ToString();
				return value;
			}
			else
			{
				int count = 0;
				while (true)
				{
					string name = "";
					if (count == 0)
						name = device.FriendlyName;
					else
						name = device.FriendlyName + " (" + count + ")";

					if (!Directory.Exists(name))
						return name;
					else
						count += 1;
				}
			}
		}

		public IEnumerable<string> ListImages(MediaDevices.MediaDevice device, string path)
		{
			List<string> excludes = new List<string>();
			excludes.Add("System Volume Information");

			foreach (var file in device.GetFiles(path))
			{
				yield return file;
			}
			foreach (var dir in device.GetDirectories(path))
			{
				if (excludes.Contains(Path.GetFileName(dir)))
				{
					Console.WriteLine("  Excluding dir: " + dir);
					continue;
				}
				foreach (var item in this.ListImages(device, dir))
					yield return item;
			}
		}
		public IEnumerable<string> ListImages(MediaDevices.MediaDevice device)
		{
			string path = device.GetRootDirectory().FullName;
			foreach (var item in this.ListImages(device, path))
			{
				yield return item;
			}
		}
		private void DEBUG([CallerLineNumber] int lineNo = 0)
		{
			this.DEBUG("", lineNo);
		}
		private void DEBUG(string message, [CallerLineNumber] int lineNo = 0)
		{
			Console.WriteLine("    DEBUG (" + message + "): Line " + lineNo + " reached.");
		}
	}
}
