namespace KS2.EasySyncClient
{
    partial class CreateRepository
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CreateRepository));
            this.panel0 = new System.Windows.Forms.Panel();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.panelMain = new System.Windows.Forms.Panel();
            this.bt_Next = new System.Windows.Forms.Button();
            this.lbl_Connecting = new System.Windows.Forms.Label();
            this.bt_Back = new System.Windows.Forms.Button();
            this.panel0.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel0
            // 
            resources.ApplyResources(this.panel0, "panel0");
            this.panel0.BackColor = System.Drawing.Color.White;
            this.panel0.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel0.Controls.Add(this.pictureBox1);
            this.panel0.Name = "panel0";
            // 
            // pictureBox1
            // 
            resources.ApplyResources(this.pictureBox1, "pictureBox1");
            this.pictureBox1.BackColor = System.Drawing.Color.White;
            this.pictureBox1.Image = global::KS2.EasySyncClient.Properties.Resources.logo_small;
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.TabStop = false;
            // 
            // groupBox1
            // 
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Controls.Add(this.panelMain);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // panelMain
            // 
            resources.ApplyResources(this.panelMain, "panelMain");
            this.panelMain.Name = "panelMain";
            // 
            // bt_Next
            // 
            resources.ApplyResources(this.bt_Next, "bt_Next");
            this.bt_Next.Name = "bt_Next";
            this.bt_Next.UseVisualStyleBackColor = true;
            this.bt_Next.Click += new System.EventHandler(this.button1_Click);
            // 
            // lbl_Connecting
            // 
            resources.ApplyResources(this.lbl_Connecting, "lbl_Connecting");
            this.lbl_Connecting.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(6)))), ((int)(((byte)(106)))), ((int)(((byte)(161)))));
            this.lbl_Connecting.Name = "lbl_Connecting";
            // 
            // bt_Back
            // 
            resources.ApplyResources(this.bt_Back, "bt_Back");
            this.bt_Back.Name = "bt_Back";
            this.bt_Back.UseVisualStyleBackColor = true;
            this.bt_Back.Click += new System.EventHandler(this.bt_Back_Click);
            // 
            // CreateRepository
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.lbl_Connecting);
            this.Controls.Add(this.bt_Back);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.bt_Next);
            this.Controls.Add(this.panel0);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CreateRepository";
            this.panel0.ResumeLayout(false);
            this.panel0.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel0;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button bt_Next;
        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.Label lbl_Connecting;
        private System.Windows.Forms.Button bt_Back;
        private System.Windows.Forms.PictureBox pictureBox1;
    }
}