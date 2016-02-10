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
using System.Security.Cryptography;
using System.Text;

namespace KS2.EasySync.Core
{
    public class PhysicalFile
    {
        private string _RootFolder;
        public string _FileFullPath;

        private  string _FileHash;
        public string FileHash
        {
            get
            {
                return _FileHash;
            }
        }

        public bool bIdentified = false;

        public string FileRelativePath
        {
            get
            {
                return _FileFullPath.Replace(_RootFolder, "");
            }
        }

        public string FileRelativeDirectory
        {
            get
            {
                return Path.GetDirectoryName(FileRelativePath);
            }
        }

        public PhysicalFile(string pFileFullPath, string pRootFolder)
        {
            this._FileFullPath = pFileFullPath;
            this._RootFolder = pRootFolder;
            
            //TODO : Ne pas recalculer le hash systématiquement
            //Se baser sur la data de derniére modif
            //Utiliser le hash pour eviter les conflits à l'upload
            string Hash;
            Tools.GetFileHash(pFileFullPath, out Hash); //If the file is in use, we may not be able to compute the hash, this possibility is handled in the sync algotithm
            this._FileHash = Hash;
        }
    }
}
