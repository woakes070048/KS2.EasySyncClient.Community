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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace KS2.EasySync.Core
{
    /// <summary>
    /// Tous les objets à synchroniser dérivent de cette classe : Root Folder / Folder / File
    /// </summary>
    [Serializable]
    public class VirtualElement
    {
        #region Ctor

        /// <summary>
        /// Pour un nouveau Virtual Element
        /// </summary>
        /// <param name="pElementType"></param>
        /// <param name="pPathRelative"></param>
        public VirtualElement(VirtualElementType pElementType, string pPathRelative)
        {
            this._ElementId = Guid.NewGuid();
            this.ElementType = pElementType;

            if (pElementType == VirtualElementType.File) this._CurrentName = Path.GetFileName(pPathRelative);
            else this._CurrentName = GetFolderNameFromPath(pPathRelative);

            this.TargetNameAfterSync = this.CurrentName;
            this._PathToRoot = Tools.GetParentPath(pPathRelative) + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Pour le rechargement depuis la base de donnée d'un VirtualElement existant
        /// </summary>
        /// <param name="pElementType"></param>
        /// <param name="pElementId"></param>
        /// <param name="pPathRelative"></param>
        public VirtualElement(VirtualElementType pElementType, Guid pElementId, string pPathRelative)
        {
            this._ElementId = pElementId;
            this.ElementType = pElementType;

            if (pElementType == VirtualElementType.File) this._CurrentName = Path.GetFileName(pPathRelative);
            else this._CurrentName = GetFolderNameFromPath(pPathRelative);
            
            this.TargetNameAfterSync = this.CurrentName;
            this._PathToRoot = Tools.GetParentPath(pPathRelative) + Path.DirectorySeparatorChar;
        }

        #endregion

        #region Members / Properties

        /// <summary>
        /// Pour synchroniser les actions sur l'objet SubElements
        /// </summary>
        private object SubElementLock = new object();

        /// <summary>
        /// La liste des sous-éléments de l'élément en cours
        /// </summary>
        private List<VirtualElement> SubElements = new List<VirtualElement>();

        /// <summary>
        /// Le type de l'élément en cours
        /// </summary>
        public VirtualElementType ElementType;

        private Task _ProcessingTask;

        public Task ProcessingTask
        {
            get
            {
                return _ProcessingTask;
            }
        }

        public bool bIdentified = false;
        public string CustomProperties = "";
        public String RemoteID = "";
        public bool IsDeleted = false;

        private String _CurrentName;
        /// <summary>
        /// Nom de l'élément
        /// </summary>
        public String CurrentName
        {
            get
            {
                return this._CurrentName;
            }
            set
            {
                this._CurrentName = value;
                if (this.ElementType == VirtualElementType.Folder) this.RecomputePathToRootForAllSubElements();
            }
        }

        /// <summary>
        /// Recalcule le chemin jusqu'à la racine pour tous les éléments enfants de cet élément (ne s'applique qu'aux répertoires)
        /// </summary>
        public void RecomputePathToRootForAllSubElements()
        {
            foreach(VirtualElement VE in this.GetSubElementsFilesAndFolders())
            {
                VE.PathToRoot = this.PathRelative + Path.DirectorySeparatorChar;
                if (VE.ElementType == VirtualElementType.Folder)
                {
                    VE.RecomputePathToRootForAllSubElements();
                }
            }
        }

        /// <summary>
        /// Futur nom de l'élement (cette valeur est mis à jour dans le process de synchro, aussitot qu'un changement de nom est détecté)
        /// </summary>
        public String TargetNameAfterSync { get; set; }

        /// <summary>
        /// Nom d'affichage (inclus le statut supprimé / non supprimé )
        /// </summary>
        public String CurrentNameForDisplay
        {
            get
            {
                if (this.ElementType == VirtualElementType.RootFolder) return "Root";
                else if (this.ElementType == VirtualElementType.Folder) return CurrentName + (this.IsDeleted ? "{Deleted}" : "") + String.Format("[{0}]", this.ActionList.Count);
                else return CurrentName + (this.IsDeleted ? "{Deleted}" : "") + String.Format("[{0}]", this.ActionList.Count);
            }
        }

        /// <summary>
        /// VirtualElement parent (ou null si le VirtualElement en cours est le RootFolder)
        /// </summary>
        private VirtualElement _ParentElement;
        public VirtualElement ParentElement
        {
            get
            {
                return this._ParentElement;
            }
        }

        private object _PathToRootLock = new object();

        private String _PathToRoot;
        public String PathToRoot
        {
            get
            {
                lock (_PathToRootLock)
                {
                    return _PathToRoot;
                }
            }
            set
            {
                lock (_PathToRootLock)
                {
                    this._PathToRoot = value;
                }
            }
        }

        /// <summary>
        /// Chemin relatif de l'élément
        /// </summary>
        public String PathRelative
        {
            get
            {
                if (this.ElementType == VirtualElementType.RootFolder) return "";
                else
                {
                    return PathToRoot + this.CurrentName;
                }
            }
        }

        /// <summary>
        /// Chemin relatif de l'élément tel qu'il sera à la fin du processus de synchro
        /// </summary>
        public String TargetPathRelativeAfterSync
        {
            get
            {
                if (this.ElementType == VirtualElementType.RootFolder) return "";
                else
                {
                    return PathToRoot + this.TargetNameAfterSync;
                }
            }
        }

        /// <summary>
        /// Identifiant de l'élément
        /// </summary>
        private Guid _ElementId;
        public Guid ElementId
        {
            get
            {
                return _ElementId;
            }
        }

        private bool _IsDispatched;
        public bool IsDispatched
        {
            get
            {
                return _IsDispatched;
            }
        }

        public Int32 ErrorCount = 0;

        public DateTime? _PostponedDate;

        private object ActionListLock = new object();
        private Queue<SyncActionItem> ActionList = new Queue<SyncActionItem>();

        #endregion

        #region SubElements operations

        public void SubElementAdd(VirtualElement VE)
        {
            lock (SubElementLock)
            {
                VE._ParentElement = this;
                VE.PathToRoot = this.PathRelative + Path.DirectorySeparatorChar;
                this.SubElements.Add(VE);
            }
        }

        public void SubElementDelete(VirtualElement VE, List<VirtualElementDeleted> RemovedElements, Int32? CallerTaskId)
        {
            VE.ActionInvalidateAll(CallerTaskId);
            VE.SubElementsDeleteAllRecursive(RemovedElements, CallerTaskId);

            lock (SubElementLock)
            {
                this.SubElements.RemoveAll(x=>x.ElementId == VE.ElementId);
            }
            if (RemovedElements != null) RemovedElements.Add(new VirtualElementDeleted() { ElementId = VE.ElementId, ElementType = VE.ElementType});
        }

        private void SubElementsDeleteAllRecursive(List<VirtualElementDeleted> RemovedElements, Int32? CallerTaskId)
        {
            lock (SubElementLock)
            {
                foreach (VirtualElement VE in this.SubElements.ToList())
                {
                    VE.ActionInvalidateAll(CallerTaskId);
                    VE.SubElementsDeleteAllRecursive(RemovedElements, CallerTaskId);

                    if (RemovedElements != null ) RemovedElements.Add(new VirtualElementDeleted() { ElementId = VE.ElementId, ElementType = VE.ElementType });
                    this.SubElements.Remove(VE);
                }
            }
        }

        public void SubElementRemove(VirtualElement RemovedElement)
        {
            lock (SubElementLock)
            {
                this.SubElements.RemoveAll(x => x._ElementId.Equals(RemovedElement.ElementId));
            }
        }

        /// <summary>
        /// Récupére la liste des sous-répertoires
        /// </summary>
        /// <returns></returns>
        public List<VirtualElement> GetSubElementsFolders()
        {
            lock (SubElementLock)
            {
                return this.SubElements.Where(x => x.ElementType == VirtualElementType.Folder).ToList();
            }
        }

        /// <summary>
        /// Récupére la liste des fichiers
        /// </summary>
        /// <returns></returns>
        public List<VirtualElement> GetSubElementsFiles()
        {
            lock (SubElementLock)
            {
                return this.SubElements.Where(x => x.ElementType == VirtualElementType.File).ToList();
            }
        }

        /// <summary>
        /// Récupére la liste des répertoires et des fichiers
        /// </summary>
        /// <returns></returns>
        public List<VirtualElement> GetSubElementsFilesAndFolders()
        {
            lock (SubElementLock)
            {
                return this.SubElements.ToList();
            }
        }

        public VirtualElement GetDirectSubElementByPath(String RelativePath, bool IncludeDeleted)
        {
            lock (SubElementLock)
            {
                if (IncludeDeleted) return this.SubElements.FirstOrDefault(x => x.PathRelative == RelativePath);
                else return this.SubElements.FirstOrDefault(x => x.PathRelative == RelativePath && !x.IsDeleted);
            }
        }

        public VirtualElement GetDirectSubElementById(Guid ElementId, bool IncludeDeleted)
        {
            lock (SubElementLock)
            {
                if (IncludeDeleted) return this.SubElements.FirstOrDefault(x => x._ElementId == ElementId);
                else return this.SubElements.FirstOrDefault(x => x._ElementId == ElementId && !x.IsDeleted);
            }
        }

        private object VirtualFileLock = new object();

        public VirtualFolder GetSubFolderByPathRecursive(string FilePathRelative, bool IncludeDeleted = false)
        {
            foreach( VirtualElement VE in SubElements.Where(x=>x.ElementType == VirtualElementType.Folder))
            {
                if (IncludeDeleted)
                {
                    if (VE.PathRelative.Equals(FilePathRelative, StringComparison.OrdinalIgnoreCase))
                    {
                        return (VirtualFolder)VE;
                    }
                }
                else
                {
                    if (VE.PathRelative.Equals(FilePathRelative, StringComparison.OrdinalIgnoreCase) && !VE.IsDeleted)
                    {
                        return (VirtualFolder)VE;
                    }
                }
            }

            foreach (VirtualElement VE in SubElements.Where(x => x.ElementType == VirtualElementType.Folder))
            {
                VirtualFolder VF = VE.GetSubFolderByPathRecursive(FilePathRelative, IncludeDeleted);
                if (VF != null)
                {
                    return VF;
                }
            }

            return null;
        }

        public VirtualFolder GetSubFolderByIDRecursive(Guid FolderId, bool IncludeDeleted = false)
        {
            foreach (VirtualElement VE in SubElements.Where(x => x.ElementType == VirtualElementType.Folder))
            {
                if (IncludeDeleted)
                {
                    if (VE.ElementId.Equals(FolderId))
                    {
                        return (VirtualFolder)VE;
                    }
                }
                else
                {
                    if (VE.ElementId.Equals(FolderId) && !VE.IsDeleted)
                    {
                        return (VirtualFolder)VE;
                    }
                }
            }

            foreach (VirtualElement VE in SubElements.Where(x => x.ElementType == VirtualElementType.Folder))
            {
                VirtualFolder VF = VE.GetSubFolderByIDRecursive(FolderId, IncludeDeleted);
                if (VF != null)
                {
                    return VF;
                }
            }

            return null;
        }

        #endregion

        #region Actions

        /// <summary>
        /// Nombre d'action d'upload prévues pour cet élément
        /// </summary>
        public Int32 UploadActionCount
        {
            get
            {
                lock (ActionListLock)
                {
                    return ActionList.Count(x => x.Action == SyncActionEnum.FileUploadNew || x.Action == SyncActionEnum.FileUploadExisting || x.Action == SyncActionEnum.FileUploadConflict);
                }
            }
        }

        /// <summary>
        /// Nombre d'actions de download prévues pour cet élément
        /// </summary>
        public Int32 DownloadActionCount
        {
            get
            {
                lock (ActionListLock)
                {
                    return ActionList.Count(x => x.Action == SyncActionEnum.FileDownloadNew || x.Action == SyncActionEnum.FileDownloadExisting);
                }
            }
        }

        public List<SyncActionItemReport> GetActionsList()
        {
            lock(ActionListLock)
            {
                List<SyncActionItemReport> ReturnValue = new List<SyncActionItemReport>();

                foreach (var Element in ActionList.ToList())
                {
                    ReturnValue.Add(new SyncActionItemReport() { Action = Element.Action, IsPosponed = _PostponedDate.HasValue });
                }

                return ReturnValue;
            }
        }

        /// <summary>
        /// Permet de récupérer la liste des actions à venir (incluant le potentiel délai lié au VirtualElement)
        /// </summary>
        public DateTime? NextActionDateIncludePostpone
        {
            get
            {
                if (_IsDispatched) return null; //The element is already busy, do not perform anything more

                if (_PostponedDate.HasValue)
                {
                    if (DateTime.Now - _PostponedDate.Value > new TimeSpan(0, 0, 20)) //Pospone delay is 20 seconds
                    {
                        return _PostponedDate.Value;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    lock (ActionListLock)
                    {
                        if (ActionList.Count > 0)
                        {
                            return ActionList.Peek().ActionDate;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Retourne la prochaine action sans prendre en compte le processus de postpone
        /// </summary>
        public DateTime? NextActionDate
        {
            get
            {
                lock (ActionListLock)
                {
                    if (ActionList.Count > 0)
                    {
                        return ActionList.Peek().ActionDate;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        public void SetDispatched(Task T)
        {
            _ProcessingTask = T;
            _IsDispatched = true;
        }

        public bool HasActions
        {
            get
            {
                lock (ActionListLock)
                {
                    return ActionList.Count > 0;
                }
            }
        }

        public bool ActionAdd(SyncActionItem SAI)
        {
            lock (ActionListLock)
            {
                if (ActionList.Count(x => x.Equals(SAI)) == 0)
                {
                    ActionList.Enqueue(SAI);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public SyncActionItem ActionGetNext()
        {
            lock (ActionListLock)
            {
                if (ActionList.Count == 0)
                {
                    return null;
                }
                else
                {
                    return ActionList.Peek();
                }
            }
        }

        public void ActionCloseCurrent(bool IsMaxRetryReached = false)
        {
            lock (ActionListLock)
            {
                _IsDispatched = false;
                ErrorCount = 0;
                if (ActionList.Count > 0)
                {
                    SyncActionItem SAI = ActionList.Dequeue(); //Actions may have been cancelled and the tread running the action is still not notified. We catch a potential error
                    if (IsMaxRetryReached)
                    {
                        Logger logger = LogManager.GetCurrentClassLogger();
                        logger.Debug(String.Format("[{0}]This action has been removed due to max error count reached", SAI.ActionItemId));
                    }
                }
            }
        }

        /// <summary>
        /// Invalide les actions en cours pour l'élément et attend la fin de l'action
        /// </summary>
        protected void ActionInvalidateAll(Int32? CallerTaskId)
        {
            lock (ActionListLock)
            {
                ActionList.Clear();
            }

            if (_IsDispatched && _ProcessingTask.Id != CallerTaskId && !_ProcessingTask.IsCompleted && !_ProcessingTask.IsCanceled && !_ProcessingTask.IsFaulted)
            {
                //Une action est en cours pour cet élément, on attand qu'elle termine (sauf si l'appelant est la tache elle-même)
                Debug.WriteLine("Waiting for task completion " + _ProcessingTask.Id.ToString());
                try { _ProcessingTask.Wait(); } //TODO-NTH : Could use a CancellationToken http://stackoverflow.com/questions/4783865/how-do-i-abort-cancel-tpl-tasks
                catch { }
            }
            _IsDispatched = false;
            ErrorCount = 0;
        }

        /// <summary>
        /// Permet de déterminer si l'action de l'élément à été annulée (elle n'apparait plus dans la liste des actions)
        /// </summary>
        /// <param name="SAI"></param>
        /// <returns></returns>
        public bool IsActionCancelled(SyncActionItem SAI)
        {
            lock (ActionListLock)
            {
                return !ActionList.Contains(SAI);
            }
        }

        public void ActionPostpone()
        {
            this.ErrorCount++;
            this._IsDispatched = false;
            if (this.ErrorCount > 3)
            {
                //Cancel action
                ActionCloseCurrent(true);
            }
            else
            {
                this._PostponedDate = DateTime.Now;
            }
        }

        public void ActionResetPostpone()
        {
            this._PostponedDate = null;
        }

        public void CancelAllActionsRecursive(Int32? CallerTaskId)
        {
            ActionInvalidateAll(CallerTaskId);
            foreach (VirtualElement VE in this.SubElements)
            {
                VE.CancelAllActionsRecursive(CallerTaskId);
            }
        }

        /*
        public void ReplaceCurrentAction(SyncActionEnum NewFirstAction)
        {
            lock( ActionListLock)
            {
                ActionList.Dequeue(); //Remote the first element
                var items = ActionList.ToArray(); //Save the remaining actions to an array
                ActionList.Clear(); //Clear the queue
                ActionList.Enqueue(new SyncActionItem(NewFirstAction)); //Insert the new element (ie replace existing first element by a new one)
                foreach (var item in items)
                {
                    ActionList.Enqueue(item); //Restore the other pending actions in the queue
                }
            }
        }
        */
        #endregion

        /// <summary>
        /// Prepare the virtual folder to be compared to remote
        /// </summary>
        public void ClearIdentification()
        {
            this.bIdentified = false;
            this.TargetNameAfterSync = this.CurrentName;

            foreach (var Element in this.SubElements)
            {
                Element.ClearIdentification();
            }
        }

        /// <summary>
        /// Recursively cancell all actions for the element and its subelements.
        /// Set the deleted flag
        /// </summary>
        public void SetDeleted(Int32? CallerTaskId)
        {
            this.ActionInvalidateAll(CallerTaskId);
            this.IsDeleted = true;

            if (this.ElementType != VirtualElementType.File)
            {
                foreach (VirtualFolder VF in this.GetSubElementsFolders())
                {
                    VF.SetDeleted(CallerTaskId);
                }

                foreach (VirtualFile VF in this.GetSubElementsFiles())
                {
                    VF.SetDeleted(CallerTaskId);
                }
            }
        }

        public void Dispose(Int32? CallerTaskId)
        {
            this.ActionInvalidateAll(CallerTaskId);
        }

        private string GetFolderNameFromPath(String ElementPath)
        {
            string FolderName;
            try
            {
                FolderName = ElementPath.Substring(ElementPath.LastIndexOf(Path.DirectorySeparatorChar) + 1);
            }
            catch
            {
                FolderName = "";
            }
            return FolderName;
        }
    }
}