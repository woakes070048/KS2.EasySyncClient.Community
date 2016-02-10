/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
#if __MonoCS__
	using System.IO;
#else
using Alphaleonis.Win32.Filesystem;
#endif
using System.Linq;
using System.Reflection;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NLog;
using System.Data;
using KS2.EasySync.Core;
using System.Data.SQLite;
using System.Net;
using System.Globalization;
using System.Text;

//TODO : Add repository folder to favorites : https://social.msdn.microsoft.com/Forums/vstudio/en-US/4d65a7e5-1c02-4c18-bbc0-8d49340ca5f7/how-can-i-add-folders-to-favorites-in-window-explrer-programmatically-c?forum=csharpgeneral

//Call Hierarchy
//Program.InitRepository
//new RepositoryUI
//    -> RepositoryUI.InitRepository
//        ->  new Repository
//    -> RepositoryUI.InitEngine
//        -> Repository.InitEngine
//            -> new SyncEngine
//    -> .StartEngine
//        -> Repository.StartEngine
//            -> SyncEngine.StartProcessing

namespace KS2.EasySyncClient
{
    static class Program
    {
        static Mutex mutex = new Mutex(false, "{D7F83B10-E914-4A24-BE34-B3457A94ECA9}"); //This is to identify the application in the system (Mutex is system-wide)

        private static System.Timers.Timer IconRefreshTimer;
        private static System.Timers.Timer ForceQuitTimer;

        /// <summary>
        /// SyncEngine generates a SyncEngineUploadDownloadCountChanged event
        /// Repository generates a RepositoryUploadDownloadCountChanged event
        /// Program generates a MainUploadDownloadCountChanged event
        /// This final event is listened by the stat from when visible
        /// </summary>
        public static event EventHandler MainUploadDownloadCountChanged;
        
        private static bool PromptNewSite = false;
        private static bool NewSiteActive = false;
        private static object NewSiteActiveLock = new object();
        
        private static Int32 IconPosition = 0;
        private static NotifyIcon icn;
        private static ContextMenuStrip CxtMenu;
        private static StoppingForm StoppingDialog;
        private static List<Type> _Connectors = new List<Type>();
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static System.Windows.Forms.ToolStripMenuItem Menu_Sites = new System.Windows.Forms.ToolStripMenuItem();
        private static List<RepositoryUI> ListOfRepositoryUI = new List<RepositoryUI>();
        private static Int32 NumberOfStartedEngines = 0;
        private static StatsForm Stats;
        private static Thread ProductUpdateThread;

        //usage : Program.RessourceManager.GetString("SERVICE_UNAVAILABLE")
        //to change culture : Thread.CurrentThread.CurrentUICulture = ci;
        public static System.Resources.ResourceManager RessourceManager = new System.Resources.ResourceManager("KS2.EasySyncClient.Resources.Translations", System.Reflection.Assembly.GetExecutingAssembly());

