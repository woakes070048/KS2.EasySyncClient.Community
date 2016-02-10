/*******************************************************************/
/* EasySync Client                                                 */
/* Author : KaliConseil                                            */
/* http://www.kaliconseil.fr or http://www.ks2.fr                  */
/* contact@ks2.fr                                                  */
/* https://github.com/KaliConseil/EasySyncClient                   */
/*******************************************************************/

using KS2.EasySync.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace KS2.EasySyncClient
{
    public partial class StatsForm : Form
    {
        private System.Timers.Timer T;
        public object ReloadingStatsLock = new object();
        public bool ReloadingStats = false;
        private List<RepositoryUI> ListOfRepositoryUI;
        public StatsForm(List<RepositoryUI>  ListOfRepositoryUI)
        {
            InitializeComponent();
            T = new System.Timers.Timer(2 * 1000);
            T.AutoReset = false;
            T.Elapsed += T_Elapsed;
            this.ListOfRepositoryUI = ListOfRepositoryUI;
        }

        void T_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (this.InvokeRequired) this.Invoke(new MethodInvoker(delegate { this.Hide(); }));
            else this.Hide();
        }

        protected override void OnLoad(EventArgs e)
        {
            //Place on Lower Right
            //Determine "rightmost" screen
            Screen PrimaryScreen = Screen.PrimaryScreen;
            /*
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.Right > PrimaryScreen.WorkingArea.Right)
                    PrimaryScreen = screen;
            }
            */
            this.Left = PrimaryScreen.WorkingArea.Right - this.Width;
            this.Top = PrimaryScreen.WorkingArea.Bottom - this.Height;
            base.OnLoad(e);
        }

        /*
        protected override void OnMouseLeave(EventArgs e)
        {
           if (this.ClientRectangle.Contains(this.PointToClient(Control.MousePosition)))
            {
                return;
            }
            else
            {
                base.OnMouseLeave(e);
            }
        }
        */

        private void StatsForm_Deactivate(object sender, EventArgs e)
        {
            Program.MainUploadDownloadCountChanged -= Program_ActionCountChanged; //Unsubscribe to the Count Changed event
            this.Hide();
        }

        private void StatsForm_Activated(object sender, EventArgs e)
        {
            Program.MainUploadDownloadCountChanged += Program_ActionCountChanged; //Subscribe to the Count Changed event
            Program_ActionCountChanged(this, null);
        }

        void Program_ActionCountChanged(object sender, EventArgs e)
        {
            lock (ReloadingStatsLock)
            {
                if (ReloadingStats) return;
                else ReloadingStats = true;
            }

            if (ListOfRepositoryUI.Count(x => x.RM.GetDownloadActionCount == -1) > 0) //Init not done yet
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new MethodInvoker(delegate
                    {
                        pnl_PlaceHolder.Controls.Clear();
                        pnl_PlaceHolder.Controls.Add(new StartsFormUI2());
                    }));
                }
                else
                {
                    pnl_PlaceHolder.Controls.Clear();
                    pnl_PlaceHolder.Controls.Add(new StartsFormUI2());
                }
            }
            else
            {
                //Build all controls to display each repository
                Int32 DownCount = ListOfRepositoryUI.Sum(x => x.RM.GetDownloadActionCount);
                Int32 UpCount = ListOfRepositoryUI.Sum(x => x.RM.GetUploadActionCount);

                if (DownCount == 0 && UpCount == 0) //No action to perform
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new MethodInvoker(delegate
                        {
                            pnl_PlaceHolder.Controls.Clear();
                            pnl_PlaceHolder.Controls.Add(new StartsFormUI0());
                        }));
                    }
                    else
                    {
                        pnl_PlaceHolder.Controls.Clear();
                        pnl_PlaceHolder.Controls.Add(new StartsFormUI0());
                    }
                }
                else //Some actions to perform
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new MethodInvoker(delegate
                        {
                            pnl_PlaceHolder.Controls.Clear();
                            pnl_PlaceHolder.Controls.Add(new StartsFormUI1());
                            ((StartsFormUI1)pnl_PlaceHolder.Controls[0]).label1.Text = DownCount.ToString();
                            ((StartsFormUI1)pnl_PlaceHolder.Controls[0]).label2.Text = UpCount.ToString();


                        }));
                    }
                    else
                    {
                        pnl_PlaceHolder.Controls.Clear();
                        pnl_PlaceHolder.Controls.Add(new StartsFormUI1());
                        ((StartsFormUI1)pnl_PlaceHolder.Controls[0]).label1.Text = DownCount.ToString();
                        ((StartsFormUI1)pnl_PlaceHolder.Controls[0]).label2.Text = UpCount.ToString();
                    }
                }
            }

            lock (ReloadingStatsLock)
            {
                ReloadingStats = false;
            }
        }
    }
}
