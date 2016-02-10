/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel;
#if __MonoCS__
	using System.IO;
#else
using Alphaleonis.Win32.Filesystem;
#endif
using System.Linq;
using System.Text;
using KS2.EasySync.Interface;

#if __MonoCS__
    using KS2.EasySync.Mac;
#else
    using KS2.EasySync.Windows;
#endif

namespace KS2.EasySync.Core
{
    public class SyncFileSystemWatcher
    {
        private VirtualRootFolder _RootVirtualFolder;

        private IDiskWatcher _FSWatcher;

        private string _DirectoryToMonitor;
        private System.Timers.Timer FSEventListAnalysisTimer;
        private List<FSSyncEvent> FSEventList;
        private static object FSEventListLock = new object();
        private static object FSEventListAnalysisLock = new object();

        private List<SyncEventIgnoreItem> FSIgnoreEventList; //Used to prevent processing fake events
        private object FSIgnoreEventListLock = new object(); //Used to prevent processing fake events

        #region SyncFileSystemWatcher Events

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
            if (Log != null) Log(this, MessageToLog);
        }

        #endregion

        public SyncFileSystemWatcher(string DirectoryToMonitor)
        {
            _DirectoryToMonitor = DirectoryToMonitor;

#if __MonoCS__
            _FSWatcher =  (IDiskWatcher)new KS2.EasySync.Mac.MacDiskWatcher();
#else
            _FSWatcher = (IDiskWatcher)new KS2.EasySync.Windows.WindowsDiskWatcher();
#endif

            _FSWatcher.FileCreated += FS_FileCreated;
            _FSWatcher.FileRenamed += FS_FileRenamed;
            _FSWatcher.FileChanged += FS_FileChanged;
            _FSWatcher.FileDeleted += FS_FileDeleted;
            _FSWatcher.DirCreated += FS_DirCreated;
            _FSWatcher.DirRenamed += FS_DirRenamed;
            _FSWatcher.DirDeleted += FS_DirDeleted;
            _FSWatcher.Error += FS_Error;
            _FSWatcher.Init(DirectoryToMonitor);

            //File event processing timer
            FSEventListAnalysisTimer = new System.Timers.Timer(5 * 1000);
            FSEventListAnalysisTimer.AutoReset = false;
            FSEventListAnalysisTimer.Elapsed += FSEventListAnalysisTimer_Elapsed;

            FSIgnoreEventList = new List<SyncEventIgnoreItem>();
            FSEventList = new List<FSSyncEvent>();
        }

        #region Original Events (from FileSystemWatcher)

        private void FS_Error(object sender, System.IO.ErrorEventArgs e)
        {
            OnLog(String.Format("FileSystemWatcher Error {0}", e.GetException().Message));
        }

        private void FS_FileCreated(object sender, System.IO.FileSystemEventArgs e)
        {
            lock (FSEventListLock)
            {
                FSEventListAnalysisTimer.Enabled = false;
                FSEventList.Add(new FSSyncEvent(BasicActionEnum.FileCreated, e.FullPath, ""));
                FSEventListAnalysisTimer.Enabled = true;
                //OnLog(String.Format("FS_FileCreated {0}", e.FullPath));
            }
        }

        private void FS_FileRenamed(object sender, System.IO.RenamedEventArgs e)
        {
            lock (FSEventListLock)
            {
                FSEventListAnalysisTimer.Enabled = false; //Stop Timer so that the analysing event will only occur when the system is idle (ie when all changes have been performed)
                FSEventList.Add(new FSSyncEvent(BasicActionEnum.FileRenamed, e.OldFullPath, e.FullPath));
                FSEventListAnalysisTimer.Enabled = true;
            }
        }

        private void FS_FileChanged(object sender, System.IO.FileSystemEventArgs e)
        {
            lock (FSEventListLock)
            {
                FSEventListAnalysisTimer.Enabled = false;
                FSEventList.Add(new FSSyncEvent(BasicActionEnum.FileChanged, e.FullPath, ""));
                FSEventListAnalysisTimer.Enabled = true;
            }
        }

