/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using KS2.EasySync.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KS2.EasySync.Windows
{
    public class WindowsDiskWatcher : IDiskWatcher
    {
        private FileSystemWatcher _FSWatcherFiles;
        private FileSystemWatcher _FSWatcherDirectories;
        private String _FolderToMonitor;

        public event FileSystemEventHandler FileCreated;

        public event FileSystemEventHandler FileChanged;

        public event RenamedEventHandler FileRenamed;

        public event FileSystemEventHandler FileDeleted;

        public event FileSystemEventHandler DirCreated;

        public event RenamedEventHandler DirRenamed;

        public event FileSystemEventHandler DirDeleted;

        public event ErrorEventHandler Error;

        public void Init(string RootFolder)
        {
            this._FolderToMonitor = RootFolder;

            //Files watcher
            _FSWatcherFiles = new FileSystemWatcher();
            _FSWatcherFiles.IncludeSubdirectories = true;
            _FSWatcherFiles.InternalBufferSize = 65536;
            _FSWatcherFiles.Path = this._FolderToMonitor;
            _FSWatcherFiles.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            _FSWatcherFiles.Created += FS_FileCreated;
            _FSWatcherFiles.Changed += FS_FileChanged;
            _FSWatcherFiles.Renamed += FS_FileRenamed;
            _FSWatcherFiles.Deleted += FS_FileDeleted;
            _FSWatcherFiles.Error += FS_Error;
            _FSWatcherFiles.Filter = "*.*";

            //Folder watcher
            _FSWatcherDirectories = new FileSystemWatcher();
            _FSWatcherDirectories.IncludeSubdirectories = true;
            _FSWatcherDirectories.InternalBufferSize = 65536;
            _FSWatcherDirectories.Path = this._FolderToMonitor;
            _FSWatcherDirectories.NotifyFilter = NotifyFilters.DirectoryName;
            _FSWatcherDirectories.Created += FS_DirCreated;
            _FSWatcherDirectories.Renamed += FS_DirRenamed;
            _FSWatcherDirectories.Deleted += FS_DirDeleted;
            _FSWatcherDirectories.Error += FS_Error;
            _FSWatcherDirectories.Filter = "*.*";
        }

        public void Start()
        {
            _FSWatcherFiles.EnableRaisingEvents = true;
            _FSWatcherDirectories.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            _FSWatcherFiles.EnableRaisingEvents = false;
            _FSWatcherDirectories.EnableRaisingEvents = false;
        }

        private void FS_DirCreated(object sender, FileSystemEventArgs e)
        {
            if (DirCreated != null) DirCreated(sender, e);
        }

        private void FS_DirRenamed(object sender, RenamedEventArgs e)
        {
            if (DirRenamed != null) DirRenamed(sender, e);
        }

        private void FS_DirDeleted(object sender, FileSystemEventArgs e)
        {
            if (DirDeleted != null) DirDeleted(sender, e);
        }

        private void FS_FileCreated(object sender, FileSystemEventArgs e)
        {
            if (FileCreated != null) FileCreated(sender, e);
        }

        private void FS_FileChanged(object sender, FileSystemEventArgs e)
        {
            if (FileChanged != null) FileChanged(sender, e);
        }

        private void FS_FileRenamed(object sender, RenamedEventArgs e)
        {
            if (FileRenamed != null) FileRenamed(sender, e);
        }

        private void FS_FileDeleted(object sender, FileSystemEventArgs e)
        {
            if (FileDeleted != null) FileDeleted(sender, e);
        }

        private void FS_Error(object sender, ErrorEventArgs e)
        {
            if (Error != null) Error(sender, e);
        }
    }
}
