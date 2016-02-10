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
using System.Runtime.Serialization;
using System.Text;

namespace KS2.EasySync.Core
{
    [Serializable]
    public enum BasicActionEnum
    {
        None,
        FileCreated,
        FileChanged,
        FileDeleted,
        FileRenamed,
        FileMoved,
        DirCreated,
        DirDeleted,
        DirRenamed,
    }

    [Serializable]
    [Flags]
    public enum SyncEventEnum
    {
        RemoteCreate = 1,           //binary 1
        RemoteUpdate = 2,           //binary 10
        RemoteDelete = 4,           //binary 100
        RemoteMove = 8,             //binary 1000

        LocalCreate = 16,           //binary 10000
        LocalRename = 32,           //binary 100000
        LocalMove = 64,             //binary 1000000
        LocalMoveAndRename = 128,   //binary 10000000
        LocalUpdate = 256,          //binary 100000000
        LocalDelete = 512,          //binary 1000000000

        BothSideUpdate = 1024,      //binary 10000000000

        RemoteRename = 2048,        //binary 100000000000
    }

    [Serializable]
    public enum LocalAndRemoteComparisonResult
    {
        None,
        RemoteUpdate
    }

    [Serializable]
    public enum ActionResult
    {
        Success,
        Retry,
        Cancel,
        RemoteServerUnreachable,
    }

    [Serializable]
    public enum SyncActionEnum
    {
        FileUploadNew,
        FileUploadExisting,
        FileDownloadNew,
        FileDownloadExisting,
        FileRemoteDelete,
        FileRemoteRename,
        FileLocalMove,
        FileLocalDelete,
        FileLocalRename,
        FileUploadConflict,
        FolderRemoteCreate,
        FolderRemoteDelete,
        FolderRemoteRename,
        FolderLocalCreate,
        FolderLocalDelete,
        FolderLocalMove,
        FolderLocalRename,
        FileRemoteMove,
        FileRemoteMoveAndRename,
        FolderRemoteMove,
        FolderRemoteMoveAndRename,
        FolderLocalParse,
        EngineInit,
    }

    [Serializable]
    [DataContract]
    public enum LockStatusEnum
    {
        [EnumMember]
        NotLocked,
        [EnumMember]
        Locked,
        [EnumMember]
        InvalidFile,
    }

    [Serializable]
    public enum HostPlatform
    {
        Unknown,
        Windows,
        Unix,
        MacOsX
    }

    [Serializable]
    public enum OrchestratorAction
    {
        FullScan,
        RemoteSync
    }

    [Serializable]
    public enum RepositoryElementType
    {
        Folder,
        File
    }

    [Serializable]
    public enum VirtualElementType
    {
        RootFolder,
        Folder,
        File
    }
}