        [STAThread]
        static void Main()
        {
            logger.Trace("KS² EasySync " + Assembly.GetExecutingAssembly().GetName().Version.ToString());

            //Pour forcer la culture FR
            //Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("fr-FR");
            SQLiteHelper oSQLHelper = null;
            object QueryResult = null;
            System.Data.SQLite.SQLiteDataReader SqlDataReader;

            #region Application can only be launched once

            //To wait on a mutex means to wait until you can acquire it.
            //WaitOne on a Mutex will return true if the mutex could be acquired in the given time.
            //If it couldn't, the method will return false.
            //If the mutex was acquired, it's your responsibility to release the mutex when you're done with it.
            if (!mutex.WaitOne(TimeSpan.FromSeconds(2), false))
            {
                MessageBox.Show(Program.RessourceManager.GetString("APP_ALREADY_STARTED"), "", MessageBoxButtons.OK);
                return;
            }

            #endregion

            #region Loading connectors (classes implementing KaliSyncPlugin.IKaliSyncPlugin)

            logger.Trace("Looking for connectors ...");

            const string qualifiedInterfaceName = "KS2.EasySync.Core.IKaliSyncPlugin";
            var interfaceFilter = new TypeFilter(InterfaceFilter);
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var di = new DirectoryInfo(path);
            foreach (var file in di.GetFiles("*.dll"))
            {
                logger.Trace("Found DLL : " + file.FullName);
                try
                {
                    var nextAssembly = Assembly.LoadFrom(file.FullName);

                    foreach (var type in nextAssembly.GetTypes())
                    {
                        if (type.IsClass)
                        {
                            var myInterfaces = type.FindInterfaces(interfaceFilter, qualifiedInterfaceName);
                            if (myInterfaces.Length > 0)
                            {
                                logger.Trace(String.Format("This is an EasySync plugin : {0}", type.ToString()));
                                // This class implements the interface
                                _Connectors.Add(type);
                            }
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    // Not a .net assembly - ignore
                }
                catch (Exception)
                {
                    // Other load error - ignore
                }
            }

            #endregion

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += Application_ThreadException;

            //Registering Windows shutdown event
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySync")))
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySync"));
                }
                catch
                {
                    logger.Debug("In Main - Cannot create directory for storing database");
                    return;
                }
            }

            Globals.GlbAppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySync");

            //Stats Dialog
            Stats = new StatsForm(ListOfRepositoryUI);

            //Stop Dialog
            StoppingDialog = new StoppingForm();
            StoppingDialog.StartPosition = FormStartPosition.CenterScreen;

            #region Create SQLLite Database

            logger.Trace("Looking for local database ...");

            //Access the metadata database
            if (!File.Exists(Globals.GlbDBFilePath))
            {
                logger.Trace(String.Format("Not found. Creating one in : {0}", Globals.GlbDBFilePath));

                //Create database
                try
                {
                    SQLiteConnection.CreateFile(Globals.GlbDBFilePath);

                    oSQLHelper = new SQLiteHelper(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, null);

                    if (!oSQLHelper.InitConnection()) throw new Exception();
                    
                    //Recreate structure
                    oSQLHelper.SetCommandText("CREATE TABLE Repository (Id_Repository INTEGER PRIMARY KEY AUTOINCREMENT, Name nvarchar(500), LocalRepository nvarchar(255), RemoteRepositoryConnector nvarchar(100), RemoteRepositoryParameters nvarchar(500))");
                    if (!oSQLHelper.ExecuteNonQuery()) throw new Exception();

                    oSQLHelper.SetCommandText("CREATE TABLE VFolder (Id_Folder VARCHAR(36), Fk_Repository int, Name nvarchar(200), RelativePath nvarchar(500), RemoteID nvarchar(200), CustomInfo nvarchar(500))");
                    if (!oSQLHelper.ExecuteNonQuery()) throw new Exception();

                    oSQLHelper.SetCommandText("CREATE TABLE VFile (Id_File VARCHAR(36), Fk_Repository int, Fk_ParentFolder VARCHAR(36), Name nvarchar(200), RelativePath nvarchar(500), RemoteID nvarchar(200), Hash nvarchar(200), CustomInfo nvarchar(500))");
                    if (!oSQLHelper.ExecuteNonQuery()) throw new Exception();

                    oSQLHelper.SetCommandText("CREATE TABLE Param(ParamName nvarchar(500), ParamValue nvarchar(500))");
                    if (!oSQLHelper.ExecuteNonQuery()) throw new Exception();

                    oSQLHelper.SetCommandText("INSERT INTO Param(ParamName, ParamValue) VALUES('INSTANCE_ID','" + Guid.NewGuid().ToString() + "')");
                    if (!oSQLHelper.ExecuteNonQuery()) throw new Exception();

                    oSQLHelper.SetCommandText("INSERT INTO Param(ParamName, ParamValue) VALUES('DATABASE_VERSION','1c')");
                    if (!oSQLHelper.ExecuteNonQuery()) throw new Exception();

                    oSQLHelper.SetCommandText("INSERT INTO Param(ParamName, ParamValue) VALUES('REMOTE_SYNC_FREQUENCY','180')");
                    if (!oSQLHelper.ExecuteNonQuery()) throw new Exception();
                }
                catch (Exception ex)
                {
                    logger.Error("Exception occured while creating database", ex);
                    goto QuitApplication;
                }
                finally
                {
                    //if (con != null) con.Close();
                    oSQLHelper.Dispose();
                }

                //TODO-NTH : Password protect the database : http://technet.microsoft.com/fr-fr/library/ms171741.aspx
            }
            else
            {
                logger.Trace(String.Format("Found in : {0}", Globals.GlbDBFilePath));
            }

            #endregion

            #region Icon

            icn = new NotifyIcon();
            icn.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            icn.Icon = ((System.Drawing.Icon)(KS2.EasySyncClient.Properties.Resources.ResourceManager.GetObject("Ok")));
            icn.Text = "EasySync";
            icn.Visible = true;
            icn.MouseDoubleClick += icn_MouseDoubleClick;
            icn.MouseClick += icn_MouseClick;
            icn.ShowBalloonTip(2000, "EasySync", Program.RessourceManager.GetString("APP_STARTED"), ToolTipIcon.Info);

            IconRefreshTimer = new System.Timers.Timer(500);
            IconRefreshTimer.AutoReset = true;
            IconRefreshTimer.Elapsed += IconRefreshTimer_Elapsed;

            ForceQuitTimer = new System.Timers.Timer(15 * 1000); //15 seconds
            ForceQuitTimer.AutoReset = false;
            ForceQuitTimer.Elapsed += ForceQuitTimer_Elapsed;

            //PromptCreateFirstSiteTimer = new System.Timers.Timer(1 * 1000); //1 second
            //PromptCreateFirstSiteTimer.AutoReset = false;
            //PromptCreateFirstSiteTimer.Elapsed += PromptCreateFirstSiteTimer_Elapsed;

            #endregion

            logger.Trace("Loading proxy configuration ...");
            Tools.LoadConfig();
            logger.Trace("Success");

            oSQLHelper = new SQLiteHelper(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, null);
			if (!oSQLHelper.InitConnection ()) goto QuitApplication;

            #region Loading Repositories

            logger.Trace("Loading repositories ...");

            //Count repositories
            oSQLHelper.SetCommandText("SELECT COUNT(*) FROM Repository");
            QueryResult = null;
            if (!oSQLHelper.ExecuteScalar(ref QueryResult)) { oSQLHelper.Dispose(); goto QuitApplication; }
            if (Convert.ToInt32(QueryResult) == 0)
            {
                PromptNewSite = true;
            }

            oSQLHelper.SetCommandText("SELECT Id_Repository FROM Repository");
            SqlDataReader = oSQLHelper.ExecuteReader();
            while (SqlDataReader.Read())
            {
                InitRepository(Convert.ToInt32(SqlDataReader["Id_Repository"]), false);
            }
            SqlDataReader.Close();
            oSQLHelper.Dispose();

            #endregion

            #region ContextMenu

            //Exit
            System.Windows.Forms.ToolStripMenuItem Menu_Close = new System.Windows.Forms.ToolStripMenuItem();
            Menu_Close.Name = "Menu_Close";
            Menu_Close.Size = new System.Drawing.Size(128, 22);
            Menu_Close.Text = Program.RessourceManager.GetString("MENU_EXIT");
            Menu_Close.Click += Menu_Close_Click;

            //Options
            System.Windows.Forms.ToolStripMenuItem Menu_Options = new System.Windows.Forms.ToolStripMenuItem();
            Menu_Options.Name = "Menu_Options";
            Menu_Options.Size = new System.Drawing.Size(128, 22);
            Menu_Options.Text = Program.RessourceManager.GetString("MENU_OPTIONS");
            Menu_Options.Click += Menu_Options_Click;

            //View Log file
            System.Windows.Forms.ToolStripMenuItem Menu_Log = new System.Windows.Forms.ToolStripMenuItem();
            Menu_Log.Name = "Menu_Log";
            Menu_Log.Size = new System.Drawing.Size(128, 22);
            Menu_Log.Text = Program.RessourceManager.GetString("MENU_LOG");
            Menu_Log.Click += Menu_Log_Click;

            //About
            System.Windows.Forms.ToolStripMenuItem Menu_About = new System.Windows.Forms.ToolStripMenuItem();
            Menu_About.Name = "Menu_About";
            Menu_About.Size = new System.Drawing.Size(128, 22);
            Menu_About.Text = Program.RessourceManager.GetString("MENU_ABOUT");
            Menu_About.Click += Menu_About_Click;

            //Add new site 
            System.Windows.Forms.ToolStripMenuItem Menu_NewSite = new System.Windows.Forms.ToolStripMenuItem();
            Menu_NewSite.Name = "Menu_NewSite";
            Menu_NewSite.Size = new System.Drawing.Size(128, 22);
            Menu_NewSite.Text = Program.RessourceManager.GetString("MENU_NEW_SITE");
            Menu_NewSite.Click += Menu_NewSite_Click;

            //Sites
            Menu_Sites.Name = "Menu_Sites";
            Menu_Sites.Size = new System.Drawing.Size(128, 22);
            Menu_Sites.Text = Program.RessourceManager.GetString("MENU_SITES");

            foreach (RepositoryUI RUI in ListOfRepositoryUI)
            {
                Menu_Sites.DropDownItems.Add(RUI.MenuItem_Main); //Ajout du menu du repository
            }

            //Création du menu final
            CxtMenu = new ContextMenuStrip();
            CxtMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            Menu_NewSite,
            Menu_Sites,
            Menu_Options,
            Menu_Log,
            Menu_About,
            Menu_Close});
            CxtMenu.Name = "EasySyncMenu";
            CxtMenu.Size = new System.Drawing.Size(129, 92);
            if (icn != null) icn.ContextMenuStrip = CxtMenu;

