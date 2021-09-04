using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Globalization;
using System.Diagnostics;

namespace ImageSortCLI
{
	class DeviceHandler
	{
		public IEnumerable<MediaDevices.MediaDevice> devices;
		private string db_filename;
		private SqliteConnection db;
		public string destination;
		public string tmpdir;

		/// <summary>
		/// Constructor, invoked once for the application's run.
		/// </summary>
		public DeviceHandler()
		{
			this.devices = MediaDevices.MediaDevice.GetDevices();

			this.destination = @"C:\Users\Art\Pictures\ImageSort";

			// Set up the DB
			this.db_filename = Path.Combine(this.destination, "ImageSort.db");
			//this.db_filename = ":memory:";
			this.setDB(this.db_filename);
			this.tmpdir = Path.Combine(this.destination, "tmp");
		}

		/// <summary>
		/// Close the DB or bad things happen.
		/// </summary>
		~DeviceHandler()
		{
			this.db.Close();
		}

		/// <summary>
		/// Set up and open the database.
		/// </summary>
		/// <param name="filename">Filename to store the database.</param>
		private void setDB(string filename)
		{
			var builder = new SqliteConnectionStringBuilder();
			builder.DataSource = filename;
			builder.Mode = SqliteOpenMode.ReadWriteCreate;

			this.db = new SqliteConnection(builder.ConnectionString);
			this.db.Open();
			this.PrepDB();
		}

		/// <summary>
		/// Prep the database.
		/// </summary>
		private void PrepDB()
		{
			using (var transaction = this.db.BeginTransaction())
			{
				string sql = "";
				SqliteCommand cmd;
/*				sql = "DROP TABLE IF EXISTS device";
				cmd = this.db.CreateCommand();
				cmd.CommandText = sql;

				cmd.ExecuteNonQuery();

				sql = "DROP TABLE IF EXISTS file";
				cmd = this.db.CreateCommand();
				cmd.CommandText = sql;
				cmd.ExecuteNonQuery();
*/
				sql = @"
				CREATE TABLE IF NOT EXISTS device (
					device_id TEXT UNIQUE,
					serial TEXT,
					name TEXT,
					local_path TEXT,	
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
					path_camera TEXT,
					path_local TEXT UNIQUE,
					sha256sum TEXT,
					size INT,
					created DATETIME,
					PRIMARY KEY (device_rowid, path_camera)
				)";
				cmd = this.db.CreateCommand();
				cmd.CommandText = sql;
				cmd.ExecuteNonQuery();
				transaction.Commit();
			}
		}

		/// <summary>
		/// Get SHA256SUM from a stream
		/// </summary>
		/// <param name="stream">A stream.</param>
		/// <returns>64 character checksum in ASCII</returns>
		public string getSHA256Sum(Stream stream)
		{
			return BitConverter.ToString(SHA256.Create().ComputeHash(stream)).Replace("-", "");
		}
		
		/// <summary>
		/// Get SHA256SUM from a string
		/// </summary>
		/// <param name="input">A string.</param>
		/// <returns>64 character checksum in ASCII</returns>
		public string getSHA256Sum(string input)
		{
			var inputBytes = Encoding.ASCII.GetBytes(input);
			return this.getSHA256Sum(inputBytes);
		}

		/// <summary>
		/// Get SHA256SUM from a byte array
		/// </summary>
		/// <param name="inputBytes">byte array</param>
		/// <returns>64 character checksum in ASCII</returns>
		public string getSHA256Sum(byte[] inputBytes)
		{
			SHA256 hash = SHA256.Create();
			byte[] hashValue = hash.ComputeHash(inputBytes);
			return BitConverter.ToString(hashValue).Replace("-", "");
		}

		/// <summary>
		/// Get MediaDevice from our Device class based on DeviceId and SerialNumber
		/// </summary>
		/// <param name="internalDevice">Device class object</param>
		/// <returns>MediaDevices.MediaDevice object</returns>
		public MediaDevices.MediaDevice GetMediaDevice(Device internalDevice)
		{
			return GetMediaDevice(internalDevice.device_id, internalDevice.serial);
		}

		/// <summary>
		/// Get MediaDevice from DeviceId and SerialNumber
		/// </summary>
		/// <param name="device_id">Device ID</param>
		/// <param name="serial">Serial Number</param>
		/// <returns>MediaDevices.MediaDevice object</returns>
		public MediaDevices.MediaDevice GetMediaDevice(string device_id, string serial)
		{
			foreach (var device in MediaDevices.MediaDevice.GetDevices())
			{
				if (device.DeviceId == device_id && device.SerialNumber == serial)
					return device;
			}
			throw new IndexOutOfRangeException("DeviceId and SerialNumber do not reference a currently attached device.");
		}

