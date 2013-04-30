using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Reflection;

namespace gutenberg.collect
{
    
    public class ScanbotContext : ApplicationContext
    {        
        public ScanbotContext(string version)
        {
            scanDir = new ScanDirectory();
            reporter = new Reporter(scanDir);
            
            InitializeContext(version);

            timer = new System.Timers.Timer(1000);
            timer.Elapsed += new ElapsedEventHandler(TimerTick);
            timer.Start();
        }
        
		protected override void Dispose(bool disposing)
		{
		    if (disposing && components != null)
			    components.Dispose();
		}
		
		private void notifyIcon_MouseUp(object sender, MouseEventArgs e) 
		{
            MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu",
                BindingFlags.Instance | BindingFlags.NonPublic);
            mi.Invoke(notifyIcon, null);
		}
        
		private void displayHelpItem_Click(object sender, EventArgs e) 
		{
		}
			
  		private void headerItem_Click(object sender, EventArgs e)
		{
            System.Diagnostics.Process.
                Start("explorer.exe", "/select," + scanDir);
	    }
        
		private void exitItem_Click(object sender, EventArgs e)
		{
            detectionStopped = true;
            if (bwDetect != null)
            {
                if (bwDetect.IsBusy) 
                {
                    bwDetect.CancelAsync();
                    detectionStopped = false;
                }
            }
            
            transmissionStopped = true;
            if (bwTransmit != null)
            {
                if (bwTransmit.IsBusy) 
                {
                    bwTransmit.CancelAsync();
                    transmissionStopped = false;
                }
            }
            
            if (detectionStopped && transmissionStopped)
                ExitThread();
	    }         
        
        private void launchGradiansItem_Click(object sender, EventArgs e)
        {
            if (e.Equals(tsmiHelp))
                System.Diagnostics.Process.Start("http://www.gradians.com/scanbot-help");
            else
                System.Diagnostics.Process.Start("http://www.gradians.com");
        }
         
        private void TimerTick(Object o, ElapsedEventArgs args)
        {
            if (countdown < 1)
            {
                countdown = 60;
                timer.Stop();
                if (detector == null)
                    detector = new Detector(scanDir);
                bwDetect = new BackgroundWorker();
                bwDetect.WorkerReportsProgress = true;
                bwDetect.WorkerSupportsCancellation = true;
                bwDetect.DoWork += new DoWorkEventHandler(detector.Execute);
                bwDetect.RunWorkerCompleted +=
                    new RunWorkerCompletedEventHandler(DetectionCompleted);
                bwDetect.ProgressChanged += 
                    new ProgressChangedEventHandler(ProgressChangedDetection);
                bwDetect.RunWorkerAsync();
            }
            else
            {
                countdown--;
            }
        }
		        
        private void balloonTip_Clicked(object sender, EventArgs e)
        {
            switch(balloonTipId)
            {
                case BALLOON_TIP_HELP:
                    tsmiHeader.PerformClick();
                    break;
                case BALLOON_TIP_SCANBOT_OFFLINE:
                    countdown = 0;
                    break;
                default:
                    break;
            }            
        }

        private void ProgressChangedDetection(Object o, 
            ProgressChangedEventArgs args)
        {
            if ((string)args.UserState == "TASK_START")
            {
                detectionStartTime = DateTime.Now;            
                tsmiDetectionStatus.Font = 
                    new Font(tsmiDetectionStatus.Font, FontStyle.Regular);
                tsmiDetectionStatus.Text = string.Format(ACTIVE_STATUS_MSG,
                    "Detecting", args.ProgressPercentage, ZERO_ZERO_ZERO);
            }
            else if ((string)args.UserState == "TASK_END")
            {
                tsmiDetectionStatus.Font = 
                    new Font(tsmiDetectionStatus.Font, FontStyle.Italic);
                tsmiDetectionStatus.Text = 
                    string.Format(INACTIVE_STATUS_MSG, "Detect");
            }        
            else 
            {
                tsmiDetectionStatus.Text = string.Format(ACTIVE_STATUS_MSG,
                    "Detecting", args.ProgressPercentage,
                    PredictTimeLeft(args.ProgressPercentage, 
                        detectionStartTime));
            }
        }

