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
using System.Threading.Tasks;

namespace KS2.EasySync.Core
{
    [Serializable]
    public class FSSyncEvent : IComparable<FSSyncEvent>
    {
        public BasicActionEnum Action;
        public string OldOrCurrentFilePathFull;
        public string NewFilePathFull;

        public string OldOrCurrentFileName
        {
            get
            {
                return Path.GetFileName(OldOrCurrentFilePathFull);
            }
        }

        public string NewFileName
        {
            get
            {
                if (NewFilePathFull.Equals(String.Empty)) return String.Empty;
                return Path.GetFileName(NewFilePathFull);
            }
        }

        public Int32 Index;

        public FSSyncEvent(BasicActionEnum pAction, string sOldOrCurrentFilePathFull, string sNewFilePathFull, Int32 pIndex = 0)
        {
            this.Index = pIndex;
            this.Action = pAction;
            this.OldOrCurrentFilePathFull = sOldOrCurrentFilePathFull;
            this.NewFilePathFull = sNewFilePathFull;
        }

        public int CompareTo(FSSyncEvent other)
        {
            return this.Index.CompareTo(other.Index);
        }
    }
}
