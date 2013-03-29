using System;
using System.IO;
using System.Xml;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Specialized;

namespace gutenberg.collect
{
    public class MainPanel
    {
        public MainPanel(string title)
        {
            scanDir = Path.Combine(System.Environment.GetFolderPath
                (Environment.SpecialFolder.Desktop), "Gradians-Lockbox");

            if (!Directory.Exists(scanDir))
                Directory.CreateDirectory(scanDir);

            FaTaPhat.serverProps = GetServerProps();
            manifest = new Manifest(scanDir);
            reporter = new Reporter(scanDir);
            
            PreparePanel(title);            
            timer = new System.Timers.Timer(1000);
            timer.SynchronizingObject = this;
            timer.Elapsed += new ElapsedEventHandler(TimerTick);
            timer.Start();
			
            ComponentResourceManager resources = new ComponentResourceManager();
            notifyIcon = new NotifyIcon();
	    notifyIcon.Icon = new Icon("scanbotgreyicon.ico");
            //notifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("scanbotgreyicon.ico")));
        }
                
        private void TimerTick(Object o, ElapsedEventArgs args)
        {
            if (countdown == 0)
            {
                countdown = 300;
                btnStart.PerformClick();
            }
            else
            {
                countdown--;
            }
        }
                
        private void ButtonClickHandler(Object o, EventArgs args)
        {
            if (o.Equals(btnStart))
            {
                SwitchMode(Mode.Manual);                
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
            else if (o.Equals(btnManual))
            {
                SwitchMode(Mode.Manual);                
                if (scanAgentManual == null)
                    scanAgentManual = new Manual(scanDir, manifest);
                object[] remainingItems = scanAgentManual.Initialize();
                nextImage = scanAgentManual.GetNextImage();
                flipped = false;
                PreparePreview(remainingItems, nextImage);
            }
            else if (o.Equals(btnFlip))
            {
                nextImage.RotateFlip(RotateFlipType.Rotate180FlipNone);
                this.SetPreviewImage(nextImage);
                flipped = !flipped;
            }
            else if (o.Equals(btnDelete))
            {
                scanAgentManual.Delete();
                if ((nextImage = scanAgentManual.GetNextImage()) != null)
                {
                    flipped = false;
                    SetPreviewImage(nextImage);
                }
                else
                {
                    frmPreview.Close();
                    InitiateSend();
                }               
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
                if ((nextImage = scanAgentManual.GetNextImage()) != null)
                {
                    flipped = false;
                    SetPreviewImage(nextImage);
                }
                else
                {
                    frmPreview.Close();
                    InitiateSend();
                }
            }
            btnFlip.Focus();//for button short cut to work
        }

        private void ProgressChangedDetection(Object o, ProgressChangedEventArgs args)
        {
            if (pbrDetection.Value == 0)
            {
                detectionStartTime = DateTime.Now;
            }
            else if (pbrDetection.Value == 100)
            {
                lblDetectionTimeLeft.Text = ZERO_ZERO_ZERO;
            }
            else if (pbrDetection.Value < args.ProgressPercentage)
            {
                lblDetectionTimeLeft.Text = PredictTimeLeft(args.ProgressPercentage, 
                    detectionStartTime);
            }
            pbrDetection.Value = args.ProgressPercentage;
        }

        private void ProgressChangedTransmission(Object o, ProgressChangedEventArgs args)
        {
            if (pbrTransmission.Value == 0)
            {
                transmissionStartTime = DateTime.Now;
            }
            else if (pbrTransmission.Value == 100)
            {
                lblTransmissionTimeLeft.Text = ZERO_ZERO_ZERO;
            }
            else if (pbrTransmission.Value < args.ProgressPercentage)
            {
                lblTransmissionTimeLeft.Text = PredictTimeLeft(args.ProgressPercentage,
                    transmissionStartTime);
            }
            pbrTransmission.Value = args.ProgressPercentage;
        }

        private void DetectionCompleted(Object o, RunWorkerCompletedEventArgs args)
        {
            if (args.Error != null)
            {
                reporter.Log(args.Error.ToString(), ReportType.Error);
            }
            SwitchMode(Mode.Automatic);
        }
        
        private void TransmissionCompleted(Object o, RunWorkerCompletedEventArgs args)
        {
            if (cancelPending)
            {
                this.Close();
            }
            else
            {
                if (args.Error != null)
                {
                    reporter.Log(args.Error.ToString(), ReportType.Error);
                }
                pbrTransmission.Value = 0;
            }
        }
        
        void OnFormClosing(Object o, FormClosingEventArgs args)
        {
            if (sender != null && !sender.Asleep)
            {
                bwTransmit.CancelAsync();
                this.Enabled = false;
                if (frmPreview != null)
                    frmPreview.Enabled = false;
                args.Cancel = true;
                cancelPending = true;
            }
        }
        
        void OnPreviewClose(Object o, FormClosedEventArgs args)
        {
            SwitchMode(Mode.Automatic);
        }

        void SwitchMode(Mode mode)
        {
            switch (mode)
            {
                case Mode.Automatic:
                    timer.Start();
                    btnStart.Enabled = true;
                    int unresolved = Directory.GetFiles(scanDir, "*"+
                        Scan.MANUAL_PROCESS
                    ).Length;           
                    if (unresolved != 0)
                    {
                        btnManual.Enabled = true;
                        btnManual.Text = string.Format("&Unresolved({0})",
                        unresolved);
                    }
                    else
                    {
                        btnManual.Text = "&Unresolved";
                    }
                    InitiateSend();
                    break;
                case Mode.Manual:
                    timer.Stop();
                    pbrDetection.Value = 0;
                    btnManual.Enabled = false;
                    btnStart.Enabled = false;
                    break;
            }            
        }

        void InitiateSend()
        {
            if (Directory.GetFiles(scanDir, "*"+Scan.DONT_PROCESS).Length != 0)
            {
                if (sender == null)
                {
                    sender = new Sender(scanDir);
                }
                
                if (sender.Asleep)
                {
                    bwTransmit = new BackgroundWorker();
                    bwTransmit.WorkerSupportsCancellation = true;
                    bwTransmit.WorkerReportsProgress = true;
                    bwTransmit.DoWork += new DoWorkEventHandler(sender.Awaken);
                    bwTransmit.ProgressChanged += 
                        new ProgressChangedEventHandler(ProgressChangedTransmission);
                    bwTransmit.RunWorkerCompleted += 
                        new RunWorkerCompletedEventHandler(TransmissionCompleted);
                    bwTransmit.RunWorkerAsync();                   
                }
            } 
        }

        private void SetPreviewImage(Image image)
        {
            //first resize
            int w = 900, h = (int)(900*((double)image.Height/image.Width));
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
            h = heightPreview-heightFrameBar-2*padding;
            Rectangle rtop = new Rectangle(new Point(0, 0), 
                new Size(w, h/2));
            top = bmp.Clone(rtop, bmp.PixelFormat);         
            Rectangle rbottom = new Rectangle(new Point(0, bmp.Height-h/2),
                new Size(w, h/2));
            bottom = bmp.Clone(rbottom, bmp.PixelFormat);
            
            Image composite = new Bitmap(w, h);
            using (Graphics gr = Graphics.FromImage(composite))
            {
                gr.DrawImage(top, new Rectangle(0, 0, w, h/2));
                gr.DrawImage(bottom, new Rectangle(0, h/2+(padding), w, h/2));
                string count = string.Format("{0:d2}",
                    Directory.GetFiles(scanDir, "*"+Scan.MANUAL_PROCESS).Length);
                gr.DrawString(count, BIG_ORNG, Brushes.DarkOrange, new PointF(30, 15));
            }
            pbPreview.Image = composite;
        }
        
        private string PredictTimeLeft(int progress, DateTime startTime)
        {
            TimeSpan interval = DateTime.Now-startTime;
            double rate = progress/interval.TotalSeconds;
            double ETA = (100-progress)/rate;
            return string.Format("{0:d2}:{1:d2}:{2:d2}",
                (int)(ETA/3600),
                (int)(ETA%3600)/60,
                (int)(ETA%3600)%60);
        }
        
        private void PreparePanel(string title)
        {
            Size szBtn = new Size(120, 40);
			
            this.SuspendLayout();
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.Fixed3D;
            this.BackColor = SystemColors.ButtonFace;
            this.Size = new Size(szBtn.Width*2 + padding*5, 2*szBtn.Height + heightFrameBar);
            this.MaximizeBox = false;
            this.Icon = new Icon("scanbotgreyicon.ico");
            this.FormClosing += new FormClosingEventHandler(this.OnFormClosing);
            
            int clientWidth = this.ClientSize.Width;

            Size szPanel = new Size(clientWidth-2*padding, btnHeight);
            Size szLabel = new Size(szPanel.Width/8, szPanel.Height);
            Size szBar = new Size(szPanel.Width*1/2, szPanel.Height);
			
	    Panel pnlTop = new Panel();
	    pnlTop.Width = szBtn.Width*2 + 3*padding;
	    pnlTop.Height = 2*szBar.Height + heightFrameBar;
            /*
            pnlDetection = new Panel();
            pnlDetection.Size = szPanel;
            pnlDetection.Location = new Point(padding, 0);
            Controls.Add(pnlDetection);
            
            lblDetection = new Label();
            lblDetection.Text = "Detection";
            lblDetection.Font = DEFAULT_FONT_9;
            lblDetection.TextAlign = ContentAlignment.MiddleLeft;
            lblDetection.Size = szLabel;
            lblDetection.ForeColor = Color.White;
            lblDetection.Location = new Point(0, 0);
            pnlDetection.Controls.Add(lblDetection);
            
            lblDetectionTimeLeft = new Label();
            lblDetectionTimeLeft.Text = ZERO_ZERO_ZERO;
            lblDetectionTimeLeft.Font = DEFAULT_FONT_9;
            lblDetectionTimeLeft.TextAlign = ContentAlignment.MiddleCenter;
            lblDetectionTimeLeft.Size = szLabel;
            lblDetectionTimeLeft.ForeColor = Color.DarkOrange;
            lblDetectionTimeLeft.Location = new Point(szLabel.Width, 0);
            pnlDetection.Controls.Add(lblDetectionTimeLeft);
            
            pbrDetection = new ProgressBar();
            pbrDetection.Size = szBar;
            pbrDetection.Location = new Point(szLabel.Width*2, 0);
            pbrDetection.Style = ProgressBarStyle.Blocks;
            pbrDetection.ForeColor = Color.DarkOrange;
            pbrDetection.Value = 0;
            pnlDetection.Controls.Add(pbrDetection);
            */
            btnStart = new Button();
            btnStart.Text = "";
            btnStart.TextAlign = ContentAlignment.MiddleCenter;
            btnStart.Size = szBtn;
            btnStart.Font = DEFAULT_FONT_9;           
            btnStart.ForeColor = Color.White;
            btnStart.FlatStyle = FlatStyle.Flat;
            btnStart.Image = Image.FromFile("glyphicons_363_cloud_upload.png");          
            btnStart.ImageAlign = ContentAlignment.MiddleLeft;
            btnStart.Width -= padding;
            btnStart.Enabled = true;
            btnStart.Location = new Point(padding, 0);
            btnStart.Click += new EventHandler(ButtonClickHandler);
	    pnlTop.Controls.Add(btnStart);
            //pnlDetection.Controls.Add(btnStart);
            Button btnProg = new Button();
            btnProg.Text = "";
            btnProg.TextAlign = ContentAlignment.MiddleCenter;
            btnProg.Size = szBtn;
            btnProg.Font = DEFAULT_FONT_9;           
            btnProg.ForeColor = Color.White;
            btnProg.FlatStyle = FlatStyle.Flat;
            btnProg.Image = Image.FromFile("glyphicons_363_cloud_upload.png");          
            btnProg.ImageAlign = ContentAlignment.MiddleLeft;
            btnProg.Width -= padding;
            btnProg.Enabled = true;
            btnProg.Location = new Point(padding, 0);
            btnProg.Click += new EventHandler(ButtonClickHandler);
	    pnlTop.Controls.Add(btnProg);
            
	    /*				
            pnlTransmission = new Panel();
            pnlTransmission.Size = szPanel;
            pnlTransmission.Location = new Point(padding, szPanel.Height);
            Controls.Add(pnlTransmission);
            
            lblTransmission = new Label();
            lblTransmission.Text = "Transmission";
            lblTransmission.Font = DEFAULT_FONT_9;
            lblTransmission.TextAlign = ContentAlignment.MiddleLeft;
            lblTransmission.Size = szLabel;
            lblTransmission.ForeColor = Color.White;
            lblTransmission.Location = new Point(0, 0);
            pnlTransmission.Controls.Add(lblTransmission);
            
            lblTransmissionTimeLeft = new Label();
            lblTransmissionTimeLeft.Text = ZERO_ZERO_ZERO;
            lblTransmissionTimeLeft.Font = DEFAULT_FONT_9;
            lblTransmissionTimeLeft.TextAlign = ContentAlignment.MiddleCenter;
            lblTransmissionTimeLeft.Size = szLabel;
            lblTransmissionTimeLeft.ForeColor = Color.DarkOrange;
            lblTransmissionTimeLeft.Location = new Point(szLabel.Width, 0);
            pnlTransmission.Controls.Add(lblTransmissionTimeLeft);
            
            pbrTransmission = new ProgressBar();
            pbrTransmission.Size = szBar;
            pbrTransmission.Location = new Point(szLabel.Width*2, 0);
            pbrTransmission.ForeColor = Color.DarkOrange;
            pbrTransmission.Style = ProgressBarStyle.Blocks;
            pbrTransmission.Value = 0;
            pnlTransmission.Controls.Add(pbrTransmission);
            */
            btnManual = new Button();
            btnManual.Text = "";
            btnManual.TextAlign = ContentAlignment.MiddleCenter;
            btnManual.Size = szBtn;
            btnManual.Font = DEFAULT_FONT_9;
            btnManual.ForeColor = Color.White;
            btnManual.FlatStyle = FlatStyle.Flat;
            btnManual.Image = Image.FromFile("glyphicons_258_qrcode.png");
            btnManual.ImageAlign = ContentAlignment.MiddleLeft;
            btnManual.Width -= padding;
            btnManual.Enabled = false;
            btnManual.Location = new Point(szBtn.Width+padding, 0);
            btnManual.Click += new EventHandler(ButtonClickHandler);
	    pnlTop.Controls.Add(btnManual);
            //pnlTransmission.Controls.Add(btnManual);
	    Controls.Add(pnlTop);

            this.ResumeLayout();
        }

        void PreparePreview(object[] remainingItems, Image image)
        {
            frmPreview = new Form();
            frmPreview.SuspendLayout();

            frmPreview.MinimumSize = 
                new Size(widthPreview+2*btnWidth+2*padding, heightPreview);
            frmPreview.FormBorderStyle = FormBorderStyle.FixedDialog;
            frmPreview.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            frmPreview.MaximizeBox = false;
            frmPreview.Icon = new Icon("scanbotgreyicon.ico");
            frmPreview.StartPosition = FormStartPosition.CenterScreen;
            frmPreview.Text = "Scanbot - Resolution Panel";
            frmPreview.FormClosed += new FormClosedEventHandler(OnPreviewClose);
            
            Size btnSize = new Size(btnWidth, btnHeight);

            Panel pnlLeft = new Panel();
            pnlLeft.Size = new Size(2*btnWidth,
                heightPreview-heightFrameBar-padding);
            pnlLeft.Location = new Point(padding, padding);
            frmPreview.Controls.Add(pnlLeft);

            btnDelete = new Button();
            btnDelete.Size = btnSize;
            btnDelete.Font = DEFAULT_FONT_9;
            btnDelete.FlatStyle = FlatStyle.Flat;
            btnDelete.ForeColor = Color.White;
            btnDelete.Location = new Point(0, 0);
            btnDelete.Text = "&Delete";
            btnDelete.Click += new EventHandler(ButtonClickHandler);
            pnlLeft.Controls.Add(btnDelete);

            btnFlip = new Button();
            btnFlip.Size = btnSize;
            btnFlip.Font = DEFAULT_FONT_9;
            btnFlip.ForeColor = Color.White;
            btnFlip.FlatStyle = FlatStyle.Flat;
            btnFlip.Location = new Point(btnSize.Width, 0);
            btnFlip.Text = "&Flip";
            btnFlip.Click += new EventHandler(ButtonClickHandler);
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
            
            // holds the Image
            Panel pnlPBox = new Panel();
            pnlPBox.MinimumSize = new Size(widthPreview-2*padding, 
                pnlLeft.Size.Height);
            pnlPBox.BorderStyle = BorderStyle.FixedSingle;
            pnlPBox.Location = new Point(2*btnWidth+2*padding,
                padding);
            pnlPBox.AutoSize = false;
            frmPreview.Controls.Add(pnlPBox);

            pbPreview = new PictureBox();
            pbPreview.SizeMode = PictureBoxSizeMode.AutoSize;
            SetPreviewImage(image);
            pnlPBox.Controls.Add(pbPreview);

            frmPreview.ResumeLayout();

            frmPreview.Show();
        }

        private NameValueCollection GetServerProps()
        {
            string configFile = Path.Combine(System.Environment.GetFolderPath
                (Environment.SpecialFolder.Personal), ".scanLoader");
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
                     ** configuration file has your password, at least it's in a
                     ** hidden folder in YOUR $HOME.
                     ****************************************************
                    */
                XmlDocument config = new XmlDocument();
                config.LoadXml(File.ReadAllText(configFile));
                XmlNodeList elements = config.GetElementsByTagName("entry");
                foreach (XmlNode element in elements)
                {
                    properties.Add(element.Attributes["key"].Value, element.InnerText);
                }
            }
            return properties;
        }
		
