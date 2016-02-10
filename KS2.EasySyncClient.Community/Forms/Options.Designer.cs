namespace KS2.EasySyncClient
{
    partial class Options
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Options));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.ProxyManualPanel = new System.Windows.Forms.Panel();
            this.label5 = new System.Windows.Forms.Label();
            this.ProxyAuthPanel = new System.Windows.Forms.Panel();
            this.label6 = new System.Windows.Forms.Label();
            this.txt_ProxyPass = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.txt_ProxyLogin = new System.Windows.Forms.TextBox();
            this.chk_UseAuth = new System.Windows.Forms.CheckBox();
            this.txt_ProxyURL = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.rb_ModeNone = new System.Windows.Forms.RadioButton();
            this.rb_ModeManual = new System.Windows.Forms.RadioButton();
            this.rb_ModeAuto = new System.Windows.Forms.RadioButton();
            this.bt_Save = new System.Windows.Forms.Button();
            this.errorProvider1 = new System.Windows.Forms.ErrorProvider(this.components);
            this.panel0 = new System.Windows.Forms.Panel();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.groupBox1.SuspendLayout();
            this.ProxyManualPanel.SuspendLayout();
            this.ProxyAuthPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.errorProvider1)).BeginInit();
            this.panel0.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Controls.Add(this.ProxyManualPanel);
            this.groupBox1.Controls.Add(this.rb_ModeNone);
            this.groupBox1.Controls.Add(this.rb_ModeManual);
            this.groupBox1.Controls.Add(this.rb_ModeAuto);
            this.errorProvider1.SetError(this.groupBox1, resources.GetString("groupBox1.Error"));
            this.errorProvider1.SetIconAlignment(this.groupBox1, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("groupBox1.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.groupBox1, ((int)(resources.GetObject("groupBox1.IconPadding"))));
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // ProxyManualPanel
            // 
            resources.ApplyResources(this.ProxyManualPanel, "ProxyManualPanel");
            this.ProxyManualPanel.Controls.Add(this.label5);
            this.ProxyManualPanel.Controls.Add(this.ProxyAuthPanel);
            this.ProxyManualPanel.Controls.Add(this.chk_UseAuth);
            this.ProxyManualPanel.Controls.Add(this.txt_ProxyURL);
            this.ProxyManualPanel.Controls.Add(this.label4);
            this.errorProvider1.SetError(this.ProxyManualPanel, resources.GetString("ProxyManualPanel.Error"));
            this.errorProvider1.SetIconAlignment(this.ProxyManualPanel, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("ProxyManualPanel.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.ProxyManualPanel, ((int)(resources.GetObject("ProxyManualPanel.IconPadding"))));
            this.ProxyManualPanel.Name = "ProxyManualPanel";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.errorProvider1.SetError(this.label5, resources.GetString("label5.Error"));
            this.errorProvider1.SetIconAlignment(this.label5, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("label5.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.label5, ((int)(resources.GetObject("label5.IconPadding"))));
            this.label5.Name = "label5";
            // 
            // ProxyAuthPanel
            // 
            resources.ApplyResources(this.ProxyAuthPanel, "ProxyAuthPanel");
            this.ProxyAuthPanel.Controls.Add(this.label6);
            this.ProxyAuthPanel.Controls.Add(this.txt_ProxyPass);
            this.ProxyAuthPanel.Controls.Add(this.label7);
            this.ProxyAuthPanel.Controls.Add(this.txt_ProxyLogin);
            this.errorProvider1.SetError(this.ProxyAuthPanel, resources.GetString("ProxyAuthPanel.Error"));
            this.errorProvider1.SetIconAlignment(this.ProxyAuthPanel, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("ProxyAuthPanel.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.ProxyAuthPanel, ((int)(resources.GetObject("ProxyAuthPanel.IconPadding"))));
            this.ProxyAuthPanel.Name = "ProxyAuthPanel";
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.errorProvider1.SetError(this.label6, resources.GetString("label6.Error"));
            this.errorProvider1.SetIconAlignment(this.label6, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("label6.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.label6, ((int)(resources.GetObject("label6.IconPadding"))));
            this.label6.Name = "label6";
            // 
            // txt_ProxyPass
            // 
            resources.ApplyResources(this.txt_ProxyPass, "txt_ProxyPass");
            this.errorProvider1.SetError(this.txt_ProxyPass, resources.GetString("txt_ProxyPass.Error"));
            this.errorProvider1.SetIconAlignment(this.txt_ProxyPass, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("txt_ProxyPass.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.txt_ProxyPass, ((int)(resources.GetObject("txt_ProxyPass.IconPadding"))));
            this.txt_ProxyPass.Name = "txt_ProxyPass";
            this.txt_ProxyPass.UseSystemPasswordChar = true;
            this.txt_ProxyPass.Leave += new System.EventHandler(this.txt_ProxyPass_Leave);
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.errorProvider1.SetError(this.label7, resources.GetString("label7.Error"));
            this.errorProvider1.SetIconAlignment(this.label7, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("label7.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.label7, ((int)(resources.GetObject("label7.IconPadding"))));
            this.label7.Name = "label7";
            // 
            // txt_ProxyLogin
            // 
            resources.ApplyResources(this.txt_ProxyLogin, "txt_ProxyLogin");
            this.errorProvider1.SetError(this.txt_ProxyLogin, resources.GetString("txt_ProxyLogin.Error"));
            this.errorProvider1.SetIconAlignment(this.txt_ProxyLogin, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("txt_ProxyLogin.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.txt_ProxyLogin, ((int)(resources.GetObject("txt_ProxyLogin.IconPadding"))));
            this.txt_ProxyLogin.Name = "txt_ProxyLogin";
            // 
            // chk_UseAuth
            // 
            resources.ApplyResources(this.chk_UseAuth, "chk_UseAuth");
            this.errorProvider1.SetError(this.chk_UseAuth, resources.GetString("chk_UseAuth.Error"));
            this.errorProvider1.SetIconAlignment(this.chk_UseAuth, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("chk_UseAuth.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.chk_UseAuth, ((int)(resources.GetObject("chk_UseAuth.IconPadding"))));
            this.chk_UseAuth.Name = "chk_UseAuth";
            this.chk_UseAuth.UseVisualStyleBackColor = true;
            this.chk_UseAuth.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // txt_ProxyURL
            // 
            resources.ApplyResources(this.txt_ProxyURL, "txt_ProxyURL");
            this.errorProvider1.SetError(this.txt_ProxyURL, resources.GetString("txt_ProxyURL.Error"));
            this.errorProvider1.SetIconAlignment(this.txt_ProxyURL, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("txt_ProxyURL.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.txt_ProxyURL, ((int)(resources.GetObject("txt_ProxyURL.IconPadding"))));
            this.txt_ProxyURL.Name = "txt_ProxyURL";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.errorProvider1.SetError(this.label4, resources.GetString("label4.Error"));
            this.errorProvider1.SetIconAlignment(this.label4, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("label4.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.label4, ((int)(resources.GetObject("label4.IconPadding"))));
            this.label4.Name = "label4";
            // 
            // rb_ModeNone
            // 
            resources.ApplyResources(this.rb_ModeNone, "rb_ModeNone");
            this.rb_ModeNone.Checked = true;
            this.errorProvider1.SetError(this.rb_ModeNone, resources.GetString("rb_ModeNone.Error"));
            this.errorProvider1.SetIconAlignment(this.rb_ModeNone, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("rb_ModeNone.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.rb_ModeNone, ((int)(resources.GetObject("rb_ModeNone.IconPadding"))));
            this.rb_ModeNone.Name = "rb_ModeNone";
            this.rb_ModeNone.TabStop = true;
            this.rb_ModeNone.UseVisualStyleBackColor = true;
            this.rb_ModeNone.CheckedChanged += new System.EventHandler(this.radioButton1_CheckedChanged);
            // 
            // rb_ModeManual
            // 
            resources.ApplyResources(this.rb_ModeManual, "rb_ModeManual");
            this.errorProvider1.SetError(this.rb_ModeManual, resources.GetString("rb_ModeManual.Error"));
            this.errorProvider1.SetIconAlignment(this.rb_ModeManual, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("rb_ModeManual.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.rb_ModeManual, ((int)(resources.GetObject("rb_ModeManual.IconPadding"))));
            this.rb_ModeManual.Name = "rb_ModeManual";
            this.rb_ModeManual.UseVisualStyleBackColor = true;
            this.rb_ModeManual.CheckedChanged += new System.EventHandler(this.radioButton3_CheckedChanged);
            // 
            // rb_ModeAuto
            // 
            resources.ApplyResources(this.rb_ModeAuto, "rb_ModeAuto");
            this.errorProvider1.SetError(this.rb_ModeAuto, resources.GetString("rb_ModeAuto.Error"));
            this.errorProvider1.SetIconAlignment(this.rb_ModeAuto, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("rb_ModeAuto.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.rb_ModeAuto, ((int)(resources.GetObject("rb_ModeAuto.IconPadding"))));
            this.rb_ModeAuto.Name = "rb_ModeAuto";
            this.rb_ModeAuto.UseVisualStyleBackColor = true;
            this.rb_ModeAuto.CheckedChanged += new System.EventHandler(this.radioButton2_CheckedChanged);
            // 
            // bt_Save
            // 
            resources.ApplyResources(this.bt_Save, "bt_Save");
            this.errorProvider1.SetError(this.bt_Save, resources.GetString("bt_Save.Error"));
            this.errorProvider1.SetIconAlignment(this.bt_Save, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("bt_Save.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.bt_Save, ((int)(resources.GetObject("bt_Save.IconPadding"))));
            this.bt_Save.Name = "bt_Save";
            this.bt_Save.UseVisualStyleBackColor = true;
            this.bt_Save.Click += new System.EventHandler(this.bt_Save_Click);
            // 
            // errorProvider1
            // 
            this.errorProvider1.ContainerControl = this;
            resources.ApplyResources(this.errorProvider1, "errorProvider1");
            // 
            // panel0
            // 
            resources.ApplyResources(this.panel0, "panel0");
            this.panel0.BackColor = System.Drawing.Color.White;
            this.panel0.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel0.Controls.Add(this.pictureBox1);
            this.errorProvider1.SetError(this.panel0, resources.GetString("panel0.Error"));
            this.errorProvider1.SetIconAlignment(this.panel0, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("panel0.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.panel0, ((int)(resources.GetObject("panel0.IconPadding"))));
            this.panel0.Name = "panel0";
            // 
            // pictureBox1
            // 
            resources.ApplyResources(this.pictureBox1, "pictureBox1");
            this.pictureBox1.BackColor = System.Drawing.Color.White;
            this.errorProvider1.SetError(this.pictureBox1, resources.GetString("pictureBox1.Error"));
            this.errorProvider1.SetIconAlignment(this.pictureBox1, ((System.Windows.Forms.ErrorIconAlignment)(resources.GetObject("pictureBox1.IconAlignment"))));
            this.errorProvider1.SetIconPadding(this.pictureBox1, ((int)(resources.GetObject("pictureBox1.IconPadding"))));
            this.pictureBox1.Image = global::KS2.EasySyncClient.Properties.Resources.logo_small;
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.TabStop = false;
            // 
            // Options
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel0);
            this.Controls.Add(this.bt_Save);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Options";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ProxyManualPanel.ResumeLayout(false);
            this.ProxyManualPanel.PerformLayout();
            this.ProxyAuthPanel.ResumeLayout(false);
            this.ProxyAuthPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.errorProvider1)).EndInit();
            this.panel0.ResumeLayout(false);
            this.panel0.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Panel ProxyManualPanel;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Panel ProxyAuthPanel;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox txt_ProxyPass;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox txt_ProxyLogin;
        private System.Windows.Forms.CheckBox chk_UseAuth;
        private System.Windows.Forms.TextBox txt_ProxyURL;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.RadioButton rb_ModeNone;
        private System.Windows.Forms.RadioButton rb_ModeManual;
        private System.Windows.Forms.RadioButton rb_ModeAuto;
        private System.Windows.Forms.Button bt_Save;
        private System.Windows.Forms.ErrorProvider errorProvider1;
        private System.Windows.Forms.Panel panel0;
        private System.Windows.Forms.PictureBox pictureBox1;
    }
}