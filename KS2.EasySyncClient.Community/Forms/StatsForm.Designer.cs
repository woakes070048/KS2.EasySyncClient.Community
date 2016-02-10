namespace KS2.EasySyncClient
{
    partial class StatsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(StatsForm));
            this.pnl_PlaceHolder = new System.Windows.Forms.Panel();
            this.SuspendLayout();
            // 
            // pnl_PlaceHolder
            // 
            this.pnl_PlaceHolder.Location = new System.Drawing.Point(5, 12);
            this.pnl_PlaceHolder.Name = "pnl_PlaceHolder";
            this.pnl_PlaceHolder.Size = new System.Drawing.Size(247, 83);
            this.pnl_PlaceHolder.TabIndex = 0;
            // 
            // StatsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(259, 104);
            this.ControlBox = false;
            this.Controls.Add(this.pnl_PlaceHolder);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(275, 120);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(275, 120);
            this.Name = "StatsForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.TopMost = true;
            this.Activated += new System.EventHandler(this.StatsForm_Activated);
            this.Deactivate += new System.EventHandler(this.StatsForm_Deactivate);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnl_PlaceHolder;


    }
}