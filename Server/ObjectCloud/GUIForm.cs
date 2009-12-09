using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud
{
	public class GUIForm : Form
	{
		public GUIForm()
		{
			InitializeComponent();
		}

        private LinkLabel LinkLabel;
		
		public GUIForm(FileHandlerFactoryLocator fileHandlerFactoryLocator)
			: this()
		{
			_FileHandlerFactoryLocator = fileHandlerFactoryLocator;

			this.Text = LinkLabel.Text = "http://" + FileHandlerFactoryLocator.HostnameAndPort;
            this.Shown += HandleShown;
		}
		
		public FileHandlerFactoryLocator FileHandlerFactoryLocator 
		{
			get { return _FileHandlerFactoryLocator; }
			set { _FileHandlerFactoryLocator = value; }
		}		
		private FileHandlerFactoryLocator _FileHandlerFactoryLocator;
		
		private void InitializeComponent()
		{
            this.LinkLabel = new System.Windows.Forms.LinkLabel();
            this.SuspendLayout();
            // 
            // LinkLabel
            // 
            this.LinkLabel.AutoSize = true;
            this.LinkLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LinkLabel.Enabled = false;
            this.LinkLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LinkLabel.LinkColor = System.Drawing.Color.Blue;
            this.LinkLabel.Location = new System.Drawing.Point(0, 0);
            this.LinkLabel.Name = "LinkLabel";
            this.LinkLabel.Size = new System.Drawing.Size(110, 25);
            this.LinkLabel.TabIndex = 0;
            this.LinkLabel.TabStop = true;
            this.LinkLabel.Text = "linkLabel1";
            this.LinkLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.LinkLabel.VisitedLinkColor = System.Drawing.Color.Blue;
            this.LinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel_LinkClicked);
            // 
            // GUIForm
            // 
            this.ClientSize = new System.Drawing.Size(292, 266);
            this.Controls.Add(this.LinkLabel);
            this.Name = "GUIForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.GUIForm_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

		}

        void HandleShown(object sender, EventArgs e)
        {
            Cursor cursor = this.Cursor;
            this.Cursor = Cursors.WaitCursor;

            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try
                {
                    FileHandlerFactoryLocator.WebServer.StartServer();

                    this.Invoke(new MethodInvoker(delegate()
                    {
                        LinkLabel.Enabled = true;
                    }));
                }
                finally
                {
                    this.Invoke(new MethodInvoker(delegate()
                    {
                        this.Cursor = cursor;
                    }));
                }
            });
        }

        private void LinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Cursor cursor = this.Cursor;
            this.Cursor = Cursors.WaitCursor;

            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try
                {
                    ProcessStartInfo processStartInfo = new ProcessStartInfo("http://" + FileHandlerFactoryLocator.HostnameAndPort);
                    Process.Start(processStartInfo);
                }
                finally
                {
                    this.Invoke(new MethodInvoker(delegate()
                    {
                        this.Cursor = cursor;
                    }));
                }
            });
        }

        private void GUIForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (null != FileHandlerFactoryLocator)
            {
                Cursor cursor = this.Cursor;
                this.Cursor = Cursors.WaitCursor;

                bool running = true;

                try
                {
                    ThreadPool.QueueUserWorkItem(delegate(object state)
                    {
                        FileHandlerFactoryLocator.WebServer.Dispose();
                        running = false;
                    });

                    while (running)
                        Application.DoEvents();
                }
                finally
                {
                    this.Cursor = cursor;
                }
            }
        }
	}
}