        private void ProgressChangedTransmission(Object o, 
            ProgressChangedEventArgs args)
        {
            if ((string)args.UserState == "TASK_START")
            {
                transmissionStartTime = DateTime.Now;
                tsmiTransmissionStatus.Font = 
                    new Font(tsmiTransmissionStatus.Font, FontStyle.Regular);
                tsmiTransmissionStatus.Text = string.Format(ACTIVE_STATUS_MSG,
                    "Transmitting", args.ProgressPercentage, ZERO_ZERO_ZERO);
            }
            else if ((string)args.UserState == "TASK_END")
            {
                tsmiTransmissionStatus.Font = 
                    new Font(tsmiTransmissionStatus.Font, FontStyle.Italic);
                tsmiTransmissionStatus.Text = 
                    string.Format(INACTIVE_STATUS_MSG, "Transmit");
            }
            else 
            {
                tsmiTransmissionStatus.Text = string.Format(ACTIVE_STATUS_MSG,
                    "Transmitting", args.ProgressPercentage,
                    PredictTimeLeft(args.ProgressPercentage, 
                        transmissionStartTime));
            }
        }
        
        private void DetectionCompleted(Object o,
            RunWorkerCompletedEventArgs args)
        {
            if (args.Error != null)
            {
                reporter.Log(args.Error.ToString(), ReportType.Error);
            }
            else if (args.Cancelled)
            {
                if (transmissionStopped) ExitThread(); 
                else detectionStopped = true;
            }
            else 
            {
                InitiateSend();
            }
            timer.Start();
	    }
        
        private void TransmissionCompleted(Object o, 
            RunWorkerCompletedEventArgs args)
        {
            sender.Asleep = true;
            if (args.Error != null)
            {
                reporter.Log(args.Error.ToString(), ReportType.Error);
                ScanbotIsInactive();
            }
            else if (args.Cancelled)
            {
                if (detectionStopped) ExitThread(); 
                else transmissionStopped = true;
            }
            else if (balloonTipId == BALLOON_TIP_SCANBOT_OFFLINE)
            {
                ScanbotIsActive();
                reporter.Dispatch();
            }
        }
        
        private void InitiateSend()
        {   
            if (scanDir.GetFiles(Scan.TO_SEND).Length == 0) return;
            
            if (sender == null)
            {
                sender = new Sender(scanDir);
            }
            
            if (sender.Asleep)
            {
                bwTransmit = new BackgroundWorker();
                bwTransmit.WorkerSupportsCancellation = true;
                bwTransmit.WorkerReportsProgress = true;
                bwTransmit.DoWork += new DoWorkEventHandler(sender.Execute);
                bwTransmit.ProgressChanged += 
                    new ProgressChangedEventHandler(ProgressChangedTransmission);
                bwTransmit.RunWorkerCompleted += 
                    new RunWorkerCompletedEventHandler(TransmissionCompleted);
                bwTransmit.RunWorkerAsync();
                sender.Asleep = false;
            }
        }
        
        private void ScanbotIsActive()
        {
            notifyIcon.BalloonTipTitle = WELCOME_MSG_TITLE;
            notifyIcon.BalloonTipText = 
                string.Format(WELCOME_MSG, ScanDirectory.SCAN_DIR);
            notifyIcon.ShowBalloonTip(5000);
            balloonTipId = BALLOON_TIP_HELP;
        }
        
        private void ScanbotIsInactive()
        {
            tsmiTransmissionStatus.Text = 
                String.Format(INACTIVE_STATUS_MSG, "Connect");
            notifyIcon.BalloonTipTitle = INACTIVE_MSG_TITLE;
            notifyIcon.BalloonTipText = INACTIVE_MSG;                
            notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            balloonTipId = BALLOON_TIP_SCANBOT_OFFLINE;
            notifyIcon.ShowBalloonTip(5000);
            countdown = 20;
        }
        
        private string PredictTimeLeft(int progress, DateTime startTime)
        {
            TimeSpan interval = DateTime.Now - startTime;
            double rate = progress/interval.TotalSeconds;
            double ETA = (100 - progress)/rate;
            return string.Format("{0:d2}:{1:d2}:{2:d2}",
                (int)(ETA/3600),
                (int)(ETA%3600)/60,
                (int)(ETA%3600)%60);
        }
        