        private void FS_FileDeleted(object sender, System.IO.FileSystemEventArgs e)
        {
            lock (FSEventListLock)
            {
                FSEventListAnalysisTimer.Enabled = false;
                FSEventList.Add(new FSSyncEvent(BasicActionEnum.FileDeleted, e.FullPath, ""));
                FSEventListAnalysisTimer.Enabled = true;
            }
        }

        private void FS_DirCreated(object sender, System.IO.FileSystemEventArgs e)
        {
            lock (FSEventListLock)
            {
                FSEventListAnalysisTimer.Enabled = false;
                FSEventList.Add(new FSSyncEvent(BasicActionEnum.DirCreated, e.FullPath, ""));
                FSEventListAnalysisTimer.Enabled = true;
            }
        }

        private void FS_DirRenamed(object sender, System.IO.RenamedEventArgs e)
        {
            lock (FSEventListLock)
            {
                FSEventListAnalysisTimer.Enabled = false;
                FSEventList.Add(new FSSyncEvent(BasicActionEnum.DirRenamed, e.OldFullPath, e.FullPath));
                FSEventListAnalysisTimer.Enabled = true;
            }
        }

        private void FS_DirDeleted(object sender, System.IO.FileSystemEventArgs e)
        {
            lock (FSEventListLock)
            {
                FSEventListAnalysisTimer.Enabled = false;
                FSEventList.Add(new FSSyncEvent(BasicActionEnum.DirDeleted, e.FullPath, ""));
                FSEventListAnalysisTimer.Enabled = true;
            }
        }

        #endregion