		/// <summary>
		/// Get our Device class object based on MediaDevice object.
		/// </summary>
		/// <param name="device">MediaDevice object</param>
		/// <returns>Device class object</returns>
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
				// Console.WriteLine("Device is being ignored: " + value);
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

		/// <summary>
		/// Does the device already exist in the database?
		/// </summary>
		/// <param name="device">MediaDevice object</param>
		/// <returns>boolean, 1 for exists, 0 for does not exist.</returns>
		public bool deviceExistsInDB(MediaDevices.MediaDevice device)
		{
			return this.deviceExistsInDB(device.DeviceId, device.SerialNumber);
		}

		/// <summary>
		/// Find new files on Devices and copy them to local.
		/// </summary>
		/// <param name="internalDevice">Internal Device class object describing the device to search.</param>
		/// <returns>List of strings containing filenames that failed to copy.</returns>
		public List<string> CopyNewFiles(Device internalDevice)
		{
			var device = this.GetMediaDevice(internalDevice);
			return this.CopyNewFiles(device);
		}

		/// <summary>
		/// Find new files on Devices and copy them to local.
		/// </summary>
		/// <param name="device">MediaDevice object describing the device to search.</param>
		/// <returns>List of strings containing filenames that failed to copy.</returns>
		public List<string> CopyNewFiles(MediaDevices.MediaDevice device)
		{
			string sql = "SELECT rowid FROM device WHERE device_id = @device_id AND serial = @serial";
			var cmd = this.db.CreateCommand();
			cmd.CommandText = sql;
			cmd.Parameters.AddWithValue("@device_id", device.DeviceId);
			cmd.Parameters.AddWithValue("@serial", device.SerialNumber);
			int rowid = (int)(long)cmd.ExecuteScalar();

			var newFiles = this.GetNewFiles(device);
			Console.WriteLine("Found " + newFiles.Count + " new files.");
			List<string> failedToCopy = new List<string>();

			string _localpath = this.GetLocalPath(device);

			Dictionary<string, long> stats = new Dictionary<string, long>();
			stats["size"] = 0;
			stats["count"] = 0;

			Stopwatch sw = new Stopwatch();
			sw.Start();

			//foreach (var file in this.ListImages(device))
			foreach (var file in newFiles)
			{
				stats["count"] += 1;
				if (stats["count"] % 10 == 0)
				{
					var elapsed = (long)sw.Elapsed.TotalSeconds;
					var count_speed = Decimal.Round(new Decimal((double)stats["count"] / (double)elapsed), 1);
					var size_speed = SizeSuffix(stats["size"] / (long)elapsed);
					Console.WriteLine("Current speed: " + count_speed + " files/sec, " + size_speed + " bytes/sec");
				}
				var _obj = device.GetFileInfo(file);

				// Get 8.3 filename
				string filebase = Path.GetFileName(file);

				// What's the parent directory?
				string parent = Path.GetDirectoryName(file);

				// Our temporary filename
				var newbase = Path.GetFileNameWithoutExtension(filebase) + "." + this.getSHA256Sum(filebase).Replace("-", "").Substring(0, 10) + Path.GetExtension(filebase);
				string tmp_filename = Path.Combine(
					this.tmpdir,
					newbase
					);

				// Create parent dir
				Directory.CreateDirectory(Path.GetDirectoryName(tmp_filename));

				//Download to temporary file
				using (FileStream fs = new FileStream(tmp_filename, FileMode.Create, FileAccess.Write))
				{
					//MemoryStream memoryStream = new System.IO.MemoryStream();
					device.DownloadFile(file, fs);
				}
				string sha256sum;
				using (FileStream fs = new FileStream(tmp_filename, FileMode.Open, FileAccess.Read))
				{
					sha256sum = getSHA256Sum(fs);
				}

				DateTime _theDate;
				// Get EXIF data
				try
				{
					_theDate = this.getMetadataDateTime(tmp_filename);
				}
				catch (MetadataExtractor.ImageProcessingException)
				{
					_theDate = (DateTime)_obj.CreationTime;
					failedToCopy.Add(file);
				}

				// Our target filename
				string newFileBase;

				// The parent directory for the target image name
				string _parentdir = _theDate.ToString("yyyy-MM");
				string filename;
				string extension = Path.GetExtension(filebase).ToLower();
				int filecount = 0;
				while (true)
				{
					if (filecount == 0)
						newFileBase = _theDate.ToString("yyyy-MM-dd HH.mm.ss") + "." + Path.GetFileNameWithoutExtension(file) + extension;
					else
						newFileBase = _theDate.ToString("yyyy-MM-dd HH.mm.ss") + "." + filecount + "." + Path.GetFileNameWithoutExtension(file) + extension;
					filename = Path.Combine(
						this.destination,
						_localpath,
						_parentdir,
						newFileBase
						);
					if (!File.Exists(filename))
					{
						break;
					}
					filecount += 1;
				}

				// Our database file object
				DeviceFile fileObj = new DeviceFile();
				fileObj.path_camera = file;
				fileObj.path_local = filename;
				fileObj.device_rowid = rowid;
				fileObj.sha256sum = sha256sum;
				fileObj.size = (int)_obj.Length;
				fileObj.created = _theDate;

				stats["size"] += (long)fileObj.size;

				// Move into place
				Directory.CreateDirectory(Path.GetDirectoryName(fileObj.path_local));
				File.Move(tmp_filename, fileObj.path_local);

				// And to screen
				string stats_count = stats["count"].ToString().PadLeft(5, ' ');
				File.SetCreationTimeUtc(fileObj.path_local, _theDate);
				File.SetLastWriteTimeUtc(fileObj.path_local, _theDate);
				long remaining = newFiles.Count - stats["count"];
				Console.WriteLine(stats["count"] + ": " + file + " => " + fileObj.path_local + "  (" + remaining + "/" + newFiles.Count + " remains)");

				// Store it
				insertFileIntoDB(fileObj);
			}

			return failedToCopy;
		}

