using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KaliSyncCommon;

namespace KaliSyncW
{
    public class ThreadContext
    {
        public bool IsFolderContext
        {
            get
            {
                return VirtualFolder != null;
            }
        }

        public bool IsFileContext
        {
            get
            {
                return VirtualFile != null;
            }
        }

        public VirtualFile VirtualFile = null;
        public VirtualSubFolder VirtualFolder = null;

        public ThreadContext(VirtualFile VF)
        {
            this.VirtualFile = VF;
        }

        public ThreadContext(VirtualSubFolder VF)
        {
            this.VirtualFolder = VF;
        }
    }
}
