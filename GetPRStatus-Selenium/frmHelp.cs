using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GetPRStatus_Selenium
{
    public partial class frmHelp : Form
    {
        System.Drawing.Point mouse_offset;
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
        (
            int nLeftRect, // x-coordinate of upper-left corner
            int nTopRect, // y-coordinate of upper-left corner
            int nRightRect, // x-coordinate of lower-right corner
            int nBottomRect, // y-coordinate of lower-right corner
            int nWidthEllipse, // height of ellipse
            int nHeightEllipse // width of ellipse
         );
        public frmHelp()
        {
            InitializeComponent();

            this.FormBorderStyle = FormBorderStyle.None;
            //adjust these parameters to get the lookyou want.
            Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(2, 2, Width - 1, Height - 1, 16, 16));
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void frmHelp_MouseDown(object sender, MouseEventArgs e)
        {
            mouse_offset = new System.Drawing.Point(-e.X, -e.Y);
        }

        private void frmHelp_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button == MouseButtons.Left)
                {
                    System.Drawing.Point mousepos = Control.MousePosition;
                    mousepos.Offset(mouse_offset.X, mouse_offset.Y);
                    Location = mousepos;
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void pbClose_MouseHover(object sender, EventArgs e)
        {
            pbClose.Image = Properties.Resources.close_c;
        }

        private void pbClose_MouseLeave(object sender, EventArgs e)
        {
            pbClose.Image = Properties.Resources.close_b;
        }

        private void pbClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void lbtnMoreDetails_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                string filePath = AppDomain.CurrentDomain.BaseDirectory + "UserManual.pdf";
                if (System.IO.File.Exists(filePath))
                {
                    System.Diagnostics.Process.Start(filePath);
                }
                else
                {
                    MessageBox.Show(" User help manual not available", "CRM :: Pull Request Status", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
            catch (Exception ex)
            {
               clsStaticMethods.WriteLog("lbtnMoreDetails_LinkClicked()-->" + ex.Message);
            }
        }
        
    }
}