		static string SizeSuffix(Int64 value, int decimalPlaces = 1)
		{
			string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
			if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
			if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }
			if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

			// mag is 0 for bytes, 1 for KB, 2, for MB, etc.
			int mag = (int)Math.Log(value, 1024);

			// 1L << (mag * 10) == 2 ^ (10 * mag) 
			// [i.e. the number of bytes in the unit corresponding to mag]
			decimal adjustedSize = (decimal)value / (1L << (mag * 10));

			// make adjustment when the value is large enough that
			// it would round up to 1000 or more
			if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
			{
				mag += 1;
				adjustedSize /= 1024;
			}

			return string.Format("{0:n" + decimalPlaces + "} {1}",
				adjustedSize,
				SizeSuffixes[mag]);
		}

		private DateTime getMetadataDateTime(string filename)
		{
			DateTime _theDateTime;

			List<string> jpeg_extensions = new List<string>();
			jpeg_extensions.Add(".jpg");
			jpeg_extensions.Add(".jpeg");

			List<string> mpeg4_extensions = new List<string>();
			mpeg4_extensions.Add(".mpeg4");
			mpeg4_extensions.Add(".mp4");
			mpeg4_extensions.Add(".m4v");

			string ext = Path.GetExtension(filename).ToLower();
			if (jpeg_extensions.Contains(ext))
				_theDateTime = getJPEGDateTime(filename);
			else if (mpeg4_extensions.Contains(ext))
				_theDateTime = getMPEG4DateTime(filename);
			else
				throw new Exception("Unable to get Date/Time");
			return _theDateTime;
		}

		/// <summary>
		/// Insert an individiaul file into the database
		/// </summary>
		/// <param name="file">Internal DeviceFile class object.</param>
		private void insertFileIntoDB(DeviceFile file)
		{
			using (SqliteTransaction transaction = this.db.BeginTransaction())
			{
				string sql = @"INSERT INTO 
					file (
						device_rowid,
						path_camera,
						path_local,
						sha256sum,
						size,
						created
					) VALUES (
						@device_rowid,
						@path_camera,
						@path_local,
						@sha256sum,
						@size,
						@created
					)";
				var cmd = this.db.CreateCommand();
				cmd.CommandText = sql;
				cmd.Parameters.AddWithValue("@device_rowid", file.device_rowid);
				cmd.Parameters.AddWithValue("@path_camera", file.path_camera);
				cmd.Parameters.AddWithValue("@path_local", file.path_local);
				cmd.Parameters.AddWithValue("@sha256sum", file.sha256sum);
				cmd.Parameters.AddWithValue("@size", file.size);
				cmd.Parameters.AddWithValue("@created", file.created);
				cmd.ExecuteNonQuery();
				transaction.Commit();
			}
		}

		/// <summary>
		/// Find new files that need to be copied. Doesn't actually copy anything.
		/// </summary>
		/// <param name="device">MediaDevice class object for device to search.</param>
		/// <returns>List of strings containing new files.</returns>
		public List<string> GetNewFiles(MediaDevices.MediaDevice device)
		{
			return this.GetNewFiles(device.DeviceId, device.SerialNumber);
		}

		/// <summary>
		/// Find new files that need to be copied.  Doesn't actually copy anything.
		/// </summary>
		/// <param name="device_id">Device ID with which to search the database.</param>
		/// <param name="serial">Serial Number with which to search the database.</param>
		/// <returns>List of strings containing new filenames.</returns>
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
		
