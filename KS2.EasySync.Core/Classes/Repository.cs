/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using KS2.EasySync.Core;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace KS2.EasySync.Core
{
    public class Repository
    {
        #region Members and properties

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public string LocalPath;
        private bool IsSyncEngineStopped = true;
        private string RemoteRepositoryConnector = null;
        private bool SyncDeletionPending = false;
        private string RemoteRepositoryParameters = null;
        private bool ProxyErrorNotified;
        private bool AuthenticationErrorNotified;

        public string SiteName { get; set; }
        public int RepositoryId { get; set; }
        public SyncEngine SE { get; set; }

        public event EventHandler EngineStarted;
        public event EventHandler EngineStopped;
        public event EventHandler IconAnimationStart;
        public event EventHandler IconAnimationStop;
        public event EventHandler EditProperties;
        public event LogEventHandler ProxyError;
        public event LogEventHandler AuthenticationError;
        public event LogEventHandler RepositoryDeleted;
        public event LogEventHandler NotifyUser;
        public event EventHandler RepositoryUploadDownloadCountChanged;

        #endregion

        public Repository(Int32 repositoryId)
        {
            this.RepositoryId = repositoryId;

            SQLiteHelper oSQLHelper = new SQLiteHelper(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, null);
            if (!oSQLHelper.InitConnection()) return;

#if __MonoCS__
            Mono.Data.Sqlite.SqliteDataReader SqlDataReader;
#else
            System.Data.SQLite.SQLiteDataReader SqlDataReader;
#endif

            oSQLHelper.SetCommandText("SELECT Name,LocalRepository,RemoteRepositoryConnector,RemoteRepositoryParameters FROM Repository WHERE Id_Repository = " + RepositoryId);
            SqlDataReader = oSQLHelper.ExecuteReader();
            SqlDataReader.Read();

            this.SiteName = SqlDataReader["Name"].ToString();
            this.LocalPath = SqlDataReader["LocalRepository"].ToString();
            this.RemoteRepositoryConnector = SqlDataReader["RemoteRepositoryConnector"].ToString();
            this.RemoteRepositoryParameters = SqlDataReader["RemoteRepositoryParameters"].ToString();

            SqlDataReader.Close();
            oSQLHelper.Dispose();

            logger.Trace(String.Format("Found : Local path {0}", LocalPath));
        }

        public bool InitEngine(List<Type> AvailableConnectors)
        {
            try
            {
                SE = new SyncEngine(RepositoryId, LocalPath, AvailableConnectors, this.RemoteRepositoryConnector, this.RemoteRepositoryParameters);
                SE.SyncStart += SE_SyncStart;
                SE.SyncStop += SE_SyncStop;
                SE.LogOutput += SE_LogOutput;
                SE.StopComplete += SE_StopComplete;
                SE.NotifyUser += SE_NotifyUser;
                SE.ProxyError += SE_ProxyError;
                SE.AuthenticationError += SE_AuthenticationError;
                SE.SyncEngineUploadDownloadCountChanged += SE_UploadDownloadCountChanged;
            }
            catch (Exception ex)
            {
                if (NotifyUser != null) NotifyUser(this, "[" + SiteName + "]" + ex.Message);
                logger.Debug(ex.Message);
                return false;
            }
            return true;
        }

        void SE_UploadDownloadCountChanged(object sender, EventArgs e)
        {
            if (this.RepositoryUploadDownloadCountChanged != null) this.RepositoryUploadDownloadCountChanged(this,null);
        }

        void SE_AuthenticationError(object sender, EventArgs e)
        {
            if (!AuthenticationErrorNotified)
            {
                AuthenticationErrorNotified = true;
                StopEngine();
                if (AuthenticationError != null) AuthenticationError(this, "[" + SiteName + "] Authentication error. Please verify the credentials you supplied. The synchronisation has been stopped");
            }
        }

        void SE_ProxyError(object sender, EventArgs e)
        {
            if (!ProxyErrorNotified)
            {
                ProxyErrorNotified = true;
                if (ProxyError != null) ProxyError(this, "[" + SiteName + "] Cannot connect to the remote repository. Please check your proxy parameters");
            }
        }

        public bool StartEngine()
        {
            ProxyErrorNotified = false;
            AuthenticationErrorNotified = false;

            logger.Trace("Starting SyncEngine for site : " + this.SiteName);
            if (SE == null) return true;
            IsSyncEngineStopped = false;
            try
            {
                SE.StartProcessing();
                if (EngineStarted != null) EngineStarted(this, null);
            }
            catch (Exception ex)
            {
                if (NotifyUser != null) NotifyUser(this, "[" + SiteName + "]" + ex.Message);
                logger.Debug(ex.Message);
                return false;
            }

            return true;
        }

        public bool StopEngine()
        {
            if (SE == null) return false;

            if (!IsSyncEngineStopped)
            {
                IsSyncEngineStopped = true;
                if (SE != null) SE.StopProcessing(); //This will pop a SE_StopComplete event when completed
                else if (EngineStopped != null) EngineStopped(this, null);

                return true;
            }

            return false;
        }

        public void DeleteRepository()
        {
            SQLiteHelper oSQLHelper = new SQLiteHelper(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, null);
            if (!oSQLHelper.InitConnection()) return;

            //Count repositories
            oSQLHelper.SetCommandText("DELETE FROM VFile WHERE Fk_Repository = " + this.RepositoryId);
            oSQLHelper.ExecuteNonQuery();

            oSQLHelper.SetCommandText("DELETE FROM VFolder WHERE Fk_Repository = " + this.RepositoryId);
            oSQLHelper.ExecuteNonQuery();

            oSQLHelper.SetCommandText("DELETE FROM Repository WHERE Id_Repository = " + this.RepositoryId);
            oSQLHelper.ExecuteNonQuery();

            oSQLHelper.Dispose();

            if (RepositoryDeleted != null) RepositoryDeleted(this, RepositoryId.ToString());
        }

        #region SyncEngine Events

        private void SE_NotifyUser(object sender, string message)
        {
            if (NotifyUser != null) NotifyUser(sender, "[" + SiteName + "]" + message);
        }

        private void SE_SyncStart(object sender, EventArgs e)
        {
            if (IconAnimationStart != null) IconAnimationStart(sender, e);
        }

        private void SE_SyncStop(object sender, EventArgs e)
        {
            if (IconAnimationStop != null) IconAnimationStop(sender, e);
        }

        private void SE_LogOutput(object sender, string message)
        {
            logger.Debug(String.Format("[{0}]{1}", this.SiteName, message));
        }

        /// <summary>
        /// Raised when the engine has stopped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SE_StopComplete(object sender, EventArgs e)
        {
            IsSyncEngineStopped = true;
            if (EngineStopped != null) EngineStopped(this, e);
            if (SyncDeletionPending) DeleteRepository();
        }

        #endregion

        #region Menu Events

        private void Menu_Start_Click(object sender, EventArgs e)
        {
            StartEngine();
        }

        private void Menu_Stop_Click(object sender, EventArgs e)
        {
            StopEngine();
        }

        public void Edit()
        {
            if (EditProperties != null) EditProperties(this, null);
        }

        public void DeleteSync()
        {
            SyncDeletionPending = true;
            if (!IsSyncEngineStopped) StopEngine(); //Wait for the engine to stop
            else SE_StopComplete(null, null);
        }

        public void OpenLocalPath()
        {
            Process.Start(LocalPath);
        }

        #endregion

        public void NotifyProxyUpdate()
        {
            SE.NotifyProxyUpdate();
        }

        public Int32 GetUploadActionCount
        {
            get
            {
                if (SE._IsIniting && SE.GetUploadActionCount == 0) return -1; //The remote call has not been made and there is nothing to upload
                else return SE.GetUploadActionCount;
            }
        }

        public Int32 GetDownloadActionCount
        {
            get
            {
                if (SE._IsIniting) return -1; //The remote call has not been made yet
                else return SE.GetDownloadActionCount;
            }
        }
    }
}