        #region Event ignore Methods

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
            _FSWatcher.Start();

        }

        public void Stop()
        {
            _FSWatcher.Stop();
        }

        private void FSEventListAnalysisTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            List<FSSyncEvent> WorkingList;
            List<FSSyncEvent> WorkingListFiltered = new List<FSSyncEvent>();
            List<FSSyncEvent> TempSubList;
            FSSyncEvent TempEvent;
            FSSyncEvent MyEvent;
            EventFileChainResult TempFileChainStatus;
            string TempFinalFileName = String.Empty;
            string TempFinalFileName2 = String.Empty;
            VirtualFile TempVirtualFile;
            SyncEventIgnoreItem SEII;

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
                        case BasicActionEnum.FileCreated:

                            #region

                            //Detect Temporary : Create / Update / Delete in the same batch
                            TempEvent = WorkingList.Find(x => x.Action.Equals(BasicActionEnum.FileDeleted) && x.Index > MyEvent.Index && x.OldOrCurrentFilePathFull.Equals(MyEvent.OldOrCurrentFilePathFull));
                            if (TempEvent != null)
                            {
                                //Log(String.Format("File {0} was temporary", MyEvent.OldOrCurrentFilePathFull)); //Temporary file, do nothing about it
                                WorkingList.Remove(TempEvent);
                            }
                            else
                            {
                                //Remove multiple Changed events for the same file
                                TempSubList = WorkingList.FindAll((x => x.Action.Equals(BasicActionEnum.FileChanged) && x.OldOrCurrentFilePathFull.Equals(MyEvent.OldOrCurrentFilePathFull) && x.Index > MyEvent.Index));
                                if (TempSubList != null)
                                {
                                    foreach (FSSyncEvent xe in TempSubList) WorkingList.Remove(xe);
                                }

                                //Check what happened to this new file
                                TempFileChainStatus = BuildFileEventChain(MyEvent.OldOrCurrentFilePathFull, MyEvent.Index, WorkingList, ref TempFinalFileName);
                                if (TempFileChainStatus.Action.Equals(BasicActionEnum.FileRenamed))
                                {
                                    WorkingListFiltered.Add(new FSSyncEvent(BasicActionEnum.FileCreated, TempFinalFileName, "", TempFileChainStatus.ElementIndex));

                                    //Log(String.Format("File was created with name {0} and finally renamed to {1}", MyEvent.OldOrCurrentFilePathFull, TempFinalFileName));
                                    TempSubList = WorkingList.FindAll((x => x.Action.Equals(BasicActionEnum.FileChanged) && x.OldOrCurrentFilePathFull.Equals(TempFinalFileName) && x.Index > TempFileChainStatus.ElementIndex));
                                    if (TempSubList != null)
                                    {
                                        foreach (FSSyncEvent xe in TempSubList) WorkingList.Remove(xe);
                                    }
                                }
                                else if (TempFileChainStatus.Action.Equals(BasicActionEnum.FileDeleted))
                                {
                                    //Log(String.Format("File {0} was a temporary file",MyEvent.OldOrCurrentFilePathFull));
                                }
                                else
                                {
                                    WorkingListFiltered.Add(new FSSyncEvent(BasicActionEnum.FileCreated, MyEvent.OldOrCurrentFilePathFull, "", TempFileChainStatus.ElementIndex));
                                    //Log(String.Format("File {0} is new", MyEvent.OldOrCurrentFilePathFull));
                                }
                            }

                            #endregion

                            break;

                        case BasicActionEnum.FileChanged:

                            #region

                            //Is there are subsequent changes for this file, ignore them
                            WorkingList.RemoveAll((x => x.Action.Equals(BasicActionEnum.FileChanged) && x.Index > MyEvent.Index && x.OldOrCurrentFilePathFull.Equals(MyEvent.OldOrCurrentFilePathFull)));

                            //Check if the file is not deleted in a next step
                            TempEvent = WorkingList.Find(x => x.Action.Equals(BasicActionEnum.FileDeleted) && x.Index > MyEvent.Index && x.OldOrCurrentFileName.Equals(MyEvent.OldOrCurrentFileName));
                            if (TempEvent != null)
                            {
                                //Log(String.Format("File {0} was deleted", MyEvent.OldOrCurrentFilePathFull));
                                WorkingListFiltered.Add(new FSSyncEvent(BasicActionEnum.FileDeleted, MyEvent.OldOrCurrentFilePathFull, ""));
                                WorkingList.Remove(TempEvent);
                            }
                            else
                            {
                                //Log(String.Format("File {0} has changed", MyEvent.OldOrCurrentFilePathFull));
                                WorkingListFiltered.Add(new FSSyncEvent(BasicActionEnum.FileChanged, MyEvent.OldOrCurrentFilePathFull, ""));
                            }

                            #endregion

                            break;

                        case BasicActionEnum.FileDeleted:

                            #region 

                            //Check if there is a following Create for the same file name, means it has been moved
                            //TODO : Maybe Move detection should be made on more than the filename / can add last modification date. This requires to make a request to the VirtualRootFolder and get a FileInfo on the new file
                            TempEvent = WorkingList.Find(x => x.Action.Equals(BasicActionEnum.FileCreated) && x.Index > MyEvent.Index && x.OldOrCurrentFileName.Equals(MyEvent.OldOrCurrentFileName)); 
                            if (TempEvent != null)
                            {
                                if (MyEvent.OldOrCurrentFilePathFull.Equals(TempEvent.OldOrCurrentFilePathFull))
                                {
                                    //Log(String.Format("File {0} has been updated", MyEvent.OldOrCurrentFilePathFull)); //A file moved to the same location is an updated file and not a moved file
                                    WorkingListFiltered.Add(new FSSyncEvent(BasicActionEnum.FileChanged, MyEvent.OldOrCurrentFilePathFull, ""));
                                }
                                else
                                {
                                    //Log(String.Format("File {0} has moved to {1}", MyEvent.OldOrCurrentFilePathFull, TempEvent.OldOrCurrentFilePathFull));
                                    WorkingListFiltered.Add(new FSSyncEvent(BasicActionEnum.FileMoved, MyEvent.OldOrCurrentFilePathFull, TempEvent.OldOrCurrentFilePathFull));
                                }

                                WorkingList.Remove(TempEvent);
                            }
                            else
                            {
                                //Log(String.Format("File {0} has been deleted", MyEvent.OldOrCurrentFilePathFull));
                                WorkingListFiltered.Add(new FSSyncEvent(BasicActionEnum.FileDeleted, MyEvent.OldOrCurrentFilePathFull, ""));
                            }

                            #endregion

                            break;

                        case BasicActionEnum.FileRenamed:

                            #region

                            TempFileChainStatus = BuildFileEventChain(MyEvent.NewFilePathFull, MyEvent.Index, WorkingList, ref TempFinalFileName);
                            if (TempFileChainStatus.Action.Equals(BasicActionEnum.FileRenamed))
                            {
                                //Log(String.Format("File {0} was renamed to {1}", MyEvent.OldOrCurrentFilePathFull, TempFinalFileName));
                                WorkingListFiltered.Add(new FSSyncEvent(BasicActionEnum.FileRenamed, MyEvent.OldOrCurrentFilePathFull, TempFinalFileName, TempFileChainStatus.ElementIndex));
                            }
                            else if (TempFileChainStatus.Action.Equals(BasicActionEnum.FileDeleted))
                            {
                                //Log(String.Format("File {0} has been renammed and then deleted", MyEvent.OldOrCurrentFilePathFull));
                                WorkingListFiltered.Add(new FSSyncEvent(BasicActionEnum.FileDeleted, MyEvent.OldOrCurrentFilePathFull, "", TempFileChainStatus.ElementIndex));
                            }
                            else
                            {
                                //Log(String.Format("File {0} was renamed to {1}", MyEvent.OldOrCurrentFilePathFull, MyEvent.NewFilePathFull));
                                WorkingListFiltered.Add(new FSSyncEvent(BasicActionEnum.FileRenamed, MyEvent.OldOrCurrentFilePathFull, MyEvent.NewFilePathFull, TempFileChainStatus.ElementIndex));
                            }

                            #endregion

                            break;

                        default :
                            //Passthrough events
                            WorkingListFiltered.Add(MyEvent);
                            break;

                    }

                    WorkingList.Remove(MyEvent);
                }
            }

            #endregion

            #region Office documents handling

            for (int i = 0; i < WorkingListFiltered.Count; i++)
            {
                WorkingListFiltered[i].Index = i;
            }

            //Ms office changed documents will appear as Created and Deleted in the same round, detect this here 
            TempSubList = WorkingListFiltered.FindAll(x => x.Action.Equals(BasicActionEnum.FileCreated));
            for (int i = 0; i < TempSubList.Count; i++)
            {
                TempEvent = WorkingListFiltered.Find(x => x.Action.Equals(BasicActionEnum.FileDeleted) && x.Index > TempSubList[i].Index && x.OldOrCurrentFilePathFull.Equals(TempSubList[i].OldOrCurrentFilePathFull) && File.Exists(TempSubList[i].OldOrCurrentFilePathFull));
                if (TempEvent != null)
                {
                    //Office document upgrade detected
                    WorkingListFiltered.Add(new FSSyncEvent(BasicActionEnum.FileChanged, TempEvent.OldOrCurrentFilePathFull, ""));
                    WorkingListFiltered.Remove(TempEvent);
                    WorkingListFiltered.Remove(TempSubList[i]);
                }
            }

            //Ms office changed documents will appear as Deleted and Created in the same round, detect this here 
            for (int i = 0; i < WorkingListFiltered.Count; i++)
            {
                WorkingListFiltered[i].Index = i;
            }
            TempSubList = WorkingListFiltered.FindAll(x => x.Action.Equals(BasicActionEnum.FileDeleted));
            for (int i = 0; i < TempSubList.Count; i++)
            {
                TempEvent = WorkingListFiltered.Find(x => x.Action.Equals(BasicActionEnum.FileCreated) && x.Index > TempSubList[i].Index && x.OldOrCurrentFilePathFull.Equals(TempSubList[i].OldOrCurrentFilePathFull));
                if (TempEvent != null)
                {
                    //Office document upgrade detected
                    WorkingListFiltered.Add(new FSSyncEvent(BasicActionEnum.FileChanged, TempEvent.OldOrCurrentFilePathFull, ""));
                    WorkingListFiltered.Remove(TempEvent);
                    WorkingListFiltered.Remove(TempSubList[i]);
                }
            }

            //Remove Office Temporary files
            WorkingListFiltered.RemoveAll(x => x.Action.Equals(BasicActionEnum.FileCreated) && x.OldOrCurrentFileName.StartsWith("~"));
            WorkingListFiltered.RemoveAll(x => x.Action.Equals(BasicActionEnum.FileDeleted) && x.OldOrCurrentFileName.StartsWith("~"));
            WorkingListFiltered.RemoveAll(x => x.Action.Equals(BasicActionEnum.FileCreated) && x.OldOrCurrentFileName.StartsWith(".DS_Store"));
            WorkingListFiltered.RemoveAll(x => x.Action.Equals(BasicActionEnum.FileDeleted) && x.OldOrCurrentFileName.StartsWith(".DS_Store"));

            #endregion

            #region Dispatch Events

            WorkingListFiltered.Sort(); //Sort events. We do that so that detected file chain event do not occur before other events on which they depend (ex : file move before folder creation)

            if (WorkingListFiltered.Count > 0)
            {
                lock (FSIgnoreEventListLock)
                {
                    for (int i = 0; i < WorkingListFiltered.Count; i++)
                    {
                        switch (WorkingListFiltered[i].Action)
                        {
                            case BasicActionEnum.FileCreated:

                                #region

                                //Is this an event to be ignored ?
                                SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalCreate) > 0 && x.NewOrCurrentFilePathFull.Equals(WorkingListFiltered[i].OldOrCurrentFilePathFull));
                                if (SEII == null)
                                {
                                    OnLog(String.Format("[{0}] - {1}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName));
                                    OnEvent(new SyncEventItem(SyncEventEnum.LocalCreate, WorkingListFiltered[i].OldOrCurrentFilePathFull, false, false));
                                }
                                else
                                {
                                    OnLog(String.Format("[{0}][Ignored] - {1}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName));
                                    FSIgnoreEventList.Remove(SEII);
                                }

                                #endregion

                                break;

                            case BasicActionEnum.FileDeleted:

                                #region

                                //Is there a file with this name ?
                                TempVirtualFile = (VirtualFile)_RootVirtualFolder.FlatElementsGetElementBasedOnPath(GetRelativeFilePath(WorkingListFiltered[i].OldOrCurrentFilePathFull), VirtualElementType.File, false);
                                if (TempVirtualFile != null)
                                {
                                    //Is this an event to be ignored ?
                                    SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalDelete) > 0 && x.NewOrCurrentFilePathFull.Equals(WorkingListFiltered[i].OldOrCurrentFilePathFull));
                                    if (SEII == null)
                                    {
                                        OnLog(String.Format("[{0}] - {1}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName));
                                        OnEvent(new SyncEventItem(SyncEventEnum.LocalDelete, TempVirtualFile.ElementId, false, false));
                                    }
                                    else
                                    {
                                        OnLog(String.Format("[{0}][Ignored] - {1}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName));
                                        FSIgnoreEventList.Remove(SEII);
                                    }
                                }
                                //Obsolete
                                //else
                                //{
                                //    //This is a folder
                                //    var TempVirtualFileList = _RootVirtualFolder.FindAllFilesBasedOnFolder(GetRelativeFilePath(FilteredList[i].OldOrCurrentFilePathFull) + @"\");
                                //    for (int k = 0; k < TempVirtualFileList.Count; k++)
                                //    {
                                //        SubTempVirtualFile = _RootVirtualFolder.FindFileBasedOnPath(TempVirtualFileList[k].FilePathRelative);
                                //        if (SubTempVirtualFile != null)
                                //        {
                                //            OnLog(String.Format("Found file {0}", SubTempVirtualFile.FilePathRelative));
                                //            OnDeleted(new SyncEventItem(SyncEventEnum.LocalDelete, SubTempVirtualFile.FileId));
                                //        }
                                //    }
                                //}
                                #endregion

                                break;

                            case BasicActionEnum.FileMoved:

                                #region

                                //Is this an event to be ignored ?
                                SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalMove) > 0 && x.NewOrCurrentFilePathFull.Equals(WorkingListFiltered[i].NewFilePathFull));
                                if (SEII == null)
                                {
                                    OnLog(String.Format("[{0}] - {1} -> {2}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName, WorkingListFiltered[i].NewFileName));
                                    OnEvent(new SyncEventItem(SyncEventEnum.LocalMove, Guid.Empty, WorkingListFiltered[i].NewFilePathFull, WorkingListFiltered[i].OldOrCurrentFilePathFull, false, false));
                                }
                                else
                                {
                                    OnLog(String.Format("[{0}][Ignored] - {1} -> {2}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName, WorkingListFiltered[i].NewFileName));
                                    FSIgnoreEventList.Remove(SEII);
                                }

                                #endregion

                                break;

                            case BasicActionEnum.FileRenamed:

                                #region

                                //Is there a file with this name ?
                                TempVirtualFile = (VirtualFile)_RootVirtualFolder.FlatElementsGetElementBasedOnPath(GetRelativeFilePath(WorkingListFiltered[i].OldOrCurrentFilePathFull), VirtualElementType.File, false);
                                if (TempVirtualFile != null)
                                {
                                    //Is this an event to be ignored ?
                                    SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalRename) > 0 && x.NewOrCurrentFilePathFull.Equals(WorkingListFiltered[i].NewFilePathFull));
                                    if (SEII == null)
                                    {
                                        OnLog(String.Format("[{0}] - {1} -> {2}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName, WorkingListFiltered[i].NewFileName));
                                        OnEvent(new SyncEventItem(SyncEventEnum.LocalRename, WorkingListFiltered[i].NewFilePathFull, WorkingListFiltered[i].OldOrCurrentFilePathFull, false, false));
                                    }
                                    else
                                    {
                                        OnLog(String.Format("[{0}][Ignored] - {1} -> {2}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName, WorkingListFiltered[i].NewFileName));
                                        FSIgnoreEventList.Remove(SEII);
                                    }
                                }
                                //Obsolete
                                //else
                                //{
                                //    //This is a folder
                                //    var TempVirtualFileList = _RootVirtualFolder.FindAllFilesBasedOnFolder(GetRelativeFilePath(FilteredList[i].OldOrCurrentFilePathFull) + @"\");
                                //    for (int k = 0; k < TempVirtualFileList.Count; k++)
                                //    {
                                //        SubTempVirtualFile = _RootVirtualFolder.FindFileBasedOnPath(TempVirtualFileList[k].FilePathRelative);
                                //        if (SubTempVirtualFile != null)
                                //        {
                                //            OnLog(String.Format("Found file {0}", SubTempVirtualFile.FilePathRelative));
                                //            OnRenamed(new SyncEventItem(SyncEventEnum.LocalRename, Guid.Empty, SubTempVirtualFile.FilePathRelative.Replace(GetRelativeFilePath(FilteredList[i].OldOrCurrentFilePathFull), GetRelativeFilePath(FilteredList[i].NewFilePathFull)), SubTempVirtualFile.FilePathRelative));
                                //        }
                                //    }
                                //}

                                #endregion

                                break;

                            case BasicActionEnum.FileChanged:

                                #region

                                //Is this an event to be ignored ?
                                SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalUpdate) > 0 && x.NewOrCurrentFilePathFull.Equals(WorkingListFiltered[i].OldOrCurrentFilePathFull));
                                if (SEII == null)
                                {
                                    OnLog(String.Format("[{0}] - {1}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName));
                                    if (File.Exists(WorkingListFiltered[i].OldOrCurrentFilePathFull)) OnEvent(new SyncEventItem(SyncEventEnum.LocalUpdate, WorkingListFiltered[i].OldOrCurrentFilePathFull, false, false));
                                }
                                else
                                {
                                    OnLog(String.Format("[{0}][Ignored] - {1}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName));
                                    FSIgnoreEventList.Remove(SEII);
                                }

                                #endregion

                                break;

                            case BasicActionEnum.DirCreated:

                                SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalCreate) > 0 && x.NewOrCurrentFilePathFull.Equals(WorkingListFiltered[i].OldOrCurrentFilePathFull));
                                if (SEII == null)
                                {
                                    OnLog(String.Format("[{0}] - {1}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName));
                                    OnEvent(new SyncEventItem(SyncEventEnum.LocalCreate, WorkingListFiltered[i].OldOrCurrentFilePathFull, true, false));
                                }
                                else
                                {
                                    OnLog(String.Format("[{0}][Ignored] - {1}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName));
                                    FSIgnoreEventList.Remove(SEII);
                                }

                                break;

                            case BasicActionEnum.DirRenamed:

                                SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalRename) > 0 && x.NewOrCurrentFilePathFull.Equals(WorkingListFiltered[i].NewFilePathFull));
                                if (SEII == null)
                                {
                                    OnLog(String.Format("[{0}] - {1}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName));
                                    OnEvent(new SyncEventItem(SyncEventEnum.LocalRename, WorkingListFiltered[i].NewFilePathFull, WorkingListFiltered[i].OldOrCurrentFilePathFull, true, false));
                                }
                                else
                                {
                                    OnLog(String.Format("[{0}][Ignored] - {1}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName));
                                    FSIgnoreEventList.Remove(SEII);
                                }

                                break;

                            case BasicActionEnum.DirDeleted:

                                SEII = FSIgnoreEventList.Find(x => (x.Event & SyncEventEnum.LocalDelete) > 0 && x.NewOrCurrentFilePathFull.Equals(WorkingListFiltered[i].OldOrCurrentFilePathFull));
                                if (SEII == null)
                                {
                                    OnLog(String.Format("[{0}] - {1}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName));
                                    OnEvent(new SyncEventItem(SyncEventEnum.LocalDelete, WorkingListFiltered[i].OldOrCurrentFilePathFull, true, false));
                                }
                                else
                                {
                                    OnLog(String.Format("[{0}][Ignored] - {1}", WorkingListFiltered[i].Action.ToString(), WorkingListFiltered[i].OldOrCurrentFileName));
                                    FSIgnoreEventList.Remove(SEII);
                                }

                                break;
                        }
                    }
                }
            }

            #endregion
        }

        /// <summary>
        /// Permet de déterminer dans une liste d'événements, ce qui est arrivé à un fichier en prenant en compte tous les événements liés à ce fichier
        /// (cela permet de bypasser tout un tas d'événements intermédiaires)
        /// </summary>
        /// <param name="FilePath"></param>
        /// <param name="CurrentIndex"></param>
        /// <param name="WorkingList"></param>
        /// <param name="FinalFileName"></param>
        /// <returns></returns>
        private EventFileChainResult BuildFileEventChain(string FilePath, Int32 CurrentIndex, List<FSSyncEvent> WorkingList, ref string FinalFileName)
        {
            EventFileChainResult ReturnValue = new EventFileChainResult() { Action = BasicActionEnum.None, ElementIndex = 0 };
            EventFileChainResult RecursiveResult = new EventFileChainResult() { Action = BasicActionEnum.None, ElementIndex = 0 };
            FSSyncEvent TempEvent;

            //Has the file been renamed
            TempEvent = WorkingList.Find(x => x.Action.Equals(BasicActionEnum.FileRenamed) && x.OldOrCurrentFilePathFull.Equals(FilePath) && x.Index > CurrentIndex);
            if (TempEvent != null)
            {
                //Log(String.Format("****File {0} was renamed {1}", FilePath, TempEvent.NewFilePath));
                FinalFileName = TempEvent.NewFilePathFull;
                ReturnValue = new EventFileChainResult() { Action = BasicActionEnum.FileRenamed, ElementIndex = TempEvent.Index };
                RecursiveResult = BuildFileEventChain(TempEvent.NewFilePathFull, TempEvent.Index, WorkingList, ref FinalFileName);
                if (RecursiveResult.Action != BasicActionEnum.None) ReturnValue = RecursiveResult;
                WorkingList.Remove(TempEvent);
            }

            //Has the file been deleted
            TempEvent = WorkingList.Find(x => x.Action.Equals(BasicActionEnum.FileDeleted) && x.OldOrCurrentFilePathFull.Equals(FilePath) && x.Index > CurrentIndex);
            if (TempEvent != null)
            {
                ReturnValue = new EventFileChainResult() { Action = BasicActionEnum.FileDeleted, ElementIndex = TempEvent.Index };
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

    public class EventFileChainResult
    {
        public BasicActionEnum Action { get;set;}
        public Int32 ElementIndex { get; set; }
    }
}