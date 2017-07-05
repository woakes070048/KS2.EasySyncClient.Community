/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
#if __MonoCS__
	using System.IO;
#else
using Alphaleonis.Win32.Filesystem;
#endif
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Diagnostics;
using Microsoft.VisualBasic.FileIO;
using NLog;
using System.Data;

namespace KS2.EasySync.Core
{
    public class SyncEngine : IPluginHost
    {
        #region Members

        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Int32 _RepositoryId;
        private String _LocalPath;
        private String _LocalPathTemp;

        private String _EngineId;
        public String EngineId
        {
            get
            {
                return _EngineId;
            }
        }

        public event LogEventHandler LogOutput;
        public event EventHandler StopComplete;
        public event LogEventHandler NotifyUser;
        public event EventHandler SyncEngineUploadDownloadCountChanged;

        public event EventHandler SyncStart;
        public event EventHandler SyncStop;
        public event EventHandler ProxyError;
        public event EventHandler AuthenticationError;

        public VirtualRootFolder _VirtualRootFolder;

        private IEasySyncPlugin _RemoteConnector;
        public IEasySyncPlugin RemoteConnector
        {
            get
            {
                return _RemoteConnector;
            }
        }

        private Guid _InstanceID;
        public Guid InstanceID
        {
            get
            {
                return _InstanceID;
            }
        }

        public bool _IsFirstComparerCall;

        public bool _IsIniting = true;

        /// <summary>
        /// Délai minimal entre deux scans du répository distant
        /// </summary>
        private Int32 _RemoteSyncFrequency;
        private Int32 RemoteSyncFrequency
        {
            get
            {
                if (this._IsFirstComparerCall) return 30;
                else return this._RemoteSyncFrequency;
            }
            set
            {
                this._RemoteSyncFrequency = value;
            }
        }

        /// <summary>
        /// Délai du full scan local en mode normal événement
        /// </summary>
        private Int32 NormalScanFrequency;

        /// <summary>
        /// Délai du full scan local en mode non événement
        /// </summary>
        private Int32 FullScanFrequency;

        private DateTime LastRemoteSync;
        private DateTime LastSyncEvent;
        private DateTime LastFullRefresh;

        private System.Timers.Timer RemoteServerCallTimer;
        private Thread RemoteServerCallThread;

        private String _UserName;
        public String UserName
        {
            get
            {
                return _UserName;
            }
        }

        private bool _EnableFullScan;
        public bool EnableFullScan
        {
            get
            {
                return _EnableFullScan;
            }
        }

        private Int32 FullScanRefreshTrigger; //The trigger in seconds to run a full scan

        private OrchestratorAction _LastSyncAction = OrchestratorAction.FullScan;

        #region FullScan

        private object FullScanDispatchedLock;
        private bool _FullScanDispatched;
        private bool FullScanDispatched
        {
            get
            {
                lock (FullScanDispatchedLock)
                {
                    return _FullScanDispatched;
                }
            }
            set
            {
                lock (FullScanDispatchedLock)
                {
                    this._FullScanDispatched = value;
                    this._LastSyncAction = OrchestratorAction.FullScan;
                }
            }
        }

        #endregion

        #region RemoteSync

        private object RemoteSyncDispatchedLock;
        private bool _RemoteSyncDispatched;
        private bool RemoteSyncDispatched
        {
            get
            {
                lock (RemoteSyncDispatchedLock)
                {
                    return _RemoteSyncDispatched;
                }
            }
            set
            {
                lock (RemoteSyncDispatchedLock)
                {
                    _RemoteSyncDispatched = value;
                    this._LastSyncAction = OrchestratorAction.RemoteSync;
                }
            }
        }

        #endregion

        public List<RepositoryElement> RemoteElementList;

        public Int32 GetUploadActionCount
        {
            get
            {
                return this._VirtualRootFolder.FlatElementsGetAllFiles().Sum(x => x.UploadActionCount);
            }
        }

        public Int32 GetDownloadActionCount
        {
            get
            {
                return this._VirtualRootFolder.FlatElementsGetAllFiles().Sum(x => x.DownloadActionCount);
            }
        }

        #endregion

        public SyncEngine(Int32 pRepositoryId, string pLocalPath, List<Type> availableConnectors, String remoteConnectorId, String remoteRepositoryParameters)
        {
            this._EngineId = Guid.NewGuid().ToString();
            this._EnableFullScan = false;
            this._RepositoryId = pRepositoryId;
            this._LocalPath = pLocalPath;
            this._LocalPathTemp = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySync"), @"Temp\" + _RepositoryId);

            String TempValue = GetParamValue("INSTANCE_ID");
            if (TempValue == null) return;
            else _InstanceID = Guid.Parse(TempValue);

            TempValue = GetParamValue("REMOTE_SYNC_FREQUENCY");
            if (TempValue == null) RemoteSyncFrequency = 3 * 60; //Default fequency is 3 minutes
            else RemoteSyncFrequency = Convert.ToInt32(TempValue);

            TempValue = GetParamValue("NORMAL_SCAN_FREQUENCY");
            if (TempValue == null) NormalScanFrequency = 30 * 60; //Default fequency is 30 minutes
            else NormalScanFrequency = Convert.ToInt32(TempValue);

            TempValue = GetParamValue("FULL_SCAN_FREQUENCY");
            if (TempValue == null) FullScanFrequency = 5 * 60; //Default fequency is 5 minutes
            else FullScanFrequency = Convert.ToInt32(TempValue);

            _SyncFileSystemWatcher = new SyncFileSystemWatcher(_LocalPath);
            _SyncFileSystemWatcher.Event += delegate(object SenderObject, SyncEventItem SyncE) { EventEnqueue(SyncE); };
            _SyncFileSystemWatcher.Log += delegate(object SenderObject, string Message) { LogFSEvent(Message); };

            logger.Trace(String.Format("Found : Connector {0}", remoteConnectorId));

            //Load a connector
            Type t = availableConnectors.FirstOrDefault(x => x.FullName.Equals(remoteConnectorId));
            if (t == null)
            {
                throw new Exception("Connector init failed");
            }

            logger.Trace("Init connector ...");
            this._RemoteConnector = (IEasySyncPlugin)Activator.CreateInstance(t);
            this._RemoteConnector.Init(remoteRepositoryParameters);
            this._RemoteConnector.LinkToEngine(this, this._InstanceID);
            this._RemoteConnector.LogOutput += new LogEventHandler(delegate(object sender, string s) { LogActionThread(s); });
            this._RemoteConnector.ProxyError += RemoteConnector_ProxyError;
            this._RemoteConnector.AuthenticationError += RemoteConnector_AuthenticationError;
            logger.Trace("Success");

            this._UserName = _RemoteConnector.GetLogin();

            //Detect FullScan mode
            var CP = ConnectorParameter.Deserialize(remoteRepositoryParameters);
            if (CP.EnableFullScan == 1)
            {
                this._EnableFullScan = true;
                logger.Trace("Fullscan mode enabled");
            }

            //Init the timer used to cancel a too long remote server call (which may not return)
            this.RemoteServerCallTimer = new System.Timers.Timer(10 * 60 * 1000); //10 minutes
            this.RemoteServerCallTimer.AutoReset = false;
            this.RemoteServerCallTimer.Elapsed += RemoteServerCallTimer_Elapsed;
            this.RemoteServerCallThread = null;
        }

        void RemoteConnector_AuthenticationError(object sender, EventArgs e)
        {
            if (this.AuthenticationError != null) this.AuthenticationError(this, null);
        }

        void RemoteConnector_ProxyError(object sender, EventArgs e)
        {
            if (this.ProxyError != null) this.ProxyError(this, null);
        }

        public void StartProcessing()
        {
            if (!Directory.Exists(_LocalPath)) throw new Exception(String.Format("Le répertoire {0} n'est plus présent sur votre poste. La synchronisation n'est pas possible", _LocalPath));
            
            //try to write a file in the destination folder
            String TempFilePath = Path.Combine(_LocalPath, Guid.NewGuid().ToString());
            System.IO.FileStream FS = null;
            try
            {
                FS = File.Create(TempFilePath);
                FS.Close();
            }
            catch
            {
                throw new Exception(String.Format("EasySync ne peut pas écrire dans le répertoire {0}", _LocalPath));
            }
            finally
            {
                if (File.Exists(TempFilePath))
                {
                    try
                    {
                        if (FS != null) FS.Close();
                        File.Delete(TempFilePath);
                    }
                    catch { }
                }
            }

            if (!Directory.Exists(_LocalPathTemp))
            {
                if (BuildFolderPath(_LocalPathTemp) != ActionResult.Success)
                {
                    throw new Exception(String.Format("Le répertoire {0} n'a pas pu étre créé sur votre poste", _LocalPathTemp));
                }
            }

            this._RemoteConnector.SetProxyParameter(Globals.GlbProxyMode, Globals.GlbProxyURL, Globals.GlbProxyAuthentication, Globals.GlbProxyLogin, Globals.GlbProxyPassword);

            this._IsFirstComparerCall = true;
            this.LastRemoteSync = DateTime.Now.AddSeconds(-30);
            this.LastSyncEvent = DateTime.Now;
            this.LastFullRefresh = DateTime.Now;
		    this.FullScanDispatchedLock = new object();
            this._FullScanDispatched = false;

		    this.RemoteSyncDispatchedLock = new object();
		    this._RemoteSyncDispatched = false;

		    this.RemoteElementList = new List<RepositoryElement>();
		    this._PendingEventsQueue = new Queue<SyncEventItem>();
		    this._PendingEventsLock = new object();
			this.OrchestratorTasks = new List<Task>();

            this._VirtualRootFolder = new VirtualRootFolder(_RepositoryId);
            this._VirtualRootFolder.Event += delegate(object SenderObject, SyncEventItem SyncE) { EventEnqueue(SyncE); };
            this._VirtualRootFolder.Log += delegate(object SenderObject, string Message) { Log(Message); };
            this._VirtualRootFolder.ActionAdd(new SyncActionItem(Guid.NewGuid(), SyncActionEnum.EngineInit)); //Request the orchestrator to init the engine

            this.Orchestrator = new BackgroundWorker();
            this.Orchestrator.DoWork += Orchestrator_DoWork;
            this.Orchestrator.RunWorkerCompleted += Orchestrator_RunWorkerCompleted;
            this.Orchestrator.WorkerReportsProgress = true;
            this.Orchestrator.WorkerSupportsCancellation = true;
            this.Orchestrator.RunWorkerAsync();

            this.EventManager = new BackgroundWorker();
            this.EventManager.DoWork += EventManager_DoWork;
            this.EventManager.WorkerSupportsCancellation = true;
            this.EventManager.RunWorkerAsync();

            if (this._EnableFullScan) FullScanEnable();
            else NormalScanEnable();
        }

        public void StopProcessing()
        {
            this._SyncFileSystemWatcher.Stop();
            this.EventManager.CancelAsync();
            this.Orchestrator.CancelAsync(); //This will raise a Orchestrator_RunWorkerCompleted event
            this._SyncFileSystemWatcher.Stop();
        }

        void RemoteServerCallTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (RemoteServerCallThread != null)
            {
                RemoteServerCallTimer.Enabled = false;
                RemoteServerCallThread.Abort();
                RemoteSyncDispatched = false;
                if (this.SyncStop != null) SyncStop(this, null);
            }
        }

        internal void NotifyProxyUpdate()
        {
            this._RemoteConnector.SetProxyParameter(Globals.GlbProxyMode, Globals.GlbProxyURL, Globals.GlbProxyAuthentication, Globals.GlbProxyLogin, Globals.GlbProxyPassword);
        }

        public void UpdateCredentials(string Login, string Password)
        {
            this._RemoteConnector.SetNewCredentials(Login, Password);
        }

        public void UpdateScanMode(bool enableFullScan)
        {
            if (this._EnableFullScan != enableFullScan) //The settings have changed
            {
                this._EnableFullScan = enableFullScan;

                if (this._EnableFullScan) FullScanEnable();
                else NormalScanEnable();
            }
        }

        /// <summary>
        /// Work with events
        /// </summary>
        private void NormalScanEnable()
        {
            this._SyncFileSystemWatcher.Start(_VirtualRootFolder);
            this.FullScanRefreshTrigger = NormalScanFrequency;
        }

        /// <summary>
        /// Work with fullscan. We do not use FileSystemWatcher but full folder scan to detect changes
        /// </summary>
        private void FullScanEnable()
        {
            this._SyncFileSystemWatcher.Stop();
            this.FullScanRefreshTrigger = FullScanFrequency;
        }

        #region Events Management

        private SyncFileSystemWatcher _SyncFileSystemWatcher;
        private object _PendingEventsLock;
        private Queue<SyncEventItem> _PendingEventsQueue;
        private BackgroundWorker EventManager;

