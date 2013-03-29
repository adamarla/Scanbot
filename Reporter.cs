using System;
using System.IO;

namespace gutenberg.collect
{
    public class Reporter
    {
        public Reporter(ScanDirectory scanDir)
        {
            hostname = System.Net.Dns.GetHostName();
            reportFile = scanDir + "/report.grdns";
        }
        
        public void Log(string text, ReportType type) 
        {
            StreamWriter writer = new StreamWriter(reportFile, true);
            writer.WriteLine("======================");
            writer.WriteLine(hostname);
            writer.WriteLine(DateTime.Now);
            writer.WriteLine(text);
            writer.WriteLine("======================");
            writer.Close();
        }
        
        public void Dispatch()
        {
            if (File.Exists(reportFile))
            {
                FaTaPhat f = new FaTaPhat();
                string remote = string.Format("scanbot-logs/{0}.{1}", hostname, DateTime.Now.Ticks);
                try 
                {
                    f.Connect();
                    f.Put(reportFile, remote, System.Net.FtpClient.FtpDataType.ASCII);
                    f.Disconnect();
                    File.Delete(reportFile);
                } catch (Exception) {}
            }            
        }
        
        string hostname;
        string reportFile;
    }
    
    public enum ReportType {
        Error,
        Info
    }
}