            if (ListOfRepositoryUI.Count == 0) Menu_Sites.Enabled = false;

            #endregion

            GC.KeepAlive(mutex);

            if (PromptNewSite) Menu_NewSite_Click(null, null);

            Application.Run();

    QuitApplication:
            if (icn != null) icn.Visible = false;
            mutex.ReleaseMutex();
        }

        #region Exit & error functions

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            QuitApplication();
        }

        private static void QuitApplication()
        {
            if (ProductUpdateThread != null && ProductUpdateThread.ThreadState == System.Threading.ThreadState.Running)
            {
                try
                {
                    ProductUpdateThread.Abort();
                }
                catch{}
            }

            if (NumberOfStartedEngines > 0)
            {
                foreach (RepositoryUI RUI in ListOfRepositoryUI)
                {
                    RUI.StopEngine(); //Request each SyncEngine to stop
                }
                ForceQuitTimer.Enabled = true; //Init the timer that will force the closing
                if (StoppingDialog.AllowClosing == false) StoppingDialog.ShowDialog(); //Display a wait dialog that will close itself when the engine is stopped and will set ProcessingIsStopped to true ( see RM_EngineStopped() )
            }

            if (icn != null) icn.Visible = false;
            Application.Exit();
        }

        static void ForceQuitTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            StoppingDialog.AllowClosing = true;
            
            if (StoppingDialog.InvokeRequired) StoppingDialog.Invoke(new MethodInvoker(delegate { StoppingDialog.Close(); }));
            else StoppingDialog.Close();

            if (icn != null) icn.Visible = false;
            Application.Exit();
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            logger.Error("Application Wide Exception", e.Exception);
        }

        #endregion

        #region Menu Events

        private static void Menu_NewSite_Click(object sender, EventArgs e)
        {
            lock (NewSiteActiveLock)
            {
                if (NewSiteActive) return; //Only one new site dialog at a time
            }

            //No repository found, prompt the user to create one
            CreateRepository CR = new CreateRepository(_Connectors, ListOfRepositoryUI);

            lock (NewSiteActiveLock)
            {
                NewSiteActive = true;
            }

            DialogResult DR = CR.ShowDialog();

            lock (NewSiteActiveLock)
            {
                NewSiteActive = false;
            }

            if (DR == DialogResult.OK)
            {
                Int32 CreatedRepositoryId;
                SQLiteHelper oSQLHelper = new SQLiteHelper(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, null);
                if (!oSQLHelper.InitConnection()) return;

                ConnectorParameter CP = new ConnectorParameter() { Server = CR.ResultSiteURL, Login = CR.ResultLogin, Password = CR.ResultPassword, SitePath = String.Format("/Sites/{0}/documentLibrary", CR.ResultSiteName), EnableFullScan = 0 };
                String ConnectorParameters = ConnectorParameter.Serialize(CP);

                oSQLHelper.SetCommandText("INSERT INTO Repository (Name,LocalRepository,RemoteRepositoryConnector,RemoteRepositoryParameters) VALUES('" + CR.ResultSiteName + "','" + CR.ResultLocalPath + "','" + CR.ResultSelectedType + "',@ConnectionString)");
                oSQLHelper.SetCommandParameter("@ConnectionString", DbType.String, 500, ConnectorParameters);
                if (!oSQLHelper.ExecuteNonQuery()) return;

                object QueryResult = null;
                oSQLHelper.SetCommandText(@"SELECT MAX(Id_Repository) FROM Repository");
                if (!oSQLHelper.ExecuteScalar(ref QueryResult)) { oSQLHelper.Dispose(); return; }
                CreatedRepositoryId = Convert.ToInt32(QueryResult);
                oSQLHelper.Dispose();

                if (!InitRepository(CreatedRepositoryId, true))
                {
                    logger.Debug("In Menu_NewSite_Click - Unable to init repository");
                    return;
                }

                //Refresh the list of sites in the menu
                Menu_Sites.DropDownItems.Clear();
                foreach (RepositoryUI TempRUI in ListOfRepositoryUI)
                {
                    Menu_Sites.DropDownItems.Add(TempRUI.MenuItem_Main);
                }

                Menu_Sites.Enabled = true;
            }
        }

        private static bool InitRepository(int RepositoryId, bool IsFirstInit)
        {
            RepositoryUI RepUI = new RepositoryUI();

            if (!RepUI.InitRepository(RepositoryId)) return false;

            //Register events
            RepUI.RM.IconAnimationStart += RM_IconAnimationStart;
            RepUI.RM.IconAnimationStop += RM_IconAnimationStop;
            RepUI.RM.EngineStarted += RM_EngineStarted;
            RepUI.RM.EngineStopped += RM_EngineStopped;
            RepUI.RM.NotifyUser += RM_NotifyUser;
            RepUI.RM.RepositoryDeleted += RM_RepositoryDeleted;
            RepUI.RM.ProxyError += RM_NotifyUser;
            RepUI.RM.AuthenticationError += RM_NotifyUser;
            RepUI.RM.EditProperties += RM_EditProperties;
            RepUI.RM.RepositoryUploadDownloadCountChanged += RM_UploadDownloadCountChanged;

            var EngineInitSuccess = RepUI.InitEngine(_Connectors);

            if (!EngineInitSuccess && IsFirstInit)
            {
                //Exécuter ce code seulement si l'appel est fait à la création d'un nouveau repository
                //Dans le cas contraire , continuer mais ne pas lancer RepUI.StartEngine();
                logger.Debug("Init repository failed. Id : " + RepositoryId);
                return false;
            }

            ListOfRepositoryUI.Add(RepUI);

            if (EngineInitSuccess) RepUI.StartEngine();
            return EngineInitSuccess;
        }

        static void RM_UploadDownloadCountChanged(object sender, EventArgs e)
        {
            if (MainUploadDownloadCountChanged != null) MainUploadDownloadCountChanged(sender, e);
        }

        private static void Menu_About_Click(object sender, EventArgs e)
        {
            About AboutBox = new About();
            AboutBox.ShowDialog();
        }

        private static void Menu_Options_Click(object sender, EventArgs e)
        {
            Options OptionsDialog = new Options();
            var Result = OptionsDialog.ShowDialog();
            if (Result == DialogResult.OK)
            {
                foreach (RepositoryUI RUI in ListOfRepositoryUI)
                {
                    RUI.NotifyProxyUpdate();
                }
            }
        }

        private static void Menu_Close_Click(object sender, EventArgs e)
        {
            QuitApplication();
        }

        private static void icn_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            //LogWindow.Show();
            //LogWindow.WindowState = FormWindowState.Normal;
        }

        static void icn_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Stats.Show();
                Stats.Activate();
            }
        }

        static void Menu_Log_Click(object sender, EventArgs e)
        {
            Process.Start(Path.Combine(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"LogView"),"logview.exe"), Path.Combine(Globals.GlbAppFolder, "EasySync.log"));
        }

        #endregion

        #region Repository Events

        private static void RM_NotifyUser(object sender, String message)
        {
            if (icn != null) icn.ShowBalloonTip(2000, "EasySync", message, ToolTipIcon.Info);
            if (MainUploadDownloadCountChanged != null) MainUploadDownloadCountChanged(sender, null);
        }

        private static void RM_IconAnimationStart(object sender, EventArgs e)
        {
            if (!IconRefreshTimer.Enabled) IconRefreshTimer.Enabled = true;
        }

        private static void RM_IconAnimationStop(object sender, EventArgs e)
        {
            if (IconRefreshTimer.Enabled) IconRefreshTimer.Enabled = false;
            icn.Icon = ((System.Drawing.Icon)(KS2.EasySyncClient.Properties.Resources.ResourceManager.GetObject("Ok")));
        }

        private static void RM_EngineStarted(object sender, EventArgs e)
        {
            NumberOfStartedEngines++;

            //Retrieve the UI of the Repository
            RepositoryUI RUI = ListOfRepositoryUI.FirstOrDefault(x => x.RepositoryId.Equals(((Repository)sender).RepositoryId));
            if (RUI != null) RUI.StartDebugger();
            if (MainUploadDownloadCountChanged != null) MainUploadDownloadCountChanged(sender, null);
        }

        private static void RM_EngineStopped(object sender, EventArgs e)
        {
            NumberOfStartedEngines--;

            //Detach the debugger from the repository
            RepositoryUI RUI = ListOfRepositoryUI.FirstOrDefault(x => x.RepositoryId.Equals(((Repository)sender).RepositoryId));
            if (RUI != null) RUI.StopDebugger();

            if (NumberOfStartedEngines == 0)
            {
                StoppingDialog.AllowClosing = true;
                if (StoppingDialog.InvokeRequired) StoppingDialog.Invoke(new MethodInvoker(delegate { StoppingDialog.Close(); }));
                else StoppingDialog.Close();
            }
            if (MainUploadDownloadCountChanged != null) MainUploadDownloadCountChanged(sender, null);
        }

        private static void RM_RepositoryDeleted(object sender, String repositoryId)
        {
            ListOfRepositoryUI.RemoveAll(x => x.RepositoryId == Convert.ToInt32(repositoryId));

            Menu_Sites.DropDownItems.Clear();
            foreach (RepositoryUI RUI in ListOfRepositoryUI)
            {
                Menu_Sites.DropDownItems.Add(RUI.MenuItem_Main); //Ajout du menu du repository
            }

            if (ListOfRepositoryUI.Count == 0) Menu_Sites.Enabled = false;
            if (MainUploadDownloadCountChanged != null) MainUploadDownloadCountChanged(sender, null);
        }

        private static void RM_EditProperties(object sender, EventArgs e)
        {
            Repository SenderRepository = (Repository)sender;

            EditRepository ER = new EditRepository(SenderRepository.RepositoryId,
                                                   SenderRepository.SE,
                                                   SenderRepository.SiteName,
                                                   SenderRepository.LocalPath);
            if (ER.ShowDialog() == DialogResult.OK)
            {
                SenderRepository.SE.UpdateCredentials(ER.NewLogin, ER.NewPassword);
                SenderRepository.SE.UpdateScanMode(ER.EnableFullScan);
            }
        }

        private static void IconRefreshTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            IconPosition++;
            if (IconPosition == 4) IconPosition = 0;
            if (icn != null) icn.Icon = ((System.Drawing.Icon)(KS2.EasySyncClient.Properties.Resources.ResourceManager.GetObject(String.Format("Anim{0}", IconPosition))));
        }
        
        #endregion
        
        private static bool InterfaceFilter(Type typeObj, Object criteriaObj)
        {
            return typeObj.ToString() == criteriaObj.ToString();
        }
    }

    public class UserToken
    {
        public String UserLogin { get; set; }
        public String MachineName { get; set; }
        public String ProductCode { get; set; }
        public String ProductVersion { get; set; }
        public String SystemVersion { get; set; }
    }
}