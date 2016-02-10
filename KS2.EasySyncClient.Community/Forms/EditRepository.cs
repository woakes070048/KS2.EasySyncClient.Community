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
    public partial class EditRepository : Form
    {
        private bool _PasswordChanged = false;
        private string _OriginalPassword;
        private Int32 _RepositoryId;
        public String NewLogin;
        public String NewPassword;
        public bool EnableFullScan = false;

        public EditRepository(Int32 RepositoryId, SyncEngine SE , String Site, String LocalPath)
        {
            InitializeComponent();

            this._RepositoryId = RepositoryId;

            txt_Site.Text = Site;
            txt_LocalPath.Text = LocalPath;

            if (SE != null)
            {
                txt_Server.Text = SE.RemoteConnector.GetEndPoint();
                txt_Login.Text = SE.RemoteConnector.GetLogin();

                if (SE.EnableFullScan) chk_FullScan.Checked = true;

                this._OriginalPassword = SE.RemoteConnector.GetPassword();
                txt_Password.Text = "Really !";
                txt_Password.TextChanged += txt_Password_TextChanged;
            }
            else
            {
                chk_FullScan.Enabled = false;
                txt_Login.Enabled = false;
                txt_Password.Enabled = false;
                bt_Save.Enabled = false;
            }
        }

        private void txt_Password_TextChanged(object sender, EventArgs e)
        {
            _PasswordChanged = true;
        }

        private void bt_Save_Click(object sender, EventArgs e)
        {
            SQLiteHelper oSQLHelper = new SQLiteHelper(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, null);
            if (!oSQLHelper.InitConnection()) return;

            Int16 FullScanStatus = 0;
            if (chk_FullScan.Checked)
            {
                FullScanStatus = 1;
                this.EnableFullScan = true;
            }

            String ConnectorParameters;
            ConnectorParameter CP;

            if (_PasswordChanged)
            {
                CP = new ConnectorParameter() { Server = txt_Server.Text, Login = txt_Login.Text, Password = txt_Password.Text, SitePath = String.Format("/Sites/{0}/documentLibrary", txt_Site.Text), EnableFullScan = FullScanStatus };
                ConnectorParameters = ConnectorParameter.Serialize(CP);
                this.NewLogin = txt_Login.Text;
                this.NewPassword = txt_Password.Text;
            }
            else
            {
                CP = new ConnectorParameter() { Server = txt_Server.Text, Login = txt_Login.Text, Password = _OriginalPassword, SitePath = String.Format("/Sites/{0}/documentLibrary", txt_Site.Text), EnableFullScan = FullScanStatus };
                ConnectorParameters = ConnectorParameter.Serialize(CP);
                this.NewLogin = txt_Login.Text;
                this.NewPassword = _OriginalPassword;
            }

            oSQLHelper.SetCommandText("UPDATE Repository SET RemoteRepositoryParameters = @ConnectionString WHERE Id_Repository = " + this._RepositoryId);
            oSQLHelper.SetCommandParameter("@ConnectionString", DbType.String, 500, ConnectorParameters);
            oSQLHelper.ExecuteNonQuery();
            
            oSQLHelper.Dispose();
            DialogResult = System.Windows.Forms.DialogResult.OK;
        }
    }
}