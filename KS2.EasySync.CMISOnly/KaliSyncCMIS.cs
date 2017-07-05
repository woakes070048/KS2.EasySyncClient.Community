/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotCMIS;
using DotCMIS.Client;
using DotCMIS.Client.Impl;
using System.Threading;
using System.Threading.Tasks;
using DotCMIS.Data;
using DotCMIS.Data.Impl;
#if __MonoCS__
	using System.IO;
#else
using Alphaleonis.Win32.Filesystem;
#endif
using System.IO.Compression;
using System.Globalization;
using NLog;
using System.Net;
using System.Xml;
using System.Xml.Serialization;
using KS2.EasySync.Core;
using Newtonsoft.Json;
using DotCMIS.Data.Extensions;

//http://chemistry.apache.org/java/examples/example-connect-dotnet.html

namespace KS2.EasySync.CMISOnly
{
    public class Connector : IEasySyncPlugin
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public event EventHandler AuthenticationError;
        public event EventHandler ProxyError;

        private object _FolderCreateLock = new object();
        private object _DeleteLock = new object();

        private bool IsLockedForDeletion;
        private bool IsLockedForCreation;

        private const Int32 LOCK_WAIT_TIME = 20; //Delay in second to wait for a file to be unlocked

        private Guid _InstanceId;
        private IPluginHost _Host;

        private string _ServiceURL;
        private string _Login;
        private string _Password;
        private string _DocumentLibraryPath;

        private HostPlatform _HostOS;

        public event LogEventHandler LogOutput;

        private ISession session; //Same session used by all methods
        private String SiteDocumentLibraryFolderId = null;

