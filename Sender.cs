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
            string[] scanFiles = scanDir.GetFiles(Scan.TO_SEND);
            FaTaPhat fataphat = new FaTaPhat();
            fataphat.Connect();
            while (scanFiles.Length != 0)
            {
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
                 
                    scan = new Scan(scanFile);
                    if (scan.IsLocked())
                        continue;
                    try 
                    {                    
                        scan.Process(fataphat);
                    }
                    catch
                    {
                        fataphat.Dispose();
                        fataphat = null;
                        throw;
                    }
                }
                
                if (((BackgroundWorker)o).CancellationPending)
                {
                    break;
                }
                
                scanFiles = scanDir.GetFiles(Scan.TO_SEND);    
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

