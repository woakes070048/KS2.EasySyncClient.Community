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
using System.Linq;
using System.Text;
using System.IO;

namespace KS2.EasySync.Mac
{
    /// <summary>
    /// File System Watcher for Mac.
    /// Cannot use the mono FileSystemWatcher which have a bug : https://bugzilla.novell.com/show_bug.cgi?id=574564
    /// Need to combine FSWatcher and FSEvents to get something accurate
    /// </summary>
    public class MacDiskWatcher : IDiskWatcher
    {
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
            //TODO
        }

        public void Start()
        {
            //TODO
        }

        public void Stop()
        {
            //TODO
        }
    }
}
