/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using KS2.EasySync.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace KS2.EasySyncClient
{
    public partial class Options : Form
    {
        private string _ProxyPassword;

        public Options()
        {
            InitializeComponent();

            //Reload Data from database
            if (Globals.GlbProxyMode == 0)
                rb_ModeNone.Checked = true;
            else if (Globals.GlbProxyMode == 1)
                rb_ModeAuto.Checked = true;
            else
            {
                rb_ModeManual.Checked = true;
                txt_ProxyURL.Text = Globals.GlbProxyURL;

                if (Globals.GlbProxyAuthentication)
                {
                    chk_UseAuth.Checked = true;
                    txt_ProxyLogin.Text = Globals.GlbProxyLogin;

                    _ProxyPassword = Globals.GlbProxyPassword;
                    txt_ProxyPass.Text = new String('x', _ProxyPassword.Length); //Prevent control sniffing - Should be enforced by using SecureStrings
                }
            }
        }

        private void bt_Save_Click(object sender, EventArgs e)
        {
            int iErrorCount = 0;

            errorProvider1.Clear();

            if (rb_ModeManual.Checked)
            {
                if (String.IsNullOrEmpty(txt_ProxyURL.Text))
                {
                    errorProvider1.SetError(txt_ProxyURL, "Error");
                    iErrorCount++;
                }

                if (!Uri.IsWellFormedUriString(txt_ProxyURL.Text, UriKind.Absolute))
                {
                    errorProvider1.SetError(txt_ProxyURL, "Error");
                    iErrorCount++;
                }

                if (chk_UseAuth.Checked)
                {
                    if (String.IsNullOrEmpty(txt_ProxyLogin.Text))
                    {
                        errorProvider1.SetError(txt_ProxyLogin, "Error");
                        iErrorCount++;
                    }
                    if (String.IsNullOrEmpty(txt_ProxyPass.Text))
                    {
                        errorProvider1.SetError(txt_ProxyPass, "Error");
                        iErrorCount++;
                    }
                }
            }

            if (iErrorCount > 0) return;

            //Refresh global values with datas
            //Save data to database
            SQLiteHelper oSQLHelper = new SQLiteHelper(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, null);
            if (!oSQLHelper.InitConnection()) return;

            oSQLHelper.SetCommandText("DELETE FROM Param WHERE ParamName IN ('PROXY_MODE','PROXY_URL','PROXY_AUTH','PROXY_LOGIN','PROXY_PASSWORD')");
            oSQLHelper.ExecuteNonQuery();

            if (rb_ModeNone.Checked)
            {
                oSQLHelper.SetCommandText("INSERT INTO Param(ParamName,ParamValue) VALUES('PROXY_MODE','0')");
                oSQLHelper.ExecuteNonQuery();
                Globals.GlbProxyMode = 0;
            }
            else if (rb_ModeAuto.Checked)
            {
                oSQLHelper.SetCommandText("INSERT INTO Param(ParamName,ParamValue) VALUES('PROXY_MODE','1')");
                oSQLHelper.ExecuteNonQuery();
                Globals.GlbProxyMode = 1;
            }
            else
            {
                oSQLHelper.SetCommandText("INSERT INTO Param(ParamName,ParamValue) VALUES('PROXY_MODE','2')");
                oSQLHelper.ExecuteNonQuery();

                oSQLHelper.SetCommandText("INSERT INTO Param(ParamName,ParamValue) VALUES('PROXY_URL',@ProxyURL)");
                oSQLHelper.SetCommandParameter("@ProxyURL", DbType.String, 500, txt_ProxyURL.Text);
                oSQLHelper.ExecuteNonQuery();

                Globals.GlbProxyMode = 2;
                Globals.GlbProxyURL = txt_ProxyURL.Text;

                if (chk_UseAuth.Checked)
                {
                    Globals.GlbProxyAuthentication = true;
                    Globals.GlbProxyLogin = txt_ProxyLogin.Text;
                    Globals.GlbProxyPassword = _ProxyPassword;

                    oSQLHelper.SetCommandText("INSERT INTO Param(ParamName,ParamValue) VALUES('PROXY_AUTH','1')");
                    oSQLHelper.ExecuteNonQuery();

                    oSQLHelper.SetCommandText("INSERT INTO Param(ParamName,ParamValue) VALUES('PROXY_LOGIN',@Login)");
                    oSQLHelper.SetCommandParameter("@Login", DbType.String, 500, txt_ProxyLogin.Text);
                    oSQLHelper.ExecuteNonQuery();

                    oSQLHelper.SetCommandText("INSERT INTO Param(ParamName,ParamValue) VALUES('PROXY_PASSWORD',@Password)");
                    oSQLHelper.SetCommandParameter("@Password", DbType.String, 500, Tools.Encrypt(_ProxyPassword));
                    oSQLHelper.ExecuteNonQuery();
                }
                else
                {
                    Globals.GlbProxyAuthentication = false;
                    oSQLHelper.SetCommandText("INSERT INTO Param(ParamName,ParamValue) VALUES('PROXY_AUTH','0')");
                    oSQLHelper.ExecuteNonQuery();
                }
            }

            oSQLHelper.Dispose();

            DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Close();
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_ModeNone.Checked)
                ProxyManualPanel.Enabled = false;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_ModeAuto.Checked)
                ProxyManualPanel.Enabled = false;
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_ModeManual.Checked)
                ProxyManualPanel.Enabled = true;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (chk_UseAuth.Checked)
                ProxyAuthPanel.Enabled = true;
            else
                ProxyAuthPanel.Enabled = false;
        }

        private void txt_ProxyPass_Leave(object sender, EventArgs e)
        {
            if (!txt_ProxyPass.Text.Equals(String.Empty))
            {
                _ProxyPassword = txt_ProxyPass.Text;
            }
        }
    }
}
