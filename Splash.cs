using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Timers;

namespace gutenberg.collect
{
    public class Splash : Form
    {
        public Splash(string imageFile, int seconds)
        {
            this.TopMost = true;
            this.ControlBox = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            byte[] image = File.ReadAllBytes(imageFile);
            this.BackgroundImage = Image.FromStream(new MemoryStream(image));
            this.ClientSize = this.BackgroundImage.Size;

            countdown = seconds*4;
            timer = new System.Timers.Timer(INTERVAL);
            timer.AutoReset = true;
            timer.SynchronizingObject = this;
            timer.Elapsed += new ElapsedEventHandler(this.Tick);
        }

        public void StartSplash()
        {
            timer.Start();
            base.Show();
            base.Refresh();
        }

        private void Tick(Object o, ElapsedEventArgs args)
        {
            if (countdown == 0)
            {
                this.Close();
            }
            else
            {
                countdown--;
                this.Opacity = (double)countdown*1.00/COUNT_START;
            }
        }

        private System.Timers.Timer timer;
        private int countdown;
        private int COUNT_START = 16;
        private int INTERVAL = 250;//millis
    }
}

