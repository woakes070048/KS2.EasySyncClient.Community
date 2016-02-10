using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KaliSyncW
{
    /// <summary>
    /// Represent the folder present on the PC where the program is launched
    /// For cleaning purpose only
    /// </summary>
    /*
    class SystemFolder
    {
        public DirectoryInfo _ThisDirectory;
        public List<FileInfo> _SubFiles ;
        public List<SystemFolder> _SubFolders;

        public bool bIdentified = false;
        public string _FullFolderPath;

        public SystemFolder(string pFullFolderPath)
        {
            this._FullFolderPath = pFullFolderPath;
            this._ThisDirectory = new DirectoryInfo(this._FullFolderPath);
            this._SubFiles = new List<FileInfo>();
            this._SubFolders = new List<SystemFolder>();

            RebuildTree();
        }

        public void RebuildTree()
        {
            DirectoryInfo t = new DirectoryInfo(this._FullFolderPath);
            DirectoryInfo[] SubDirs = t.GetDirectories();
            for (int i = 0; i < SubDirs.Count(); i++)
            {
                this._SubFolders.Add(new SystemFolder(SubDirs[i].FullName));
            }

            FileInfo[] Files = t.GetFiles();
            for (int i = 0; i < Files.Count(); i++)
            {
                this._SubFiles.Add(Files[i]);
            }
        }

        public void Clean()
        {
            for (int i = 0; i < _SubFolders.Count(); i++)
            {
                this._SubFolders[i].Clean();
            }

            for (int i = 0; i < _SubFiles.Count(); i++)
            {
                this._SubFiles[i].Delete();
            }

            for (int i = 0; i < _SubFolders.Count(); i++)
            {
                this._SubFolders[i]._ThisDirectory.Delete();
            }
        }
    }*/
}
