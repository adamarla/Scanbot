using System;
using System.IO;
using System.ComponentModel;

namespace gutenberg.collect
{
    public class Sender
    {
        public Sender(ScanDirectory scanDir)
        {
            this.scanDir = scanDir;
            asleep = true;
        }
     
        public void Execute(Object o, DoWorkEventArgs args)
        {
            asleep = false;
            Scan scan = null;
            string[] scanFiles = scanDir.GetFiles(Scan.DONT_PROCESS);
            FaTaPhat fataphat = new FaTaPhat();
            fataphat.Connect();
            while (scanFiles.Length != 0)
            {
                char[] sep = {'_'};
                int fileCount = 0;
                int progress = (int)(fileCount * 100.00 / scanFiles.Length);
				((BackgroundWorker)o).ReportProgress(progress, "TASK_START");
				foreach (string scanFile in scanFiles)
                {
                    if (((BackgroundWorker)o).CancellationPending)
                    {
                        args.Cancel = true;
                        break;
                    }
                    
                    fileCount++;
                    progress = (int)(fileCount * 100.00 / scanFiles.Length);
                    ((BackgroundWorker)o).ReportProgress(progress);
                 
                    string fileName = Path.GetFileName(scanFile);
                    string[] tokens = fileName.Substring(0, 
                     fileName.IndexOf(Scan.DONT_PROCESS)).Split(sep);
                    scan = new Scan(tokens[0], tokens[1], 
                     int.Parse(tokens[2]), scanFile);
                    scan.Process(fataphat);
                }
                
                if (((BackgroundWorker)o).CancellationPending)
                {
                    break;
                }
                
                scanFiles = scanDir.GetFiles(Scan.DONT_PROCESS);
            }
            ((BackgroundWorker)o).ReportProgress(100, "TASK_END");
            fataphat.Disconnect();
            asleep = true;
        }
     
        public bool Asleep 
        {
            get 
            {
                return asleep;   
            } 
            
            set
            {
                asleep = value;
            }
        }
     
        private ScanDirectory scanDir;
        private bool asleep;
    }
}

