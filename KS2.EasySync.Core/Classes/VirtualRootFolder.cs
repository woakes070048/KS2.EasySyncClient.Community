/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using System;
using System.Collections.Generic;
#if __MonoCS__
	using System.IO;
#else
using Alphaleonis.Win32.Filesystem;
#endif
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace KS2.EasySync.Core
{
    /// <summary>
    /// Contains the virtual counterpart of the physical file structure
    /// </summary>
    public class VirtualRootFolder : VirtualElement
    {
        private Int32 _RepositoryId;
        public delegate void SyncFSEvent(object sender, SyncEventItem e);
        public delegate void SyncFSLogEvent(object sender, string s);
        public event SyncFSEvent Event;
        private static object FlatElementsLock = new object();
        private List<VirtualElement> FlatElements = new List<VirtualElement>();

        public event SyncFSLogEvent Log;

        protected virtual void OnEvent(SyncEventItem e)
        {
            if (Event != null) Event(this, e);
        }

        protected virtual void OnLog(string MessageToLog)
        {
            if (Log != null) Log(this, MessageToLog);
        }

        /// <summary>
        /// Root Folder
        /// </summary>
        /// <param name="pRepositoryId"></param>
        public VirtualRootFolder(Int32 pRepositoryId) : base(VirtualElementType.RootFolder, Guid.Empty, "")
        {
            this._RepositoryId = pRepositoryId;
            this.FlatElementAdd(this);
        }

        public bool ReloadDataFromDatabase()
        {
            SQLiteHelper oSQLHelper = new SQLiteHelper(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.FullName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name, null);
            if (!oSQLHelper.InitConnection()) return false;

#if __MonoCS__
            Mono.Data.Sqlite.SqliteDataReader SqlDataReader;
#else
            System.Data.SQLite.SQLiteDataReader SqlDataReader;
#endif

            OnLog("Reload from database start");

            OnLog("Folders");

            //Get Folders
            oSQLHelper.SetCommandText("SELECT Id_Folder, Name, RelativePath, RemoteID, CustomInfo FROM VFolder WHERE Fk_Repository = " + this._RepositoryId + " ORDER BY LENGTH(RelativePath)");
            SqlDataReader = oSQLHelper.ExecuteReader();
            while (SqlDataReader.Read())
            {
                //Is there a known parent folder ?
                String CurrentPath = SqlDataReader["RelativePath"].ToString();
                String ParentPath = Tools.GetParentPath(CurrentPath);

                VirtualFolder VF;

                VF = new VirtualFolder(Guid.Parse(SqlDataReader["Id_Folder"].ToString()),
                       CurrentPath,
                       SqlDataReader["RemoteID"].ToString(),
                       SqlDataReader["CustomInfo"].ToString());

                if (ParentPath.Equals(""))
                {
                    this.SubElementAdd(VF);
                    this.FlatElementAdd(VF);
                }
                else
                {
                    VirtualFolder ParentFolder = (VirtualFolder)this.FlatElementsGetElementBasedOnPath(ParentPath, VirtualElementType.Folder, false);
                    if (ParentFolder == null)
                    {
                        OnLog(String.Format("Parent folder of folder {0} is missing. Database is corrupted. Bypassing Element", ParentPath));
                        //TODO : Delete folder from database
                    }
                    else
                    {
                        ParentFolder.SubElementAdd(VF);
                        this.FlatElementAdd(VF);
                    }
                }
            }
            SqlDataReader.Close();

            OnLog("Files");

            oSQLHelper.SetCommandText("SELECT Id_File, Fk_ParentFolder, Name, RelativePath, RemoteID, Hash, CustomInfo FROM VFile WHERE Fk_Repository = " + this._RepositoryId);
            SqlDataReader = oSQLHelper.ExecuteReader();
            while (SqlDataReader.Read())
            {
                //Is there a known parent folder ?
                String CurrentPath = SqlDataReader["RelativePath"].ToString();
                //String ParentPath = Tools.GetParentPath(CurrentPath);
                Guid ParentFolderId = Guid.Parse(SqlDataReader["Fk_ParentFolder"].ToString());

                VirtualFile VF;
                VF = new VirtualFile(Guid.Parse(SqlDataReader["Id_File"].ToString()),
                       CurrentPath,
                       SqlDataReader["RemoteID"].ToString(),
                       SqlDataReader["Hash"].ToString(),
                       SqlDataReader["CustomInfo"].ToString());

                if (ParentFolderId == Guid.Empty)
                {
                    this.SubElementAdd(VF);
                    this.FlatElementAdd(VF);
                }
                else
                {
                    VirtualFolder ParentFolder = (VirtualFolder)this.FlatElementsGetElementBasedOnId(ParentFolderId, VirtualElementType.Folder, false);

                    if (ParentFolder == null)
                    {
                        OnLog(String.Format("Parent folder with id {0} of file {1} is missing. Database is corrupted. Bypassing Element", ParentFolderId, CurrentPath));
                        //TODO : Delete file from database
                    }
                    else
                    {
                        ParentFolder.SubElementAdd(VF);
                        this.FlatElementAdd(VF);
                    }
                }
            }

            SqlDataReader.Close();
            oSQLHelper.Dispose();

            OnLog("Reload from database end");
            OnLog(String.Format("Found {0} folder(s) and {1} file(s)",this.FlatElementsGetAllFolders().Count, this.FlatElementsGetAllFiles().Count));

            return true;
        }

        /// <summary>
        /// Compare Physical Files with Virtual Files
        /// </summary>
        /// <param name="pPhysicalRootFolder"></param>
        internal void Compare(PhysicalRootFolder pPhysicalRootFolder)
        {
            //Note : TempPhysicalFile.PhysicalFileFileHash may be empty if the file was locked

            OnLog("Detect Changes since last Run");

            #region Folders

            #region Matching Folders

            OnLog("Detecting matching folders start based on path");

            //Looking for matching folders (based on path)
            foreach(VirtualElement VE in this.FlatElementsGetAllFolders())
            {
                PhysicalFolder TempPhysicalFolder = pPhysicalRootFolder._Folders.FirstOrDefault(x => x.RelativeFolderPath.Equals(VE.PathRelative, StringComparison.OrdinalIgnoreCase) && !x.bIdentified);
                if (TempPhysicalFolder != null)
                {
                    //The local folder path is still existing
                    TempPhysicalFolder.bIdentified = true;
                    VE.bIdentified = true;
                }
            }

            OnLog("Detecting matching folders end");

            #endregion

            #region Deleted Folders

            OnLog("Detecting deleted folders start");

            //Detect deleted folders ( by default, they are sorted from deepest to nearest)
            foreach (VirtualElement VE in this.FlatElementsGetAllFolders())
            {
                if (!VE.bIdentified)
                {
                    var SEI = new SyncEventItem(SyncEventEnum.LocalDelete, VE.ElementId, true, true);
                    OnEvent(SEI);
                    OnLog(String.Format("Local folder is present in the Local DB but missing physically : {0}. LocalDelete Event : {1}", VE.PathRelative, SEI.SyncEventId));
                }
            }

            OnLog("Detecting deleted folders end");

            #endregion

            #region Detect new folders

            OnLog("Detecting created folders start");

            //Sort folders from nearest to deepest (we create nearest folder first)
            pPhysicalRootFolder._Folders.Sort((x, y) => x._FullFolderPath.Count(z => z == Path.DirectorySeparatorChar).CompareTo(y._FullFolderPath.Count(z => z == Path.DirectorySeparatorChar)));

            for (int i = 0; i < pPhysicalRootFolder._Folders.Count; i++)
            {
                if (!pPhysicalRootFolder._Folders[i].bIdentified)
                {
                    pPhysicalRootFolder._Folders[i].bIdentified = true;
                    OnEvent(new SyncEventItem(SyncEventEnum.LocalCreate, pPhysicalRootFolder._Folders[i]._FullFolderPath, true, true));
                    OnLog(String.Format("Local folder created {0}", pPhysicalRootFolder._Folders[i].RelativeFolderPath));
                }
            }

            OnLog("Detecting created folders end");

            #endregion

            #endregion

            #region Files

            OnLog("Detecting matching files start based on path");

            //Looking for matching files (physical vs virtual)
            foreach (VirtualElement VE in this.FlatElementsGetAllFiles())
            {
                VirtualFile VF = (VirtualFile)VE;

                //Identify files based on their path
                PhysicalFile TempPhysicalFile = pPhysicalRootFolder._Files.FirstOrDefault(x => x.FileRelativePath.Equals(VF.PathRelative, StringComparison.OrdinalIgnoreCase) && !x.bIdentified);
                if (TempPhysicalFile != null)
                {
                    if (VF.FileHash.Equals(TempPhysicalFile.FileHash, StringComparison.OrdinalIgnoreCase))
                    {
                        VF.bIdentified = true;
                        TempPhysicalFile.bIdentified = true;
                    }
                    else
                    {
                        //Physical file has changed or is locked
                        //Update Virtual file => the sync process will process it as it has changed
                        VF.bIdentified = true;
                        TempPhysicalFile.bIdentified = true;
                        var SEI = new SyncEventItem(SyncEventEnum.LocalUpdate, VF.ElementId, false, true);
                        OnEvent(SEI);
                        OnLog(String.Format("Local file {0} has been updated -> Event {1}", TempPhysicalFile._FileFullPath, SEI.SyncEventId));
                    }
                }
            }

            OnLog("Detecting matching files end");

            //Note : Impossible to identify renamed + modified files (to do this, we would need to use AlternateDataStreams)

            OnLog("Performing file comparison start");

            //Looking for Virtual files not identified => there is no physical file where there was one => deleted, moved or renamed file
            foreach (VirtualElement VE in this.FlatElementsGetAllFiles())
            {
                VirtualFile VF = (VirtualFile)VE;
                if (!VF.bIdentified)
                {
                    PhysicalFile TempPhysicalFile;

                    OnLog(String.Format("Local file is present in the Local DB but missing physically : {0}", VF.PathRelative));

                    //Look if the file hasn't been renamed in the same folder
                    TempPhysicalFile = pPhysicalRootFolder._Files.FirstOrDefault(x => x.FileRelativeDirectory.Equals(Tools.GetParentPath(VF.PathRelative), StringComparison.OrdinalIgnoreCase) && x.FileHash.Equals(VF.FileHash, StringComparison.OrdinalIgnoreCase) && !x.bIdentified);
                    if (TempPhysicalFile != null)
                    {
                        OnLog(String.Format("Local file has been renamed to {0}", TempPhysicalFile._FileFullPath));
                        OnEvent(new SyncEventItem(SyncEventEnum.LocalRename, VF.ElementId, TempPhysicalFile.FileRelativePath, VF.PathRelative, false, true));
                        VF.bIdentified = true;
                        TempPhysicalFile.bIdentified = true;
                    }
                    else
                    {
                        //Look if the file hasn't been moved to somewhere. File with same name and same hash not already identified
                        TempPhysicalFile = pPhysicalRootFolder._Files.FirstOrDefault(x => Path.GetFileName(x._FileFullPath).Equals(VF.CurrentName, StringComparison.OrdinalIgnoreCase) && x.FileHash.Equals(VF.FileHash, StringComparison.OrdinalIgnoreCase) && !x.bIdentified);
                        if (TempPhysicalFile != null)
                        {
                            OnLog(String.Format("Local file has been moved to {0}", TempPhysicalFile._FileFullPath));
                            OnEvent(new SyncEventItem(SyncEventEnum.LocalMove, VF.ElementId, TempPhysicalFile.FileRelativePath, VF.PathRelative, false, true));
                            VF.bIdentified = true;
                            TempPhysicalFile.bIdentified = true;
                        }
                        else
                        {
                            //Note : We don't limit the rename check on same folder only

                            //Look if the file hasn't been renamed based on the hash
                            TempPhysicalFile = pPhysicalRootFolder._Files.FirstOrDefault(x => { return x.FileHash.Equals(VF.FileHash, StringComparison.OrdinalIgnoreCase) && !x.bIdentified; });
                            if (TempPhysicalFile != null)
                            {
                                OnLog(String.Format("Local file has been moved and renamed to {0}", TempPhysicalFile._FileFullPath));
                                OnEvent(new SyncEventItem(SyncEventEnum.LocalMoveAndRename, VF.ElementId, TempPhysicalFile.FileRelativePath, VF.PathRelative, false, true));
                                VF.bIdentified = true;
                                TempPhysicalFile.bIdentified = true;
                            }
                            else
                            {
                                OnLog(String.Format("Local file has been deleted", VF.PathRelative));
                                OnEvent(new SyncEventItem(SyncEventEnum.LocalDelete, VF.ElementId, false, true));
                                VF.bIdentified = true;
                            }
                        }
                    }
                }
            }

            //Looking for Physical files not identified => this file was not in the virtual list
            //=> New, moved or renamed file
            for (int i = 0; i < pPhysicalRootFolder._Files.Count; i++)
            {
                if (!pPhysicalRootFolder._Files[i].bIdentified)
                {
                    OnLog(String.Format("New local file {0}", pPhysicalRootFolder._Files[i].FileRelativePath));
                    OnEvent(new SyncEventItem(SyncEventEnum.LocalCreate, pPhysicalRootFolder._Files[i]._FileFullPath, false, true));
                }
            }

            OnLog("Performing file comparison end");

            #endregion

            OnLog("Done");
        }

        /// <summary>
        /// Trouve un élément basé sur son ID
        /// </summary>
        /// <param name="pElementId"></param>
        /// <param name="IncludeDeleted"></param>
        /// <returns></returns>
        public VirtualElement FlatElementGetBasedOnId(Guid pElementId, bool IncludeDeleted)
        {
            lock (FlatElementsLock)
            {
                if (IncludeDeleted) return this.FlatElements.FirstOrDefault(x => x.ElementId.Equals(pElementId));
                else return this.FlatElements.FirstOrDefault(x => x.ElementId.Equals(pElementId) && !x.IsDeleted);
            }
        }

        /// <summary>
        /// Trouve un élément basé sur son RemoteID
        /// </summary>
        /// <param name="pRemoteId"></param>
        /// <param name="IncludeDeleted"></param>
        /// <returns></returns>
        public VirtualElement FlatElementGetBasedOnRemoteId(String pRemoteId, bool IncludeDeleted)
        {
            lock (FlatElementsLock)
            {
                if (IncludeDeleted) return this.FlatElements.FirstOrDefault(x => x.RemoteID.Equals(pRemoteId, StringComparison.OrdinalIgnoreCase));
                else return this.FlatElements.FirstOrDefault(x => x.RemoteID.Equals(pRemoteId, StringComparison.OrdinalIgnoreCase) && !x.IsDeleted);
            }
        }

        public VirtualElement FlatElementsGetElementBasedOnId(Guid ElementId, VirtualElementType ElementType,  bool IncludeDeleted)
        {
            lock (FlatElementsLock)
            {
                if (IncludeDeleted) return this.FlatElements.FirstOrDefault(x => x.ElementId.Equals(ElementId) && x.ElementType == ElementType);
                else return this.FlatElements.FirstOrDefault(x => x.ElementId.Equals(ElementId) && !x.IsDeleted && x.ElementType == ElementType);
            }
        }

        public VirtualElement FlatElementsGetElementBasedOnPath(String RelativePath, VirtualElementType ElementType, bool IncludeDeleted, bool SearchTargetNameFirst = false)
        {
            lock (FlatElementsLock)
            {
                if (String.IsNullOrEmpty(RelativePath))
                {
                    return (VirtualElement)this;
                }
                else
                {
                    VirtualElement VE = null;

                    if (SearchTargetNameFirst)
                    {
                        if (IncludeDeleted) VE = this.FlatElements.FirstOrDefault(x => x.TargetPathRelativeAfterSync.Equals(RelativePath, StringComparison.OrdinalIgnoreCase) && x.ElementType == ElementType);
                        else VE = this.FlatElements.FirstOrDefault(x => x.TargetPathRelativeAfterSync.Equals(RelativePath, StringComparison.OrdinalIgnoreCase) && !x.IsDeleted && x.ElementType == ElementType);

                        if (VE != null) return VE;
                    }

                    if (IncludeDeleted) VE = this.FlatElements.FirstOrDefault(x => x.PathRelative.Equals(RelativePath, StringComparison.OrdinalIgnoreCase) && x.ElementType == ElementType);
                    else VE = this.FlatElements.FirstOrDefault(x => x.PathRelative.Equals(RelativePath, StringComparison.OrdinalIgnoreCase) && !x.IsDeleted && x.ElementType == ElementType);

                    return VE;
                }
            }
        }

        public void FlatElementAdd(VirtualElement VE)
        {
            lock (FlatElementsLock)
            {
                this.FlatElements.Add(VE);
            }
        }

        public void FlatElementRemove(Guid pElementId)
        {
            lock (FlatElementsLock)
            {
                this.FlatElements.RemoveAll(x => x.ElementId == pElementId);
            }
        }

        public List<VirtualElement> FlatElementsGetAll()
        {
            lock (FlatElementsLock)
            {
                return this.FlatElements;
            }
        }

        public List<VirtualElement> FlatElementsGetAllFiles()
        {
            lock (FlatElementsLock)
            {
                return this.FlatElements.Where(x => x.ElementType == VirtualElementType.File).ToList();
            }
        }

        public List<VirtualElement> FlatElementsGetAllFolders()
        {
            lock (FlatElementsLock)
            {
                return this.FlatElements.Where(x => x.ElementType == VirtualElementType.Folder).ToList();
            }
        }

        /// <summary>
        /// Retourne la liste des actions dans l'ordre sans ternir compte d'éventuels postpone
        /// </summary>
        /// <returns></returns>
        public List<VirtualElement> FlatElementsGetActionsSortedByDate()
        {
            lock (FlatElementsLock)
            {
                return this.FlatElements.Where(x => x.NextActionDate != null).OrderBy(x => x.NextActionDate.Value).ToList();
            }
        }
    }
}

