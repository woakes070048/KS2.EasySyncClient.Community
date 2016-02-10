using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using KaliSyncCommon;

namespace KaliSyncW
{
    public class SyncFileSystemWatcher
    {
        private VirtualRootFolder _RootVirtualFolder;
        
        private FileSystemWatcher _FSWatcherFiles;
        private FileSystemWatcher _FSWatcherDirectories;

        private string _DirectoryToMonitor;
        private System.Timers.Timer FSEventListAnalysisTimer;
        private List<FSSyncEvent> FSEventList = new List<FSSyncEvent>();
        private static object FSEventListLock = new object();
        private static object FSEventListAnalysisLock = new object();

        private List<SyncEventIgnoreItem> FSIgnoreEventList = new List<SyncEventIgnoreItem>(); //Used to prevent processing fake events
        private object FSIgnoreEventListLock = new object(); //Used to prevent processing fake events

        public SyncFileSystemWatcher(string DirectoryToMonitor)
        {
            _DirectoryToMonitor = DirectoryToMonitor;

            //Files watcher
            _FSWatcherFiles = new FileSystemWatcher();
            _FSWatcherFiles.IncludeSubdirectories = true;
            _FSWatcherFiles.InternalBufferSize = 2 * _FSWatcherFiles.InternalBufferSize;
            _FSWatcherFiles.Path = _DirectoryToMonitor;
            _FSWatcherFiles.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            _FSWatcherFiles.Created += FS_Created;
            _FSWatcherFiles.Changed += FS_Changed;
            _FSWatcherFiles.Renamed += FS_Renamed;
            _FSWatcherFiles.Deleted += FS_Deleted;
            _FSWatcherFiles.Error += FS_Error;
            _FSWatcherFiles.Filter = "*.*";

            //Folder watcher
            _FSWatcherDirectories = new FileSystemWatcher();
            _FSWatcherDirectories.IncludeSubdirectories = true;
            _FSWatcherDirectories.InternalBufferSize = 2 * _FSWatcherDirectories.InternalBufferSize;
            _FSWatcherDirectories.Path = _DirectoryToMonitor;
            _FSWatcherDirectories.NotifyFilter = NotifyFilters.DirectoryName;
            _FSWatcherDirectories.Created += FS_DirCreated;
            _FSWatcherDirectories.Renamed += FS_DirRenamed;
            _FSWatcherDirectories.Deleted += FS_DirDeleted;
            _FSWatcherDirectories.Error += FS_Error;
            _FSWatcherDirectories.Filter = "*.*";

            //File event processing timer
            FSEventListAnalysisTimer = new System.Timers.Timer(5 * 1000);
            FSEventListAnalysisTimer.AutoReset = false;
            FSEventListAnalysisTimer.Elapsed += FSEventListAnalysisTimer_Elapsed;
        }

        #region Original Events (from FileSystemWatcher)

        private void FS_Renamed(object sender, RenamedEventArgs e)
        {
            lock (FSEventListLock)
            {
                FSEventListAnalysisTimer.Enabled = false; //Stop Timer so that the analysing event will only occur when the system is idle (ie when all changes have been performed)
                FSEventList.Add(new FSSyncEvent(BasicActionEnum.Renamed, e.OldFullPath, e.FullPath));
                FSEventListAnalysisTimer.Enabled = true;
            }
        }

        private void FS_Error(object sender, ErrorEventArgs e)
        {
            OnLog(String.Format("FileSystemWatcher Error {0}", e.GetException().Message));
        }

        private void FS_Deleted(object sender, FileSystemEventArgs e)
        {
            lock (FSEventListLock)
            {
                FSEventListAnalysisTimer.Enabled = false;
                FSEventList.Add(new FSSyncEvent(BasicActionEnum.Deleted, e.FullPath, ""));
                FSEventListAnalysisTimer.Enabled = true;
            }
        }

        private void FS_Created(object sender, FileSystemEventArgs e)
        {
            lock (FSEventListLock)
            {
                FSEventListAnalysisTimer.Enabled = false;
                FSEventList.Add(new FSSyncEvent(BasicActionEnum.Created, e.FullPath, ""));
                FSEventListAnalysisTimer.Enabled = true;
            }
        }

