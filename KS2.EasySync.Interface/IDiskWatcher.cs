/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KS2.EasySync.Interface
{
    public interface IDiskWatcher
    {
       event FileSystemEventHandler FileCreated;
       event FileSystemEventHandler FileChanged;
       event RenamedEventHandler    FileRenamed;
       event FileSystemEventHandler FileDeleted;

       event FileSystemEventHandler DirCreated;
       event RenamedEventHandler    DirRenamed;
       event FileSystemEventHandler DirDeleted;

       event ErrorEventHandler Error;

       void Init(String RootFolder);

       void Start();

       void Stop();
    }
}
