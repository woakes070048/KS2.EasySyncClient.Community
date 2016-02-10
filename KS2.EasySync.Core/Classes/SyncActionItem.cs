/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KS2.EasySync.Core
{
    public class SyncActionItem
    {
        public DateTime ActionDate;
        public Guid ActionItemId;
        public SyncActionEnum Action;
        public String StringParameter;
        public RepositoryElement RemoteElementParameter;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public SyncActionItem(Guid pActionItemId, SyncActionEnum pAction)
        {
            this.ActionItemId = pActionItemId;
            this.Action = pAction;
            this.StringParameter = null;
            this.RemoteElementParameter = null;
            this.ActionDate = DateTime.Now;
        }

        public SyncActionItem(Guid pActionItemId, SyncActionEnum pAction, RepositoryElement pRemoteFileParameter)
        {
            this.ActionItemId = pActionItemId;
            this.Action = pAction;
            this.StringParameter = null;
            this.RemoteElementParameter = pRemoteFileParameter;
            this.ActionDate = DateTime.Now;
        }

        public SyncActionItem(Guid pActionItemId, SyncActionEnum pAction, String pStringParameter)
        {
            this.ActionItemId = pActionItemId;
            this.Action = pAction;
            this.StringParameter = pStringParameter;
            this.RemoteElementParameter = null;
            this.ActionDate = DateTime.Now;
        }

        /// <summary>
        /// Compare 2 SyncActionItem objects
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            SyncActionItem objToCompare = (SyncActionItem)obj;

            if (StringParameter == null && RemoteElementParameter == null)
            {
                return this.Action.Equals(objToCompare.Action);
            }
            else if (StringParameter != null)
            {
                return this.Action.Equals(objToCompare.Action) && this.StringParameter.Equals(objToCompare.StringParameter);
            }
            else
            {
                return this.Action.Equals(objToCompare.Action) && this.RemoteElementParameter.ElementId.Equals(objToCompare.RemoteElementParameter.ElementId);
            }
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class SyncActionItemReport
    {
        public SyncActionEnum Action { get; set; }
        public bool IsPosponed { get; set; }
    }
}