		/// <summary>
		/// Check if file exists in Database.
		/// </summary>
		/// <param name="device_id">DeviceId as its known by MediaDevice</param>
		/// <param name="serial">Serial Number as its known by MediaDevice</param>
		/// <param name="device_filename">Filename as it exists on the device.</param>
		/// <returns>Boolean, 1 = exists, 0 = does not exist</returns>
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
			var rowid = reader.GetInt64(0);

			sql = @"SELECT COUNT(*) FROM file WHERE device_rowid = @device_rowid AND path_camera = @path_camera";
			cmd = this.db.CreateCommand();
			cmd.CommandText = sql;
			cmd.Parameters.AddWithValue("@device_rowid", rowid);
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

		/// <summary>
		/// Insert's device into database
		/// </summary>
		/// <param name="device">MediaDevice class object</param>
		public void InsertDeviceIntoDB(MediaDevices.MediaDevice device, string localPathOverride = "")
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
				if (localPathOverride == "")
					cmd.Parameters.AddWithValue("@local_path", this.GetLocalPath(device));
				else
					cmd.Parameters.AddWithValue("@local_path", localPathOverride);
				cmd.Parameters.AddWithValue("@added", DateTime.Now);
				cmd.Parameters.AddWithValue("@ignore", false);

				var reader = cmd.ExecuteNonQuery();
				transaction.Commit();
			}
		}

		/// <summary>
		/// Gets Local Path that corresponds to a device.
		/// </summary>
		/// <param name="device">MediaDevice class object</param>
		/// <returns>string containing file path</returns>
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

		/// <summary>
		/// Recursive function that finds all files on a device.
		/// </summary>
		/// <param name="device">MediaDevice class object</param>
		/// <param name="path">Path of file</param>
		/// <returns>Iterator of strings describing the paths of files.</returns>
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

		/// <summary>
		/// Recursive function that finds all files on a device.  Assumes root directory as the starting point.
		/// </summary>
		/// <param name="device">MediaDevice class object</param>
		/// <returns>Iterator of strings describing the paths of files.</returns>
		public IEnumerable<string> ListImages(MediaDevices.MediaDevice device)
		{
			string path = device.GetRootDirectory().FullName;
			foreach (var item in this.ListImages(device, path))
			{
				yield return item;
			}
		}

		private DateTime getMPEG4DateTime(string filename)
		{
			var taglist = getMetadata(filename);
			foreach (var _dir in taglist)
			{
				if (_dir.Name == "QuickTime Track Header")
				{
					foreach (var _tag in _dir.Tags)
					{
						if (_tag.Name == "Created")
						{
							bool success = DateTime.TryParseExact(
								_tag.Description,
								"ddd MMM dd HH:mm:ss yyyy",
								new CultureInfo("en-US"),
								DateTimeStyles.None,
								out DateTime _output
								);
							if (!success)
								throw new FormatException($"Date/Time cannot be parsed ({_tag.Description}).");
							return _output;
						}
					}
				}
			}
			throw new KeyNotFoundException("Date/Time Metadata tag not found.");
		}

		private DateTime getJPEGDateTime(string filename)
		{
			var taglist = getMetadata(filename);
			foreach (var _dir in taglist)
			{
				if (_dir.Name == "Exif IFD0")
				{
					foreach (var _tag in _dir.Tags)
					{
						if (_tag.Name == "Date/Time")
						{
							bool success = DateTime.TryParseExact(
								_tag.Description,
								"yyyy:MM:dd HH:mm:ss",
								new CultureInfo("en-US"),
								DateTimeStyles.None,
								out DateTime _output
								);
							if (!success)
								throw new FormatException($"Date/Time cannot be parsed ({_tag.Description}).");
							return _output;
						}
					}
				}
			}
			throw new KeyNotFoundException("Date/Time EXIF tag not found. (Exif IFD0 - Date/Time)");
		}

		private List<MetadataExtractor.Directory> getMetadata(string filename)
		{
			IEnumerable<MetadataExtractor.Directory> directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(filename);
			return directories.ToList();
		}

		/// <summary>
		/// DEBUG function to print crap to screen.
		/// </summary>
		/// <param name="lineNo"></param>
		private void DEBUG([CallerLineNumber] int lineNo = 0)
		{
			this.DEBUG("", lineNo);
		}

		/// <summary>
		/// DEBUG function to print crap to screen.
		/// </summary>
		/// <param name="message"></param>
		/// <param name="lineNo"></param>
		private void DEBUG(string message, [CallerLineNumber] int lineNo = 0)
		{
			Console.WriteLine("    DEBUG (" + message + "): Line " + lineNo + " reached.");
		}
	}
}
