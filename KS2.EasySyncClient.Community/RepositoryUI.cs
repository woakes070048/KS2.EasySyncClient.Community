/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using KS2.EasySync.Core;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace KS2.EasySyncClient
{
    public class RepositoryUI
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public Repository RM;
        public ToolStripMenuItem MenuItem_Main;
        public ToolStripMenuItem MenuItem_Start;
        public ToolStripMenuItem MenuItem_Stop;
        public ToolStripMenuItem MenuItem_OpenLocalFolder;
        public ToolStripMenuItem MenuItem_Delete;
        public ToolStripMenuItem MenuItem_Edit;
        public Int32 RepositoryId;

        public String SiteName
        {
            get
            {
                if (RM != null) return RM.SiteName;
                else return "";
            }
        }

        public String LocalPath
        {
            get
            {
                if (RM != null) return RM.LocalPath;
                else return "";
            }
        }

        public RepositoryUI()
        {
            MenuItem_Main = new System.Windows.Forms.ToolStripMenuItem();
            MenuItem_Start = new System.Windows.Forms.ToolStripMenuItem();
            MenuItem_Stop = new System.Windows.Forms.ToolStripMenuItem();
            MenuItem_OpenLocalFolder = new System.Windows.Forms.ToolStripMenuItem();
            MenuItem_Delete = new System.Windows.Forms.ToolStripMenuItem();
            MenuItem_Edit = new System.Windows.Forms.ToolStripMenuItem();
        }

        public bool InitRepository(Int32 RepositoryId)
        {
            this.RepositoryId = RepositoryId;

            try
            {
                RM = new Repository(RepositoryId);
            }
            catch (Exception ex)
            {
                logger.Trace("Repository init failed. Stopping ...");
                logger.Trace(ex.Message);
                return false;
            }

            MenuItem_Main.Name = "Main" + RM.RepositoryId;
            MenuItem_Main.Size = new System.Drawing.Size(128, 22);
            MenuItem_Main.Text = RM.SiteName;

            MenuItem_Start.Name = "Start" + RM.RepositoryId;
            MenuItem_Start.Size = new System.Drawing.Size(128, 22);
            MenuItem_Start.Text = Program.RessourceManager.GetString("MENU_START");
            MenuItem_Start.Click += Menu_Start_Click;

            MenuItem_Stop.Name = "Stop" + RM.RepositoryId;
            MenuItem_Stop.Size = new System.Drawing.Size(128, 22);
            MenuItem_Stop.Text = Program.RessourceManager.GetString("MENU_STOP");
            MenuItem_Stop.Click += Menu_Stop_Click;

            MenuItem_OpenLocalFolder.Name = "OpenLocalFolder" + RM.RepositoryId;
            MenuItem_OpenLocalFolder.Size = new System.Drawing.Size(128, 22);
            MenuItem_OpenLocalFolder.Text = Program.RessourceManager.GetString("MENU_OPEN_FOLDER");
            MenuItem_OpenLocalFolder.Click += Menu_OpenLocalFolder_Click;

            MenuItem_Delete.Name = "DeleteSync" + RM.RepositoryId;
            MenuItem_Delete.Size = new System.Drawing.Size(128, 22);
            MenuItem_Delete.Text = Program.RessourceManager.GetString("MENU_DELETE");
            MenuItem_Delete.Click += Menu_DeleteSync_Click;

            MenuItem_Edit.Name = "EditSyn" + RM.RepositoryId;
            MenuItem_Edit.Size = new System.Drawing.Size(128, 22);
            MenuItem_Edit.Text = Program.RessourceManager.GetString("MENU_EDIT");
            MenuItem_Edit.Click += Menu_EditSync_Click;

            MenuItem_Main.DropDownItems.Add(MenuItem_Start);
            MenuItem_Main.DropDownItems.Add(MenuItem_Stop);
            MenuItem_Main.DropDownItems.Add(MenuItem_OpenLocalFolder);
            MenuItem_Main.DropDownItems.Add(MenuItem_Edit);
            MenuItem_Main.DropDownItems.Add(MenuItem_Delete);

            return true;
        }

        internal void Menu_Start_Click(object sender, EventArgs e)
        {
            if (!this.StartEngine())
            {
				MenuItem_Start.Enabled = false;
				MenuItem_Stop.Enabled = false;
				MenuItem_OpenLocalFolder.Enabled = false;
            }
        }

        internal void Menu_Stop_Click(object sender, EventArgs e)
        {
            this.StopEngine();
        }

        internal void Menu_OpenLocalFolder_Click(object sender, EventArgs e)
        {
            this.RM.OpenLocalPath();
        }

        internal void Menu_DeleteSync_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(String.Format(Program.RessourceManager.GetString("MENU_DELETE_CONFIRMATION"),this.RM.SiteName), "", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                this.RM.DeleteSync();
            }
        }

        internal void Menu_EditSync_Click(object sender, EventArgs e)
        {
            this.RM.Edit();
        }

        internal void StartDebugger()
        {
            //TODO : Create a better debugger (similar to EasysyncServer)
        }

        internal void StopDebugger()
        {
            //TODO : Stop debugger
        }

        internal bool InitEngine(List<Type> _Connectors)
        {
            var EngineInitSuccess = RM.InitEngine(_Connectors);
            if (!EngineInitSuccess)
            {
                MenuItem_Start.Enabled = false;
                MenuItem_Stop.Enabled = false;
                MenuItem_OpenLocalFolder.Enabled = false;
            }
            return EngineInitSuccess;
        }

        internal bool StartEngine()
        {
            MenuItem_Stop.Enabled = true;
            MenuItem_Start.Enabled = false;
            return RM.StartEngine();
        }

        internal bool StopEngine()
        {
            MenuItem_Stop.Enabled = false;
            MenuItem_Start.Enabled = true;
            return RM.StopEngine();
        }

        internal void NotifyProxyUpdate()
        {
            RM.NotifyProxyUpdate();
        }
    }
}