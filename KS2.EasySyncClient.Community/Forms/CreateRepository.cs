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
#if __MonoCS__
	using System.IO;
#else
using Alphaleonis.Win32.Filesystem;
#endif
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KS2.EasySyncClient
{
    public partial class CreateRepository : Form
    {
        private Int16 CurrentStep = 0;
        private UserControl CurrentUserControl;
        private List<Type> _Connectors;
        private List<RepositoryUI> _ListOfRepositoryUI;

        public String ResultSelectedType = null; //Step0

        public String ResultSiteURL = null; //Step1
        public String ResultLogin = null; //Step1
        public String ResultPassword = null; //Step1

        private List<string> TargetRepositories; //Step2
        public String ResultSiteName = null;    //Step2

        public String ResultLocalPath = null;   //Step3

        public CreateRepository(List<Type> AvailableConnectors, List<RepositoryUI> ListOfRepositoryUI)
        {
            InitializeComponent();
            this._Connectors = AvailableConnectors;
            this._ListOfRepositoryUI = ListOfRepositoryUI;

            if (this._Connectors.Count == 0)
            {
                Label lb = new Label();
                lb.Text = Program.RessourceManager.GetString("STEP0_ERROR1");
                lb.Size = new System.Drawing.Size(645,146);
                lb.TextAlign = ContentAlignment.MiddleCenter;
                panelMain.Controls.Clear();
                panelMain.Controls.Add(lb);
                bt_Back.Visible = false;
                bt_Next.Visible = false;
            }
            else if (this._Connectors.Count == 1)
            {
                ResultSelectedType = this._Connectors[0].ToString();
                LoadStep1();
            }
            else
            {
                LoadStep0();
            }
        }

        private void LoadStep0()
        {
            CurrentStep = 0;

            panelMain.Controls.Clear();
            CurrentUserControl = new CreateRepository0();
            CreateRepository0 RealCurrentUserControl = (CreateRepository0)CurrentUserControl;
            panelMain.Controls.Add(CurrentUserControl);

            foreach (Type T in this._Connectors)
            {
                RealCurrentUserControl.comboBox1.Items.Add(T.ToString());
            }

            if (ResultSelectedType == null) RealCurrentUserControl.comboBox1.SelectedIndex = 0;
            else RealCurrentUserControl.comboBox1.SelectedIndex = RealCurrentUserControl.comboBox1.Items.IndexOf(ResultSelectedType);

            bt_Back.Visible = false;
            bt_Next.Focus();
        }

        private bool ValidateStep0()
        {
            CreateRepository0 RealCurrentUserControl = (CreateRepository0)CurrentUserControl;
            ResultSelectedType = RealCurrentUserControl.comboBox1.Items[RealCurrentUserControl.comboBox1.SelectedIndex].ToString();
            return true;
        }

        private void LoadStep1()
        {
            CurrentStep = 1;

            panelMain.Controls.Clear();
            CurrentUserControl = new CreateRepository1();
            CreateRepository1 RealCurrentUserControl = (CreateRepository1)CurrentUserControl;
            panelMain.Controls.Add(CurrentUserControl);

            bt_Back.Visible = true;
            bt_Next.Enabled = true;
            bt_Back.Enabled = true;

            if (ResultSiteURL != null)
            {
                RealCurrentUserControl.txt_AlfrescoURL.Text = ResultSiteURL;
                RealCurrentUserControl.pnl_ServerURL.Visible = true;
            }
            if (ResultLogin != null) RealCurrentUserControl.txt_Login.Text = ResultLogin;
            if (ResultPassword != null) RealCurrentUserControl.txt_Password.Text = ResultPassword;

            bt_Next.Focus();
        }

        private bool ValidateStep1()
        {
            CreateRepository1 RealCurrentUserControl = (CreateRepository1)CurrentUserControl;

            if (String.IsNullOrEmpty(RealCurrentUserControl.txt_Login.Text))
            {
                MessageBox.Show(Program.RessourceManager.GetString("STEP1_ERROR2"));
                return false;
            }

            if (String.IsNullOrEmpty(RealCurrentUserControl.txt_Password.Text))
            {
                MessageBox.Show(Program.RessourceManager.GetString("STEP1_ERROR3"));
                return false;
            }

            ResultLogin = RealCurrentUserControl.txt_Login.Text;
            ResultPassword = RealCurrentUserControl.txt_Password.Text;

            if (String.IsNullOrEmpty(RealCurrentUserControl.txt_AlfrescoURL.Text))
            {
                lbl_Connecting.Visible = false;
                RealCurrentUserControl.pnl_ServerURL.Visible = true;
                return false;
            }

            ResultSiteURL = RealCurrentUserControl.txt_AlfrescoURL.Text;

            //Init connector
            Type t = _Connectors.FirstOrDefault(x => x.FullName.Equals(ResultSelectedType));
            if (t == null)
            {
                MessageBox.Show(Program.RessourceManager.GetString("STEP1_ERROR4"));
                return false;
            }

            TargetRepositories = new List<string>();

            lbl_Connecting.Visible = true;
            bt_Next.Enabled = false;
            bt_Back.Enabled = false;
            this.Refresh();

            bool Result = GetRepositorySites(TargetRepositories, t, RealCurrentUserControl.txt_AlfrescoURL.Text, RealCurrentUserControl.txt_Login.Text, RealCurrentUserControl.txt_Password.Text);

            /*
            // Create function delegate (it can be any delegate)
            var FunFunc = new Func<List<string>, Type, String, String, String, bool>(GetRepositorySites);
            // Start executing function on thread pool with parameters
            IAsyncResult FunFuncResult = FunFunc.BeginInvoke(TargetRepositories, t, ((CreateRepository1)CurrentUserControl).txt_AlfrescoURL.Text, ((CreateRepository1)CurrentUserControl).txt_Login.Text, ((CreateRepository1)CurrentUserControl).txt_Password.Text, null, null);

            // Wait for asynchronous call completion and get result
            bool Result = FunFunc.EndInvoke(FunFuncResult);
            */
            lbl_Connecting.Visible = false;
            RealCurrentUserControl.pnl_ServerURL.Visible = true;

            if (!Result)
            {
                MessageBox.Show(Program.RessourceManager.GetString("STEP1_ERROR5"));
                bt_Next.Enabled = true;
                bt_Back.Enabled = true;
                return false;
            }
            

            return true;
        }

        private void LoadStep2()
        {
            CurrentStep = 2;

            panelMain.Controls.Clear();
            CurrentUserControl = new CreateRepository2();
            CreateRepository2 RealCurrentUserControl = (CreateRepository2)CurrentUserControl;
            panelMain.Controls.Add(CurrentUserControl);

            foreach (String Repo in TargetRepositories)
            {
                RealCurrentUserControl.comboBox1.Items.Add(Repo);
            }

            bt_Next.Enabled = true;
            bt_Back.Enabled = true;
            lbl_Connecting.Visible = false;

            if (ResultSiteName == null)
            {
                if (RealCurrentUserControl.comboBox1.Items.Count > 0)
                {
                    RealCurrentUserControl.comboBox1.SelectedIndex = 0;
                }
                else
                {
                    bt_Next.Enabled = false;
                }
            }
            else
            {
                RealCurrentUserControl.comboBox1.SelectedIndex = RealCurrentUserControl.comboBox1.Items.IndexOf(ResultSiteName);
            }

            bt_Next.Text = Program.RessourceManager.GetString("STEP0_NEXT");
            bt_Next.Focus();
        }

        private bool ValidateStep2()
        {
            CreateRepository2 RealCurrentUserControl = (CreateRepository2)CurrentUserControl;
            ResultSiteName = RealCurrentUserControl.comboBox1.Items[RealCurrentUserControl.comboBox1.SelectedIndex].ToString();

            if (_ListOfRepositoryUI != null && _ListOfRepositoryUI.Exists(x => x.SiteName.Equals(ResultSiteName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(Program.RessourceManager.GetString("STEP2_ERROR1"));
                return false;
            }

            return true;
        }

        private void LoadStep3()
        {
            CurrentStep = 3;

            panelMain.Controls.Clear();
            CurrentUserControl = new CreateRepository3();
            CreateRepository3 RealCurrentUserControl = (CreateRepository3)CurrentUserControl;
            panelMain.Controls.Add(CurrentUserControl);

            if (ResultLocalPath != null) RealCurrentUserControl.textBox1.Text = ResultLocalPath;

            bt_Next.Text = Program.RessourceManager.GetString("STEP0_FINISH");
            bt_Next.Focus();
        }

        private bool ValidateStep3()
        {
            CreateRepository3 RealCurrentUserControl = (CreateRepository3)CurrentUserControl;

            if (String.IsNullOrEmpty(((CreateRepository3)CurrentUserControl).textBox1.Text))
            {
                MessageBox.Show(Program.RessourceManager.GetString("STEP3_ERROR1"));
                return false;
            }

            ResultLocalPath = ((CreateRepository3)CurrentUserControl).textBox1.Text;

            //Check if the selected folder is not a subdirectory of an existing replication
            if (_ListOfRepositoryUI != null && (_ListOfRepositoryUI.Exists(x => ResultLocalPath.StartsWith(x.LocalPath + Path.DirectorySeparatorChar))) || _ListOfRepositoryUI.Exists(x => ResultLocalPath.Equals(x.LocalPath, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(Program.RessourceManager.GetString("STEP3_ERROR2"));
                return false;
            }

            DirectoryInfo DI = new DirectoryInfo(ResultLocalPath);
            if (DI.GetDirectories().Count() > 0 || DI.GetFiles().Count() > 0)
            {
                if (MessageBox.Show(Program.RessourceManager.GetString("STEP3_ERROR3"), "", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.No)
                {
                    return false;
                }
            }

            return true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (CurrentStep == 0)
            {
                #region Choix du connecteur -> Step0 to Step1

                if (!ValidateStep0()) return;
                LoadStep1();

                #endregion
            }
            else if (CurrentStep == 1)
            {
                #region Paramétres de connexion à Alfresco -> Step1 to Step2

                if (!ValidateStep1()) return;
                LoadStep2();

                #endregion
            }
            else if (CurrentStep == 2)
            {
                #region Choix du site -> Step2 to Step3

                if (!ValidateStep2()) return;
                LoadStep3();

                #endregion
            }
            else if (CurrentStep == 3)
            {
                #region Choix du répertoire -> Step3 to StepFinal

                if (!ValidateStep3()) return;
                
                CurrentStep = 4;
                DialogResult = System.Windows.Forms.DialogResult.OK;
                this.Close();

                #endregion
            }
        }

        private void bt_Back_Click(object sender, EventArgs e)
        {
            if (CurrentStep == 3)
            {
                LoadStep2();
            }
            else if (CurrentStep == 2)
            {
                LoadStep1();
            }
            else if (CurrentStep == 1)
            {
                LoadStep0();
            }
        }

        private bool GetRepositorySites(List<String> TargetRepositories, Type t, String URL, String Login, String Password)
		{
			var connector = (IKaliSyncPlugin)Activator.CreateInstance (t);
			try {
                connector.SetProxyParameter(Globals.GlbProxyMode, Globals.GlbProxyURL, Globals.GlbProxyAuthentication, Globals.GlbProxyLogin, Globals.GlbProxyPassword);
                if (connector.Setup(URL, Login, Password, TargetRepositories) != ActionResult.Success)
                {
					return false;
				}

			} catch (Exception ex) {
				return false;
			}

			return true;
		}

        private string GetKS2DocsURLFromMonCompte(string UserLogin)
        {
            String KS2DocsSyncURL;
            WebClient wc = new WebClient();

            #region Proxy settings

            ServicePointManager.DefaultConnectionLimit = 1000;
            if (Globals.GlbProxyMode == 0) //No proxy
            {
                wc.Proxy = null;
            }
            else if (Globals.GlbProxyMode == 1) //Autoproxy (IE)
            {
                IWebProxy webProxy = WebRequest.DefaultWebProxy;
                webProxy.Credentials = CredentialCache.DefaultNetworkCredentials;
                wc.Proxy = webProxy;
            }
            else if (Globals.GlbProxyMode == 2) //Custom proxy
            {
                WebProxy webProxy2 = new WebProxy(Globals.GlbProxyURL);
                webProxy2.BypassProxyOnLocal = false;

                if (Globals.GlbProxyAuthentication)
                {
                    webProxy2.Credentials = new NetworkCredential(Globals.GlbProxyLogin, Globals.GlbProxyPassword);
                }

                wc.Proxy = webProxy2;
            }

            #endregion

            try
            {
#if DEBUG
                KS2DocsSyncURL = wc.DownloadString("https://localhost:44303/External/GetSyncURL/05F15DEE-998F-4624-BF44-22C56D3073D0/" + UserLogin);
#else
                KS2DocsSyncURL = wc.DownloadString("https://moncompte.ks2.fr/External/GetSyncURL/05F15DEE-998F-4624-BF44-22C56D3073D0/" + UserLogin);
#endif
            }
            catch { return ""; }

            return KS2DocsSyncURL;
        }
    }
}