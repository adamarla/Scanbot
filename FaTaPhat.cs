using System;
using System.IO;
using System.Net;
using System.Net.FtpClient;
using System.Xml;
using System.Collections.Specialized;

namespace gutenberg.collect
{
	public class FaTaPhat : IDisposable
	{
		
		public FaTaPhat()
		{
			this.server = FaTaPhat.serverProps["hostname"];
			this.login = FaTaPhat.serverProps["login"];
			this.password = FaTaPhat.serverProps["password"];
			this.bankroot = FaTaPhat.serverProps["bankroot"];
		}

		public void Connect()
		{
			if (client != null && client.IsConnected) return;
            
			client = new FtpClient();
			client.Host = server;
			client.Credentials = new NetworkCredential(login, password);
			client.DataConnectionType = FtpDataConnectionType.PASV;			
			client.Connect();
		}
		
		public void Disconnect()
		{
			if (client != null && client.IsConnected)
				client.Disconnect();
		}
		
		public bool Exists(string remote)
		{
			remote = bankroot + "/" + remote;
			return client.FileExists(remote);
		}
		
		public bool Put(string local, string remote, FtpDataType fileType)
		{
			remote = bankroot + "/" + remote;
			byte[] buffer = new byte[262144];
			Stream istream = File.Open(local, FileMode.Open);
			int bytesRead = 0;
            using (Stream ostream = client.OpenWrite(remote, fileType)) 
            {
                while ((bytesRead = 
                    istream.Read(buffer, 0, buffer.Length)) != 0) 
                {
                    ostream.Write(buffer, 0, bytesRead);
                    ostream.Flush();
                }
                ostream.Close();
                istream.Close();
            }
			return true;
		}
		
		public bool Get(string local, string remote, FtpDataType fileType)
		{
			remote = bankroot + "/" + remote;
			Stream ostream = File.Create(local);
			byte[] buffer = new byte[262144];
			int bytesRead = 0; 
			using (Stream istream = client.OpenRead(remote, fileType)) 
            {
				while ((bytesRead = 
                    istream.Read(buffer, 0, buffer.Length)) != 0)
                {
					ostream.Write(buffer, 0, bytesRead);
					ostream.Flush();
				}
				ostream.Close();
				istream.Close();
			}
			return true;
		}
        
        public void Dispose()
        {
            client.Dispose();
        }
        		
		private FtpClient client;
		private string server, login, password, bankroot;
		private static NameValueCollection serverProps = FaTaPhat.GetServerProps();
		        
        private static NameValueCollection GetServerProps()
        {
            string configFile = Path.Combine(System.Environment.GetFolderPath
                (Environment.SpecialFolder.Personal), ".scanbot");
            configFile = Path.Combine(configFile, "config.xml");
            bool runLocal = File.Exists(configFile);
            /**
                 ****************************************************************
                 * WARNING: DO NOT EDIT THIS BLOCK OF CODE, YOU RISK SENDING
                 *          SCANS TO PRODUCTION BY ACCIDENT
                 ****************************************************************
                 */
            NameValueCollection properties = new NameValueCollection();
            if (!runLocal)
            {
                properties.Add("hostname", "109.74.201.62");
                properties.Add("login", "gutenberg");
                properties.Add("password", "shibb0leth");
                properties.Add("bankroot", "/home/gutenberg/bank");
            }
            else
            {
                /**
                 ****************************************************
                 ** configuration file has your password, at least it
                 ** is in a hidden folder in YOUR $HOME.
                 ****************************************************
                 */
                XmlDocument config = new XmlDocument();
                config.LoadXml(File.ReadAllText(configFile));
                XmlNodeList elements = config.GetElementsByTagName("entry");
                foreach (XmlNode element in elements)
                {
                    properties.Add(element.Attributes["key"].Value, 
                        element.InnerText);
                }
            }
            return properties;
        }
        
	}
}
