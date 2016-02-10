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
using System.Threading.Tasks;

namespace KS2.EasySync.Core
{
    public interface IKaliSyncPlugin
    {
        event EventHandler AuthenticationError;
        event EventHandler ProxyError;
        event LogEventHandler LogOutput;

        ActionResult GetFileList(Guid pInstanceId, bool pIsFirstComparerCall, ref List<RepositoryElement> RepositoryFileList);
        ActionResult RenameRepositoryFile(VirtualFile pVirtualFile, SyncActionItem SAI);
        ActionResult MoveRepositoryFile(VirtualFile pVirtualFile, SyncActionItem SAI);
        ActionResult MoveRenameRepositoryFile(VirtualFile pVirtualFile, SyncActionItem SAI);
        ActionResult DeleteRepositoryFile(VirtualFile pVirtualFile, SyncActionItem SAI);
        ActionResult UploadVirtualFile(VirtualFile pVirtualFile, SyncActionItem SAI);
        ActionResult DownloadVirtualFile(VirtualFile pVirtualFile, SyncActionItem SAI);
        ActionResult CreateFolder(VirtualFolder pVirtualFolder, SyncActionItem SAI);
        ActionResult DeleteRepositoryFolder(VirtualFolder FolderToProcess, SyncActionItem SAI);
        ActionResult RenameRepositoryFolder(VirtualFolder FolderToProcess, string p);

        void LinkToEngine(IPluginHost host, Guid InstanceId);
        void Init(string RemoteRepositoryParameters);
        void SetProxyParameter(Int16 ProxyMode, string ProxyURL, bool ProxyAuthentication, string ProxyLogin, string ProxyPassword);
        string GetLogin();
        string GetPassword();
        string GetEndPoint();
        void SetNewCredentials(string Login, string Password);
        ActionResult Setup(String URL, String Login, String Password, List<String> TargetRepository);

        LocalAndRemoteComparisonResult CompareFile(VirtualFile pVirtualFile, RepositoryElement pRepositoryFile);
    }

    public interface IPluginHost
    {
        string GetTemporaryFolderPath();
        string GetRootFolderPath();
        ActionResult BuildFilePath(string pFullPath);
        ActionResult BuildFolderPath(string pFullPath);
        ActionResult SecureMoveRenamePhysicalFile(VirtualFile pVirtualFile, string NewLocationPathFull);
        ActionResult SecureCopyPhysicalFile(VirtualFile pVirtualFile, string NewLocationPathFull);
        ActionResult SecureMoveRenamePhysicalFileFromTemp(VirtualFile pVirtualFile, string NewLocationPathFull);
        ActionResult SecureMovePhysicalFileFromTemp(VirtualFile pVirtualFile);
        void DispatchEvent(SyncEventItem SEI);
        void VirtualElement_Serialize(VirtualElement VF);
        bool VirtualFile_DeleteTemporaryCopy(VirtualFile VF);
        bool VirtualFile_CreateTemporaryCopy(VirtualFile VF); //Test result when called in plugins
        bool VirtualFile_DeleteSeedCopy(VirtualFile VF);
        //bool VirtualFile_CreateSeedCopy(VirtualFile VF); //Test result when called in plugins
        
        //void VirtualFile_RemoveFromFolder(VirtualFile VF);

        string GetRelativeFilePathTemp(string TempFullFilePath);
        string GetFullFilePathTemp(string TempRelativeFilePath);
        string GetRelativeElementPath(string FullFilePath);
        string GetFullElementPath(string RelativeFilePath);
        String GetParamValue(String ParamName);
    }
}
