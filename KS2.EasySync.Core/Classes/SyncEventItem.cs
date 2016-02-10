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

namespace KS2.EasySync.Core
{
    public class SyncEventItem
    {
        private Guid _SyncEventId;
        public Guid SyncEventId
        {
            get
            {
                return _SyncEventId;
            }
        }

        private SyncEventEnum _SyncEvent;
        public SyncEventEnum SyncEvent
        {
            get
            {
                return _SyncEvent;
            }
        }

        public Guid LocalElementId;

        private string _ActualEntityPathFull;
        public string ActualEntityPathFull
        {
            get
            {
                if (_ActualEntityPathFull == null) throw new NullReferenceException();
                return _ActualEntityPathFull;
            }
        }

        private string _OldFilePathFull;
        public string OldFilePathFull
        {
            get
            {
                if (_OldFilePathFull == null) throw new NullReferenceException();
                return _OldFilePathFull;
            }
        }

        private RepositoryElement _RemoteElement;
        public RepositoryElement RemoteElement
        {
            get
            {
                if (_RemoteElement == null) throw new NullReferenceException();
                return _RemoteElement;
            }
        }

        private bool _IsDirectoryEvent;
        public bool IsDirectoryEvent
        {
            get
            {
                return _IsDirectoryEvent;
            }
        }

        private bool _IsStartupEvent;
        public bool IsStartupEvent
        {
            get
            {
                return _IsStartupEvent;
            }
        }

        //private object StatusLock = new object();
        //private bool _IsActionActive;
        /*public bool IsActionActive
        {
            get
            {
                lock (StatusLock)
                {
                    return _IsActionActive;
                }
            }
            set
            {
                lock (StatusLock)
                {
                    _IsActionActive = value;
                }
            }
        }*/

        //public Int32 ErrorCount = 0;
        /*
        public string ActualFilePathRelative
        {
            get
            {
                return ActualFilePathFull.Replace(Globals._GlbLocalFileStoragePath, "");
            }
        }

        public string OldFilePathRelative
        {
            get
            {
                return OldFilePathFull.Replace(Globals._GlbLocalFileStoragePath, "");
            }
        }
        */
        public SyncEventItem(SyncEventEnum pAction, Guid pEntityID, String pActualFilePathFull, String pOldFilePathFull, bool IsDirectoryEvent, bool IsStartupEvent)
        {
            this._SyncEventId = Guid.NewGuid();
            this._SyncEvent = pAction;
            this.LocalElementId = pEntityID;
            this._ActualEntityPathFull = pActualFilePathFull;
            this._OldFilePathFull = pOldFilePathFull;
            this._RemoteElement = null;
            this._IsDirectoryEvent = IsDirectoryEvent;
            this._IsStartupEvent = IsStartupEvent;
        }

        public SyncEventItem(SyncEventEnum pAction, Guid pEntityID, bool IsDirectoryEvent, bool IsStartupEvent)
        {
            this._SyncEventId = Guid.NewGuid();
            this._SyncEvent = pAction;
            this.LocalElementId = pEntityID;
            this._ActualEntityPathFull = null;
            this._OldFilePathFull = null;
            this._RemoteElement = null;
            this._IsDirectoryEvent = IsDirectoryEvent;
            this._IsStartupEvent = IsStartupEvent;
        }

        public SyncEventItem(SyncEventEnum pAction, String pActualFilePathFull, bool IsDirectoryEvent, bool IsStartupEvent)
        {
            this._SyncEventId = Guid.NewGuid();
            this._SyncEvent = pAction;
            this.LocalElementId = Guid.Empty;
            this._ActualEntityPathFull = pActualFilePathFull;
            this._OldFilePathFull = null;
            this._RemoteElement = null;
            this._IsDirectoryEvent = IsDirectoryEvent;
            this._IsStartupEvent = IsStartupEvent;
        }

        public SyncEventItem(SyncEventEnum pAction, String pActualFilePathFull, String pOldFilePathFull, bool IsDirectoryEvent, bool IsStartupEvent)
        {
            this._SyncEventId = Guid.NewGuid();
            this._SyncEvent = pAction;
            this.LocalElementId = Guid.Empty;
            this._ActualEntityPathFull = pActualFilePathFull;
            this._OldFilePathFull = pOldFilePathFull;
            this._RemoteElement = null;
            this._IsDirectoryEvent = IsDirectoryEvent;
            this._IsStartupEvent = IsStartupEvent;
        }


        public SyncEventItem(SyncEventEnum pAction, RepositoryElement pRemoteFile, bool IsDirectoryEvent, bool IsStartupEvent)
        {
            this._SyncEventId = Guid.NewGuid();
            this._SyncEvent = pAction;
            this.LocalElementId = Guid.Empty;
            this._ActualEntityPathFull = null;
            this._OldFilePathFull = null; ;
            this._RemoteElement = pRemoteFile;
            this._IsDirectoryEvent = IsDirectoryEvent;
            this._IsStartupEvent = IsStartupEvent;
        }

        public SyncEventItem(SyncEventEnum pAction, Guid pLocalEntityID, RepositoryElement pRemoteFile, bool IsDirectoryEvent, bool IsStartupEvent)
        {
            this._SyncEventId = Guid.NewGuid();
            this._SyncEvent = pAction;
            this.LocalElementId = pLocalEntityID;
            this._ActualEntityPathFull = null;
            this._OldFilePathFull = null; ;
            this._RemoteElement = pRemoteFile;
            this._IsDirectoryEvent = IsDirectoryEvent;
            this._IsStartupEvent = IsStartupEvent;
        }

        public SyncEventItem(SyncEventEnum pAction, Guid pLocalEntityID, String pActualFilePathFull, bool IsDirectoryEvent, bool IsStartupEvent)
        {
            this._SyncEventId = Guid.NewGuid();
            this._SyncEvent = pAction;
            this.LocalElementId = pLocalEntityID;
            this._ActualEntityPathFull = pActualFilePathFull;
            this._OldFilePathFull = null;
            this._RemoteElement = null;
            this._IsDirectoryEvent = IsDirectoryEvent;
            this._IsStartupEvent = IsStartupEvent;
        }
    }
}
