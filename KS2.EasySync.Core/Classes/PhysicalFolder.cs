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
    public class PhysicalFolder
    {
        public string _FullFolderPath;
        public string _RootPath;
        public bool bIdentified = false;

        public string RelativeFolderPath
        {
            get
            {
                return this._FullFolderPath.Replace(this._RootPath,"");
            }
        }

        public PhysicalFolder(string pFullFolderPath, string pRootPath)
        {
            this._FullFolderPath = pFullFolderPath;
            this._RootPath = pRootPath;
        }
    }
}
