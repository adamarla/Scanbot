using System;
using System.IO;
using System.Collections;
using System.Collections.Specialized;
using System.Net.FtpClient;

namespace gutenberg.collect
{
	public class Manifest
	{
		public Manifest(ScanDirectory scanDir)
		{
			manifest = new NameValueCollection();
			keyFilesManifest = new NameValueCollection();
			leftovers = scanDir.Leftovers;
			if (File.Exists(leftovers))
			{
				this.LoadFile(manifest, leftovers);
			}
			keyfiles = scanDir.Keyfiles;
			if (File.Exists(keyfiles))
			{
				this.LoadFile(keyFilesManifest, keyfiles);
			}
			ExpireOldKeyFiles();
		}

		public void Checkpoint()
		{
			SaveFile(manifest, leftovers);
		}

		public string Get(string scanKey)
		{
			if (manifest[scanKey] == null)
			{
				string fileKey = scanKey.Substring(0, 6);
				if (keyFilesManifest[fileKey] == null)
				{
					FetchManifest(scanKey);
				}					
			}
			return manifest[scanKey];
		}
		
		public void Remove(string scanKey, bool save)
		{
			manifest.Remove(scanKey);
			if (save)
				this.Checkpoint();
		}
		
		public void Clear(bool save)
		{
			manifest.Clear();
			if (save)
				this.Checkpoint();
		}
		
		public string[] GetKeys()
		{
			return manifest.AllKeys;
		}
		
		public bool IsComplete(string key)
		{
			bool complete = true;
			string val = manifest[key];
			string prefix = key.Substring(0, key.Length - 1);
			char[] sep = {'-'};
			string[] tokens = val.Split(sep);
			int totalPages = int.Parse(tokens[7]);
			for (int i = 1; i <= totalPages; i++)
			{
				if (manifest[prefix + i] == null)
				{
					complete = false;
					break;	
				}				
			}
			return complete;
		}
		
		private void ExpireOldKeyFiles()
		{
			foreach(string keyPrefix in keyFilesManifest.AllKeys)
			{
				DateTime timeofkey = DateTime.ParseExact(
					keyFilesManifest[keyPrefix], FORMAT, null);
				TimeSpan t = DateTime.Now - timeofkey;
				if (t.TotalHours > TWO_DAYS)
				{
                     foreach (string key in manifest.AllKeys)
                     {
                         if (key.StartsWith(keyPrefix))
                         {
                             manifest.Remove(key);
                         }
                     }
                     this.Checkpoint();
                     keyFilesManifest.Remove(keyPrefix);
                     SaveFile(keyFilesManifest, keyfiles);                    
				}
			}
		}

		private void FetchManifest(string scanKey)
		{
			string key = null;
			string keyFile = null;
			string format = null;
			if (IsSuggestion(scanKey))
			{
				key = scanKey;
				format = "front-desk/teachers/{0}/petty-cash/keyFile";
			}
			else
			{
				key = scanKey.Substring(0, 6);
				format = "atm/{0}/keyFile";
			}
			
			keyFile = string.Format(format, key);
			string local = System.DateTime.Now.Millisecond.ToString();
			using (FaTaPhat fataphat = new FaTaPhat()) 
            {
                fataphat.Connect();
                if (fataphat.Exists(keyFile))
                {
                    fataphat.Get(local, keyFile, FtpDataType.ASCII);
                    this.LoadFile(manifest, local);
                    File.Delete(local);
                }
                fataphat.Disconnect();
            }
            this.Checkpoint();
            if (!IsSuggestion(scanKey))
			{
				keyFilesManifest.Add(key, DateTime.Now.ToString(FORMAT));
				SaveFile(this.keyFilesManifest, this.keyfiles);
			}
		}
		
		private void LoadFile(NameValueCollection collection, string file)
		{
			string[] lines = File.ReadAllLines(file);
			string[] tokens = null;
			char[] sep = {'=', ':'};
			foreach (string line in lines)
			{
				if (line.StartsWith("#"))
					continue;
				tokens = line.Split(sep);
				if (collection[tokens[0]] == null)
					collection.Add(tokens[0], tokens[1]);
			}			
		}
		
		private void SaveFile(NameValueCollection collection, string file)
		{
			string[] lines = new string[collection.Count];
			int i = 0;
			foreach (string key in collection)
			{
				lines[i] = string.Format("{0}={1}", key, collection[key]);
				i++;
			}
			File.WriteAllLines(file, lines);
		}
        
        private bool IsSuggestion(string scanKey)
        {            
            return scanKey.Contains("-");   
        }
		
		private NameValueCollection manifest, keyFilesManifest;
		private string leftovers, keyfiles;
		private const string FORMAT = "ddMMyyHHmmss";
		private const int TWO_DAYS = 24;//hours
	}
}