        public void EventEnqueue(SyncEventItem SEI)
        {
            lock (_PendingEventsLock)
            {
                _PendingEventsQueue.Enqueue(SEI);
            }
        }

        void EventManager_DoWork(object sender, DoWorkEventArgs e)
        {
            SyncEventItem SEI = null;
            while (!((BackgroundWorker)sender).CancellationPending)
            {
                if (_PendingEventsQueue.Count > 0 && !RemoteSyncDispatched)
                {
                    lock (_PendingEventsLock) { SEI = _PendingEventsQueue.Dequeue(); }

                    try
                    {
                        DispatchEvent(SEI);
                    }
                    catch ( Exception ex)
                    {
                        Log(String.Format("EventManager_DoWork - Something went wrong : [EVENT][{0}][{1}] {2})", SEI.SyncEventId, SEI.SyncEvent.ToString(), ex.Message));
                    }
                }
                else
                {
                    Thread.Sleep(200);
                }
            }
        }

        public void DispatchEvent(SyncEventItem SEI)
        {
            VirtualFile TempLocalFile;
            VirtualElement TempParentFolder;
            VirtualElement TempLocalElement;

            String TempCurrentParentPath;
            String TempNewParentPath;
            
            VirtualElement CurrentParentFolder;
            VirtualElement NewParentFolder;

            String TempRelativePath;
            String TempParentPath;

            switch (SEI.SyncEvent)
            {
                case SyncEventEnum.LocalCreate: //Fichiers et répertoires

                    #region Local Element has been created


					LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element path : {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.ActualEntityPathFull));

					TempRelativePath = GetRelativeElementPath(SEI.ActualEntityPathFull);
					TempParentPath = Tools.GetParentPath(TempRelativePath);

					if (SEI.IsDirectoryEvent) TempLocalElement = (VirtualElement)new VirtualFolder(TempRelativePath);
					else TempLocalElement = (VirtualElement)new VirtualFile(TempRelativePath);

					TempParentFolder = _VirtualRootFolder.FlatElementsGetElementBasedOnPath(TempParentPath, VirtualElementType.Folder, false);

					//If there is already an element having the same path => return
					if (TempParentFolder.GetDirectSubElementByPath(TempRelativePath, false) != null) return;

					TempParentFolder.SubElementAdd(TempLocalElement);

                    _VirtualRootFolder.FlatElementAdd(TempLocalElement);

                    LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element path is {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), TempLocalElement.PathRelative));

                    if (SEI.IsDirectoryEvent)
                    {
                        //LogDispatchEvent(String.Format("Registering action SyncActionEnum.FolderRemoteCreate : {0}", TempSubFolder.PathRelative));
                        TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FolderRemoteCreate));

                        if (SEI.LocalElementId.Equals(Guid.Empty) && !SEI.IsStartupEvent) //Le répertoire existe déjà, on vérifie s'il n'y a pas des sous-répertoires a recréer | On ne prend pas en compte les événements de type startup car le process de startup est récursif et a déjà lancé des évélements pour les sous-élements
                        {
                            //LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Registering action SyncActionEnum.FolderLocalParse : {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), TempLocalElement.PathRelative));
                            TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FolderLocalParse));
                        }
                    }
                    else
                    {
                        //LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Registering action SyncActionEnum.UploadNew : {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), TempLocalElement.PathRelative));
                        if (!TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FileUploadNew))) LogDispatchEvent(String.Format("Action already registered"));
                        if (SyncEngineUploadDownloadCountChanged != null) SyncEngineUploadDownloadCountChanged(this, null);
                    }

                    #endregion

                    break;

                case SyncEventEnum.LocalDelete: //Fichiers et répertoires

                    #region Local Element has been deleted => Perform a remote delete

                    if (SEI.LocalElementId.Equals(Guid.Empty)) //L'événement DELETE peut contenir soit un EntityID soit un ActualEntityPathFull
                    {
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Relative Path {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.ActualEntityPathFull));
                        TempLocalElement = _VirtualRootFolder.FlatElementsGetElementBasedOnPath(GetRelativeElementPath(SEI.ActualEntityPathFull), SEI.IsDirectoryEvent ? VirtualElementType.Folder : VirtualElementType.File, false);
                        if (TempLocalElement == null)
                        {
                            LogDispatchEvent(String.Format("[EVENT][{0}][{1}] ERROR Cannot find Element {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.ActualEntityPathFull));
                            return;
                        }
                    }
                    else
                    {
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element Id {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId));
                        TempLocalElement = _VirtualRootFolder.FlatElementsGetElementBasedOnId(SEI.LocalElementId, SEI.IsDirectoryEvent ? VirtualElementType.Folder : VirtualElementType.File, false);
                        if (TempLocalElement == null)
                        {
                            LogDispatchEvent(String.Format("[EVENT][{0}][{1}] ERROR - Cannot find Element {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId));
                            return;
                        }
                    }

                    LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element path was {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), TempLocalElement.PathRelative));

                    //if (TempLocalElement.ProcessingTask != null) Debug.WriteLine(" Event dispatcher send delete event to Task" + TempLocalElement.ProcessingTask.Id);
                    TempLocalElement.SetDeleted(-1);

                    if (SEI.IsDirectoryEvent)
                    {
                        //Delete Element remotely
                        if (!TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FolderRemoteDelete))) LogDispatchEvent(String.Format("Action already registered"));
                    }
                    else
                    {
                        //Delete file remotely
                        if (!TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FileRemoteDelete))) LogDispatchEvent(String.Format("Action already registered"));
                    }

                    #endregion

                    break;

                case SyncEventEnum.LocalRename: //Fichiers et répertoires

                    #region Local element has been renammed => Perform a remote rename

                    if (SEI.LocalElementId.Equals(Guid.Empty))
                    {
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Renamed from {2} to {3}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.OldFilePathFull, SEI.ActualEntityPathFull));
                        TempLocalElement = _VirtualRootFolder.FlatElementsGetElementBasedOnPath(GetRelativeElementPath(SEI.OldFilePathFull), SEI.IsDirectoryEvent ? VirtualElementType.Folder : VirtualElementType.File, false);
                        SEI.LocalElementId = TempLocalElement.ElementId;
                    }
                    else
                    {
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element with id {2} renamed to {3}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId, SEI.ActualEntityPathFull));
                        TempLocalElement = _VirtualRootFolder.FlatElementsGetElementBasedOnId(SEI.LocalElementId, SEI.IsDirectoryEvent ? VirtualElementType.Folder : VirtualElementType.File, false);
                    }

                    if (TempLocalElement == null)
                    {
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element path was {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), TempLocalElement.PathRelative));

                        if (SEI.LocalElementId.Equals(Guid.Empty))
                        {
                            LogDispatchEvent(String.Format("[EVENT][{0}][{1}] ERROR - Cannot find file {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), GetRelativeElementPath(SEI.OldFilePathFull)));
                        }
                        else
                        {
                            LogDispatchEvent(String.Format("[EVENT][{0}][{1}] ERROR - Cannot find file {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId.ToString()));
                        }
                        return;
                    }

                    TempLocalElement.CurrentName = Path.GetFileName(SEI.ActualEntityPathFull);

                    if (SEI.IsDirectoryEvent)
                    {
                        //LogDispatchEvent(String.Format("Registering action SyncActionEnum.FolderRemoteRename"));
                        if (!TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FolderRemoteRename, GetRelativeElementPath(SEI.ActualEntityPathFull)))) LogDispatchEvent(String.Format("Action already registered"));
                    }
                    else
                    {
                        //LogDispatchEvent(String.Format("Registering action SyncActionEnum.FileRemoteRename"));
                        if (!TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FileRemoteRename, GetRelativeElementPath(SEI.ActualEntityPathFull)))) LogDispatchEvent(String.Format("Action already registered"));
                    }

                    #endregion

                    break;

                case SyncEventEnum.LocalMove: //Ne concerne que les fichiers

                    #region Local file has been moved => Perform a remote move

                    if (SEI.LocalElementId.Equals(Guid.Empty))
                    {
                        TempLocalFile = (VirtualFile)_VirtualRootFolder.FlatElementsGetElementBasedOnPath(GetRelativeElementPath(SEI.OldFilePathFull), VirtualElementType.File, false);
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Moved from {2} to {3}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.OldFilePathFull, SEI.ActualEntityPathFull));
                    }
                    else
                    {
                        TempLocalFile = (VirtualFile)_VirtualRootFolder.FlatElementsGetElementBasedOnId(SEI.LocalElementId, VirtualElementType.File, false);
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element with id {2} move to {3}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId, SEI.ActualEntityPathFull));
                    }

                    if (TempLocalFile == null)
                    {
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element path was {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), TempLocalFile.PathRelative));

                        if (SEI.LocalElementId.Equals(Guid.Empty))
                        {
                            LogDispatchEvent(String.Format("[EVENT][{0}][{1}] ERROR - Cannot find file {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), GetRelativeElementPath(SEI.OldFilePathFull)));
                        }
                        else
                        {
                            LogDispatchEvent(String.Format("[EVENT][{0}][{1}] ERROR - Cannot find file {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId.ToString()));
                        }
                        return;
                    }

                    SEI.LocalElementId = TempLocalFile.ElementId;

                    TempCurrentParentPath = Tools.GetParentPath(GetRelativeElementPath(SEI.OldFilePathFull));
                    TempNewParentPath = Tools.GetParentPath(GetRelativeElementPath(SEI.ActualEntityPathFull));

                    CurrentParentFolder = _VirtualRootFolder.FlatElementsGetElementBasedOnPath(TempCurrentParentPath, VirtualElementType.Folder, false);
                    NewParentFolder = _VirtualRootFolder.FlatElementsGetElementBasedOnPath(TempNewParentPath, VirtualElementType.Folder, false);

                    CurrentParentFolder.SubElementRemove(TempLocalFile);
                    NewParentFolder.SubElementAdd(TempLocalFile);

                    //LogDispatchEvent(String.Format("Registering action SyncActionEnum.FileRemoteMove"));
                    if (!TempLocalFile.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FileRemoteMove, GetRelativeElementPath(SEI.ActualEntityPathFull)))) LogDispatchEvent(String.Format("Action already registered"));

                    #endregion

                    break;

                case SyncEventEnum.LocalMoveAndRename: //Ne concerne que les fichiers

                    #region LocalMoveAndRename

                    if (SEI.LocalElementId.Equals(Guid.Empty))
                    {
                        TempLocalFile = (VirtualFile)_VirtualRootFolder.FlatElementsGetElementBasedOnPath(GetRelativeElementPath(SEI.OldFilePathFull), VirtualElementType.File, false);
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element moved&renamed from {2} to {3}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.OldFilePathFull, SEI.ActualEntityPathFull));
                    }
                    else
                    {
                        TempLocalFile = (VirtualFile)_VirtualRootFolder.FlatElementsGetElementBasedOnId(SEI.LocalElementId, VirtualElementType.File, false);
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element with id {2} moved&renamed to {3}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId, SEI.ActualEntityPathFull));
                    }

                    if (TempLocalFile == null)
                    {
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element path was {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), TempLocalFile.PathRelative));

                        if (SEI.LocalElementId.Equals(Guid.Empty))
                        {
                            LogDispatchEvent(String.Format("[EVENT][{0}][{1}]ERROR - Cannot find file {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), GetRelativeElementPath(SEI.OldFilePathFull)));
                        }
                        else
                        {
                            LogDispatchEvent(String.Format("[EVENT][{0}][{1}]ERROR - Cannot find file {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId.ToString()));
                        }
                        return;
                    }
                    
                    SEI.LocalElementId = TempLocalFile.ElementId;

                    TempCurrentParentPath = Tools.GetParentPath(GetRelativeElementPath(SEI.OldFilePathFull));
                    TempNewParentPath = Tools.GetParentPath(GetRelativeElementPath(SEI.ActualEntityPathFull));

                    CurrentParentFolder = _VirtualRootFolder.FlatElementsGetElementBasedOnPath(TempCurrentParentPath, VirtualElementType.Folder, false);
                    NewParentFolder = _VirtualRootFolder.FlatElementsGetElementBasedOnPath(TempNewParentPath, VirtualElementType.Folder, false);

                    CurrentParentFolder.SubElementRemove(TempLocalFile);
                    NewParentFolder.SubElementAdd(TempLocalFile);
                    TempLocalFile.CurrentName = Path.GetFileName(SEI.ActualEntityPathFull);

                    //LogDispatchEvent(String.Format("Registering action SyncActionEnum.FileRemoteMove"));
                    if (!TempLocalFile.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FileRemoteMoveAndRename, GetRelativeElementPath(SEI.ActualEntityPathFull)))) LogDispatchEvent(String.Format("Action already registered"));

                    #endregion

                    break;

                case SyncEventEnum.LocalUpdate: //Ne concerne que les fichiers

                    #region Local file has been updated

                    if (SEI.LocalElementId.Equals(Guid.Empty))
                    {
                        TempLocalFile = (VirtualFile)_VirtualRootFolder.FlatElementsGetElementBasedOnPath(GetRelativeElementPath(SEI.ActualEntityPathFull), VirtualElementType.File, false);
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element updated {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.ActualEntityPathFull));
                    }
                    else
                    {
                        TempLocalFile = (VirtualFile)_VirtualRootFolder.FlatElementsGetElementBasedOnId(SEI.LocalElementId, VirtualElementType.File, false);
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element with id {2} updated", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId));
                    }

                    if (TempLocalFile == null) //Ignore action if the file does not exist anymore at his previous location
                    {
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element path is {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), TempLocalFile.PathRelative));

                        if (SEI.LocalElementId.Equals(Guid.Empty))
                        {
                            LogDispatchEvent(String.Format("[EVENT][{0}][{1}] ERROR - Cannot find file {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), GetRelativeElementPath(SEI.ActualEntityPathFull)));
                        }
                        else
                        {
                            LogDispatchEvent(String.Format("[EVENT][{0}][{1}] ERROR - Cannot find file {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId.ToString()));
                        }
                        return;
                    }

                    SEI.LocalElementId = TempLocalFile.ElementId;

                    //LogDispatchEvent(String.Format("Registering action SyncActionEnum.UploadExisting"));
                    if (!TempLocalFile.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FileUploadExisting))) LogDispatchEvent(String.Format("Action already registered"));
                    if (SyncEngineUploadDownloadCountChanged != null) SyncEngineUploadDownloadCountChanged(this, null);

                    #endregion

                    break;

                case SyncEventEnum.RemoteCreate: //Fichiers et répertoires

                    #region Remote Create

                    LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Remote element created at path {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.RemoteElement.PathRelative));

                    if (SEI.IsDirectoryEvent) TempLocalElement = (VirtualElement)new VirtualFolder(SEI.RemoteElement.PathRelative);
                    else TempLocalElement = (VirtualElement)new VirtualFile(SEI.RemoteElement.PathRelative);

                    TempLocalElement.RemoteID = SEI.RemoteElement.ElementId;
                    TempParentPath = SEI.RemoteElement.PathRelative.Substring(0, SEI.RemoteElement.PathRelative.LastIndexOf(Path.DirectorySeparatorChar));
                    TempParentFolder = _VirtualRootFolder.FlatElementsGetElementBasedOnPath(TempParentPath, VirtualElementType.Folder, false, true); //On utilise UseTargetNameFirst, cela permet de parer au cas où un répertoire a été renommé à distance et qu'un fichier a été créé dans ce répertoire.En effet, si nous ne faisons pas cela,nous ne pouvons pas retrouver le répertoire parent du fichier. Le répertoire n'ayant pas changé de nom avant la réalisation de l'action correspondante

                    if (TempParentFolder == null)
                    {
                        RebuildMissingFolderForAction(TempParentPath);
                        TempParentFolder = _VirtualRootFolder.FlatElementsGetElementBasedOnPath(TempParentPath, VirtualElementType.Folder, false);
                        if (TempParentFolder == null)
                        {
                            LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Cannot get parent element whereas RebuildMissingFolderForAction has ran. Path is : {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), TempParentPath));
                            return;
                        }
                    }

                    //If there is already an element having the same path => return
                    var ExistingElement = TempParentFolder.GetDirectSubElementByPath(SEI.RemoteElement.PathRelative, false);
                    if (ExistingElement != null)
                    {
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] There is already an element with same name at this location {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.RemoteElement.PathRelative));
                        ExistingElement.RemoteID = SEI.RemoteElement.ElementId;
                        VirtualElement_Serialize(ExistingElement);
                        return;
                    }

                    TempParentFolder.SubElementAdd(TempLocalElement);
                    _VirtualRootFolder.FlatElementAdd(TempLocalElement);

                    if (SEI.IsDirectoryEvent)
                    {
                        if (!TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FolderLocalCreate, SEI.RemoteElement))) LogDispatchEvent(String.Format("Action already registered"));
                    }
                    else
                    {
                        if (!TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FileDownloadNew, SEI.RemoteElement))) LogDispatchEvent(String.Format("Action already registered"));
                        if (SyncEngineUploadDownloadCountChanged != null) SyncEngineUploadDownloadCountChanged(this, null);
                    }

                    #endregion

                    break;

                case SyncEventEnum.RemoteUpdate: //Fichiers seulement

                    #region RemoteUpdate

                    LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element with id {2} was remotely updated. Path is : {3}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId, SEI.RemoteElement.PathRelative));

                    TempLocalElement = (VirtualElement)_VirtualRootFolder.FlatElementsGetElementBasedOnId(SEI.LocalElementId, SEI.IsDirectoryEvent ? VirtualElementType.Folder : VirtualElementType.File, false);
                    if (TempLocalElement != null)
                    {
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element path is {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), TempLocalElement.PathRelative));

                        if (!TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FileDownloadExisting, SEI.RemoteElement))) LogDispatchEvent(String.Format("Action already registered"));
                        if (SyncEngineUploadDownloadCountChanged != null) SyncEngineUploadDownloadCountChanged(this, null);
                    }

                    #endregion

                    break;

                case SyncEventEnum.RemoteRename: //Fichiers et répertoires

                    #region RemoteRename

                    LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element with id {2} was remotely renamed to {3}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId, SEI.RemoteElement.PathRelative));

                    TempLocalElement = (VirtualElement)_VirtualRootFolder.FlatElementsGetElementBasedOnId(SEI.LocalElementId, SEI.IsDirectoryEvent ? VirtualElementType.Folder : VirtualElementType.File, false);
                    if (TempLocalElement != null)
                    {
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element path was {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), TempLocalElement.PathRelative));

                        if (SEI.IsDirectoryEvent)
                        {
                            if (!TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FolderLocalRename, SEI.RemoteElement))) LogDispatchEvent(String.Format("Action already registered"));
                        }
                        else
                        {
                            if (!TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FileLocalRename, SEI.RemoteElement))) LogDispatchEvent(String.Format("Action already registered"));
                        }
                    }

                    #endregion

                    break;

                case SyncEventEnum.RemoteMove: //Fichiers et répertoires

                    #region RemoteMove

                    LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element with id {2} was remotely moved to {3}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId, SEI.RemoteElement.PathRelative));

                    TempLocalElement = (VirtualElement)_VirtualRootFolder.FlatElementsGetElementBasedOnId(SEI.LocalElementId, SEI.IsDirectoryEvent ? VirtualElementType.Folder : VirtualElementType.File, false);
                    if (TempLocalElement != null)
                    {
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element path was {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), TempLocalElement.PathRelative));

                        if (SEI.IsDirectoryEvent)
                        {
                            if (!TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FolderLocalMove, SEI.RemoteElement))) LogDispatchEvent(String.Format("Action already registered"));
                        }
                        else
                        {
                            if (!TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FileLocalMove, SEI.RemoteElement))) LogDispatchEvent(String.Format("Action already registered"));
                        }
                    }

                    #endregion

                    break;

                case SyncEventEnum.RemoteDelete: //Fichiers et répertoires

                    #region RemoteDelete

                    LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element with id {2} was deleted", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId));

                    TempLocalElement = (VirtualElement)_VirtualRootFolder.FlatElementsGetElementBasedOnId(SEI.LocalElementId, SEI.IsDirectoryEvent ? VirtualElementType.Folder : VirtualElementType.File, false);
                    if (TempLocalElement != null)
                    {
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element path was {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), TempLocalElement.PathRelative));

                        if (SEI.IsDirectoryEvent)
                        {
                            if (!TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FolderLocalDelete))) LogDispatchEvent(String.Format("Action already registered"));
                        }
                        else
                        {
                            if (!TempLocalElement.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FileLocalDelete))) LogDispatchEvent(String.Format("Action already registered"));
                        }
                    }

                    #endregion

                    break;

                case SyncEventEnum.BothSideUpdate: //File Only

                    #region In an upload action, both local file and remote file have changed

                    LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element with id {2} has a conflict", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId.ToString()));

                    TempLocalFile = (VirtualFile)_VirtualRootFolder.FlatElementsGetElementBasedOnId(SEI.LocalElementId, VirtualElementType.File, false);
                    if (TempLocalFile == null)
                    {
                        LogDispatchEvent(String.Format("[EVENT][{0}][{1}] - ERROR Cannot find file {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), SEI.LocalElementId.ToString()));
                        return;
                    }

                    LogDispatchEvent(String.Format("[EVENT][{0}][{1}] Element path is {2}", SEI.SyncEventId, SEI.SyncEvent.ToString(), TempLocalFile.PathRelative));

                    if (!TempLocalFile.ActionAdd(new SyncActionItem(SEI.SyncEventId, SyncActionEnum.FileUploadConflict, SEI.RemoteElement))) LogDispatchEvent(String.Format("Action already registered"));
                    if (SyncEngineUploadDownloadCountChanged != null) SyncEngineUploadDownloadCountChanged(this, null);

                    #endregion

                    break;
            }
        }

        #endregion

        #region Orchestrator

        //Orchestrator select the tasks to be performed and make them processed in sub-tasks
        private BackgroundWorker Orchestrator;
        private List<Task> OrchestratorTasks;

        void Orchestrator_DoWork(object sender, DoWorkEventArgs e)
        {
            Int32 DispatchedActions;
            Int32 MaxConcurrentThreads = 1;
            Int32 CurrentConcurrentThread = 0;

            while (true)
            {
                DispatchedActions = 0;

                OrchestratorTasks.RemoveAll(x => x.Status == TaskStatus.Canceled || x.Status == TaskStatus.Faulted || x.Status == TaskStatus.RanToCompletion);
                CurrentConcurrentThread = OrchestratorTasks.Count();

                if (((BackgroundWorker)sender).CancellationPending)
                {
                    logger.Debug("Orchestrator is requested to stop");
                    if (CurrentConcurrentThread == 0)
                    {
                        logger.Debug("No tasks are pending. Quitting !");
                        return;
                    }
                    else
                    {
                        logger.Debug("Cancel all tasks and wait ...");
                        _VirtualRootFolder.CancelAllActionsRecursive(-1);
                        Task.WaitAll(OrchestratorTasks.ToArray());
                    }
                }

                List<VirtualElement> ActionList = this._VirtualRootFolder.FlatElementsGetActionsSortedByDate(); //On récupére tous les évements du plus ancien au plus récent

                if (ActionList.Count == 0
                    && !RemoteSyncDispatched
                    && (DateTime.Now - LastFullRefresh).TotalSeconds >= this.FullScanRefreshTrigger
                    && _LastSyncAction == OrchestratorAction.RemoteSync //The last action is not already a full scan => this leave time for RemoteScan to execute
                    )
                {
                    FullScanDispatched = true;
                    OrchestratorTasks.Add(Task.Factory.StartNew(() => FullRefresh()));
                }

                //Si aucune tache n'est en cours et que la derniére synchro remonte à plus de [RemoteSyncFrequency] secondes, on déclenche une synchro
                if (ActionList.Count == 0 //aucune tache n'est en cours
                    && (DateTime.Now - LastRemoteSync).TotalSeconds >= this.RemoteSyncFrequency //derniére synchro remonte à plus de x secondes
                    && (DateTime.Now - LastSyncEvent).TotalSeconds > 10 //dernier événement date de plus de 10 secondes (pour laisser le temps à Alfresco de gérer le dernier événement)
                    && !RemoteSyncDispatched //Pas déjà dispatché
                    && !FullScanDispatched //Pas de dispatch de full refresh
                    )
                {
                    RemoteSyncDispatched = true;
                    OrchestratorTasks.Add(Task.Factory.StartNew(() => CompareToRemote()));
                }

                if (!RemoteSyncDispatched && !FullScanDispatched)
                {
                    if (CurrentConcurrentThread < MaxConcurrentThreads)
                    {
                        if (ActionList.Count > 0)
                        {
                            if (ActionList[0].NextActionDateIncludePostpone != null) //Si l'évémenent est posponé, on attend. Sinon, on le processe
                            {
                                Task T = Task.Factory.StartNew(() => Orchestrator_ProcessAction(ActionList[0]));
                                ActionList[0].SetDispatched(T);
                                OrchestratorTasks.Add(T);
                                DispatchedActions++;
                                LastSyncEvent = DateTime.Now;
                                if (this.SyncStart != null) SyncStart(this, null);
                            }
                        }

                        #region To rework
                        /*
                        TODO :
                        Orchestrator plus intelligent en fonction de la nature des actions
                        If the event is DeleteFolder => wait for all sub elements (folders and files) to have completed their actions
                        Only process one delete at a time ??

                        // les créations se font de la racine vers les feuilles
                        // les suppressions se font des feuilles vers la racine

                        foreach (var VF in ActionList)
                        {
                            bool IsHierarchyDispatched = false;

                            //Planify actions for folders

                            //Check that there is no planned action for one of the parent of this element
                            VirtualElement CurrentElement = VF;
                            while (true)
                            {
                                VirtualElement ParentElement = CurrentElement.ParentElement;
                                if (ParentElement == null) break;

                                if (ParentElement.HasActions)
                                {
                                    IsHierarchyDispatched = true;
                                    break;
                                }
                                else
                                {
                                    CurrentElement = ParentElement;
                                }
                            }

                            //Il y a t'il un autre objet avec le même nom avec des actions en cours ?? (ex : il ne faut pas que la création d'un nouveau répertoire "test" soit effectuée avant la suppression d'un précédent répertoire "test"
                            if (!IsHierarchyDispatched)
                            {
                                if (_VirtualRootFolder.FlatElementsGetAll().Count(x => x.PathRelative.ToLower().Equals(VF.PathRelative.ToLower()) && x.ElementId != VF.ElementId && x.NextActionDateRaw.HasValue && x.NextActionDateRaw.Value < VF.NextActionDateRaw.Value) > 0)
                                {
                                    IsHierarchyDispatched = true;
                                }
                            }

                            if (!IsHierarchyDispatched)
                            {
                                VF.SetDispatched();
                                OrchestratorTasks.Add(Task.Factory.StartNew(() => Orchestrator_ProcessAction(VF)));
                                DispatchedActions++;
                            }
                        }
                        */
                        /*
                        foreach (var VF in this._VirtualRootFolder.VirtualFiles.Where(x => x.NextActionDate != null).OrderBy(x => x.NextActionDate))
                        {
                            //Check that there is no planned action for one of the parent of this element

                            bool IsParentDispatched = false;
                            string TemporaryPath = "";
                        
                            //Planify actions for files

                            //Check that there is no planned action for one of the parent of this element
                            foreach (string s in Path.GetDirectoryName(VF.PathRelative).Split(new Char[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                TemporaryPath += "\\" + s;
                                var Temp = _VirtualRootFolder.FindFolderBasedOnPath(TemporaryPath);
                                if (Temp != null && Temp.IsDispatched)
                                {
                                    IsParentDispatched = true;
                                    break;
                                }
                            }

                            if (!IsParentDispatched)
                            {
                                VF.SetDispatched();
                                OrchestratorTasks.Add(Task.Factory.StartNew(() => ProcessAction(VF)));
                                DispatchedActions++;
                            }
                        }
                        */
                        #endregion
                    }
                }

                if (DispatchedActions == 0) Thread.Sleep(1 * 100); //No actions dispatched. We wait for a second
            }
        }

        public void Orchestrator_ProcessAction(VirtualElement context)
        {
            System.IO.FileStream FS = null;
            Guid SyncActionItemId;
            SyncActionItem SAI = null;
            SyncEventIgnoreItem SAII;
            ActionResult AR;
            Random Rnd;
            bool NotifyUploadDownloadCompletion = false;

            if (context.ElementType == VirtualElementType.RootFolder)
            {
                #region Root Folder Action

                try
                {
                    SAI = ((VirtualRootFolder)context).ActionGetNext();

                    switch (SAI.Action)
                    {
                        case SyncActionEnum.EngineInit:

                            //Clean Temp Folder
                            foreach (string file in Directory.GetFiles(_LocalPathTemp)) { try { File.Delete(file); } catch { } }
                            foreach (string subDirectory in Directory.GetDirectories(_LocalPathTemp)) { try { Directory.Delete(subDirectory, true); } catch { } }

                            //Get the physical file structure
                            //This is done only when the program starts
                            //Other modifications to the folder structure are monitored by the FileSystemWatcher
                            Log("[INIT]Start Physical inventory");
                            PhysicalRootFolder SyncRootFolder = new PhysicalRootFolder(_LocalPath);
                            Log("[INIT]End Physical inventory");
                            Log(String.Format("[INIT]Found {0} Folder(s) and {1} File(s)", SyncRootFolder._Folders.Count, SyncRootFolder._Files.Count));

                            Log("[INIT]Start Virtual inventory");
                            if (!this._VirtualRootFolder.ReloadDataFromDatabase())
                            {
                                LogActionThread(String.Format("[{0}][{1}] Postponed", SAI.ActionItemId, SAI.Action));
                                ((VirtualRootFolder)context).ActionPostpone();
                                return;
                            }
                            
                            this._VirtualRootFolder.ClearIdentification();
                            this._VirtualRootFolder.Compare(SyncRootFolder);
                            this._VirtualRootFolder.ClearIdentification();
                            Log("[INIT]End Virtual inventory");

                            break;
                    }
                }
                catch (Exception ex)
                {
                    ((VirtualRootFolder)context).ActionPostpone();
                    LogActionThread(String.Format("Orchestrator_ProcessAction - Something went wrong [{0}][{1}] {2} {3}", SAI.ActionItemId, SAI.Action, ex.Message, ex.StackTrace));
                }

                ((VirtualRootFolder)context).ActionCloseCurrent();
               
                #endregion
            }
            else if (context.ElementType == VirtualElementType.File)
            {
                #region File Actions

                VirtualFile FileToProcess = null;
                FileToProcess = (VirtualFile)context;

                try
                {
                    SAI = FileToProcess.ActionGetNext();

                    if (SAI != null)
                    {
                        switch (SAI.Action)
                        {
                            case SyncActionEnum.FileUploadNew:
                            case SyncActionEnum.FileUploadExisting:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                AR = _RemoteConnector.UploadVirtualFile(FileToProcess, SAI);
                                if (AR == ActionResult.Retry || AR == ActionResult.RemoteServerUnreachable)
                                {
                                    LogActionThread(String.Format("[{0}][{1}] Postponed", SAI.ActionItemId, SAI.Action));
                                    FileToProcess.ActionPostpone();
                                    return;
                                }
                                else if (AR == ActionResult.Cancel)
                                {
                                    LogActionThread(String.Format("[{0}][{1}] Cancelled", SAI.ActionItemId, SAI.Action));
                                }
                                else
                                {
                                    VirtualElement_Serialize(FileToProcess);
                                    NotifyUploadDownloadCompletion = true;
                                    LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));
                                }

                                #endregion

                                break;

                            case SyncActionEnum.FileRemoteDelete:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                if (!FileToProcess.RemoteID.Equals(String.Empty)) //File may not be uploaded, we only perform remote deletion if the file is uploaded
                                {
                                    AR = _RemoteConnector.DeleteRepositoryFile(FileToProcess, SAI);

                                    if (AR == ActionResult.Retry || AR == ActionResult.RemoteServerUnreachable)
                                    {
                                        LogActionThread(String.Format("[{0}][{1}] Postponed", SAI.ActionItemId, SAI.Action));
                                        FileToProcess.ActionPostpone();
                                        return;
                                    }
                                    else if (AR == ActionResult.Cancel)
                                    {
                                        LogActionThread(String.Format("[{0}][{1}] Cancelled", SAI.ActionItemId, SAI.Action));
                                    }
                                }

                                VirtualFolder_DeleteFile(FileToProcess, Task.CurrentId);

                                LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));

                                #endregion

                                break;

                            case SyncActionEnum.FileRemoteRename:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                if (!FileToProcess.RemoteID.Equals(String.Empty)) //File may not be uploaded, we only perform remote rename if the file is uploaded
                                {
                                    AR = _RemoteConnector.RenameRepositoryFile(FileToProcess, SAI);
                                    if (AR == ActionResult.Retry || AR == ActionResult.RemoteServerUnreachable)
                                    {
                                        LogActionThread(String.Format("[{0}][{1}] Postponed", SAI.ActionItemId, SAI.Action));
                                        FileToProcess.ActionPostpone();
                                        return;
                                    }
                                    else if (AR == ActionResult.Cancel)
                                    {
                                        LogActionThread(String.Format("[{0}][{1}] Cancelled", SAI.ActionItemId, SAI.Action));
                                    }
                                }

                                VirtualElement_Serialize(FileToProcess);
                                LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));

                                #endregion

                                break;

                            case SyncActionEnum.FileRemoteMove:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                if (!FileToProcess.RemoteID.Equals(String.Empty)) //File may not be uploaded, we only perform remote rename if the file is uploaded
                                {
                                    AR = _RemoteConnector.MoveRepositoryFile(FileToProcess, SAI);
                                    if (AR == ActionResult.Retry || AR == ActionResult.RemoteServerUnreachable)
                                    {
                                        LogActionThread(String.Format("[{0}][{1}] Postponed", SAI.ActionItemId, SAI.Action));
                                        FileToProcess.ActionPostpone();
                                        return;
                                    }
                                    else if (AR == ActionResult.Cancel)
                                    {
                                        LogActionThread(String.Format("[{0}][{1}] Cancelled", SAI.ActionItemId, SAI.Action));
                                    }
                                }

                                VirtualElement_Serialize(FileToProcess);
                                LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));

                                #endregion

                                break;

                            case SyncActionEnum.FileRemoteMoveAndRename:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                if (!FileToProcess.RemoteID.Equals(String.Empty)) //File may not be uploaded, we only perform remote rename if the file is uploaded
                                {
                                    AR = _RemoteConnector.MoveRenameRepositoryFile(FileToProcess, SAI);
                                    if (AR == ActionResult.Retry || AR == ActionResult.RemoteServerUnreachable)
                                    {
                                        LogActionThread(String.Format("[{0}][{1}] Postponed", SAI.ActionItemId, SAI.Action));
                                        FileToProcess.ActionPostpone();
                                        return;
                                    }
                                    else if (AR == ActionResult.Cancel)
                                    {
                                        LogActionThread(String.Format("[{0}][{1}] Cancelled", SAI.ActionItemId, SAI.Action));
                                    }
                                }

                                VirtualElement_Serialize(FileToProcess);
                                LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));

                                #endregion

                                break;

                            case SyncActionEnum.FileLocalRename:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                if (FileToProcess == null || !File.Exists(GetFullElementPath(FileToProcess.PathRelative)))
                                {
                                    LogActionThread(String.Format("[{0}][{1}] ERROR - Cannot find file {2}", SAI.ActionItemId, SAI.Action, FileToProcess.ElementId));
                                    FileToProcess.ActionCloseCurrent();
                                    return;
                                }

                                AR = SecureRenameElement(FileToProcess, GetFullElementPath(SAI.RemoteElementParameter.PathRelative));
                                if (AR == ActionResult.Cancel) LogActionThread(String.Format("[{0}][{1}] Cancelled", SAI.ActionItemId, SAI.Action));
                                else LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));

                                FileToProcess.TargetNameAfterSync = FileToProcess.CurrentName;

                                #endregion

                                break;

                            case SyncActionEnum.FileLocalMove:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                if (FileToProcess == null || !File.Exists(GetFullElementPath(FileToProcess.PathRelative)))
                                {
                                    LogActionThread(String.Format("[{0}][{1}] ERROR - Cannot find file with id {2} and path {3}", SAI.ActionItemId, SAI.Action, FileToProcess.ElementId, GetFullElementPath(FileToProcess.PathRelative)));
                                    FileToProcess.ActionCloseCurrent();
                                    return;
                                }
                                //Note : If file has been moved locally in the mean time, the new move will be applied
                                //If the file has been deleted, the action is cancelled so postpone will finish it

                                //Is the local file available for performing the operation ?
                                if (Tools.IsFileAvailable(GetFullElementPath(FileToProcess.PathRelative), ref FS))
                                {
                                    FS.Close();
                                    SecureMoveElement(FileToProcess, GetFullElementPath(SAI.RemoteElementParameter.PathRelative));
                                    LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));
                                }
                                else
                                {
                                    LogActionThread(String.Format("[{0}][{1}] Postponed", SAI.ActionItemId, SAI.Action));
                                    FileToProcess.ActionPostpone();
                                    return;
                                }

                                #endregion

                                break;

                            case SyncActionEnum.FileLocalDelete:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                if (FileToProcess == null || !File.Exists(GetFullElementPath(FileToProcess.PathRelative)))
                                {
                                    LogActionThread(String.Format("[{0}][{1}] File doest not exist - It must have been deleted locally before the RemoteDelete could be processed", SAI.ActionItemId, SAI.Action));
                                    FileToProcess.SetDeleted(Task.CurrentId);
                                    VirtualFolder_DeleteFile(FileToProcess, Task.CurrentId);
                                    return;
                                }
                                else
                                {
                                    if (Tools.IsFileAvailable(GetFullElementPath(FileToProcess.PathRelative), ref FS))
                                    {
                                        FS.Close();

                                        //[Conflict]
                                        //TODO-NTH : If the localfile does not have the same hash than the last version of the remote file => do not delete => conflict (only available if the remote file is deactivated and not deleted)

                                        SAII = new SyncEventIgnoreItem(SyncEventEnum.LocalDelete, GetFullElementPath(FileToProcess.PathRelative));
                                        SyncActionItemId = SAII.SyncEventId;
                                        _SyncFileSystemWatcher.IgnoreEventAdd(SAII);

                                        #if __MonoCS__
                                            File.Delete(GetFullElementPath(FileToProcess.PathRelative));
                                        #else
                                            //File.Delete(GetFullElementPath(FileToProcess.PathRelative));
                                            FileSystem.DeleteFile(GetFullElementPath(FileToProcess.PathRelative), UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                                        #endif

                                        FileToProcess.SetDeleted(Task.CurrentId);
                                        VirtualFolder_DeleteFile(FileToProcess, Task.CurrentId);

                                        GC.Collect();
                                        GC.WaitForPendingFinalizers();
                                        LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));
                                    }
                                    else
                                    {
                                        LogActionThread(String.Format("[{0}][{1}] Postponed", SAI.ActionItemId, SAI.Action));
                                        FileToProcess.ActionPostpone();
                                        return;
                                    }
                                }

                                #endregion

                                break;

                            case SyncActionEnum.FileDownloadNew:
                            case SyncActionEnum.FileDownloadExisting:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                AR = _RemoteConnector.DownloadVirtualFile(FileToProcess, SAI);
                                if (AR == ActionResult.Retry)
                                {
                                    LogActionThread(String.Format("[{0}][{1}] Postponed", SAI.ActionItemId, SAI.Action));
                                    FileToProcess.ActionPostpone();
                                    return;
                                }
                                else if (AR == ActionResult.Cancel)
                                {
                                    LogActionThread(String.Format("[{0}][{1}] Cancelled", SAI.ActionItemId, SAI.Action));
                                    NotifyUploadDownloadCompletion = true;
                                    if (SAI.Action == SyncActionEnum.FileDownloadNew)
                                    {
                                        VirtualFolder_DeleteFile(FileToProcess, Task.CurrentId);
                                        return;
                                    }
                                }
                                else
                                {
                                    VirtualElement_Serialize(FileToProcess);
                                    LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));
                                    NotifyUploadDownloadCompletion = true;
                                }

                                #endregion

                                break;

                            case SyncActionEnum.FileUploadConflict:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                //Action : Upload localfile as a new file (declare the file as new)
                                Rnd = new Random();
                                string NewConflictFilePathLocal = Path.GetDirectoryName(GetFullElementPath(FileToProcess.PathRelative)) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(GetFullElementPath(FileToProcess.PathRelative)) + "(conflict " + this._UserName + "_" + DateTime.Now.ToString("ddMMyyyyHHmmss") + "_" + Rnd.Next(1000).ToString() + ")" + Path.GetExtension(GetFullElementPath(FileToProcess.PathRelative));
                                SecureCopyPhysicalFile(FileToProcess, NewConflictFilePathLocal);
                                EventEnqueue(new SyncEventItem(SyncEventEnum.LocalCreate, NewConflictFilePathLocal, false, false));

                                //Action : Download Remote File as the current file
                                EventEnqueue(new SyncEventItem(SyncEventEnum.RemoteUpdate, FileToProcess.ElementId, ObjectCopier.Clone(SAI.RemoteElementParameter), false, false));

                                if (SyncEngineUploadDownloadCountChanged != null) SyncEngineUploadDownloadCountChanged(this, null);
                                
                                LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));

                                #endregion

                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    FileToProcess.ActionPostpone();
                    LogActionThread(String.Format("Orchestrator_ProcessAction - Something went wrong [{0}][{1}] {2} {3}", SAI.ActionItemId, SAI.Action, ex.Message, ex.StackTrace));
                }

                if (FileToProcess != null) FileToProcess.ActionCloseCurrent();
                if (this.SyncStop != null) SyncStop(this, null);
                if (NotifyUploadDownloadCompletion && SyncEngineUploadDownloadCountChanged != null) SyncEngineUploadDownloadCountChanged(this, null);

                #endregion
            }
            else
            {
                #region Folder actions

                VirtualFolder FolderToProcess = null;
                FolderToProcess = (VirtualFolder)context;

                try
                {
                    SAI = FolderToProcess.ActionGetNext();
                    if (SAI != null)
                    {
                        switch (SAI.Action)
                        {
                            case SyncActionEnum.FolderRemoteCreate:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                AR = _RemoteConnector.CreateFolder(FolderToProcess, SAI);
                                if (AR == ActionResult.Retry)
                                {
                                    LogActionThread(String.Format("[{0}][{1}] Postponed", SAI.ActionItemId, SAI.Action));
                                    FolderToProcess.ActionPostpone();
                                    return;
                                }
                                else if (AR == ActionResult.Cancel)
                                {
                                    LogActionThread(String.Format("[{0}][{1}] Cancelled", SAI.ActionItemId, SAI.Action));
                                }
                                else
                                {
                                    VirtualElement_Serialize(FolderToProcess);
                                    LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));
                                }

                                #endregion

                                break;

                            case SyncActionEnum.FolderRemoteDelete:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                if (!FolderToProcess.RemoteID.Equals(String.Empty)) //Folder may not be existing yet, we only perform remote deletion if the folder is created
                                {
                                    AR = _RemoteConnector.DeleteRepositoryFolder(FolderToProcess, SAI);
                                    if (AR == ActionResult.Retry)
                                    {
                                        LogActionThread(String.Format("[{0}][{1}] Postponed", SAI.ActionItemId, SAI.Action));
                                        FolderToProcess.ActionPostpone();
                                        return;
                                    }
                                    else if (AR == ActionResult.Cancel)
                                    {
                                        LogActionThread(String.Format("[{0}][{1}] Cancelled", SAI.ActionItemId, SAI.Action));
                                    }
                                }

                                VirtualFolder_DeleteFolder(FolderToProcess, Task.CurrentId);
                                LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));

                                #endregion

                                break;

                            case SyncActionEnum.FolderRemoteRename:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                if (!FolderToProcess.RemoteID.Equals(String.Empty)) //Folder may not be existing yet, we only perform remote deletion if the folder is created
                                {
                                    AR = _RemoteConnector.RenameRepositoryFolder(FolderToProcess, SAI.StringParameter);
                                    if (AR == ActionResult.Retry)
                                    {
                                        LogActionThread(String.Format("[{0}][{1}] Postponed", SAI.ActionItemId, SAI.Action));
                                        FolderToProcess.ActionPostpone();
                                        return;
                                    }
                                    else if (AR == ActionResult.Cancel)
                                    {
                                        LogActionThread(String.Format("[{0}][{1}] Cancelled", SAI.ActionItemId, SAI.Action));
                                    }
                                }

                                VirtualFolder_SerializeTree(FolderToProcess);
                                LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));

                                #endregion

                                break;

                            case SyncActionEnum.FolderLocalCreate:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                //FSWatcher Event Ignore Mechanism
                                AR = SecureFolderCreate(FolderToProcess, GetFullElementPath(SAI.RemoteElementParameter.PathRelative));

                                if (AR == ActionResult.Cancel)
                                {
                                    LogActionThread(String.Format("[{0}][{1}] Cancelled", SAI.ActionItemId, SAI.Action));
                                }
                                else
                                {
                                    LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));
                                }

                                #endregion

                                break;

                            case SyncActionEnum.FolderLocalParse:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                AR = GenerateEventsForFolder(GetFullElementPath(FolderToProcess.PathRelative));

                                if (AR == ActionResult.Cancel)
                                {
                                    LogActionThread(String.Format("[{0}][{1}] Cancelled", SAI.ActionItemId, SAI.Action));
                                }
                                else
                                {
                                    LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));
                                }


                                #endregion

                                break;

                            case SyncActionEnum.FolderLocalDelete:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                if (!Directory.Exists(GetFullElementPath(FolderToProcess.PathRelative)))
                                {
                                    LogActionThread(String.Format("[{0}][{1}] Folder doest not exist - It must have been deleted locally before the FolderLocalDelete could be processed", SAI.ActionItemId, SAI.Action));
                                    VirtualFolder_DeleteFolder(FolderToProcess, Task.CurrentId);
                                }
                                else
                                {
                                    //Set Ignore for a delete event for the current folder and all its sub-folders
                                    this.GenerateRecursiveDeleteIgnoreEvents(FolderToProcess);

#if __MonoCS__
                                        Directory.Delete(GetFullElementPath(FolderToProcess.PathRelative), true);
#else
                                    //Directory.Delete(GetFullElementPath(FolderToProcess.PathRelative), true);
                                    FileSystem.DeleteDirectory(GetFullElementPath(FolderToProcess.PathRelative), UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
#endif

                                    VirtualFolder_DeleteFolder(FolderToProcess, Task.CurrentId);
                                }

                                LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));

                                #endregion

                                break;

                            case SyncActionEnum.FolderLocalRename:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                AR = SecureRenameElement(FolderToProcess, GetFullElementPath(SAI.RemoteElementParameter.PathRelative));
                                if (AR == ActionResult.Cancel) LogActionThread(String.Format("[{0}][{1}] Cancelled", SAI.ActionItemId, SAI.Action));
                                else LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));

                                FolderToProcess.TargetNameAfterSync = FolderToProcess.CurrentName;

                                #endregion

                                break;

                            case SyncActionEnum.FolderLocalMove:

                                #region

                                LogActionThread(String.Format("[{0}][{1}] Started", SAI.ActionItemId, SAI.Action));

                                AR = SecureMoveElement(FolderToProcess, GetFullElementPath(SAI.RemoteElementParameter.PathRelative));
                                if (AR == ActionResult.Cancel)
                                {
                                    LogActionThread(String.Format("[{0}][{1}] Cancelled", SAI.ActionItemId, SAI.Action));
                                }
                                else
                                {
                                    LogActionThread(String.Format("[{0}][{1}] Completed", SAI.ActionItemId, SAI.Action));
                                }

                                #endregion

                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    FolderToProcess.ActionPostpone();
                    LogActionThread(String.Format("Orchestrator_ProcessAction - Something went wrong [{0}][{1}] {2} {3}", SAI.ActionItemId, SAI.Action, ex.Message, ex.StackTrace));
                }

                if (FolderToProcess != null) FolderToProcess.ActionCloseCurrent();
                if (this.SyncStop != null) SyncStop(this, null);

                #endregion
            }
        }

        /// <summary>
        /// Ajoute un IgnoreEvent pour le répertoire passé en paramétre et tous ses sous-répertoires (ceux marqués IsDeleted)
        /// </summary>
        /// <param name="FolderToProcess"></param>
        private void GenerateRecursiveDeleteIgnoreEvents(VirtualFolder FolderToProcess)
        {
            SyncEventIgnoreItem SAII = new SyncEventIgnoreItem(SyncEventEnum.LocalDelete, GetFullElementPath(FolderToProcess.PathRelative));
            this._SyncFileSystemWatcher.IgnoreEventAdd(SAII);

            foreach (VirtualFolder VF in FolderToProcess.GetSubElementsFolders())
            {
                if (VF.IsDeleted)
                {
                    GenerateRecursiveDeleteIgnoreEvents(VF);
                }
            }
        }

        /// <summary>
        /// Compare Local Virtual Files to Remote Files
        /// Only report remote changes. Local changes are reported by the FS watcher
        /// As the VirtualRootFolder is part of the elements in the list, we take care to never perform action on it
        /// </summary>
        private void CompareToRemote()
        {
            if (this.SyncStart != null) SyncStart(this, null);

            LogComparer("Start");

            this._VirtualRootFolder.ClearIdentification();
            this.RemoteElementList.Clear();

            //We init a timer so that if the remote call is stucked, we can abort the thread running this task
            RemoteServerCallTimer.Enabled = true;
            RemoteServerCallThread = Thread.CurrentThread;

            try
            {
                var AR = _RemoteConnector.GetFileList(this._InstanceID, this._IsFirstComparerCall, ref RemoteElementList);
                if (AR != ActionResult.Success) goto CompareToRemoteCompletion;
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                goto CompareToRemoteCompletion;
            }

            //The call succeeded, we stop the timer
            RemoteServerCallTimer.Enabled = false;
            RemoteServerCallThread = null;

            this._IsFirstComparerCall = false;

            if (RemoteElementList == null) goto CompareToRemoteCompletion;

            LogComparer("Matching elements based on their Noderef ...");

            try
            {
                List<MatchingElement> MatchingElementList = new List<MatchingElement>();

                LogComparer("Compare element list start");

                #region Compare element list (both files and folders)

                //Try to match local files with remote files (based on Ids)
                for (int i = 0; i < RemoteElementList.Count; i++)
                {
                    VirtualElement TempVirtualElement = _VirtualRootFolder.FlatElementGetBasedOnRemoteId(RemoteElementList[i].ElementId, false); //On ne prend en compte que les éléments considérés comme encore présent sur le disque
                    if (TempVirtualElement != null)
                    {
                        TempVirtualElement.bIdentified = true;
                        RemoteElementList[i].IsIdentified = true;
                        MatchingElementList.Add(new MatchingElement(TempVirtualElement, RemoteElementList[i]));
                    }
                }

                LogComparer("Compare element list end");

                #endregion

                #region Compare folders

                #region Process non matching local folders

                LogComparer("Processing non matching local folders => RemoteDelete event");

                foreach (VirtualElement VE in this._VirtualRootFolder.FlatElementsGetAllFolders())
                {
                    if (!VE.bIdentified && VE.RemoteID != String.Empty) //Le répertoire local n'est pas identifié alors qu'il a normalement un répertoire equivalent sur le serveur
                    {
                        //The localfolder does not exist anymore
                        VE.bIdentified = true;
                        var SEI = new SyncEventItem(SyncEventEnum.RemoteDelete, VE.ElementId, true, false);
                        EventEnqueue(SEI);
                        LogComparer(String.Format("Delete local folder due to remote folder deletion {0} -> Event : {1}",
                            VE.PathRelative,
                            SEI.SyncEventId));
                    }
                }

                #endregion

                LogComparer("Processing non matching remote folders => RemoteCreate event");

                //Process non matching remote folders (folders created remotely and missing locally)
                foreach (RepositoryElement RE in RemoteElementList.Where(x => x.ElementType == RepositoryElementType.Folder && !x.IsIdentified))
                {
                    var SEI = new SyncEventItem(SyncEventEnum.RemoteCreate, ObjectCopier.Clone(RE), true, false);
                    EventEnqueue(SEI);
                    LogComparer(String.Format("New remote folder detected {0} -> Event : {1}",
                        RE.PathRelative,
                        SEI.SyncEventId));
                }

                LogComparer("Processing matching folders");
                
                //Process matching folders
                foreach (MatchingElement ME in MatchingElementList.Where(x => x._LocalElement.ElementType == VirtualElementType.Folder))
                {
                    if (!ME._LocalElement.HasActions) //If the local element has actions, we do not process (those actions may contain the ones that will sync both local and remote elements)
                    {
                        //Folder may have been renamed
                        if (ME._LocalElement.CurrentName != ME._RemoteElement.ElementName)
                        {
                            ME._LocalElement.TargetNameAfterSync = ME._RemoteElement.ElementName; //On affecte au répertoire local son futur nom dans la variable TargetNameAfterSync, cela va nous servir dans le test suivant à déterminer si le répertoire a été déplacé ou non en plus d'avoir été renommé
                            //Element have been renamed
                            var SEI = new SyncEventItem(SyncEventEnum.RemoteRename, ME._LocalElement.ElementId, ObjectCopier.Clone(ME._RemoteElement), true, false);
                            EventEnqueue(SEI);
                            LogComparer(String.Format("Folder remote rename detected from {0} to {1} -> Event : {2}",
                                ME._LocalElement.PathRelative,
                                ME._RemoteElement.PathRelative,
                                SEI.SyncEventId));
                        }
                        //On utilise TargetPathAfterSync qui inclue les éventuels renommage de répertoires parent celà permet de bypasser de faux événements "RemoteMove"
                        //En effet, si on ne fait pas ca, tous les sous répertoires d'un répertoire renommé vont considérer qu'ils ont été déplacés, ce qui n'est pas le cas
                        if (Tools.GetParentPath(ME._LocalElement.TargetPathRelativeAfterSync) != Tools.GetParentPath(ME._RemoteElement.PathRelative))
                        {
                            //Folder have been moved
                            var SEI = new SyncEventItem(SyncEventEnum.RemoteMove, ME._LocalElement.ElementId, ObjectCopier.Clone(ME._RemoteElement), true, false);
                            EventEnqueue(SEI);
                            LogComparer(String.Format("Folder remote move detected from {0} to {1} -> Event : {2}",
                                Tools.GetParentPath(ME._LocalElement.TargetPathRelativeAfterSync),
                                Tools.GetParentPath(ME._RemoteElement.PathRelative),
                                SEI.SyncEventId));
                        }
                    }
                }

                #endregion

                #region Files

                #region Process non matching local files

                LogComparer("Processing non matching local files => RemoteDelete event");

                foreach (VirtualElement VE in this._VirtualRootFolder.FlatElementsGetAllFiles())
                {
                    if (!VE.bIdentified) //Processing non matching local element
                    {
                        if (VE.RemoteID.Equals(String.Empty)) //This is a new file as it has no RemoteId defined
                        {
                            //The current element has no remote peer (seems to be)
                        }
                        else
                        {
                            if (!VE.HasActions) //If the local file has pending actions, wait for them to complete
                            {
                                #region The remote file does not exist anymore. We need to delete local file => We generate a RemoteDelete Event

                                //TODO-NTH : Instead of deleting the remote file, set it offline. So when we arrive here, we can check if the deleted remote file correspond to the local file
                                //If true, delete local file
                                //If false, the local file is newer that the file that was deleted => conflict !!
                                //Is it safe to delete a file based on the fact that is does not exist anymore remotely ... ???
                                //Shouldn't we wait for an effective remote delete action flag ?
                                VE.bIdentified = true;
                                var SEI = new SyncEventItem(SyncEventEnum.RemoteDelete, VE.ElementId, false, false);
                                EventEnqueue(SEI);
                                LogComparer(String.Format("Delete local file due to remote file deletion {0} -> Event : {1}", 
                                    VE.PathRelative,
                                    SEI.SyncEventId));

                                #endregion
                            }
                        }

                    }
                }

                #endregion

                #region Process non matching remote files

                LogComparer("Processing non matching remote files => RemoteCreate event");

                foreach (RepositoryElement RE in RemoteElementList.Where(x => x.ElementType == RepositoryElementType.File && !x.IsIdentified))
                {
                    #region Download remote file => Generate a DownloadNew Action

                    var SEI = new SyncEventItem(SyncEventEnum.RemoteCreate, ObjectCopier.Clone(RE), false, false);
                    EventEnqueue(SEI);
                    LogComparer(String.Format("New remote file detected {0} -> Event : {1}", 
                        RE.PathRelative,
                        SEI.SyncEventId));

                    #endregion
                }

                #endregion

                #region Process matching files

                LogComparer("Processing matching files");

                foreach (MatchingElement ME in MatchingElementList.Where(x => x._LocalElement.ElementType == VirtualElementType.File))
                {
                    if (!ME._LocalElement.HasActions) //If the local file has pending actions, wait for them to complete
                    {
                        if (!ME._LocalElement.CurrentName.Equals(ME._RemoteElement.ElementName, StringComparison.OrdinalIgnoreCase))
                        {
                            ME._LocalElement.TargetNameAfterSync = ME._RemoteElement.ElementName;
                            //Element have been renamed
                            var SEI = new SyncEventItem(SyncEventEnum.RemoteRename, ME._LocalElement.ElementId, ObjectCopier.Clone(ME._RemoteElement), false, false);
                            EventEnqueue(SEI);
                            LogComparer(String.Format("File has been renamed on remote side from {0} to {1} -> Event : {2}", 
                                ME._LocalElement,
                                ME._LocalElement.TargetNameAfterSync,
                                SEI.SyncEventId));
                        }

                        if (Tools.GetParentPath(ME._LocalElement.TargetPathRelativeAfterSync) != Tools.GetParentPath(ME._RemoteElement.PathRelative))
                        {
                            var SEI = new SyncEventItem(SyncEventEnum.RemoteMove, ME._LocalElement.ElementId, ObjectCopier.Clone(ME._RemoteElement), false, false);
                            EventEnqueue(SEI);
                            LogComparer(String.Format("File has been moved on remote side {0} -> Event : {1}", 
                                GetFullElementPath(ME._LocalElement.PathRelative),
                                SEI.SyncEventId));
                        }

                        switch (_RemoteConnector.CompareFile((VirtualFile)ME._LocalElement, ME._RemoteElement))
                        {
                            case LocalAndRemoteComparisonResult.None:
                                break;
                            case LocalAndRemoteComparisonResult.RemoteUpdate:
                                var SEI = new SyncEventItem(SyncEventEnum.RemoteUpdate, ME._LocalElement.ElementId, ObjectCopier.Clone(ME._RemoteElement), false, false);
                                EventEnqueue(SEI);
                                LogComparer(String.Format("RemoteUpdate for element {0} LocalTAG {1} - RemoteTAG {2} -> Event : {3}",
                                    ME._RemoteElement.PathRelative,
                                    ((VirtualFile)ME._LocalElement).CustomProperties,
                                    ME._RemoteElement.CustomProperties,
                                    SEI.SyncEventId));
                                break;
                        }
                    }
                }

                #endregion

                #endregion
            }
            catch (Exception ex)
            {
                LogComparer("In CompareToRemote - " + ex.Message);
            }

        CompareToRemoteCompletion:
            if (this._IsIniting)
            {
                this._IsIniting = false;
                if (SyncEngineUploadDownloadCountChanged != null) SyncEngineUploadDownloadCountChanged(this, null); //We do this so that the "Initing dialog box does not remain on if the connection to server does not succeed
            }
            RemoteServerCallTimer.Enabled = false;
            RemoteServerCallThread = null;
            LastRemoteSync = DateTime.Now;
            RemoteSyncDispatched = false;
            if (this.SyncStop != null) SyncStop(this, null);
            LogComparer("Complete");
        }

        /// <summary>
        /// Refresh the VirtualRootFolder based on a parsing of the local directory
        /// </summary>
        private void FullRefresh()
        {
            if (this.SyncStart != null) SyncStart(this, null);

            Log("Full Refresh started");

            this._VirtualRootFolder.ClearIdentification();
            PhysicalRootFolder SyncRootFolder = new PhysicalRootFolder(_LocalPath);
            this._VirtualRootFolder.Compare(SyncRootFolder);
            this._VirtualRootFolder.ClearIdentification();

            this.LastFullRefresh = DateTime.Now;
            FullScanDispatched = false;

            Log("Full Refresh End");

            if (this.SyncStop != null) SyncStop(this, null);
        }

        /// <summary>
        /// When parsing a folder, generate events for each sub folder and file (recursive)
        /// </summary>
        /// <param name="FolderPath"></param>
        private ActionResult GenerateEventsForFolder(String FolderPath)
        {
            DirectoryInfo DI = new DirectoryInfo(FolderPath);

            try
            {
                DirectoryInfo[] SubDirs = DI.GetDirectories();
                for (int i = 0; i < SubDirs.Count(); i++)
                {
                    EventEnqueue(new SyncEventItem(SyncEventEnum.LocalCreate, SubDirs[i].FullName, true, false));
                    GenerateEventsForFolder(SubDirs[i].FullName);
                }
            }
            catch
            {
                return ActionResult.Cancel;
            }

            //Generate new file event
            try
            {
                FileInfo[] Files = DI.GetFiles();
                for (int i = 0; i < Files.Count(); i++)
                {
                    //TODO : Check that the file is not already registered
                    EventEnqueue(new SyncEventItem(SyncEventEnum.LocalCreate, Files[i].FullName, false, false));
                }
            }
            catch
            {
                return ActionResult.Cancel;
            }

            return ActionResult.Success;
        }

        /// <summary>
        /// When a folder doesn't exist anymore but there is a pending action needing it => this recreates the folder (virtually and physically)
        /// </summary>
        /// <param name="NewPathRelative"></param>
        private void RebuildMissingFolderForAction(string NewPathRelative)
        {
            //The initial target folder of the move operation does not exist anymore ...
            //We recreate it from scratch
            if (String.IsNullOrEmpty(NewPathRelative)) return; //The parent is the root folder
            String[] PathHierarchy = NewPathRelative.Split(new char[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            VirtualElement TempVirtualElement = this._VirtualRootFolder;
            String TempCurrentPathRelative = Path.DirectorySeparatorChar.ToString();
            String TempCurrentPathFull = this._LocalPath;

            foreach (string F in PathHierarchy)
            {
                TempCurrentPathFull = Path.Combine(TempCurrentPathFull, F);
                TempCurrentPathRelative = Path.Combine(TempCurrentPathRelative, F);

                if (!Directory.Exists(TempCurrentPathFull))
                {
                    VirtualFolder TempFolder = new VirtualFolder(TempCurrentPathRelative);
                    if (SecureFolderCreate(TempFolder, TempCurrentPathFull) == ActionResult.Success)
                    {
                        TempVirtualElement.SubElementAdd(TempFolder);
                        this._VirtualRootFolder.FlatElementAdd(TempFolder);
                        TempVirtualElement = TempFolder;
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        void Orchestrator_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                logger.Error(e.Error, "Orchestrator has stopped");
                Orchestrator.RunWorkerAsync(); //Restart the worker
            }

            if (this.StopComplete != null) this.StopComplete(this, null);
        }

        #endregion

        #region Interface IPluginHost

        public ActionResult BuildFilePath(string pFileFullPath)
        {
            if (!new DirectoryInfo(Path.GetDirectoryName(pFileFullPath)).Exists)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(pFileFullPath));
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.Debug("In BuildFilePath - " + ex.Message);
                    return ActionResult.Cancel;
                }
                catch (System.IO.PathTooLongException ex)
                {
                    logger.Debug("In BuildFilePath - " + ex.Message);
                    return ActionResult.Cancel;
                }
                catch ( Exception ex)
                {
                    logger.Debug("In BuildFilePath - " + ex.Message);
                    return ActionResult.Cancel;
                }
            }

            return ActionResult.Success;
        }

        public ActionResult BuildFolderPath(string pFolderFullPath)
        {
            if (!new DirectoryInfo(pFolderFullPath).Exists)
            {
                try
                {
                    Directory.CreateDirectory(pFolderFullPath);
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.Debug("In BuildFilePath - " + ex.Message);
                    return ActionResult.Cancel;
                }
                catch (System.IO.PathTooLongException ex)
                {
                    logger.Debug("In BuildFilePath - " + ex.Message);
                    return ActionResult.Cancel;
                }
                catch (Exception ex)
                {
                    logger.Debug("In BuildFilePath - " + ex.Message);
                    return ActionResult.Cancel;
                }
            }

            return ActionResult.Success;
        }

        private ActionResult SecureFolderCreate(VirtualFolder FolderToProcess, string FolderFullPath)
        {
            SyncEventIgnoreItem SEII = new SyncEventIgnoreItem(SyncEventEnum.LocalCreate, FolderFullPath);
            _SyncFileSystemWatcher.IgnoreEventAdd(SEII);
            ActionResult AR = BuildFolderPath(FolderFullPath);
            if (AR != ActionResult.Success)
            {
                _SyncFileSystemWatcher.IgnoreEventRemove(SEII.SyncEventId);
                return AR;
            }
            VirtualElement_Serialize(FolderToProcess);

            return ActionResult.Success;
        }

        public ActionResult SecureMoveRenamePhysicalFile(VirtualFile pVirtualFile, string NewLocationPathFull)
        {
            //FSWatcher Event Ignore Mechanism
            SyncEventIgnoreItem SEII = new SyncEventIgnoreItem(SyncEventEnum.LocalMove | SyncEventEnum.LocalRename, NewLocationPathFull);
            _SyncFileSystemWatcher.IgnoreEventAdd(SEII);
            ActionResult AR = BuildFilePath(NewLocationPathFull);
            if (AR != ActionResult.Success)
            {
                _SyncFileSystemWatcher.IgnoreEventRemove(SEII.SyncEventId);
                return AR;
            }

            if (!GetFullElementPath(pVirtualFile.PathRelative).Equals(NewLocationPathFull, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Move(GetFullElementPath(pVirtualFile.PathRelative), NewLocationPathFull);
                    pVirtualFile.CurrentName = Path.GetFileName(NewLocationPathFull);
                }
                catch
                {
                    _SyncFileSystemWatcher.IgnoreEventRemove(SEII.SyncEventId);

                    if (File.Exists(NewLocationPathFull))
                    {

                        //Il existe déjà un fichier avec le nouveau nom que l'on souahite donner au fichier en cours
                        //Il a été créé entre deux processus de syncho
                        //Il y a donc un conflit de fichiers
                        //[Conflict]
                        Random Rnd = new Random();
                        String FileName = Path.GetFileNameWithoutExtension(NewLocationPathFull);
                        String FileExtension = Path.GetExtension(NewLocationPathFull);
                        String FileDirectory = Path.GetDirectoryName(NewLocationPathFull);
                        String NewFileName = FileName + "(conflict_" + this._UserName + "_" + DateTime.Now.ToString("ddMMyyyyHHmmss") + "_" + Rnd.Next(1000).ToString() + ")" + FileExtension;
                        String NewFilePath = FileDirectory + @"\\" + NewFileName;
                        File.Move(GetFullElementPath(pVirtualFile.PathRelative), NewFilePath);
                    }
                    else
                    {
                        logger.Debug("In SecureMoveRenamePhysicalFile : the file to move doesn't exist anymore");
                    }
                }
            }

            return ActionResult.Success;
        }

        public ActionResult SecureMovePhysicalFileFromTemp(VirtualFile pVirtualFile)
        {
            //Guid SyncActionItemId;

            //FSWatcher Event Ignore Mechanism
            SyncEventIgnoreItem SEII = new SyncEventIgnoreItem(SyncEventEnum.LocalCreate | SyncEventEnum.LocalUpdate, GetFullElementPath(pVirtualFile.PathRelative));
            _SyncFileSystemWatcher.IgnoreEventAdd(SEII);

            try
            {
                File.Copy(pVirtualFile.TemporaryDownloadFilePathFull, GetFullElementPath(pVirtualFile.PathRelative), true);
                File.SetAttributes(GetFullElementPath(pVirtualFile.PathRelative), System.IO.FileAttributes.Normal);
            }
            catch
            {
                _SyncFileSystemWatcher.IgnoreEventRemove(SEII.SyncEventId);
                return ActionResult.Cancel;
            }

            try
            {
                File.Delete(GetFullFilePathTemp(pVirtualFile.PathRelative));
            }
            catch { }

            return ActionResult.Success;
        }

        /// <summary>
        /// This function is used in conflict management. So there cannot be any conflict in the File.Move within
        /// </summary>
        /// <param name="pVirtualFile"></param>
        /// <param name="NewLocationPathFull"></param>
        /// <returns></returns>
        public ActionResult SecureMoveRenamePhysicalFileFromTemp(VirtualFile pVirtualFile, string NewLocationPathFull)
        {
            Guid SyncActionItemId;

            //FSWatcher Event Ignore Mechanism
            SyncEventIgnoreItem SAI = new SyncEventIgnoreItem(SyncEventEnum.LocalCreate, NewLocationPathFull);
            SyncActionItemId = SAI.SyncEventId;
            _SyncFileSystemWatcher.IgnoreEventAdd(SAI);

            //the local file will have a new name
            try
            {
                File.Move(GetFullFilePathTemp(pVirtualFile.PathRelative), NewLocationPathFull);
                File.SetAttributes(GetFullElementPath(pVirtualFile.PathRelative), System.IO.FileAttributes.Normal);
            }
            catch
            {
                return ActionResult.Cancel;
            }

            return ActionResult.Success;
        }

        /// <summary>
        /// Cette fonction est appelée au sein d'une procédure de gestion des conflits. Le file Copy ne devrait pas pouvoir lever une exception
        /// </summary>
        /// <param name="pVirtualFile"></param>
        /// <param name="NewLocationPathFull"></param>
        /// <returns></returns>
        public ActionResult SecureCopyPhysicalFile(VirtualFile pVirtualFile, string NewLocationPathFull)
        {

            //FSWatcher Event Ignore Mechanism
            SyncEventIgnoreItem SEII = new SyncEventIgnoreItem(SyncEventEnum.LocalCreate, NewLocationPathFull);
            _SyncFileSystemWatcher.IgnoreEventAdd(SEII);

            ActionResult AR = BuildFilePath(NewLocationPathFull);
            if (AR != ActionResult.Success)
            {
                _SyncFileSystemWatcher.IgnoreEventRemove(SEII.SyncEventId);
                return AR;
            }

            try
            {
                File.Copy(GetFullElementPath(pVirtualFile.PathRelative), NewLocationPathFull);
            }
            catch
            {
                _SyncFileSystemWatcher.IgnoreEventRemove(SEII.SyncEventId);
                return ActionResult.Cancel;
            }

            return ActionResult.Success;
        }
        
        public ActionResult SecureRenameElement(VirtualElement pVirtualElement, string NewElementPathFull)
        {
            Guid SyncActionItemId;
            Random Rnd;
            ActionResult AR;

            String CurrentFullElementPath = GetFullElementPath(pVirtualElement.PathRelative);

            SyncEventIgnoreItem SAI = new SyncEventIgnoreItem(SyncEventEnum.LocalRename, NewElementPathFull);
            SyncActionItemId = SAI.SyncEventId;
            _SyncFileSystemWatcher.IgnoreEventAdd(SAI);

            if (!CurrentFullElementPath.Equals(NewElementPathFull, StringComparison.OrdinalIgnoreCase))
            {
                if (pVirtualElement.ElementType == VirtualElementType.Folder)
                {
                    #region Folder

                    if (!Directory.Exists(CurrentFullElementPath))
                    {
                        logger.Error("In SecureRenameElement : Source folder " + CurrentFullElementPath + " does not exist. Cancelling");
                        return ActionResult.Cancel;
                    }

                    AR = BuildFolderPath(Tools.GetParentPath(NewElementPathFull));
                    if (AR != ActionResult.Success) return ActionResult.Cancel;

                    try
                    {
                        Directory.Move(CurrentFullElementPath, NewElementPathFull);
                        pVirtualElement.CurrentName = Tools.GetFolderNameFromPath(NewElementPathFull);
                        VirtualElement_Serialize(pVirtualElement);
                    }
                    catch (Exception ex)
                    {
                        _SyncFileSystemWatcher.IgnoreEventRemove(SAI.SyncEventId);

                        if (Directory.Exists(NewElementPathFull))
                        {
                            //Il existe déjà un répertoire avec le nouveau nom que l'on souhaite donner au répertoire en cours
                            //Il a été créé entre deux processus de syncho
                            //Il y a donc un conflit de répertoires
                            //[Conflict]
                            Rnd = new Random();
                            String ConflictSuffix = "(conflict " + this._UserName + "_" + DateTime.Now.ToString("ddMMyyyyHHmmss") + "_" + Rnd.Next(1000).ToString() + ")";
                            Directory.Move(CurrentFullElementPath, NewElementPathFull + ConflictSuffix);
                        }
                        else
                        {
                            logger.Error("In SecureRenameElement : the directory to rename doesn't exist anymore");
                            logger.Error("In SecureRenameElement : " + ex.Message);
                        }
                    }

                    #endregion
                }
                else
                {
                    #region File

                    if (!File.Exists(CurrentFullElementPath))
                    {
                        logger.Error("In SecureRenameElement : Source file " + CurrentFullElementPath + " does not exist. Cancelling");
                        return ActionResult.Cancel;
                    }

                    AR = BuildFilePath(NewElementPathFull);
                    if (AR != ActionResult.Success) return ActionResult.Cancel;

                    try
                    {
                        File.Move(CurrentFullElementPath, NewElementPathFull);
                        pVirtualElement.CurrentName = Path.GetFileName(NewElementPathFull);
                        VirtualElement_Serialize(pVirtualElement);
                    }
                    catch (Exception ex)
                    {
                        _SyncFileSystemWatcher.IgnoreEventRemove(SAI.SyncEventId);

                        if (File.Exists(NewElementPathFull))
                        {
                            //Il existe déjà un fichier avec le nouveau nom que l'on souahite donner au fichier en cours
                            //Il a été créé entre deux processus de syncho
                            //Il y a donc un conflit de fichiers
                            //[Conflict]
                            Rnd = new Random();
                            String FileName = Path.GetFileNameWithoutExtension(NewElementPathFull);
                            String FileExtension = Path.GetExtension(NewElementPathFull);
                            String FileDirectory = Path.GetDirectoryName(NewElementPathFull);
                            String NewFileName = FileName + "(conflict_" + this._UserName + "_" + DateTime.Now.ToString("ddMMyyyyHHmmss") + "_" + Rnd.Next(1000).ToString() + ")" + FileExtension;
                            String NewFilePath = FileDirectory + @"\\" + NewFileName;
                            File.Move(CurrentFullElementPath, NewFilePath);
                        }
                        else
                        {
                            logger.Error("In SecureRenameElement : " + ex.Message);
                        }
                    }

                    #endregion
                }
            }
            return ActionResult.Success;
        }

        public ActionResult SecureMoveElement(VirtualElement pVirtualElement, string NewElementPathFull)
        {
            SyncEventIgnoreItem SAI;
            Random Rnd;
            ActionResult AR;

            String CurrentFullElementPath = GetFullElementPath(pVirtualElement.PathRelative);

            if (pVirtualElement.ElementType == VirtualElementType.File)
            {
                #region File

                if (!File.Exists(CurrentFullElementPath))
                {
                    logger.Error("In SecureMoveElement : Source file " + CurrentFullElementPath + " does not exist. Cancelling");
                    return ActionResult.Cancel;
                }

                SAI = new SyncEventIgnoreItem(SyncEventEnum.LocalMove, NewElementPathFull);
                _SyncFileSystemWatcher.IgnoreEventAdd(SAI);

                if (CurrentFullElementPath != NewElementPathFull)
                {
                    AR = BuildFilePath(NewElementPathFull);
                    if (AR != ActionResult.Success) return ActionResult.Cancel;

                    try
                    {
                        File.Move(CurrentFullElementPath, NewElementPathFull);
                        AR = VirtualElement_Move(pVirtualElement, GetRelativeElementPath(NewElementPathFull));
                        if (AR != ActionResult.Success)
                        {
                            return AR;
                        }
                        VirtualElement_Serialize(pVirtualElement);
                    }
                    catch (Exception ex)
                    {
                        _SyncFileSystemWatcher.IgnoreEventRemove(SAI.SyncEventId);

                        if (File.Exists(NewElementPathFull))
                        {
                            //The target file may already exist => Conflict

                            //Il existe déjà un fichier avec le nouveau nom que l'on souahite donner au fichier en cours
                            //Il a été créé entre deux processus de syncho
                            //Il y a donc un conflit de fichiers
                            //[Conflict]
                            Rnd = new Random();
                            String FileName = Path.GetFileNameWithoutExtension(NewElementPathFull);
                            String FileExtension = Path.GetExtension(NewElementPathFull);
                            String FileDirectory = Path.GetDirectoryName(NewElementPathFull);
                            String NewFileName = FileName + "(conflict " + this._UserName + "_" + DateTime.Now.ToString("ddMMyyyyHHmmss") + "_" + Rnd.Next(1000).ToString() + ")" + FileExtension;
                            String NewFilePath = FileDirectory + @"\\" + NewFileName;
                            File.Move(GetFullElementPath(pVirtualElement.PathRelative), NewFilePath);
                        }
                        else
                        {
                            logger.Debug("In SecureMoveElement : the file to rename doesn't exist anymore");
                            logger.Debug("In SecureMoveElement : " + ex.Message);
                        }
                    }
                }

                #endregion
            }
            else
            {
                #region Folder

                if (!Directory.Exists(CurrentFullElementPath))
                {
                    logger.Error("In SecureMoveElement : Source folder " + CurrentFullElementPath + " does not exist. Cancelling");
                    return ActionResult.Cancel;
                }

                SAI = new SyncEventIgnoreItem(SyncEventEnum.LocalCreate, NewElementPathFull);
                _SyncFileSystemWatcher.IgnoreEventAdd(SAI);
                SAI = new SyncEventIgnoreItem(SyncEventEnum.LocalDelete, CurrentFullElementPath);
                _SyncFileSystemWatcher.IgnoreEventAdd(SAI);

                if (CurrentFullElementPath != NewElementPathFull)
                {
                    AR = BuildFolderPath(Tools.GetParentPath(NewElementPathFull));
                    if (AR != ActionResult.Success) return ActionResult.Cancel;

                    try
                    {
                        Directory.Move(CurrentFullElementPath, NewElementPathFull); 
                        VirtualElement_Move(pVirtualElement, GetRelativeElementPath(NewElementPathFull));
                        VirtualElement_Serialize(pVirtualElement);
                    }
                    catch
                    {
                        _SyncFileSystemWatcher.IgnoreEventRemove(SAI.SyncEventId);

                        if (Directory.Exists(NewElementPathFull))
                        {
                            //The target folder may already exist => Conflict
                            //Il existe déjà un fichier avec le nouveau nom que l'on souahite donner au fichier en cours
                            //Il a été créé entre deux processus de syncho
                            //Il y a donc un conflit de fichiers
                            //[Conflict]
                            Rnd = new Random();
                            String ConflictSuffix = "(conflict " + this._UserName + "_" + DateTime.Now.ToString("ddMMyyyyHHmmss") + "_" + Rnd.Next(1000).ToString() + ")";
                            Directory.Move(GetFullElementPath(pVirtualElement.PathRelative), NewElementPathFull + ConflictSuffix);
                        }
                        else
                        {
                            logger.Debug("In SecureMoveElement : the directory to rename doesn't exist anymore");
                        }
                    }
                }

                #endregion
            }

            return ActionResult.Success;
        }

        public string GetTemporaryFolderPath()
        {
            return _LocalPathTemp;   
        }

        public string GetRootFolderPath()
        {
            return this._LocalPath;
        }

        public string GetRelativeFilePathTemp(string TempFullFilePath)
        {
            return TempFullFilePath.Replace(_LocalPathTemp, "");
        }

        public string GetFullFilePathTemp(string TempRelativeFilePath)
        {
            return _LocalPathTemp + TempRelativeFilePath;
        }

        public string GetRelativeElementPath(string FullElementPath)
        {
            return FullElementPath.Replace(_LocalPath, "");
        }

        public string GetFullElementPath(string RelativeElementPath)
        {
            return _LocalPath + RelativeElementPath;
        }

        public void VirtualElement_Serialize(VirtualElement VE)
        {
            if (VE.ElementType == VirtualElementType.File) ((VirtualFile)VE).Serialize(_RepositoryId);
            else ((VirtualFolder)VE).Serialize(_RepositoryId);
        }

        /// <summary>
        /// Sauvegarde en base un VirtualFolder et tous ses fils
        /// </summary>
        /// <param name="VF"></param>
        public void VirtualFolder_SerializeTree(VirtualFolder VF)
        {
            VF.Serialize(this._RepositoryId);

            foreach (VirtualFolder TVF in VF.GetSubElementsFolders())
            {
                TVF.Serialize(this._RepositoryId);
            }

            foreach( VirtualFile VFF in  VF.GetSubElementsFiles())
            {
                VFF.Serialize(this._RepositoryId);
            }
        }

        /// <summary>
        /// Supprime un Virtual Folder et tous ses fils
        /// </summary>
        /// <param name="VF"></param>
        public void VirtualFolder_DeleteFolder(VirtualFolder VF, Int32? CurrentTaskId)
        {
            SQLiteHelper oSQLHelper = new SQLiteHelper(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, null);

            try
            {
                VF.SetDeleted(CurrentTaskId);
                List<VirtualElementDeleted> RemovedElements = new List<VirtualElementDeleted>();
                if (VF.ParentElement == null) _VirtualRootFolder.SubElementDelete(VF, RemovedElements, CurrentTaskId);
                else VF.ParentElement.SubElementDelete(VF, RemovedElements, CurrentTaskId);

                if (!oSQLHelper.InitConnection()) return;

                foreach (VirtualElementDeleted VED in RemovedElements)
                {
                    if (VED.ElementType == VirtualElementType.Folder)
                    {
                        oSQLHelper.SetCommandText("DELETE FROM VFolder WHERE Id_Folder = '" + VED.ElementId.ToString() + "'");
                        oSQLHelper.ExecuteNonQuery();
                    }
                    else if (VED.ElementType == VirtualElementType.File)
                    {
                        oSQLHelper.SetCommandText("DELETE FROM VFile WHERE Id_File = '" + VED.ElementId.ToString() + "'");
                        oSQLHelper.ExecuteNonQuery();
                    }

                    _VirtualRootFolder.FlatElementRemove(VED.ElementId);
                }

                oSQLHelper.Dispose();

                VF.Dispose(CurrentTaskId);
                VF = null;
            }
            catch
            {
                //Si cette action génére une exception (connexion base de données par exemple), le répertoire sera nettoyé au prochain full refresh
            }
        }

        public void VirtualFolder_DeleteFile(VirtualFile VF, Int32? CurrentTaskId)
        {
            if (VF.ParentElement == null) _VirtualRootFolder.SubElementDelete(VF, null, CurrentTaskId);
            else VF.ParentElement.SubElementDelete(VF, null, CurrentTaskId);

            SQLiteHelper oSQLHelper = new SQLiteHelper(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, null);
            if (!oSQLHelper.InitConnection()) return;

            oSQLHelper.SetCommandText("DELETE FROM VFile WHERE Id_File = '" + VF.ElementId.ToString() + "'");
            oSQLHelper.ExecuteNonQuery();

            _VirtualRootFolder.FlatElementRemove(VF.ElementId);

            oSQLHelper.Dispose();
        }

        public bool VirtualFile_CreateTemporaryCopy(VirtualFile VF)
        {
            VF.FileTemporaryCopyFullPath = GetFullFilePathTemp(Path.DirectorySeparatorChar + Guid.NewGuid().ToString());

            try
            {
                File.Copy(GetFullElementPath(VF.PathRelative), VF.FileTemporaryCopyFullPath, true);
            }
            catch
            {
                VF.FileTemporaryCopyFullPath = String.Empty;
                return false;
            }

            return true;
        }

        public bool VirtualFile_DeleteTemporaryCopy(VirtualFile VF)
        {
            if (!VF.FileTemporaryCopyFullPath.Equals(string.Empty))
            {
                try
                {
                    File.Delete(VF.FileTemporaryCopyFullPath);
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        public void VirtualFile_RemoveFromFolder(VirtualFile VF, Int32? CurrentTaskId)
        {
            var ParentElement = VF.ParentElement;
            ParentElement.SubElementDelete(VF, null, CurrentTaskId);
            _VirtualRootFolder.FlatElementRemove(VF.ElementId);
        }

        public bool VirtualFile_DeleteSeedCopy(VirtualFile VF)
        {
            if (!VF.SeedTemporaryCopyFullPath.Equals(string.Empty))
            {
                try
                {
                    File.Delete(VF.SeedTemporaryCopyFullPath);
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        /*
        public bool VirtualFile_CreateSeedCopy(VirtualFile VF)
        {
            VF.SeedTemporaryCopyFullPath = GetFullFilePathTemp(Path.DirectorySeparatorChar + Guid.NewGuid().ToString());

            try
            {
                File.Copy(GetFullElementPath(VF.PathRelative), VF.SeedTemporaryCopyFullPath);
            }
            catch
            {
                VF.SeedTemporaryCopyFullPath = String.Empty;
                return false;
            }

            return true;
        }
        */

        private ActionResult VirtualElement_Move(VirtualElement VE, string NewPathRelative)
        {
            //Detach from parent Element
            VE.ParentElement.SubElementRemove(VE);
 
            //Look for the new parent element
            String TempNewParentPathRelative = Tools.GetParentPath(NewPathRelative);
            VirtualElement TempNewParentFolder = _VirtualRootFolder.FlatElementsGetElementBasedOnPath(TempNewParentPathRelative, VirtualElementType.Folder, false);

            if (TempNewParentFolder == null)
            {
                RebuildMissingFolderForAction(TempNewParentPathRelative);
                TempNewParentFolder = _VirtualRootFolder.FlatElementsGetElementBasedOnPath(TempNewParentPathRelative, VirtualElementType.Folder, false);

                if (TempNewParentFolder == null)
                {
                    return ActionResult.Cancel;
                }
            }

            //If there is already an element having the same path => return
            if (TempNewParentFolder.GetDirectSubElementByPath(NewPathRelative, false) != null) return ActionResult.Cancel;

            TempNewParentFolder.SubElementAdd(VE);
            if (VE.ElementType == VirtualElementType.Folder) VE.RecomputePathToRootForAllSubElements();

            return ActionResult.Success;
        }

        /// <summary>
        /// Retrieve a value from the param table
        /// </summary>
        /// <param name="ParamName"></param>
        /// <returns></returns>
        public String GetParamValue(String ParamName)
        {
            SQLiteHelper oSQLHelper = new SQLiteHelper(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, null);
            if (!oSQLHelper.InitConnection()) return null;

            oSQLHelper.SetCommandText("SELECT ParamValue FROM Param WHERE ParamName = '" + ParamName + "'");

            object QueryResult = null;
            if (!oSQLHelper.ExecuteScalar(ref QueryResult)) { oSQLHelper.Dispose(); return null; }
            oSQLHelper.Dispose();

            return QueryResult == null ? null : QueryResult.ToString();
        }

        #endregion

        #region Log

        private void Log(string Message)
        {
            if (LogOutput != null) LogOutput(this, Message);
        }

        private void LogFSEvent(string Message)
        {
            Log(String.Format("[FSEvent] - {0}", Message));
        }

        private void LogDispatchEvent(string Message)
        {
            Log(Message);
        }

        private void LogActionThread(string Message)
        {
            Log(String.Format("[ACTION][TASK:{0}] - {1}", Task.CurrentId.HasValue ? Task.CurrentId.Value.ToString() : "?", Message));
        }

        private void LogComparer(string Message)
        {
            Log("[COMPARER]" + Message);
        }

        #endregion
    }
}