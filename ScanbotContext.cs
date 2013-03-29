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
        public const string ICON_FILE = @"scanbotgreyicon.ico";       
        
        public ScanbotContext(string version)
        {
            scanDir = new ScanDirectory();
            manifest = new Manifest(scanDir);
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
            int unresolved = scanDir.GetFiles(Scan.MANUAL_PROCESS).Length;
			tsmiPreview.Text = string.Format(UNRESOLVED_SCANS, unresolved);
            if (unresolved > 0) 
                tsmiPreview.Font = new Font(tsmiPreview.Font, FontStyle.Bold);
            else    
                tsmiPreview.Font = new Font(tsmiPreview.Font, FontStyle.Regular);
            MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu",
                BindingFlags.Instance | BindingFlags.NonPublic);
            mi.Invoke(notifyIcon, null);
		}
        
		private void displayHelpItem_Click(object sender, EventArgs e) 
		{
            notifyIcon.BalloonTipTitle = WELCOME_MSG_TITLE;
            notifyIcon.BalloonTipText = 
                string.Format(WELCOME_MSG, ScanDirectory.SCAN_DIR);
            notifyIcon.ShowBalloonTip(5000);
            balloonTipId = BALLOON_TIP_HELP;
		}
			
		private void showPreviewItem_Click(object sender, EventArgs e) 
		{
            if (frmPreview == null) PrepareFormPreview();
        
            if (frmPreview.Visible)
            {
                frmPreview.BringToFront();
            }
            else
            {
                if (scanAgentManual == null)
                    scanAgentManual = new Manual(scanDir, manifest);                
                RefreshFromManifest(scanAgentManual.Initialize());            
                SetPreviewImage(scanAgentManual.GetNextImage());
                frmPreview.Show();
            }
		}
        
  		private void headerItem_Click(object sender, EventArgs e)
		{
            System.Diagnostics.Process.
                Start("explorer.exe", "/select," + scanDir);
	    }
        
		private void exitItem_Click(object sender, EventArgs e)
		{
		    if (frmPreview != null) 
			{
		      frmPreview.Close();
		    }

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
                tsmiPreview.Enabled = false;
                if (scanAgentAuto == null)
                    scanAgentAuto = new Automatic(scanDir, manifest);
                bwDetect = new BackgroundWorker();
                bwDetect.WorkerReportsProgress = true;
                bwDetect.WorkerSupportsCancellation = true;
                bwDetect.DoWork += new DoWorkEventHandler(scanAgentAuto.Execute);
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
		        
		private void BtnFlip_Click(Object o, EventArgs args)
		{
			currentImage.RotateFlip(RotateFlipType.Rotate180FlipNone);
			SetPreviewImage(currentImage);
		}
		
		private void BtnDelete_Click(Object o, EventArgs args)
		{
			scanAgentManual.Delete();
		    if (!SetPreviewImage(scanAgentManual.GetNextImage()))
			{
				frmPreview.Close();
				InitiateSend();
			}
		}
                
        private void lvManifestEventHandler(object sender, EventArgs e)
        {
            ListViewItem lvi = lvManifest.FocusedItem;
            ListViewItem.ListViewSubItem lvsi = lvi.SubItems[1];
            if (!lvsi.Font.Strikeout)
            {
                lvi.UseItemStyleForSubItems = false;
                lvsi.Font = new Font(lvi.Font, FontStyle.Strikeout);
                lvsi.BackColor = lvi.BackColor;

                scanAgentManual.Execute(lvManifest.FocusedItem.Index, flipped);
                if (!SetPreviewImage(scanAgentManual.GetNextImage()))
                {
                    frmPreview.Close();
                    InitiateSend();
                }
            }
            btnFlip.Focus();//for button short cut to work
        }
        
        private void balloonTip_Clicked(object sender, EventArgs e)
        {
            switch(balloonTipId) 
            {
                case BALLOON_TIP_DETECTION_COMPLETED:
                    tsmiPreview.PerformClick();
                    break;
                case BALLOON_TIP_TRANSMISSION_COMPLETED:
                    break;
                case BALLOON_TIP_HELP:
                    System.Diagnostics.Process.
                        Start(@"explorer.exe", @"/select," + scanDir);
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
public bool detectionStopped = false, transmissionStopped = false;
        private void DetectionCompleted(Object o,
            RunWorkerCompletedEventArgs args)
        {
            if (args.Error != null)
            {
                GoOffline(args.Error.ToString());
            }
            else if (args.Cancelled)
            {
                if (transmissionStopped) ExitThread(); 
                else detectionStopped = true;
            }
            else 
            {
                tsmiHeader.Text = "Scanbot";
                int unresolved = scanDir.GetFiles(Scan.MANUAL_PROCESS).Length;
                tsmiPreview.Text = string.Format(UNRESOLVED_SCANS, unresolved);
                if (unresolved > 0)
                {
                    tsmiPreview.Font = new Font(tsmiPreview.Font, FontStyle.Bold);
                    notifyIcon.BalloonTipTitle = 
                        string.Format(UNRESOLVED_SCANS, unresolved);
                    notifyIcon.BalloonTipText = 
                        "Please resolve manually (click to view)";
                    notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                    balloonTipId = BALLOON_TIP_DETECTION_COMPLETED;
                    notifyIcon.ShowBalloonTip(30000);
                }
                // else
                // {
                    // tsmiPreview.Font = new Font(tsmiPreview.Font, FontStyle.Regular);
                    // notifyIcon.BalloonTipTitle = WELCOME_MSG_TITLE;
                    // notifyIcon.BalloonTipText = 
                        // string.Format(WELCOME_MSG, ScanDirectory.SCAN_DIR);
                    // notifyIcon.BalloonTipIcon = ToolTipIcon.Info;    
                    // balloonTipId = BALLOON_TIP_HELP;
                    // notifyIcon.ShowBalloonTip(5000);
                // }
                tsmiPreview.Enabled = true;
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
                GoOffline(args.Error.ToString());
            }
            else if (args.Cancelled)
            {
                if (detectionStopped) ExitThread(); 
                else transmissionStopped = true;
            }
            
        }
        
        private void OnPreviewClose(Object o, FormClosedEventArgs args)
        {
			frmPreview = null;
            InitiateSend();            
        }

        private void InitiateSend()
        {   
            if (scanDir.GetFiles(Scan.DONT_PROCESS).Length == 0) return;
            
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

		private void RefreshFromManifest(object[] remainingItems)
		{
            lvManifest.Items.Clear();
            foreach (object o in remainingItems)
            {
                DisplayItem di = (DisplayItem)o;
                ListViewItem lvi = lvManifest.Items.Add(di.DisplayPageNumber);
                lvi.SubItems.Add(di.DisplayName);
                if (di.CompleteSet)
                {
                    lvi.BackColor = Color.DarkGray;
                }
            }
		}		
		
        private bool SetPreviewImage(Image image)
        {
            if (image == null) return false;
        
            this.flipped = (image == this.currentImage)? true:false;
            this.currentImage = image;        
            //first resize
            int w = pnlPBox.Width, h = (int)(w*((double)image.Height/image.Width));
            Image newImage = new Bitmap(w, h);
            using (Graphics gr = Graphics.FromImage(newImage))
            {               
                gr.SmoothingMode = SmoothingMode.HighQuality;
                gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
                gr.PixelOffsetMode = PixelOffsetMode.HighQuality;
                gr.DrawImage(image, new Rectangle(0, 0, w, h));
            }
             
            //then crop (top and bottom sections)
            Bitmap bmp = new Bitmap(newImage);
            Bitmap top = null, bottom = null;
            h = pnlPBox.Height;
            Rectangle rtop = new Rectangle(new Point(0, 0), 
                new Size(w, h/2));
            top = bmp.Clone(rtop, bmp.PixelFormat);         
            Rectangle rbottom = new Rectangle(new Point(0, bmp.Height - h/2),
                new Size(w, h/2));
            bottom = bmp.Clone(rbottom, bmp.PixelFormat);
            
			int padding = 5;
            Image composite = new Bitmap(w, h);
            using (Graphics gr = Graphics.FromImage(composite))
            {
                gr.DrawImage(top, new Rectangle(0, 0, w, h/2));
                gr.DrawImage(bottom, new Rectangle(0, h/2 + (padding), w, h/2));
                string remaining = string.Format("{0:d2}", 
                    scanDir.GetFiles(Scan.MANUAL_PROCESS).Length);
                gr.DrawString(remaining, BIG_ORNG,
                    Brushes.DarkOrange, new PointF(5, 5));
            }
            pbPreview.Image = composite;
            return true;
        }
        
        private void GoOffline(string error)
        {
            reporter.Log(error, ReportType.Error);
            tsmiHeader.Text = "Scanbot (offline)";
            tsmiDetectionStatus.Text = 
                string.Format(INACTIVE_STATUS_MSG, "connect");
            tsmiTransmissionStatus.Text = 
                string.Format(INACTIVE_STATUS_MSG, "connect");
            notifyIcon.BalloonTipTitle = "Scanbot is offline";
            notifyIcon.BalloonTipText = 
                "Will attempt to reconnect in 20 seconds... (try now)";
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
            
			tsmiPreview = new ToolStripMenuItem(
                string.Format(UNRESOLVED_SCANS, 0));
			tsmiPreview.Click += showPreviewItem_Click;
			tsmiPreview.ToolTipText = "Preview unresolved Scans";
			items.Add(tsmiPreview);
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
		}

        private void PrepareFormPreview()
        {
            frmPreview = new Form();
            frmPreview.SuspendLayout();
			
			int widthPreview = 750, btnWidth = 120, btnHeight = 30,
			    padding = 5, heightPreview = 500, heightFrameBar = 30;

            frmPreview.MinimumSize = 
                new Size(widthPreview + 2*btnWidth + 2*padding, 
				heightPreview + heightFrameBar + 2*padding);
            frmPreview.FormBorderStyle = FormBorderStyle.FixedDialog;
            frmPreview.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            frmPreview.MaximizeBox = false;
            frmPreview.Icon = new Icon(ICON_FILE);
            frmPreview.StartPosition = FormStartPosition.CenterScreen;
            frmPreview.Text = "Scanbot - Resolution Panel";
            frmPreview.FormClosed += new FormClosedEventHandler(OnPreviewClose);
            
            Size btnSize = new Size(btnWidth, btnHeight);

            Panel pnlLeft = new Panel();
            pnlLeft.Size = new Size(2*btnWidth, heightPreview);
            pnlLeft.Location = new Point(padding, padding);
            frmPreview.Controls.Add(pnlLeft);

            btnDelete = new Button();
            btnDelete.Size = btnSize;
            btnDelete.Font = DEFAULT_FONT_9;
            btnDelete.FlatStyle = FlatStyle.Flat;
            btnDelete.ForeColor = Color.White;
            btnDelete.Location = new Point(0, 0);
            btnDelete.Text = "&Delete";
            btnDelete.Click += new EventHandler(BtnDelete_Click);
            pnlLeft.Controls.Add(btnDelete);

            btnFlip = new Button();
            btnFlip.Size = btnSize;
            btnFlip.Font = DEFAULT_FONT_9;
            btnFlip.ForeColor = Color.White;
            btnFlip.FlatStyle = FlatStyle.Flat;
            btnFlip.Location = new Point(btnSize.Width, 0);
            btnFlip.Text = "&Flip";
            btnFlip.Click += new EventHandler(BtnFlip_Click);
            pnlLeft.Controls.Add(btnFlip);

            lvManifest = new ListView();
            lvManifest.View = View.Details;
            lvManifest.Columns.Add("Pg", 30, HorizontalAlignment.Center);
            lvManifest.Columns.Add("Student Name", 2*btnSize.Width-50,
                HorizontalAlignment.Left);
            lvManifest.Size = new Size(2*btnSize.Width, 
                pnlLeft.Size.Height-btnSize.Height);
            lvManifest.BorderStyle = BorderStyle.FixedSingle;
            lvManifest.Location = new Point(0, btnSize.Height+padding);
            lvManifest.Font = DEFAULT_FONT_10;
            lvManifest.TabStop = false;
            lvManifest.Enabled = true;
            lvManifest.GridLines = true;
            lvManifest.FullRowSelect = true;
            lvManifest.Click += new EventHandler(lvManifestEventHandler);
            pnlLeft.Controls.Add(lvManifest);
			
            // holds the Image
            pnlPBox = new Panel();
            pnlPBox.MinimumSize = new Size(widthPreview, heightPreview);
            pnlPBox.BorderStyle = BorderStyle.FixedSingle;
            pnlPBox.Location = new Point(2*btnWidth + 2*padding,
                padding);
            pnlPBox.AutoSize = false;
            frmPreview.Controls.Add(pnlPBox);

            pbPreview = new PictureBox();
            pbPreview.SizeMode = PictureBoxSizeMode.AutoSize;
            pnlPBox.Controls.Add(pbPreview);

            frmPreview.ResumeLayout();
        }
               
        //Non-widgets
        private int countdown, balloonTipId;
        private bool flipped;
        private ScanDirectory scanDir;
        private Image currentImage;
        private Manifest manifest;
        private Manual scanAgentManual;
        private Automatic scanAgentAuto;
        private BackgroundWorker bwDetect, bwTransmit;
        private Sender sender;
        private Reporter reporter;
        private System.Timers.Timer timer;
        private DateTime detectionStartTime,
            transmissionStartTime;
        
        //Widgets
		private System.ComponentModel.IContainer components;
		private NotifyIcon notifyIcon;
		private ToolStripMenuItem tsmiHeader, tsmiDetectionStatus, 
            tsmiTransmissionStatus, tsmiPreview, tsmiHelp;
        private PictureBox pbPreview;
        private ListView lvManifest;
        private Panel pnlPBox;
		private Button btnFlip, btnDelete;
        private Form frmPreview;
        
        private readonly Font DEFAULT_FONT_9 = 
            new Font("Trebuchet MS", 9, FontStyle.Regular);
        private readonly Font DEFAULT_FONT_10 = 
            new Font("Trebuchet MS", 10, FontStyle.Regular);
        private readonly Font BIG_ORNG = 
            new Font("Trebuchet MS", 48, FontStyle.Regular);
            
        private const string ZERO_ZERO_ZERO = "00:00:00";
        private const string WELCOME_MSG_TITLE = "Scanbot is active";
        private const string WELCOME_MSG = 
            "Drop scanned worksheets into Scantray on your Desktop (click to view)";
        private const string ACTIVE_STATUS_MSG = "{0} {1}% ({2}) ";
        private const string INACTIVE_STATUS_MSG = "Waiting to {0}...";
        private const string UNRESOLVED_SCANS = "Unresolved Scans ({0})";
        private const int 
            BALLOON_TIP_DETECTION_COMPLETED = 1,
            BALLOON_TIP_TRANSMISSION_COMPLETED = 2,
            BALLOON_TIP_HELP = 3,
            BALLOON_TIP_SCANBOT_OFFLINE = 4;
    }
    
}
