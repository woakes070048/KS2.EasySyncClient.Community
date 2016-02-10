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
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KS2.EasySync.Core
{
    public class SyncEventIgnoreItem
    {
        public Guid SyncEventId;
        public SyncEventEnum Event;
        public string NewOrCurrentFilePathFull;

        public SyncEventIgnoreItem(SyncEventEnum pAction,string pNewOrCurrentFilePathFull)
        {
            this.SyncEventId = Guid.NewGuid();
            this.Event = pAction;
            this.NewOrCurrentFilePathFull = pNewOrCurrentFilePathFull;
        }
    }
}
