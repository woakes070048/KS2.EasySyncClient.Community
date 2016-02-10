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
#if __MonoCS__
	using System.IO;
#else
using Alphaleonis.Win32.Filesystem;
#endif

namespace KS2.EasySync.Core
{
    public class PhysicalRootFolder
    {
        public string _FullFolderPath;
        public string _RootPath;
        public bool bIdentified = false;
        public List<PhysicalFile> _Files;
        public List<PhysicalFolder> _Folders;
        public object AccessLock = new object();

        public string RelativeFolderPath
        {
            get
            {
                return this._FullFolderPath.Replace(this._RootPath, "");
            }
        }

        /// <summary>
        /// Parse a folder recursively to get all inside elements
        /// </summary>
        /// <param name="pFullFolderPath"></param>
        /// <param name="RebuildTree">Request a rebuild within constructor (true when running in a background thread, false when running from the main thread)</param>
        public PhysicalRootFolder(string pFullFolderPath) //Called for root folder
        {
            this._FullFolderPath = pFullFolderPath;
            this._RootPath = pFullFolderPath;
            this._Files = new List<PhysicalFile>();
            this._Folders = new List<PhysicalFolder>();

            DirectoryInfo t = new DirectoryInfo(this._FullFolderPath);

            foreach (DirectoryInfo DI in t.EnumerateDirectories("*", System.IO.SearchOption.AllDirectories))
            {
                this._Folders.Add(new PhysicalFolder(DI.FullName, this._RootPath));
            }

            foreach (FileInfo FI in t.EnumerateFiles("*", System.IO.SearchOption.AllDirectories))
            {
                if (!FI.Name.StartsWith("~") && !FI.Name.StartsWith(".DS_Store")) _Files.Add(new PhysicalFile(FI.FullName, this._RootPath));
            }
        }
    }
}