        public Connector()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    _HostOS = HostPlatform.Windows;
                    logger.Debug("Detected operating system : Windows");
                    break;
                case PlatformID.MacOSX:
                    _HostOS = HostPlatform.MacOsX;
                    logger.Debug("Detected operating system : MacOSX");
                    break;
                case PlatformID.Unix:
                    _HostOS = HostPlatform.Unix;
                    logger.Debug("Detected operating system : Unix");
                    break;
                default:
                    _HostOS = HostPlatform.Unknown;
                    logger.Debug("Unknown operating system");
                    break;
            }
        }

        ~Connector()
        {
            if (session != null)
            {
                try
                {
                    session.Clear();
                }
                catch
                {
                }
            }
        }

        #region Interface implementation

        public void Init(string RemoteRepositoryParameters)
        {
            var CP = ConnectorParameter.Deserialize(RemoteRepositoryParameters);
            _ServiceURL = CP.Server;
            _Login = CP.Login;
            _Password = CP.Password;
            _DocumentLibraryPath = CP.SitePath;
        }

        public void SetProxyParameter(short ProxyMode, string ProxyURL, bool ProxyAuthentication, string ProxyLogin, string ProxyPassword)
        {
            DotCMIS.ProxyParameters.GlbProxyMode = ProxyMode;
            DotCMIS.ProxyParameters.GlbProxyURL = ProxyURL;
            DotCMIS.ProxyParameters.GlbProxyAuthentication = ProxyAuthentication;
            DotCMIS.ProxyParameters.GlbProxyLogin = ProxyLogin;
            DotCMIS.ProxyParameters.GlbProxyPassword = ProxyPassword;
        }

        public void LinkToEngine(IPluginHost host, Guid InstanceId)
        {
            this._Host = host;
            this._InstanceId = InstanceId;
        }

        public string GetLogin()
        {
            return _Login;
        }

        public string GetPassword()
        {
            return _Password;
        }

        public string GetEndPoint()
        {
            return _ServiceURL;
        }

        public void SetNewCredentials(string NewLogin, string NewPassword)
        {
            this._Login = NewLogin;
            this._Password = NewPassword;
        }

        public ActionResult GetFileList(Guid pInstanceId, bool pIsFirstComparerCall, ref List<RepositoryElement> RepositoryFileList)
        {
            bool InvalidResponse = false;

            if (session == null && !GetServiceSession(ref session, ref SiteDocumentLibraryFolderId)) return ActionResult.Retry;

            Log("KS2.EasySync.CMISOnly - GetFileList Started");

            foreach (var Element in session.Query(@"SELECT alfcmis:nodeRef,cmis:path,cmis:name FROM cmis:folder WHERE IN_TREE('" + SiteDocumentLibraryFolderId + "')", false).ToList())
            {
                RepositoryElement r = new RepositoryElement()
                {
                    ElementType = RepositoryElementType.Folder,
                    ElementId = Element["alfcmis:nodeRef"].FirstValue.ToString(),
                    PathRelative = Element["cmis:path"].FirstValue.ToString().Replace(_DocumentLibraryPath, "").Replace('/', Path.DirectorySeparatorChar), //Alf to local
                    ElementName = Element["cmis:name"].FirstValue.ToString()
                };
                RepositoryFileList.Add(r);
            }

            Log(String.Format("KS2.EasySync.CMISOnly - GetFileList Folder : {0} elements", RepositoryFileList.Count()));

            foreach (var Element in session.Query(@"SELECT alfcmis:nodeRef,cmis:name,cmis:lastModificationDate FROM cmis:document WHERE IN_TREE('" + SiteDocumentLibraryFolderId + "')", false).ToList())
            {
                RepositoryElement r = new RepositoryElement()
                {
                    ElementType = RepositoryElementType.File,
                    ElementId = Element["alfcmis:nodeRef"].FirstValue.ToString(),
                    ElementName = Element["cmis:name"].FirstValue.ToString()
                };

                Document doc = (Document)session.GetObject(Element["alfcmis:nodeRef"].FirstValue.ToString());
                if (doc.Paths.Count > 0)
                {
                    r.PathRelative = doc.Paths[0].Replace(_DocumentLibraryPath, "").Replace('/', Path.DirectorySeparatorChar);
                    r.CustomProperties = GetSyncIdForFile(doc);
                    RepositoryFileList.Add(r);
                }
            }

            Log(String.Format("KS2.EasySync.CMISOnly - GetFileList End. {0} elements", RepositoryFileList.Count()));

            RepositoryFileList.Sort((x, y) => x.PathRelative.Count(z => z == Path.DirectorySeparatorChar).CompareTo(y.PathRelative.Count(z => z == Path.DirectorySeparatorChar)));

            if (InvalidResponse) return ActionResult.Retry;
            else return ActionResult.Success;
        }

        public ActionResult RenameRepositoryFile(VirtualFile pVirtualFile, SyncActionItem SAI)
        {
            Folder ParentFolder;
            ActionResult ReturnValue = ActionResult.Success;

            if (session == null && !GetServiceSession(ref session, ref SiteDocumentLibraryFolderId)) return ActionResult.Retry;

            string NewFilePath = SAI.StringParameter;
            string NewFileName = Path.GetFileName(SAI.StringParameter);

            try
            {
                Document ExistingDocument = (Document)session.GetObject(pVirtualFile.RemoteID);

                if (ExistingDocument.IsLatestVersion.HasValue && !ExistingDocument.IsLatestVersion.Value) ExistingDocument = (Document)session.GetLatestDocumentVersion(ExistingDocument.Id);

                ParentFolder = (Folder)ExistingDocument.Parents[0];

                bool IsConflict = false;
                foreach (ICmisObject obj in ParentFolder.GetChildren())
                {
                    if (obj.BaseTypeId == DotCMIS.Enums.BaseTypeId.CmisDocument && GetNodeRefFromObjectId(obj.Id) != GetNodeRefFromObjectId(ExistingDocument.Id) && obj.Name.Equals(NewFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        IsConflict = true;
                        break;
                    }
                }

                if (IsConflict)
                {
                    Random Rnd = new Random();

                    String FileName = Path.GetFileNameWithoutExtension(NewFilePath);
                    String FileExtension = Path.GetExtension(NewFilePath);
                    String FileDirectory = Path.GetDirectoryName(NewFilePath);

                    NewFileName = FileName + "(conflict " + this._Login + "_" + DateTime.Now.ToString("ddMMyyyyHHmmss") + "_" + Rnd.Next(1000).ToString() + ")" + FileExtension;
                    NewFilePath = FileDirectory + @"\\" + NewFileName;

                    //Il serait possible ici de faire renommer le fichier local avec la fonction _host.SecureMoveRenamePhysicalFile() et de mettre à jour pVirtualFile.CurrentName
                    //On laisse le processus de synchro gérer la mise à jour locale (et un evéntuel conflit si le fichier local a été renommé entre-temps
                }

                IDictionary<string, object> properties = new Dictionary<string, object>();
                properties["cmis:name"] = NewFileName;
                ExistingDocument = (Document)ExistingDocument.UpdateProperties(properties);
                if (ExistingDocument.IsLatestVersion.HasValue && !ExistingDocument.IsLatestVersion.Value) ExistingDocument = (Document)session.GetLatestDocumentVersion(ExistingDocument.Id);

                pVirtualFile.RemoteID = GetNodeRefFromObjectId(ExistingDocument.Id);
                pVirtualFile.CustomProperties = GetSyncIdForFile(ExistingDocument);
            }
            catch (DotCMIS.Exceptions.CmisObjectNotFoundException ex)
            {
                logger.Error(ex, "In RenameRepositoryFile");
                logger.Error(ex.StackTrace);
                logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                return ActionResult.Cancel;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "In RenameRepositoryFile");
                logger.Error(ex.StackTrace);
                logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                ReturnValue = ActionResult.Retry;
            }
            finally
            {
                try { session.Clear(); }
                catch { }
            }

            return ReturnValue;
        }

        public ActionResult MoveRepositoryFile(VirtualFile pVirtualFile, SyncActionItem SAI)
        {
            ActionResult ReturnValue = ActionResult.Success;
            Folder TargetFolder;

            if (session == null)
            {
                if (!GetServiceSession(ref session, ref SiteDocumentLibraryFolderId)) return ActionResult.Retry;
            }

            string NewFilePath = pVirtualFile.PathRelative;
            string NewFileName = Path.GetFileName(pVirtualFile.PathRelative);

            try
            {
                Document ExistingDocument = (Document)session.GetObject(pVirtualFile.RemoteID);

                if (ExistingDocument.IsLatestVersion.HasValue && !ExistingDocument.IsLatestVersion.Value) ExistingDocument = (Document)session.GetLatestDocumentVersion(ExistingDocument.Id);

                if (pVirtualFile.ParentElement.ElementType == VirtualElementType.RootFolder) //Root Folder
                {
                    TargetFolder = (Folder)session.GetObject(SiteDocumentLibraryFolderId);
                }
                else //Normal Folder
                {
                    if (String.IsNullOrEmpty(pVirtualFile.ParentElement.RemoteID))
                    {
                        //Le répertoire parent n'a pas de remote id. Il n'exite plus localement. On abandonne l'action
                        try { session.Clear(); }
                        catch { }
                        return ActionResult.Cancel;
                    }

                    try
                    {
                        TargetFolder = (Folder)session.GetObject(pVirtualFile.ParentElement.RemoteID);
                    }
                    catch (DotCMIS.Exceptions.CmisObjectNotFoundException ex)
                    {
                        logger.Error(ex, "In MoveRepositoryFile");
                        //Parent dos not exist anymore
                        try { session.Clear(); }
                        catch { }
                        return ActionResult.Cancel;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "In MoveRepositoryFile");
                        try { session.Clear(); }
                        catch { }
                        return ActionResult.Retry;
                    }
                }

                bool IsConflict = false;
                foreach (ICmisObject obj in TargetFolder.GetChildren())
                {
                    if (obj.BaseTypeId == DotCMIS.Enums.BaseTypeId.CmisDocument && GetNodeRefFromObjectId(obj.Id) != GetNodeRefFromObjectId(ExistingDocument.Id) && obj.Name.Equals(Path.GetFileName(NewFileName), StringComparison.OrdinalIgnoreCase))
                    {
                        IsConflict = true;
                        break;
                    }
                }

                if (IsConflict)
                {
                    Random Rnd = new Random();

                    String FileName = Path.GetFileNameWithoutExtension(NewFilePath);
                    String FileExtension = Path.GetExtension(NewFilePath);
                    String FileDirectory = Path.GetDirectoryName(NewFilePath);

                    NewFileName = FileName + "(conflict " + this._Login + "_" + DateTime.Now.ToString("ddMMyyyyHHmmss") + "_" + Rnd.Next(1000).ToString() + ")" + FileExtension;
                    NewFilePath = FileDirectory + @"\\" + NewFileName;

                    IDictionary<string, object> properties = new Dictionary<string, object>();
                    properties["cmis:name"] = NewFileName;
                    ExistingDocument = (Document)ExistingDocument.UpdateProperties(properties);
                    if (ExistingDocument.IsLatestVersion.HasValue && !ExistingDocument.IsLatestVersion.Value) ExistingDocument = (Document)session.GetLatestDocumentVersion(ExistingDocument.Id);
                }

                ExistingDocument = (Document)ExistingDocument.Move(ExistingDocument.Parents[0], TargetFolder);

                pVirtualFile.RemoteID = GetNodeRefFromObjectId(ExistingDocument.Id);
                pVirtualFile.CustomProperties = GetSyncIdForFile(ExistingDocument);
            }
            catch (DotCMIS.Exceptions.CmisObjectNotFoundException ex)
            {
                logger.Error(ex, "In MoveRepositoryFile");
                logger.Error(ex.StackTrace);
                logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                ReturnValue = ActionResult.Cancel;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "In MoveRepositoryFile");
                logger.Error(ex.StackTrace);
                logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                ReturnValue = ActionResult.Retry;
            }
            finally
            {
                RemoveFolderCreateLock();
                try { session.Clear(); }
                catch { }
            }

            return ReturnValue;
        }

        public ActionResult MoveRenameRepositoryFile(VirtualFile pVirtualFile, SyncActionItem SAI)
        {
            ActionResult ReturnValue = ActionResult.Success;
            Folder TargetFolder;

            if (session == null)
            {
                if (!GetServiceSession(ref session, ref SiteDocumentLibraryFolderId)) return ActionResult.Retry;
            }

            string NewFilePath = SAI.StringParameter;
            string NewFileName = Path.GetFileName(SAI.StringParameter);

            try
            {
                Document ExistingDocument = (Document)session.GetObject(pVirtualFile.RemoteID);

                if (ExistingDocument.IsLatestVersion.HasValue && !ExistingDocument.IsLatestVersion.Value) ExistingDocument = (Document)session.GetLatestDocumentVersion(ExistingDocument.Id);

                if (pVirtualFile.ParentElement.ElementType == VirtualElementType.RootFolder) //Root Folder
                {
                    TargetFolder = (Folder)session.GetObject(SiteDocumentLibraryFolderId);
                }
                else //Normal Folder
                {
                    if (String.IsNullOrEmpty(pVirtualFile.ParentElement.RemoteID))
                    {
                        //Le répertoire parent n'a pas de remote id. Il n'exite plus localement. On abandonne l'action
                        try { session.Clear(); }
                        catch { }
                        return ActionResult.Cancel;
                    }

                    try
                    {
                        TargetFolder = (Folder)session.GetObject(pVirtualFile.ParentElement.RemoteID);
                    }
                    catch (DotCMIS.Exceptions.CmisObjectNotFoundException ex)
                    {
                        logger.Error(ex, "In MoveRenameRepositoryFile");
                        //Parent dos not exist anymore
                        try { session.Clear(); }
                        catch { }
                        return ActionResult.Cancel;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "In MoveRenameRepositoryFile");
                        try { session.Clear(); }
                        catch { }
                        return ActionResult.Retry;
                    }
                }

                bool IsConflict = false;
                foreach (ICmisObject obj in TargetFolder.GetChildren())
                {
                    if (obj.BaseTypeId == DotCMIS.Enums.BaseTypeId.CmisDocument && GetNodeRefFromObjectId(obj.Id) != GetNodeRefFromObjectId(ExistingDocument.Id) && obj.Name.Equals(Path.GetFileName(NewFileName), StringComparison.OrdinalIgnoreCase))
                    {
                        IsConflict = true;
                        break;
                    }
                }

                if (IsConflict)
                {
                    Random Rnd = new Random();

                    String FileName = Path.GetFileNameWithoutExtension(NewFilePath);
                    String FileExtension = Path.GetExtension(NewFilePath);
                    String FileDirectory = Path.GetDirectoryName(NewFilePath);

                    NewFileName = FileName + "(conflict " + this._Login + "_" + DateTime.Now.ToString("ddMMyyyyHHmmss") + "_" + Rnd.Next(1000).ToString() + ")" + FileExtension;
                    NewFilePath = FileDirectory + @"\\" + NewFileName;

                    IDictionary<string, object> properties = new Dictionary<string, object>();
                    properties["cmis:name"] = NewFileName;
                    ExistingDocument = (Document)ExistingDocument.UpdateProperties(properties);
                    if (ExistingDocument.IsLatestVersion.HasValue && !ExistingDocument.IsLatestVersion.Value) ExistingDocument = (Document)session.GetLatestDocumentVersion(ExistingDocument.Id);
                }
                else
                {
                    IDictionary<string, object> properties = new Dictionary<string, object>();
                    properties["cmis:name"] = NewFileName;
                    ExistingDocument = (Document)ExistingDocument.UpdateProperties(properties);
                    if (ExistingDocument.IsLatestVersion.HasValue && !ExistingDocument.IsLatestVersion.Value) ExistingDocument = (Document)session.GetLatestDocumentVersion(ExistingDocument.Id);
                }

                ExistingDocument = (Document)ExistingDocument.Move(ExistingDocument.Parents[0], TargetFolder);

                pVirtualFile.RemoteID = GetNodeRefFromObjectId(ExistingDocument.Id);
                pVirtualFile.CustomProperties = GetSyncIdForFile(ExistingDocument);
            }
            catch (DotCMIS.Exceptions.CmisObjectNotFoundException ex)
            {
                logger.Error(ex, "In MoveRenameRepositoryFile");
                logger.Error(ex.StackTrace);
                logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                ReturnValue = ActionResult.Cancel;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "In MoveRenameRepositoryFile");
                logger.Error(ex.StackTrace);
                logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                ReturnValue = ActionResult.Retry;
            }
            finally
            {
                RemoveFolderCreateLock();
                try { session.Clear(); }
                catch { }
            }
            return ReturnValue;
        }

        public ActionResult DeleteRepositoryFile(VirtualFile pVirtualFile, SyncActionItem SAI)
        {
            logger.Debug("DeleteRepositoryFile - Started - " + pVirtualFile.ElementId);
            ActionResult ReturnValue = ActionResult.Success;

            if (session == null)
            {
                if (!GetServiceSession(ref session, ref SiteDocumentLibraryFolderId)) return ActionResult.Retry;
            }

            while (!SetDeleteLock() && !pVirtualFile.IsActionCancelled(SAI))
            {
                Thread.Sleep(500);
            }

            if (pVirtualFile.IsActionCancelled(SAI)) return ActionResult.Cancel;

            try
            {
                Document ExistingDocument = (Document)session.GetObject(pVirtualFile.RemoteID);

                if (ExistingDocument.IsLatestVersion.HasValue && !ExistingDocument.IsLatestVersion.Value)
                {
                    ExistingDocument = (Document)session.GetLatestDocumentVersion(ExistingDocument.Id);
                }

                ExistingDocument.Delete(true);
            }
            catch (DotCMIS.Exceptions.CmisObjectNotFoundException ex)
            {
                logger.Error(ex, "In DeleteRepositoryFile");
                logger.Error(ex.StackTrace);
                logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                ReturnValue = ActionResult.Cancel;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "In DeleteRepositoryFile");
                logger.Error(ex.StackTrace);
                logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                ReturnValue = ActionResult.Retry;
            }
            finally
            {
                RemoveDeleteLock();
                try { session.Clear(); }
                catch { }
            }

            session.Clear();
            logger.Debug("DeleteRepositoryFile - End - " + pVirtualFile.ElementId);
            return ReturnValue;
        }

        public ActionResult UploadVirtualFile(VirtualFile pVirtualFile, SyncActionItem SAI)
        {
            //Beware of documents too big to be uploaded (should use AppendContentStream but it is not supported in dotCMIS 0.6)
            //https://forums.alfresco.com/forum/developer-discussions/other-apis/upload-large-files-using-alfresco-01202010-0857

            bool StopProcessing = false;
            System.IO.FileStream FS = null;

            while (!Tools.IsFileAvailable(_Host.GetFullElementPath(pVirtualFile.PathRelative), ref FS) && !pVirtualFile.IsActionCancelled(SAI))
            {
                #region Get access to the physical file

                System.Threading.Thread.Sleep(1000);

                //If the file was moved in the mean time, cancel this action, another one will be thrown
                if (!File.Exists(_Host.GetFullElementPath(pVirtualFile.PathRelative)))
                {
                    //Create file can have been renamed
                    Log("UploadFile - Physical file is not available anymore. Another action will clean it");
                    StopProcessing = true;
                    break;
                }

                Log(String.Format("UploadFile - Waiting for file release {0}", pVirtualFile.PathRelative));

                #endregion
            }

            if (StopProcessing || pVirtualFile.IsActionCancelled(SAI))
            {
                if (FS != null) FS.Close();
                return ActionResult.Cancel;
            }

            //Retrieve local file hash
            String Hash = "";
            Tools.GetFileHash(FS, out Hash);
            FS.Close();
            pVirtualFile.FileHash = Hash;

            if (session == null && !GetServiceSession(ref session, ref SiteDocumentLibraryFolderId)) return ActionResult.Retry;

            Folder ParentFolder;

            if (pVirtualFile.RemoteID.Equals(String.Empty)) //This is a new file
            {
                #region Upload new file

                //On récupére le répertoire parent
                if (pVirtualFile.ParentElement.ElementType == VirtualElementType.RootFolder) //Root Folder
                {
                    ParentFolder = (Folder)session.GetObject(SiteDocumentLibraryFolderId);
                }
                else //Normal Folder
                {
                    if (String.IsNullOrEmpty(pVirtualFile.ParentElement.RemoteID))
                    {
                        //Le répertoire parent n'a pas de remote id. Il n'exite plus localement. On abandonne l'action
                        try { session.Clear(); }
                        catch { }
                        return ActionResult.Cancel;
                    }

                    try
                    {
                        ParentFolder = (Folder)session.GetObject(pVirtualFile.ParentElement.RemoteID);
                    }
                    catch (DotCMIS.Exceptions.CmisObjectNotFoundException ex)
                    {
                        logger.Error(ex, "In UploadVirtualFile");
                        //Parent dos not exist anymore
                        try { session.Clear(); }
                        catch { }
                        return ActionResult.Cancel;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "In UploadVirtualFile");
                        try { session.Clear(); }
                        catch { }
                        return ActionResult.Retry;
                    }
                }

                bool IsConflict = false;
                foreach (ICmisObject obj in ParentFolder.GetChildren())
                {
                    if (obj.BaseTypeId == DotCMIS.Enums.BaseTypeId.CmisDocument && obj.Name.Equals(Path.GetFileName(pVirtualFile.CurrentName), StringComparison.OrdinalIgnoreCase))
                    {
                        //There is already a file at the same location.
                        //Is it the same file ?
                        if (GetMD5ForFile(session, GetNodeRefFromObjectId(obj.Id)).Equals(pVirtualFile.FileHash.Replace("-", "").ToUpper()))
                        {
                            //Files are the same -> make the match
                            pVirtualFile.RemoteID = GetNodeRefFromObjectId(obj.Id);
                            pVirtualFile.CustomProperties = GetSyncIdForFile((Document)obj);
                            _Host.VirtualElement_Serialize(pVirtualFile);
                            return ActionResult.Success;
                        }

                        IsConflict = true;
                        break;
                    }
                }

                if (IsConflict)
                {
                    try
                    {
                        //There is already a file on the remote location
                        //We change the name of the file we are uploading (both physically and virtually)
                        Random Rnd = new Random();
                        string NewConflictFilePathLocal = Path.GetDirectoryName(_Host.GetFullElementPath(pVirtualFile.PathRelative)) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(_Host.GetFullElementPath(pVirtualFile.PathRelative)) + "(conflict " + this._Login + "_" + DateTime.Now.ToString("ddMMyyyyHHmmss") + "_" + Rnd.Next(1000).ToString() + ")" + Path.GetExtension(_Host.GetFullElementPath(pVirtualFile.PathRelative));
                        _Host.SecureMoveRenamePhysicalFile(pVirtualFile, NewConflictFilePathLocal);
                        _Host.VirtualFile_DeleteTemporaryCopy(pVirtualFile);
                        _Host.VirtualFile_CreateTemporaryCopy(pVirtualFile);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "In UploadVirtualFile");
                        logger.Error(ex.StackTrace);
                        logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                        logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                        try
                        {
                            session.Clear();
                        }
                        catch { }
                        return ActionResult.Retry;
                    }
                }

                //Make a copy of the file to be uploaded into the temp folder
                if (!_Host.VirtualFile_CreateTemporaryCopy(pVirtualFile)) return ActionResult.Retry;

                IDictionary<string, object> properties;

                string FilePath = pVirtualFile.FileTemporaryCopyFullPath;
                string FileName = Path.GetFileName(pVirtualFile.CurrentName);

                properties = new Dictionary<string, object>();
                properties[PropertyIds.Name] = FileName;
                properties[PropertyIds.ObjectTypeId] = "cmis:document";

                ContentStream contentStream = null;
                Document NewDocument = null;

                try
                {
                    File.SetAttributes(pVirtualFile.FileTemporaryCopyFullPath, System.IO.FileAttributes.Normal);
                    FS = File.Open(FilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);

                    contentStream = new ContentStream();
                    contentStream.FileName = FileName;
                    contentStream.MimeType = GetMimeType(FileName);
                    contentStream.Length = FS.Length;
                    contentStream.Stream = FS;

                    NewDocument = (Document)ParentFolder.CreateDocument(properties, contentStream, null);
                    NewDocument.Refresh();

                    //TODO - NTH
                    //Use WebDav for uploading !!!
                    //Detect document too big and Implement a pVirtualFile.ActionIsCancelled(SAI)
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "In UploadVirtualFile");
                    logger.Error(ex.StackTrace);
                    logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                    logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                    goto UploadVirtualFile_Retry;
                }
                finally
                {
                    if (FS != null) FS.Close();
                    if (contentStream != null && contentStream.Stream != null) contentStream.Stream.Close();
                    try { session.Clear(); }
                    catch { }
                }

                //Save the new values
                pVirtualFile.RemoteID = GetNodeRefFromObjectId(NewDocument.Id);
                pVirtualFile.CustomProperties = GetSyncIdForFile(NewDocument);
                _Host.VirtualElement_Serialize(pVirtualFile);

                if (pVirtualFile.IsActionCancelled(SAI))
                {
                    Log("Finishing Upload Cancel");
                    goto UploadVirtualFile_Cancel;
                }
                else
                {
                    goto UploadVirtualFile_Success;
                }

                #endregion
            }
            else //This is an existing file
            {
                #region Upload Existing file

                IObjectId objectId = null;
                IObjectId CheckedOutDocumentId = null;
                Document ExistingDocument = null;

                try
                {
                    ExistingDocument = (Document)session.GetObject(pVirtualFile.RemoteID); //Try/catch if the document does not exist anymore
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "In UploadVirtualFile Existing");
                    logger.Error(ex.StackTrace);
                    Log("UploadFile - Physical file is not available anymore. Another action will clean it");
                    try
                    {
                        session.Clear();
                    }
                    catch { }
                    return ActionResult.Cancel;
                }

                if (ExistingDocument.IsLatestVersion.HasValue && !ExistingDocument.IsLatestVersion.Value)
                {
                    //The File id we have is not corresponding to the last version of the document => there is a pending conflict
                    //We get the last file Id
                    try
                    {
                        ExistingDocument = (Document)session.GetLatestDocumentVersion(ExistingDocument.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "In UploadVirtualFile Existing");
                        logger.Error(ex.StackTrace);
                        logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                        logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                        try
                        {
                            session.Clear();
                        }
                        catch { }
                        return ActionResult.Retry;
                    }
                }

                //Detect a potential conflict if we try to replace a file which source does not correspond to the remote file
                //If the remote file was renamed / moved while waiting => perform local move first
                if (!pVirtualFile.PathRelative.Equals(GetRelativePathFromDocument(ExistingDocument), StringComparison.OrdinalIgnoreCase))
                {
                    _Host.SecureMoveRenamePhysicalFile(pVirtualFile, _Host.GetFullElementPath(GetRelativePathFromDocument(ExistingDocument)));
                }

                if (!pVirtualFile.CustomProperties.Equals(GetSyncIdForFile(ExistingDocument)))
                {
                    //The remote file has been replaced also => There is a conflict

                    //[Conflict]Upload
                    //We are uploading a modified file but the remote file has also been modified
                    Log(String.Format("[UPLOAD Function]Conflict for file {0}", pVirtualFile.PathRelative));

                    RepositoryElement r = new RepositoryElement();
                    r.ElementId = ExistingDocument.Id;
                    r.PathRelative = GetRelativePathFromDocument(ExistingDocument);
                    r.CustomProperties = GetSyncIdForFile(ExistingDocument);

                    _Host.DispatchEvent(new SyncEventItem(SyncEventEnum.BothSideUpdate, pVirtualFile.ElementId, ObjectCopier.Clone(r), false, false));
                    try
                    {
                        session.Clear();
                    }
                    catch { }

                    return ActionResult.Success;
                }

                //Make a copy of the file to be uploaded into the temp folder
                if (!_Host.VirtualFile_CreateTemporaryCopy(pVirtualFile)) return ActionResult.Retry;

                if (ExistingDocument.AllowableActions.Actions.Contains("canCheckOut"))
                {
                    #region Checkout And Upload

                    #region Checkout the document

                    while (true)
                    {
                        if (pVirtualFile.IsActionCancelled(SAI)) goto UploadVirtualFile_Cancel;

                        ExistingDocument.Refresh();

                        if (ExistingDocument.IsVersionSeriesCheckedOut.HasValue && ExistingDocument.IsVersionSeriesCheckedOut.Value)
                        {
                            Thread.Sleep(LOCK_WAIT_TIME * 1000);
                        }
                        else
                        {
                            try
                            {
                                CheckedOutDocumentId = ExistingDocument.CheckOut();
                            }
                            catch (DotCMIS.Exceptions.CmisConstraintException ex)
                            {
                                if (ex.Message.Equals("Conflict"))
                                {
                                    CheckedOutDocumentId = null;
                                }
                                else
                                {
                                    logger.Error(ex, "In UploadVirtualFile");
                                    logger.Error(ex.StackTrace);
                                    logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                                    logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                                    try
                                    {
                                        session.Clear();
                                    }
                                    catch { }
                                    goto UploadVirtualFile_Retry;
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Error(ex, "In UploadVirtualFile");
                                logger.Error(ex.StackTrace);
                                logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                                logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                                try
                                {
                                    session.Clear();
                                }
                                catch { }
                                goto UploadVirtualFile_Retry;
                            }

                            if (CheckedOutDocumentId != null) break;
                            Thread.Sleep(LOCK_WAIT_TIME * 1000);
                        }
                    }

                    #endregion

                    Document CheckedOutDocument = null;

                    try
                    {
                        CheckedOutDocument = (Document)session.GetObject(CheckedOutDocumentId);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "In UploadVirtualFile");
                        logger.Error(ex.StackTrace);
                        logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                        logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                        try
                        {
                            session.Clear();
                        }
                        catch { }
                        goto UploadVirtualFile_Retry;
                    }

                    IContentStream contentStream = null;

                    try
                    {
                        File.SetAttributes(pVirtualFile.FileTemporaryCopyFullPath, System.IO.FileAttributes.Normal);
                        FS = File.Open(pVirtualFile.FileTemporaryCopyFullPath, System.IO.FileMode.Open, System.IO.FileAccess.Read);

                        contentStream = session.ObjectFactory.CreateContentStream(pVirtualFile.CurrentName, FS.Length, GetMimeType(pVirtualFile.CurrentName), FS);
                        objectId = CheckedOutDocument.CheckIn(true, null, contentStream, "");
                        //TODO - NTH
                        //Use WebDav for uploading !!!
                        //Detect document too big and Implement a pVirtualFile.ActionIsCancelled(SAI)
                        ExistingDocument.Refresh();
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "In UploadVirtualFile");
                        logger.Error(ex.StackTrace);
                        logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                        logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                        goto UploadVirtualFile_Retry;
                    }
                    finally
                    {
                        if (FS != null) FS.Close();
                        if (contentStream != null && contentStream.Stream != null) contentStream.Stream.Close();
                        try
                        {
                            session.Clear();
                        }
                        catch { }
                        _Host.VirtualFile_DeleteTemporaryCopy(pVirtualFile); //Try to clean the file
                    }

                    ExistingDocument = (Document)session.GetObject(objectId);

                    //Save the new values
                    pVirtualFile.RemoteID = GetNodeRefFromObjectId(ExistingDocument.Id);
                    pVirtualFile.CustomProperties = GetSyncIdForFile(ExistingDocument);
                    _Host.VirtualElement_Serialize(pVirtualFile);

                    #endregion
                }
                else
                {
                    #region Upload without Checkout

                    IContentStream contentStream = null;
                    try
                    {
                        File.SetAttributes(pVirtualFile.FileTemporaryCopyFullPath, System.IO.FileAttributes.Normal);
                        FS = File.Open(pVirtualFile.FileTemporaryCopyFullPath, System.IO.FileMode.Open, System.IO.FileAccess.Read);

                        contentStream = session.ObjectFactory.CreateContentStream(pVirtualFile.CurrentName, FS.Length, GetMimeType(pVirtualFile.CurrentName), FS);
                        objectId = ExistingDocument.SetContentStream(contentStream, true, true);
                        //TODO - NTH Detect document too big and Implement a pVirtualFile.ActionIsCancelled(SAI)
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "In UploadVirtualFile");
                        logger.Error(ex.StackTrace);
                        logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                        logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                        goto UploadVirtualFile_Retry;
                    }
                    finally
                    {
                        if (FS != null) FS.Close();
                        if (contentStream != null && contentStream.Stream != null) contentStream.Stream.Close();
                        try
                        {
                            session.Clear();
                        }
                        catch { }
                    }

                    if (objectId != null) ExistingDocument = (Document)session.GetObject(objectId);

                    //Save the new values
                    pVirtualFile.RemoteID = GetNodeRefFromObjectId(ExistingDocument.Id);
                    pVirtualFile.CustomProperties = GetSyncIdForFile(ExistingDocument);
                    _Host.VirtualElement_Serialize(pVirtualFile);

                    #endregion
                }

                #endregion
            }

            UploadVirtualFile_Success:
            _Host.VirtualFile_DeleteTemporaryCopy(pVirtualFile); //Try to clean the file
            return ActionResult.Success;
            UploadVirtualFile_Retry:
            _Host.VirtualFile_DeleteTemporaryCopy(pVirtualFile); //Try to clean the file
            return ActionResult.Retry;
            UploadVirtualFile_Cancel:
            _Host.VirtualFile_DeleteTemporaryCopy(pVirtualFile); //Try to clean the file
            return ActionResult.Cancel;
        }

        public ActionResult DownloadVirtualFile(VirtualFile pVirtualFile, SyncActionItem SAI)
        {
            ActionResult AR;
            Random Rnd;
            RepositoryElement RemoteFile = (RepositoryElement)SAI.RemoteElementParameter;
            string Hash;

            System.IO.FileStream FS = null;
            bool StopProcessing = false;

            AR = _Host.BuildFilePath(_Host.GetFullFilePathTemp(RemoteFile.PathRelative));
            if (AR != ActionResult.Success) return AR;

            //Int64 FileSize = RemoteFile._FileSize;
            string RelativeFilePath = RemoteFile.PathRelative;

            if (SAI.Action == SyncActionEnum.FileDownloadNew)
            {
                Log("SyncActionEnum.DownloadNew Started");

                AR = Download(pVirtualFile, SAI);
                if (AR == ActionResult.Cancel) { Log("SyncActionEnum.DownloadNew cancelled"); return AR; }
                else if (AR == ActionResult.Retry) { Log("SyncActionEnum.DownloadNew cancelled/postponed"); return AR; }

                if (AR == ActionResult.Cancel)
                {
                    //Delete the partially downloaded file
                    if (File.Exists(_Host.GetFullFilePathTemp(RelativeFilePath))) File.Delete(_Host.GetFullFilePathTemp(RelativeFilePath));
                    //Delete TempFile
                    _Host.VirtualFile_DeleteTemporaryCopy(pVirtualFile);
                    Log("Finishing Download Cancel");
                    return ActionResult.Cancel;
                }

                Tools.GetFileHash(_Host.GetFullFilePathTemp(RelativeFilePath), out Hash);

                pVirtualFile.FileHash = Hash;
                pVirtualFile.RemoteID = GetNodeRefFromObjectId(RemoteFile.ElementId);
                pVirtualFile.CustomProperties = RemoteFile.CustomProperties;
                pVirtualFile.bIdentified = true;

                //[Conflict] If a file with the same name has been created in the meantime => conflict
                if (File.Exists(_Host.GetFullElementPath(pVirtualFile.PathRelative)))
                {
                    Log("SyncActionEnum.DownloadNew Conflict !!!");

                    Rnd = new Random();

                    string NewConflictFilePathDownloaded = Path.GetDirectoryName(_Host.GetFullElementPath(pVirtualFile.PathRelative)) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(_Host.GetFullElementPath(pVirtualFile.PathRelative)) + "(conflict " + Rnd.Next(1000, 2000).ToString() + ")" + Path.GetExtension(_Host.GetFullElementPath(pVirtualFile.PathRelative));

                    //Copy the temp file to the production folder with a new file name
                    _Host.SecureMoveRenamePhysicalFileFromTemp(pVirtualFile, NewConflictFilePathDownloaded);
                    pVirtualFile.CurrentName = Path.GetFileName(_Host.GetRelativeElementPath(NewConflictFilePathDownloaded));

                    _Host.DispatchEvent(new SyncEventItem(SyncEventEnum.LocalRename, pVirtualFile.ElementId, NewConflictFilePathDownloaded, false, false));
                }
                else
                {
                    AR = _Host.BuildFilePath(_Host.GetFullElementPath(pVirtualFile.PathRelative));
                    if (AR != ActionResult.Success) return AR;

                    AR = _Host.SecureMovePhysicalFileFromTemp(pVirtualFile);
                    if (AR != ActionResult.Success) return AR;
                }

                _Host.VirtualElement_Serialize(pVirtualFile);
                Log("SyncActionEnum.DownloadNew Complete");
            }
            else
            {
                Log("SyncActionEnum.DownloadExisting Started");

                string LocalFileHashWhenDownloadStarted = pVirtualFile.FileHash;
                string LocalFileHashWhenDownloadEnded;

                AR = Download(pVirtualFile, SAI);
                if (AR == ActionResult.Cancel) { Log("SyncActionEnum.DownloadExisting cancelled"); return AR; }
                else if (AR == ActionResult.Retry) { Log("SyncActionEnum.DownloadExisting cancelled/postponed"); return AR; }

                //Check if the file is still present and accessible
                while (!Tools.IsFileAvailable(_Host.GetFullElementPath(pVirtualFile.PathRelative), ref FS))
                {
                    //TODO : How long to wait before cleaning and postponing ?

                    System.Threading.Thread.Sleep(1000);

                    //If the file was moved in the mean time, cancel this action, another one will be thrown
                    if (!File.Exists(_Host.GetFullElementPath(pVirtualFile.PathRelative)))
                    {
                        Log("SyncActionEnum.DownloadExisting - Physical file is not available anymore. Another action will clean it");
                        StopProcessing = true;
                        break;
                    }

                    if (pVirtualFile.IsActionCancelled(SAI)) { return ActionResult.Cancel; }

                    Log(String.Format("SyncActionEnum.DownloadExisting waiting for file release {0}", pVirtualFile.PathRelative));
                }

                if (!StopProcessing)
                {
                    Tools.GetFileHash(FS, out Hash);
                    FS.Close();

                    LocalFileHashWhenDownloadEnded = Hash;

                    if (!LocalFileHashWhenDownloadStarted.Equals(LocalFileHashWhenDownloadEnded))
                    {
                        //[Conflict] The local file was modified while the remote file was downloaded. We cannot just replace the localfile. We need to set a conflict

                        Log(" !!! SyncActionEnum.DownloadExisting Conflict !!!");

                        //The local file cannot be replaced by the download file because the local file has been changed during the time of the download. Both files are no more in sync
                        //We have a temporary file : TempSFile located in Globals._LocalFileTempPath
                        //We have an existing file : FileToProcess located in Globals._LocalFileStoragePath                                

                        //The current file is copied to a new conflict name
                        //The downloaded file replaces the current file

                        Rnd = new Random();
                        string NewConflictFilePathDownloaded = Path.GetDirectoryName(_Host.GetFullElementPath(pVirtualFile.PathRelative)) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(_Host.GetFullElementPath(pVirtualFile.PathRelative)) + "(conflict " + Rnd.Next(1000, 2000).ToString() + ")" + Path.GetExtension(_Host.GetFullElementPath(pVirtualFile.PathRelative));
                        File.Copy(_Host.GetFullElementPath(pVirtualFile.PathRelative), NewConflictFilePathDownloaded, true); //Copy the existing file to a conflict file
                    }

                    Tools.GetFileHash(_Host.GetFullFilePathTemp(RelativeFilePath), out Hash);

                    //Replace the existing file with the downloaded file
                    AR = _Host.SecureMovePhysicalFileFromTemp(pVirtualFile);
                    if (AR != ActionResult.Success) return AR;

                    pVirtualFile.FileHash = Hash;
                    pVirtualFile.CustomProperties = RemoteFile.CustomProperties;
                    _Host.VirtualElement_Serialize(pVirtualFile);
                }
                else
                {
                    if (FS != null) FS.Close();
                }

                Log("SyncActionEnum.DownloadExisting Complete");
            }

            return ActionResult.Success;
        }

        public ActionResult Download(VirtualFile pVirtualFile, SyncActionItem SAI)
        {
            System.IO.FileStream FS = null;
            IContentStream ICS = null;
            ActionResult ReturnValue = ActionResult.Success;

            if (session == null && !GetServiceSession(ref session, ref SiteDocumentLibraryFolderId)) return ActionResult.Retry;

            RepositoryElement RemoteFile = SAI.RemoteElementParameter;
            string RelativeFilePath = RemoteFile.PathRelative;
            String FullFilePathTemporary = _Host.GetFullFilePathTemp(RelativeFilePath);
            pVirtualFile.TemporaryDownloadFilePathFull = FullFilePathTemporary;

            Int32 bufferSize = 1 * 1024 * 1024;
            try
            {
                Document document = (Document)session.GetObject(RemoteFile.ElementId);
                if (document.IsLatestVersion.HasValue && !document.IsLatestVersion.Value) document = (Document)session.GetLatestDocumentVersion(document.Id);

                ICS = document.GetContentStream();
                FS = File.Open(FullFilePathTemporary, System.IO.FileMode.Create, System.IO.FileAccess.ReadWrite);
                byte[] bytes = new byte[bufferSize];
                Int32 ByteRead;
                while ((ByteRead = ICS.Stream.Read(bytes, 0, bufferSize)) > 0 && !pVirtualFile.IsActionCancelled(SAI))
                {
                    FS.Write(bytes, 0, ByteRead);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "In Download");
                logger.Error(ex.StackTrace);
                logger.Error("Param pVirtualFile : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFile));
                logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                ReturnValue = ActionResult.Cancel;
            }
            finally
            {
                if (FS != null) FS.Close();
                if (ICS != null && ICS.Stream != null) ICS.Stream.Close();
                try { session.Clear(); } catch { }
            }

            if (pVirtualFile.IsActionCancelled(SAI)) return ActionResult.Cancel;

            return ReturnValue;
        }

        public ActionResult CreateFolder(VirtualFolder pVirtualFolder, SyncActionItem SAI)
        {
            Folder CurrentFolder = null;

            while (!SetFolderCreateLock())
            {
                Thread.Sleep(500);
                if (pVirtualFolder.IsActionCancelled(SAI)) return ActionResult.Cancel;
            }

            try
            {
                if (!CreateFolder(pVirtualFolder, SAI, out CurrentFolder))
                {
                    return ActionResult.Retry;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "In CreateFolder");
                logger.Error(ex.StackTrace);
                logger.Error("Param pVirtualFolder : " + Newtonsoft.Json.JsonConvert.SerializeObject(pVirtualFolder));
                logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                return ActionResult.Retry;
            }
            finally
            {
                RemoveFolderCreateLock();
            }

            pVirtualFolder.RemoteID = GetNodeRefFromObjectId(CurrentFolder.Id);
            pVirtualFolder.CustomProperties = String.Empty;

            return ActionResult.Success;
        }

        /// <summary>
        /// Check localfile vs remotefile 
        /// </summary>
        /// <param name="pVirtualFile"></param>
        /// <param name="pRepositoryFile"></param>
        /// <returns></returns>
        public LocalAndRemoteComparisonResult CompareFile(VirtualFile pVirtualFile, RepositoryElement pRepositoryFile)
        {
            //Compare local version to remote version. If remote version is higher, download the remote version

            if (pVirtualFile.CustomProperties == null || pVirtualFile.CustomProperties.Equals(String.Empty))
            {
                return LocalAndRemoteComparisonResult.None;
            }
            else
            {
                String[] LocalFileSplit = pVirtualFile.CustomProperties.Split('|');
                String[] RemoteFileSplit = pRepositoryFile.CustomProperties.Split('|');

                if (LocalFileSplit[1].Equals(RemoteFileSplit[1]))
                {
                    return LocalAndRemoteComparisonResult.None;
                }
                else
                {
                    if (Convert.ToInt64(LocalFileSplit[0]) < Convert.ToInt64(RemoteFileSplit[0]))
                    {
                        return LocalAndRemoteComparisonResult.RemoteUpdate;
                    }
                }
            }

            return LocalAndRemoteComparisonResult.None;
        }

        public ActionResult DeleteRepositoryFolder(VirtualFolder FolderToProcess, SyncActionItem SAI)
        {
            ActionResult ReturnValue = ActionResult.Success;

            if (session == null)
            {
                if (!GetServiceSession(ref session, ref SiteDocumentLibraryFolderId))
                {
                    return ActionResult.Retry;
                }
            }

            while (!SetDeleteLock())
            {
                Thread.Sleep(500);
                if (FolderToProcess.IsActionCancelled(SAI)) return ActionResult.Cancel;
            }

            try
            {
                Folder folder = (Folder)session.GetObject(FolderToProcess.RemoteID);
                folder.DeleteTree(true, DotCMIS.Enums.UnfileObject.Delete, true);
                session.Clear();
            }
            catch (DotCMIS.Exceptions.CmisObjectNotFoundException ex)
            {
                //File does not exist anymore
                logger.Error("In DeleteRepositoryFolder", ex);
                logger.Error(ex.StackTrace);
                logger.Error("Param FolderToProcess : " + Newtonsoft.Json.JsonConvert.SerializeObject(FolderToProcess));
                logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                ReturnValue = ActionResult.Cancel;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "In DeleteRepositoryFolder");
                logger.Error(ex.StackTrace);
                logger.Error("Param FolderToProcess : " + Newtonsoft.Json.JsonConvert.SerializeObject(FolderToProcess));
                logger.Error("Param SAI : " + Newtonsoft.Json.JsonConvert.SerializeObject(SAI));
                ReturnValue = ActionResult.Retry;
            }
            finally
            {
                RemoveDeleteLock();
                try
                {
                    session.Clear();
                }
                catch { }
            }

            return ReturnValue;
        }

        public ActionResult RenameRepositoryFolder(VirtualFolder FolderToProcess, string NewName)
        {
            if (session == null && !GetServiceSession(ref session, ref SiteDocumentLibraryFolderId)) return ActionResult.Retry;

            try
            {
                Folder folder = (Folder)session.GetObject(FolderToProcess.RemoteID);
                Folder ParentFolder = (Folder)folder.Parents[0];

                bool IsConflict = false;
                foreach (ICmisObject obj in ParentFolder.GetChildren())
                {
                    if (obj.BaseTypeId == DotCMIS.Enums.BaseTypeId.CmisFolder && GetNodeRefFromObjectId(obj.Id) != GetNodeRefFromObjectId(folder.Id) && obj.Name.Equals(Path.GetFileName(NewName), StringComparison.OrdinalIgnoreCase))
                    {
                        IsConflict = true;
                        break;
                    }
                }

                if (IsConflict)
                {
                    Random Rnd = new Random();
                    NewName += "(conflict " + this._Login + "_" + DateTime.Now.ToString("ddMMyyyyHHmmss") + "_" + Rnd.Next(1000).ToString() + ")";
                }

                IDictionary<string, object> properties = new Dictionary<string, object>();
                properties["cmis:name"] = NewName.Substring(NewName.LastIndexOf(Path.DirectorySeparatorChar) + 1);

                IObjectId newId = folder.UpdateProperties(properties);

                if (newId.Id == FolderToProcess.RemoteID)
                {
                    // the repository updated this object - refresh the object
                    folder.Refresh();
                }
                else
                {
                    // the repository created a new version - fetch the new version
                    folder = (Folder)session.GetObject(newId);
                    FolderToProcess.RemoteID = GetNodeRefFromObjectId(newId.Id);
                }
            }
            catch (DotCMIS.Exceptions.CmisObjectNotFoundException ex)
            {
                logger.Error(ex, "In CreateFolder");
                logger.Error(ex.StackTrace);
                logger.Error("Param FolderToProcess : " + Newtonsoft.Json.JsonConvert.SerializeObject(FolderToProcess));
                logger.Error("Param NewName : " + Newtonsoft.Json.JsonConvert.SerializeObject(NewName));
                return ActionResult.Cancel;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "In CreateFolder");
                logger.Error(ex.StackTrace);
                logger.Error("Param FolderToProcess : " + Newtonsoft.Json.JsonConvert.SerializeObject(FolderToProcess));
                logger.Error("Param NewName : " + Newtonsoft.Json.JsonConvert.SerializeObject(NewName));
                return ActionResult.Retry;
            }
            finally
            {
                try
                {
                    session.Clear();
                }
                catch { }
            }

            return ActionResult.Success;
        }

        #endregion

        #region Helpers

        public ActionResult Setup(string URL, string Login, string Password, List<string> TargetRepository)
        {
            Folder RootFolder;
            Folder Sites;
            SessionFactory Factory;
            IRepository Repo0;
            ISession session;
            string RepositoryId;

            // define dictonary with key value pair
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            //ATOM
            //http://chemistry.apache.org/dotnet/session-parameters.html

            parameters[DotCMIS.SessionParameter.BindingType] = BindingType.AtomPub;     // define binding type, in our example we are using ATOMPUB as stated above
            parameters[DotCMIS.SessionParameter.AtomPubUrl] = URL/* + "/core/cmisatom"*/;   // define CMIS available path which is already available under alfresco
            parameters[DotCMIS.SessionParameter.User] = Login;                          // alfresco portal admin user name
            parameters[DotCMIS.SessionParameter.Password] = Password;                   // alfresco portal admin password

            // using session factory get the default repository, on this repository we would be performing actions & create session on this repository
            try
            {
                Factory = SessionFactory.NewInstance();
                Repo0 = Factory.GetRepositories(parameters)[0];
                RepositoryId = Repo0.Id;
                session = Repo0.CreateSession();
                RootFolder = (Folder)session.GetRootFolder();
                Sites = (Folder)session.GetObjectByPath("/Sites");

                //Get the list of sites
                foreach (ICmisObject F in Sites.GetChildren())
                {
                    TargetRepository.Add(F.Name);
                }

                IRepositoryCapabilities capa = Repo0.Capabilities;
            }
            catch (DotCMIS.Exceptions.CmisConnectionException ex)
            {
                logger.Error(ex, "In GetServiceSession");
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);
                return ActionResult.Cancel;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "In GetServiceSession");
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);
                return ActionResult.Cancel;
            }

            return ActionResult.Success;
        }

        /// <summary>
        /// Retrieve a session for running CMIS commands
        /// </summary>
        /// <param name="session"></param>
        /// <param name="rootFolderId"></param>
        /// <returns></returns>
        private bool GetServiceSession(ref ISession session, ref String rootFolderId)
        {
            Folder RootFolder;
            Folder DocumentLibraryFolder;
            SessionFactory Factory;
            IRepository Repo0;
            string RepositoryId;

            // define dictonary with key value pair
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            //ATOM
            //http://chemistry.apache.org/dotnet/session-parameters.html

            parameters[DotCMIS.SessionParameter.BindingType] = BindingType.AtomPub;             // define binding type, in our example we are using ATOMPUB as stated above
            parameters[DotCMIS.SessionParameter.AtomPubUrl] = _ServiceURL/* + "/core/cmisatom"*/;   // define CMIS available path which is already available under alfresco
            parameters[DotCMIS.SessionParameter.User] = _Login;                                 // alfresco portal admin user name
            parameters[DotCMIS.SessionParameter.Password] = _Password;                          // alfresco portal admin password

            /*
            parameters[DotCMIS.SessionParameter.BindingType] = BindingType.WebServices;     // define binding type, in our example we are using ATOMPUB as stated above
            parameters[DotCMIS.SessionParameter.User] = _Login;                         // alfresco portal admin user name
            parameters[DotCMIS.SessionParameter.Password] = _Password;                  // alfresco portal admin password
            parameters.Add(DotCMIS.SessionParameter.WebServicesAclService, (_ServiceURL + "ACLService?wsdl").ToString());
            parameters.Add(DotCMIS.SessionParameter.WebServicesDiscoveryService, (_ServiceURL + "DiscoveryService?wsdl").ToString());
            parameters.Add(DotCMIS.SessionParameter.WebServicesMultifilingService, (_ServiceURL + "MultiFilingService?wsdl").ToString());
            parameters.Add(DotCMIS.SessionParameter.WebServicesNavigationService, (_ServiceURL + "NavigationService?wsdl").ToString());
            parameters.Add(DotCMIS.SessionParameter.WebServicesObjectService, (_ServiceURL + "ObjectService?wsdl").ToString());
            parameters.Add(DotCMIS.SessionParameter.WebServicesPolicyService, (_ServiceURL + "PolicyService?wsdl").ToString());
            parameters.Add(DotCMIS.SessionParameter.WebServicesRelationshipService, (_ServiceURL + "RelationshipService?wsdl").ToString());
            parameters.Add(DotCMIS.SessionParameter.WebServicesRepositoryService, (_ServiceURL + "RepositoryService?wsdl").ToString());
            parameters.Add(DotCMIS.SessionParameter.WebServicesVersioningService, (_ServiceURL + "VersioningService?wsdl").ToString());
            */

            //parameters[DotCMIS.SessionParameter.AtomPubUrl] = "https://content.kaliconseil.fr/alfresco/service/cmis";
            //parameters[DotCMIS.SessionParameter.AtomPubUrl] = "https://content.kaliconseil.fr/alfresco/cmisatom";

            //To connect to a specific directory
            //parameters[SessionParameter.RepositoryId] = "<repositoryId>";
            //SessionFactory factory = SessionFactory.NewInstance();
            //ISession session = factory.CreateSession(parameters);

            // using session factory get the default repository, on this repository we would be performing actions & create session on this repository
            try
            {
                Factory = SessionFactory.NewInstance();
                Repo0 = Factory.GetRepositories(parameters)[0];
                RepositoryId = Repo0.Id;
                session = Repo0.CreateSession();

                RootFolder = (Folder)session.GetRootFolder();
                DocumentLibraryFolder = (Folder)session.GetObjectByPath(_DocumentLibraryPath);
                rootFolderId = DocumentLibraryFolder.Id;

                IRepositoryCapabilities capa = Repo0.Capabilities;
                if (capa.ChangesCapability.HasValue && capa.ChangesCapability.Value != DotCMIS.Enums.CapabilityChanges.None)
                {
                    Console.WriteLine("Log changes enabled");
                }
            }
            catch (DotCMIS.Exceptions.CmisUnauthorizedAccessException ex)
            {
                //Authentication error
                logger.Error(ex.Message);
                if (AuthenticationError != null) AuthenticationError(this, null);
                return false;
            }
            catch (DotCMIS.Exceptions.CmisRuntimeException ex)
            {
                logger.Error(ex, "In GetServiceSession");
                logger.Error(ex.Message);
                if (ex.Message.Equals("ConnectFailure"))
                {
                    //Connection issue : check proxy parameters
                    if (ProxyError != null) ProxyError(this, null);
                }
                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "In GetServiceSession");
                logger.Error(ex.Message);
                return false;
            }

            /*
            Log("------------------------------------");
            Log("Id : " + session.RepositoryInfo.Id);
            Log("Name : " + session.RepositoryInfo.Name);
            Log("Product Name : " + session.RepositoryInfo.ProductName);
            Log("Product Version : " + session.RepositoryInfo.ProductVersion);
            Log("Vendor : " + session.RepositoryInfo.VendorName);
            Log("Root folder : " + RootFolder.Name);
            Log("LatestChangeLogToken : " + session.RepositoryInfo.LatestChangeLogToken);
            Log("------------------------------------");

            Log(String.Format("canDeleteObject : {0}", DocumentLibraryFolder.AllowableActions.Actions.Contains("canDeleteObject") ? "OK" : "KO"));
            Log(String.Format("canUpdateProperties : {0}", DocumentLibraryFolder.AllowableActions.Actions.Contains("canUpdateProperties") ? "OK" : "KO"));
            Log(String.Format("canMoveObject : {0}", DocumentLibraryFolder.AllowableActions.Actions.Contains("canMoveObject") ? "OK" : "KO"));
            Log(String.Format("canGetFolderTree : {0}", DocumentLibraryFolder.AllowableActions.Actions.Contains("canGetFolderTree") ? "OK" : "KO"));
            Log(String.Format("canCreateDocument : {0}", DocumentLibraryFolder.AllowableActions.Actions.Contains("canCreateDocument") ? "OK" : "KO"));
            Log(String.Format("canCreateFolder : {0}", DocumentLibraryFolder.AllowableActions.Actions.Contains("canCreateFolder") ? "OK" : "KO"));
            Log(String.Format("canDeleteTree : {0}", DocumentLibraryFolder.AllowableActions.Actions.Contains("canDeleteTree") ? "OK" : "KO"));
            */
            return true;
        }

        private void Log(string Message)
        {
            if (LogOutput != null) LogOutput(this, Message);
        }

        private string GetRelativePathFromDocument(Document doc)
        {
            //Is it possible to have multiple path for one document ???

            string TempPath;
            try
            {
                TempPath = doc.Paths.FirstOrDefault(x => x.StartsWith(_DocumentLibraryPath, StringComparison.OrdinalIgnoreCase));
                if (TempPath != null) TempPath = TempPath.Replace(_DocumentLibraryPath, "").Replace('/', Path.DirectorySeparatorChar); //Alfreco format to local format
            }
            catch
            {
                return null;
            }

            return TempPath;
        }

        /// <summary>
        /// If versionning is enabled on Alfresco, the version label is appended to the UUID. We need to remove it
        /// ObjectId = NodeRef + Version Number
        /// </summary>
        /// <param name="ElementId"></param>
        /// <returns></returns>
        private string GetNodeRefFromObjectId(String ElementId)
        {
            if (ElementId.LastIndexOf(';') != -1) return ElementId.Substring(0, ElementId.LastIndexOf(';'));
            else return ElementId;
        }

        /// <summary>
        /// Build remote folder path
        /// </summary>
        /// <param name="pDispatchedElement"></param>
        /// <param name="SAI"></param>
        /// <param name="CurrentFolder"></param>
        /// <returns></returns>
        private bool CreateFolder(VirtualElement pDispatchedElement, SyncActionItem SAI, out Folder CurrentFolder)
        {
            CurrentFolder = null;

            if (session == null)
            {
                if (!GetServiceSession(ref session, ref SiteDocumentLibraryFolderId))
                {
                    return false;
                }
            }

            IDictionary<string, object> properties;

            string ElementPath;

            if (pDispatchedElement.ElementType == VirtualElementType.File) ElementPath = Path.GetDirectoryName(_DocumentLibraryPath + pDispatchedElement.PathRelative).Replace(Path.DirectorySeparatorChar, '/'); //Local format to Alf
            else ElementPath = (_DocumentLibraryPath + pDispatchedElement.PathRelative).Replace(Path.DirectorySeparatorChar, '/'); //Local format to Alf

            //Build remote folder
            String CurrentPath = "";
            CurrentFolder = (Folder)session.GetRootFolder();
            foreach (String s in ElementPath.Split(new Char[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                CurrentPath = CurrentPath + "/" + s;
                try
                {
                    CurrentFolder = (Folder)session.GetObjectByPath(CurrentPath);
                }
                catch
                {
                    //The folder does not exists

                    properties = new Dictionary<string, object>();
                    properties[PropertyIds.Name] = s;
                    properties[PropertyIds.ObjectTypeId] = "cmis:folder";

                    try
                    {
                        CurrentFolder = (Folder)CurrentFolder.CreateFolder(properties);
                    }
                    catch (Exception ex2)
                    {
                        logger.Debug("In CreateFolder - " + ex2.Message);
                        logger.Error(ex2.StackTrace);
                        session.Clear();
                        return false;
                    }
                }
            }

            session.Clear();

            return true;
        }

        private string GetMimeType(string fileName)
        {
            string mimeType = "application/unknown";
            if (_HostOS == HostPlatform.Windows)
            {
                string ext = Path.GetExtension(fileName).ToLower();
                Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
                if (regKey != null && regKey.GetValue("Content Type") != null) mimeType = regKey.GetValue("Content Type").ToString();
            }
            return mimeType;
        }

        private bool SetFolderCreateLock()
        {
            lock (_FolderCreateLock)
            {
                if (IsLockedForCreation) return false;
                IsLockedForCreation = true;
                return true;
            }
        }

        private void RemoveFolderCreateLock()
        {
            lock (_FolderCreateLock)
            {
                IsLockedForCreation = false;
            }
        }

        /// <summary>
        /// This is to avoid concurrent deletion operation. Alfresco doesn't like them
        /// </summary>
        /// <returns></returns>
        private bool SetDeleteLock()
        {
            lock (_DeleteLock)
            {
                if (IsLockedForDeletion) return false;
                IsLockedForDeletion = true;
                return true;
            }
        }

        private void RemoveDeleteLock()
        {
            lock (_DeleteLock)
            {
                IsLockedForDeletion = false;
            }
        }

        private String GetSyncIdForFile(Document SyncDocument)
        {
            String SyncId = String.Format("{0}|{1}", SyncDocument.LastModificationDate.HasValue ? SyncDocument.LastModificationDate.Value.ToString("yyyyMMddHHmmssfff") : DateTime.Now.ToString("yyyyMMddHHmmssfff"), SyncDocument.ContentStreamId);
            return SyncId;
        }

        private String GetMD5ForFile(ISession session, String FileNodeRef)
        {
            return "";
        }

        /// <summary>
        /// Check if the element has the KS2CM:Synchronizable attribute
        /// </summary>
        /// <param name="SubF"></param>
        /// <returns></returns>
        private bool IsKS2Synchronizable(ICmisObject SubF)
        {
            var Extensions = SubF.GetExtensions(ExtensionLevel.Properties);
            for (int i = 0; i < Extensions.Count; i++)
            {
                for (int j = 0; j < Extensions[i].Children.Count; j++)
                {
                    if (Extensions[i].Children[j].Value.Equals("P:ks2cm:synchronizable")) return true;
                }
            }

            return false;
        }

        #endregion
    }

    [Serializable]
    public class XMLFILE
    {
        [XmlAttribute("EASYSYNCGUID")]
        public String EASYSYNCGUID { get; set; }

        [XmlAttribute("NAME")]
        public String NAME { get; set; }

        [XmlAttribute("NODEREF")]
        public String NODEREF { get; set; }

        [XmlAttribute("PATH")]
        public String PATH { get; set; }

        [XmlAttribute("KS2LASTWRITE")]
        public String KS2LASTWRITE { get; set; }

        [XmlAttribute("DIGESTMD5")]
        public String DIGESTMD5 { get; set; }
    }

    [Serializable]
    public class XMLFOLDER
    {
        [XmlAttribute("EASYSYNCGUID")]
        public String EASYSYNCGUID { get; set; }

        [XmlAttribute("NAME")]
        public String NAME { get; set; }

        [XmlAttribute("NODEREF")]
        public String NODEREF { get; set; }

        [XmlAttribute("PATH")]
        public String PATH { get; set; }

        [XmlAttribute("KS2LASTWRITE")]
        public String KS2LASTWRITE { get; set; }
    }

    [Serializable]
    public class XMLUSER
    {
        [XmlElement("USERNAME")]
        public String USERNAME { get; set; }

        [XmlElement("SITE")]
        public String SITE { get; set; }

        [XmlElement("ROLE")]
        public String ROLE { get; set; }
    }

    [Serializable]
    public class XMLCHILDS
    {
        [XmlElement("FILE", typeof(XMLFILE))]
        public List<XMLFILE> Files { get; set; }

        [XmlElement("FOLDER", typeof(XMLFOLDER))]
        public List<XMLFOLDER> Folders { get; set; }
    }

    [XmlRoot("ROOT")]
    [Serializable]
    public class XMLRootNode
    {
        [XmlElement("USER", typeof(XMLUSER))]
        public XMLUSER USER { get; set; }

        [XmlElement("CHILDS", typeof(XMLCHILDS))]
        public XMLCHILDS CHILDS { get; set; }

        [XmlElement("EASYSYNC_VERSION", typeof(String))]
        public String EASYSYNC_VERSION { get; set; }

    }
}