        private void FS_Changed(object sender, FileSystemEventArgs e)
        {
            lock (FSEventListLock)
            {
                FSEventListAnalysisTimer.Enabled = false;
                FSEventList.Add(new FSSyncEvent(BasicActionEnum.Changed, e.FullPath, ""));
                FSEventListAnalysisTimer.Enabled = true;
            }
        }

        private void FS_DirCreated(object sender, FileSystemEventArgs e)
        {
            lock (FSIgnoreEventListLock)
            {
                SyncEventIgnoreItem SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalCreate) > 0 && x.NewOrCurrentFilePathFull.Equals(e.FullPath));
                if (SEII == null)
                {
                    OnEvent(new SyncEventItem(SyncEventEnum.LocalCreate, e.FullPath, true, false));
                }
                else
                {
                    FSIgnoreEventList.Remove(SEII);
                    OnLog("Event Ignored");
                }
            }
        }

        private void FS_DirRenamed(object sender, RenamedEventArgs e)
        {
            lock (FSIgnoreEventListLock)
            {
                SyncEventIgnoreItem SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalRename) > 0 && x.NewOrCurrentFilePathFull.Equals(e.FullPath));
                if (SEII == null)
                {
                    OnEvent(new SyncEventItem(SyncEventEnum.LocalRename, e.FullPath, e.OldFullPath, true, false));
                }
                else
                {
                    FSIgnoreEventList.Remove(SEII);
                    OnLog("Event Ignored");
                }
            }
        }

        private void FS_DirDeleted(object sender, FileSystemEventArgs e)
        {
            lock (FSIgnoreEventListLock)
            {
                SyncEventIgnoreItem SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalDelete) > 0 && x.NewOrCurrentFilePathFull.Equals(e.FullPath));
                if (SEII == null)
                {
                    var TempVirtualFolder = _RootVirtualFolder.FindFolderBasedOnPath(GetRelativeFilePath(e.FullPath));
                    if (TempVirtualFolder != null)
                    {
                        OnEvent(new SyncEventItem(SyncEventEnum.LocalDelete, TempVirtualFolder.FolderId, true, false));
                    }
                }
                else
                {
                    FSIgnoreEventList.Remove(SEII);
                    OnLog("Event Ignored");
                }
            }
        }

        #endregion

        #region Reworked Events

        public delegate void SyncFSEvent(object sender, SyncEventItem e);
        public delegate void SyncFSLogEvent(object sender, string s);

        public event SyncFSEvent Event;
        public event SyncFSLogEvent Log;

        protected virtual void OnEvent(SyncEventItem e)
        {
            if (Event != null) Event(this, e);
        }

        protected virtual void OnLog(string MessageToLog)
        {
            if ( Log != null ) Log(this,MessageToLog);
        }

        #endregion

        #region Event ignore

        public void IgnoreEventAdd(SyncEventIgnoreItem SEII)
        {
            lock (FSIgnoreEventListLock)
            {
                FSIgnoreEventList.Add(SEII);
            }
        }

        public void IgnoreEventRemove(Guid EventId)
        {
            lock (FSIgnoreEventListLock)
            {
                FSIgnoreEventList.RemoveAll(x => x.SyncEventId.Equals(EventId));
            }
        }

        #endregion

        public void Start(VirtualRootFolder RootVirtualFolder)
        {
            _RootVirtualFolder = RootVirtualFolder;
            _FSWatcherFiles.EnableRaisingEvents = true;
            _FSWatcherDirectories.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            _FSWatcherFiles.EnableRaisingEvents = false;
            _FSWatcherDirectories.EnableRaisingEvents = false;
        }

        private void FSEventListAnalysisTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            List<FSSyncEvent> WorkingList;
            List<FSSyncEvent> TempList;
            FSSyncEvent TempEvent;
            FSSyncEvent MyEvent;
            BasicActionEnum TempFileChainStatus;
            string TempFinalFileName = String.Empty;
            string TempFinalFileName2 = String.Empty;
            VirtualFile TempVirtualFile;
            SyncEventIgnoreItem SEII;

            List<FSSyncEvent> FilteredList = new List<FSSyncEvent>();

            lock (FSEventListLock)
            {
                //Extract the list for analysis
                WorkingList = ObjectCopier.Clone(FSEventList);
                FSEventList.Clear();
            }

            #region Event Analysis

            for (int i = 0; i < WorkingList.Count; i++) WorkingList[i].Index = i; //Reindex the list of actions

            lock (FSEventListAnalysisLock)
            {
                while (WorkingList.Count > 0)
                {
                    MyEvent = WorkingList[0];

                    switch (MyEvent.Action)
                    {
                        case BasicActionEnum.Created:

                            //Detect Temporary : Create / Update / Delete in the same batch
                            TempEvent = WorkingList.Find(x => x.Action.Equals(BasicActionEnum.Deleted) && x.Index > MyEvent.Index && x.OldOrCurrentFilePathFull.Equals(MyEvent.OldOrCurrentFilePathFull));
                            if (TempEvent != null)
                            {
                                //Log(String.Format("File {0} was temporary", MyEvent.OldOrCurrentFilePathFull)); //Temporary file, do nothing about it
                                WorkingList.Remove(TempEvent);
                            }
                            else
                            {
                                //Remove multiple Changed events for the same file
                                TempList = WorkingList.FindAll((x => x.Action.Equals(BasicActionEnum.Changed) && x.OldOrCurrentFilePathFull.Equals(MyEvent.OldOrCurrentFilePathFull)));
                                if (TempList != null)
                                {
                                    foreach (FSSyncEvent xe in TempList) WorkingList.Remove(xe);
                                }

                                //Check what happened to this new file
                                TempFileChainStatus = BuildFileEventChain(MyEvent.OldOrCurrentFilePathFull, MyEvent.Index, WorkingList, ref TempFinalFileName/*, ref TempIndex*/);
                                if (TempFileChainStatus.Equals(BasicActionEnum.Renamed))
                                {
                                    FilteredList.Add(new FSSyncEvent(BasicActionEnum.Created, TempFinalFileName, ""));
                                    //Log(String.Format("File was created with name {0} and finally renamed to {1}", MyEvent.OldOrCurrentFilePathFull, TempFinalFileName));
                                }
                                else if (TempFileChainStatus.Equals(BasicActionEnum.Deleted))
                                {
                                    //Log(String.Format("File {0} was a temporary file",MyEvent.OldOrCurrentFilePathFull));
                                }
                                else
                                {
                                    FilteredList.Add(new FSSyncEvent(BasicActionEnum.Created, MyEvent.OldOrCurrentFilePathFull, ""));
                                    //Log(String.Format("File {0} is new", MyEvent.OldOrCurrentFilePathFull));
                                }
                            }

                            break;

                        case BasicActionEnum.Changed:

                            //Is there are subsequent changes for this file, ignore them
                            WorkingList.RemoveAll((x => x.Action.Equals(BasicActionEnum.Changed) && x.Index > MyEvent.Index && x.OldOrCurrentFilePathFull.Equals(MyEvent.OldOrCurrentFilePathFull)));

                            //Check if the file is not deleted in a next step
                            TempEvent = WorkingList.Find(x => x.Action.Equals(BasicActionEnum.Deleted) && x.Index > MyEvent.Index && x.OldOrCurrentFileName.Equals(MyEvent.OldOrCurrentFileName));
                            if (TempEvent != null)
                            {
                                //Log(String.Format("File {0} was deleted", MyEvent.OldOrCurrentFilePathFull));
                                FilteredList.Add(new FSSyncEvent(BasicActionEnum.Deleted, MyEvent.OldOrCurrentFilePathFull, ""));
                                WorkingList.Remove(TempEvent);
                            }
                            else
                            {
                                //Log(String.Format("File {0} has changed", MyEvent.OldOrCurrentFilePathFull));
                                FilteredList.Add(new FSSyncEvent(BasicActionEnum.Changed, MyEvent.OldOrCurrentFilePathFull, ""));
                            }

                            break;

                        case BasicActionEnum.Deleted:
                            //Check if there is a following Create for the same file name, means it has been moved
                            //TODO : Maybe Move detected should be made on more than the filename / can add last modification date
                            TempEvent = WorkingList.Find(x => x.Action.Equals(BasicActionEnum.Created) && x.Index > MyEvent.Index && x.OldOrCurrentFileName.Equals(MyEvent.OldOrCurrentFileName)); 
                            if (TempEvent != null)
                            {
                                if (MyEvent.OldOrCurrentFilePathFull.Equals(TempEvent.OldOrCurrentFilePathFull))
                                {
                                    //Log(String.Format("File {0} has been updated", MyEvent.OldOrCurrentFilePathFull)); //A file moved to the same location is an updated file and not a moved file
                                    FilteredList.Add(new FSSyncEvent(BasicActionEnum.Changed, MyEvent.OldOrCurrentFilePathFull, ""));
                                }
                                else
                                {
                                    //Log(String.Format("File {0} has moved to {1}", MyEvent.OldOrCurrentFilePathFull, TempEvent.OldOrCurrentFilePathFull));
                                    FilteredList.Add(new FSSyncEvent(BasicActionEnum.Moved, MyEvent.OldOrCurrentFilePathFull, TempEvent.OldOrCurrentFilePathFull));
                                }

                                WorkingList.Remove(TempEvent);
                            }
                            else
                            {
                                //Log(String.Format("File {0} has been deleted", MyEvent.OldOrCurrentFilePathFull));
                                FilteredList.Add(new FSSyncEvent(BasicActionEnum.Deleted, MyEvent.OldOrCurrentFilePathFull, ""));
                            }
                            break;

                        case BasicActionEnum.Renamed:

                            TempFileChainStatus = BuildFileEventChain(MyEvent.NewFilePathFull, MyEvent.Index, WorkingList, ref TempFinalFileName/*, ref TempIndex*/);
                            if (TempFileChainStatus.Equals(BasicActionEnum.Renamed))
                            {
                                //Log(String.Format("File {0} was renamed to {1}", MyEvent.OldOrCurrentFilePathFull, TempFinalFileName));
                                FilteredList.Add(new FSSyncEvent(BasicActionEnum.Renamed, MyEvent.OldOrCurrentFilePathFull, TempFinalFileName));
                            }
                            else if (TempFileChainStatus.Equals(BasicActionEnum.Deleted))
                            {
                                //Log(String.Format("File {0} has been renammed and then deleted", MyEvent.OldOrCurrentFilePathFull));
                                FilteredList.Add(new FSSyncEvent(BasicActionEnum.Deleted, MyEvent.OldOrCurrentFilePathFull, ""));
                            }
                            else
                            {
                                //Log(String.Format("File {0} was renamed to {1}", MyEvent.OldOrCurrentFilePathFull, MyEvent.NewFilePathFull));
                                FilteredList.Add(new FSSyncEvent(BasicActionEnum.Renamed, MyEvent.OldOrCurrentFilePathFull, MyEvent.NewFilePathFull));
                            }

                            break;
                    }

                    WorkingList.Remove(MyEvent);
                }
            }

            #endregion

            #region Office documents handling

            for (int i = 0; i < FilteredList.Count; i++)
            {
                FilteredList[i].Index = i;
            }

            //Ms office changed documents will appear as Created and Deleted in the same round, detect this here 
            TempList = FilteredList.FindAll(x => x.Action.Equals(BasicActionEnum.Created));
            for (int i = 0; i < TempList.Count; i++)
            {
                TempEvent = FilteredList.Find(x => x.Action.Equals(BasicActionEnum.Deleted) && x.Index > TempList[i].Index && x.OldOrCurrentFilePathFull.Equals(TempList[i].OldOrCurrentFilePathFull) && File.Exists(TempList[i].OldOrCurrentFilePathFull));
                if (TempEvent != null)
                {
                    //Office document upgrade detected
                    FilteredList.Add(new FSSyncEvent(BasicActionEnum.Changed, TempEvent.OldOrCurrentFilePathFull, ""));
                    FilteredList.Remove(TempEvent);
                    FilteredList.Remove(TempList[i]);
                }
            }

            //Ms office changed documents will appear as Deleted and Created in the same round, detect this here 
            for (int i = 0; i < FilteredList.Count; i++)
            {
                FilteredList[i].Index = i;
            }
            TempList = FilteredList.FindAll(x => x.Action.Equals(BasicActionEnum.Deleted));
            for (int i = 0; i < TempList.Count; i++)
            {
                TempEvent = FilteredList.Find(x => x.Action.Equals(BasicActionEnum.Created) && x.Index > TempList[i].Index && x.OldOrCurrentFilePathFull.Equals(TempList[i].OldOrCurrentFilePathFull));
                if (TempEvent != null)
                {
                    //Office document upgrade detected
                    FilteredList.Add(new FSSyncEvent(BasicActionEnum.Changed, TempEvent.OldOrCurrentFilePathFull, ""));
                    FilteredList.Remove(TempEvent);
                    FilteredList.Remove(TempList[i]);
                }
            }

            //Remove Office Temporary files
            FilteredList.RemoveAll(x => x.Action.Equals(BasicActionEnum.Created) && x.OldOrCurrentFileName.StartsWith("~"));
            FilteredList.RemoveAll(x => x.Action.Equals(BasicActionEnum.Deleted) && x.OldOrCurrentFileName.StartsWith("~"));

            #endregion

            #region Dispatch Events

            if (FilteredList.Count > 0)
            {
                lock (FSIgnoreEventListLock)
                {
                    for (int i = 0; i < FilteredList.Count; i++)
                    {
                        OnLog(String.Format("[{0}] - {1} -> {2}", FilteredList[i].Action.ToString(), FilteredList[i].OldOrCurrentFileName, FilteredList[i].NewFileName));
                        switch (FilteredList[i].Action)
                        {
                            case BasicActionEnum.Created:

                                //Is this an event to be ignored ?
                                SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalCreate) > 0 && x.NewOrCurrentFilePathFull.Equals(FilteredList[i].OldOrCurrentFilePathFull));
                                if (SEII  == null)
                                {
                                    if (File.Exists(FilteredList[i].OldOrCurrentFilePathFull)) OnEvent(new SyncEventItem(SyncEventEnum.LocalCreate, FilteredList[i].OldOrCurrentFilePathFull, false, false));
                                }
                                else
                                {
                                    FSIgnoreEventList.Remove(SEII);
                                    OnLog("Event Ignored");
                                }
                                break;

                            case BasicActionEnum.Deleted:

                                //Is there a file with this name ?
                                TempVirtualFile = _RootVirtualFolder.FindFileBasedOnPath(GetRelativeFilePath(FilteredList[i].OldOrCurrentFilePathFull));
                                if (TempVirtualFile != null)
                                {
                                    //Is this an event to be ignored ?
                                    SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalDelete) > 0 && x.NewOrCurrentFilePathFull.Equals(FilteredList[i].OldOrCurrentFilePathFull));
                                    if (SEII  == null)
                                    {
                                        OnEvent(new SyncEventItem(SyncEventEnum.LocalDelete, TempVirtualFile.FileId, false, false));
                                    }
                                    else
                                    {
                                        FSIgnoreEventList.Remove(SEII);
                                        OnLog("Event Ignored");
                                    }
                                }
                                /*
                                else
                                {
                                    //This is a folder
                                    var TempVirtualFileList = _RootVirtualFolder.FindAllFilesBasedOnFolder(GetRelativeFilePath(FilteredList[i].OldOrCurrentFilePathFull) + @"\");
                                    for (int k = 0; k < TempVirtualFileList.Count; k++)
                                    {
                                        SubTempVirtualFile = _RootVirtualFolder.FindFileBasedOnPath(TempVirtualFileList[k].FilePathRelative);
                                        if (SubTempVirtualFile != null)
                                        {
                                            OnLog(String.Format("Found file {0}", SubTempVirtualFile.FilePathRelative));
                                            OnDeleted(new SyncEventItem(SyncEventEnum.LocalDelete, SubTempVirtualFile.FileId));
                                        }
                                    }
                                }
                                */
                                break;

                            case BasicActionEnum.Moved:

                                //Is this an event to be ignored ?
                                SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalMove) > 0 && x.NewOrCurrentFilePathFull.Equals(FilteredList[i].NewFilePathFull));
                                if ( SEII == null)
                                {
                                    if (File.Exists(FilteredList[i].NewFilePathFull)) OnEvent(new SyncEventItem(SyncEventEnum.LocalMove, Guid.Empty, FilteredList[i].NewFilePathFull, FilteredList[i].OldOrCurrentFilePathFull, false, false));
                                }
                                else
                                {
                                    FSIgnoreEventList.Remove(SEII);
                                    OnLog("Event Ignored");
                                }
                                break;

                            case BasicActionEnum.Renamed:

                                //Is there a file with this name ?
                                TempVirtualFile = _RootVirtualFolder.FindFileBasedOnPath(GetRelativeFilePath(FilteredList[i].OldOrCurrentFilePathFull));
                                if (TempVirtualFile != null)
                                {
                                    //Is this an event to be ignored ?
                                    SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalRename) > 0 && x.NewOrCurrentFilePathFull.Equals(FilteredList[i].NewFilePathFull));
                                    if (SEII == null)
                                    {
                                        OnEvent(new SyncEventItem(SyncEventEnum.LocalRename, Guid.Empty, FilteredList[i].NewFilePathFull, FilteredList[i].OldOrCurrentFilePathFull, false, false));
                                    }
                                    else
                                    {
                                        FSIgnoreEventList.Remove(SEII);
                                        OnLog("Event Ignored");
                                    }
                                }
                                /*else
                                {
                                    //This is a folder
                                    var TempVirtualFileList = _RootVirtualFolder.FindAllFilesBasedOnFolder(GetRelativeFilePath(FilteredList[i].OldOrCurrentFilePathFull) + @"\");
                                    for (int k = 0; k < TempVirtualFileList.Count; k++)
                                    {
                                        SubTempVirtualFile = _RootVirtualFolder.FindFileBasedOnPath(TempVirtualFileList[k].FilePathRelative);
                                        if (SubTempVirtualFile != null)
                                        {
                                            OnLog(String.Format("Found file {0}", SubTempVirtualFile.FilePathRelative));
                                            OnRenamed(new SyncEventItem(SyncEventEnum.LocalRename, Guid.Empty, SubTempVirtualFile.FilePathRelative.Replace(GetRelativeFilePath(FilteredList[i].OldOrCurrentFilePathFull), GetRelativeFilePath(FilteredList[i].NewFilePathFull)), SubTempVirtualFile.FilePathRelative));
                                        }
                                    }
                                }
                                */
                                break;

                            case BasicActionEnum.Changed:

                                //Is this an event to be ignored ?
                                SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalUpdate) > 0 && x.NewOrCurrentFilePathFull.Equals(FilteredList[i].OldOrCurrentFilePathFull));
                                if (SEII == null)
                                {
                                    if (File.Exists(FilteredList[i].OldOrCurrentFilePathFull)) OnEvent(new SyncEventItem(SyncEventEnum.LocalUpdate, FilteredList[i].OldOrCurrentFilePathFull, false, false));
                                }
                                else
                                {
                                    FSIgnoreEventList.Remove(SEII);
                                    OnLog("Event Ignored");
                                }
                                break;
                        }

                    }
                }
            }

            #endregion
        }

        private BasicActionEnum BuildFileEventChain(string FilePath, Int32 CurrentIndex, List<FSSyncEvent> WorkingList, ref string FinalFileName)
        {
            BasicActionEnum ReturnValue = BasicActionEnum.None;
            BasicActionEnum RecursiveResult = BasicActionEnum.None;
            FSSyncEvent TempEvent;

            //Has the file been renamed
            TempEvent = WorkingList.Find(x => x.Action.Equals(BasicActionEnum.Renamed) && x.OldOrCurrentFilePathFull.Equals(FilePath) && x.Index > CurrentIndex);
            if (TempEvent != null)
            {
                //Log(String.Format("****File {0} was renamed {1}", FilePath, TempEvent.NewFilePath));
                FinalFileName = TempEvent.NewFilePathFull;
                ReturnValue = BasicActionEnum.Renamed;
                RecursiveResult = BuildFileEventChain(TempEvent.NewFilePathFull, TempEvent.Index, WorkingList, ref FinalFileName);
                if (RecursiveResult != BasicActionEnum.None) ReturnValue = RecursiveResult;
                WorkingList.Remove(TempEvent);
            }

            //Has the file been deleted
            TempEvent = WorkingList.Find(x => x.Action.Equals(BasicActionEnum.Deleted) && x.OldOrCurrentFilePathFull.Equals(FilePath) && x.Index > CurrentIndex);
            if (TempEvent != null)
            {
                ReturnValue = BasicActionEnum.Deleted;
                //Log(String.Format("****File {0} was deleted ", TempEvent.FilePath));
                WorkingList.Remove(TempEvent);
            }

            return ReturnValue;
        }

        private string GetRelativeFilePath(string FullFilePath)
        {
            return FullFilePath.Replace(_DirectoryToMonitor, "");
        }
    }
}