		private void InitializeContext(string version) 
		{
		    components = new System.ComponentModel.Container();
			notifyIcon = new NotifyIcon(components);
			notifyIcon.ContextMenuStrip = new ContextMenuStrip();
			notifyIcon.Icon = new Icon(ICON_FILE);
            notifyIcon.Text = "Scanbot " + version;
            
            notifyIcon.MouseUp += new MouseEventHandler(notifyIcon_MouseUp);
            notifyIcon.BalloonTipClicked += new EventHandler(balloonTip_Clicked);
			
			// Menu Items
			ContextMenuStrip menuStrip = new ContextMenuStrip();
			ToolStripItemCollection items = menuStrip.Items;
            string titleText = string.Format("Scanbot");
            
            tsmiHeader = new ToolStripMenuItem(titleText,
                Image.FromFile(ICON_FILE));            
            tsmiHeader.Font = new Font("Trebuchet MS", 10, FontStyle.Bold);
            tsmiHeader.ToolTipText = "View Scantray folder location";
            tsmiHeader.Click += headerItem_Click;
            items.Add(tsmiHeader);
			items.Add(new ToolStripSeparator());
            
			tsmiDetectionStatus = new ToolStripMenuItem(
                string.Format(INACTIVE_STATUS_MSG, "Detect"));
            tsmiDetectionStatus.Font = new Font(tsmiDetectionStatus.Font, 
                FontStyle.Italic);
			tsmiDetectionStatus.ToolTipText = "Detection status";
            tsmiDetectionStatus.Enabled = false;
			items.Add(tsmiDetectionStatus);
			tsmiTransmissionStatus = new ToolStripMenuItem(
                string.Format(INACTIVE_STATUS_MSG, "Transmit"));
			tsmiTransmissionStatus.Font = new Font(tsmiTransmissionStatus.Font,
                FontStyle.Italic);
			tsmiTransmissionStatus.ToolTipText = "Transmission status"; 
            tsmiTransmissionStatus.Enabled = false;
			items.Add(tsmiTransmissionStatus);
			items.Add(new ToolStripSeparator());
            
            ToolStripMenuItem tsmiLaunchGradians = new 
                ToolStripMenuItem("Launch Gradians Website");
            tsmiLaunchGradians.Click += launchGradiansItem_Click;
            tsmiLaunchGradians.ToolTipText = "Launch Gradians.com";
            items.Add(tsmiLaunchGradians);
			tsmiHelp = new ToolStripMenuItem("Help center");
			tsmiHelp.Click += launchGradiansItem_Click;
			tsmiHelp.ToolTipText = "Launch online Help";
			items.Add(tsmiHelp);
			items.Add(new ToolStripSeparator());
			
            ToolStripMenuItem tsmiExit = new ToolStripMenuItem("Exit");
			tsmiExit.Click += exitItem_Click;
			tsmiExit.ToolTipText = "Exit the application";
			items.Add(tsmiExit);
			notifyIcon.ContextMenuStrip = menuStrip;
            
            notifyIcon.Visible = true;
            ScanbotIsActive();            
		}
        
               
        //Non-widgets
        private int countdown, balloonTipId;
        private bool detectionStopped = false, 
            transmissionStopped = false;
        private ScanDirectory scanDir;
        private Detector detector;
        private Sender sender;
        private BackgroundWorker bwDetect, bwTransmit;
        private Reporter reporter;
        private System.Timers.Timer timer;
        private DateTime detectionStartTime,
            transmissionStartTime;
        
        //Widgets
		private System.ComponentModel.IContainer components;
		private NotifyIcon notifyIcon;
		private ToolStripMenuItem tsmiHeader, tsmiDetectionStatus, 
            tsmiTransmissionStatus, tsmiHelp;
        
        private readonly Font DEFAULT_FONT_9 = 
            new Font("Trebuchet MS", 9, FontStyle.Regular);
        private readonly Font DEFAULT_FONT_10 = 
            new Font("Trebuchet MS", 10, FontStyle.Regular);
            
        private const string 
            ZERO_ZERO_ZERO = "00:00:00",
            WELCOME_MSG_TITLE = "Scanbot is active",
            INACTIVE_MSG_TITLE = "Scanbot is offline",
            WELCOME_MSG = 
                "Drop scanned worksheets into Scantray on " +
                "your Desktop (click to view)",
            INACTIVE_MSG =
                "Will attempt to reconnect in 20 seconds... (try now)",
            ACTIVE_STATUS_MSG = "{0} {1}% ({2}) ",
            INACTIVE_STATUS_MSG = "Waiting to {0}...",
            UNRESOLVED_SCANS = "Unresolved Scans ({0})",
            ICON_FILE = @"scanbotgreyicon.ico";       
        private const int 
            BALLOON_TIP_DETECTION_COMPLETED = 1,
            BALLOON_TIP_TRANSMISSION_COMPLETED = 2,
            BALLOON_TIP_HELP = 3,
            BALLOON_TIP_SCANBOT_OFFLINE = 4;
    }
    
}