	private void frmMain_Resize(object sender, EventArgs e)
	{
		if (FormWindowState.Minimized == this.WindowState) {
			notifyIcon.Visible = true;
			notifyIcon.ShowBalloonTip (500);
			this.Hide ();
		} else if (FormWindowState.Normal == this.WindowState) {
			notifyIcon.Visible = false;
		}
	}
	private System.Windows.Forms.NotifyIcon notifyIcon;
		
        //Non-widgets
        private int countdown;
        private bool flipped, cancelPending = false;
        private string scanDir;
        private Image nextImage;
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
        private Button btnStart, btnFlip, btnDelete, btnManual;
        private PictureBox pbPreview;
        private ListView lvManifest;
        private Panel pnlDetection, pnlTransmission;
        private Label lblDetection, lblTransmission;
        private Label lblDetectionTimeLeft, lblTransmissionTimeLeft;
        private ProgressBar pbrDetection, pbrTransmission;
        private Form frmPreview;
        
        //Dimensions
        private int btnHeight = 30, btnWidth = 120 , padding = 5 ;
        private int widthMain = 740;
        private int heightMain = 30+30+30;
        private int heightFrameBar = 30;
        private int widthPreview = 900;
        private int heightPreview = 580;
        private readonly string ZERO_ZERO_ZERO = "00:00:00";
        private readonly Font DEFAULT_FONT_9 = 
            new Font("Trebuchet MS", 9, FontStyle.Regular);
        private readonly Font DEFAULT_FONT_10 = 
            new Font("Trebuchet MS", 10, FontStyle.Regular);
        private readonly Font BIG_ORNG = 
            new Font("Trebuchet MS", 48, FontStyle.Regular);
    }
    
    enum Mode
    {
        Manual,
        Automatic
    }

}