/*
public VirtualFile FindFileBasedOnPath(string FilePathRelative)
{
    lock (VirtualFileLock)
    {
        return _VirtualFiles.Find(x => x.PathRelative.Equals(FilePathRelative));
    }
}

public VirtualFolder FindFolderBasedOnID(Guid FolderId)
{
    lock (VirtualFileLock)
    {
        return this._VirtualFolders.Find(x => x.FolderId.Equals(FolderId));
    }
}

public VirtualFile FindFileBasedOnID(Guid FileId)
{
    lock (VirtualFileLock)
    {
        return _VirtualFiles.Find(x => x.FileId.Equals(FileId));
    }
}

public VirtualFile FindFileBasedOnRemoteID(String FileRemoteId)
{
    lock (VirtualFileLock)
    {
        return _VirtualFiles.Find(x => x.RemoteID.Equals(FileRemoteId));
    }
}

public List<VirtualFile> FindFilesInFolder(String FolderPath)
{
    lock (VirtualFileLock)
    {
        return _VirtualFiles.Where(x => x.PathRelative.StartsWith(FolderPath)).ToList();
    }
}

public List<VirtualFolder> FindAllFoldersBasedOnFolder(String FolderPath)
{
    lock (VirtualFileLock)
    {

        return _VirtualFolders.Where(x => x.PathRelative.StartsWith(FolderPath+Path.DirectorySeparatorChar)).ToList();
    }
}

/// <summary>
/// Get the list of files located in the folder
/// </summary>
/// <param name="FolderPathRelative"></param>
/// <returns></returns>
public List<VirtualFile> FindAllFilesBasedOnFolder(string FolderPathRelative)
{
    lock (VirtualFileLock)
    {
        return _VirtualFiles.FindAll(x => x.PathRelative.StartsWith(FolderPathRelative));
    }
}

public void AddFileToFolder(VirtualFile f)
{
    lock (VirtualFileLock)
    {
        _VirtualFiles.Add(f);
    }
}

public void AddFolderToFolder(VirtualFolder f)
{
    lock (VirtualFileLock)
    {
        this._VirtualFolders.Add(f);
    }
}

/// <summary>
/// Save the whole object in database
/// </summary>
public void Serialize()
{
    lock (VirtualFileLock)
    {
        SqlCeConnection con = new SqlCeConnection(Globals._GlbConnectionString);
        con.Open();

        SqlCeCommand oSqlCeCommand = new SqlCeCommand(String.Format("DELETE VFolder WHERE Fk_Repository = {0}",_RepositoryId), con);
        oSqlCeCommand.ExecuteNonQuery();

        oSqlCeCommand.CommandText = String.Format("DELETE VFile WHERE Fk_Repository = {0}",_RepositoryId);
        oSqlCeCommand.ExecuteNonQuery();

        con.Close();

        //Serialize SubFolders
        for (int i = 0; i < this._VirtualFolders.Count(); i++)
        {
            Globals.VirtualFolderSerialize(this._VirtualFolders[i], _RepositoryId);
        }

        //Serialize files
        for (int i = 0; i < this._VirtualFiles.Count(); i++)
        {
            Globals.VirtualFileSerialize(this._VirtualFiles[i], _RepositoryId);
        }
    }
}
        
/// <summary>
/// Delete a serialized file from the database
/// </summary>
/// <param name="FileId"></param>
public void DeleteFile(Guid FileId)
{
    RemoveFileFromFolder(FileId);

    SqlCeConnection con = new SqlCeConnection(Globals._GlbConnectionString);
    con.Open();

    SqlCeCommand oSqlCeCommand = new SqlCeCommand("DELETE VFile WHERE Id_File = '" + FileId.ToString() + "'", con);
    oSqlCeCommand.ExecuteNonQuery();

    con.Close();
}
*/

/*
/// <summary>
/// Remove the file from the list of files
/// </summary>
/// <param name="v"></param>
public void RemoveFileFromFolder(Guid FileId)
{
    lock (VirtualFileLock)
    {
        VirtualFile VF = this._VirtualFiles.FirstOrDefault(x => x.FileId.Equals(FileId));
        VF.Dispose();
        this._VirtualFiles.Remove(VF);
    }
}

public void RemoveFolderFromFolder(Guid FolderId)
{
    lock (VirtualFileLock)
    {
        VirtualFolder VF = this._VirtualFolders.FirstOrDefault(x => x.FolderId.Equals(FolderId));
        VF.Dispose();
        this._VirtualFolders.Remove(VF);
    }
}

public void MoveFile(Guid FileId, string NewPath)
{
    lock (VirtualFileLock)
    {
        this._VirtualFiles.Find(x => x.FileId.Equals(FileId)).PathRelative = NewPath;
    }
}
*/