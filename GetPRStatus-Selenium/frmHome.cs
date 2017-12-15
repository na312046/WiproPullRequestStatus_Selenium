using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Configuration;
using System.IO;
using System.Data.OleDb;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using System.Diagnostics;
using System.Collections;

namespace GetPRStatus_Selenium
{
    public partial class frmHome : Form
    {
        string strFailedLst = "";
        string strXLSFile = "";
        string fileExt = ".XLS";
        string OcnStr = "";
        DataSet ods = default(DataSet);
        DataTable odtbl = default(DataTable);
        //string CRMServerURL = ConfigurationManager.AppSettings["CRMServerURL"].ToString();
        string strCaption = "CRM :: Pull Request Status";
        bool isFirstPR = true;
        DateTime startTime;
		Stopwatch stopwatch;
        public frmHome()
        {
            InitializeComponent();

            System.Drawing.Drawing2D.GraphicsPath gp = new System.Drawing.Drawing2D.GraphicsPath();
            gp.AddEllipse(-10 , -1, pbBrowse.Width + 9, pbBrowse.Height + 1);
            pbBrowse.Region = new Region(gp);

            btnBrowse.GotFocus += btnBrowse_GotFocus;
            btnBrowse.LostFocus += btnBrowse_LostFocus;
			stopwatch = new Stopwatch();

        }
        private void btnBrowse_GotFocus(object sender, EventArgs e)
        {
            pbBrowse.BorderStyle = BorderStyle.FixedSingle;
            pbBrowse.Image = Properties.Resources.browse_cr;
        }

        private void btnBrowse_LostFocus(object sender, EventArgs e)
        {
            pbBrowse.BorderStyle = BorderStyle.None;
            pbBrowse.Image = Properties.Resources.browse_br;
        }
        private delegate void addlistbox(string msg);
        private void addlog(string msg)
        {
            if (this.lstBxLog.InvokeRequired)
            {
                addlistbox mydel = new addlistbox(addlog);
                this.Invoke(mydel, new object[] { msg });
            }
            else
            {
                this.lstBxLog.Items.Add(msg);
            }

        }
        private void frmHome_Load(object sender, EventArgs e)
        {
            lblElapsedTime.Text = "";
            cmbReQBuild.SelectedIndex = 0;
            clsStaticMethods.CopyUserManual();
        }
        private void btnBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                txtFileName.Text = string.Empty;
                DialogResult dr = OpenFileDlg.ShowDialog();
                if (dr != DialogResult.OK)
                    return;
                txtFileName.Text = OpenFileDlg.FileName.Trim();
            }
            catch (Exception ex)
            {
                clsStaticMethods.WriteLog("btnBrowse_Click()-->" + ex.Message);
            }
        }
 
        private void pbBrowse_MouseHover(object sender, EventArgs e)
        {
            pbBrowse.Image = Properties.Resources.browse_cr;
        }

        private void pbBrowse_MouseLeave(object sender, EventArgs e)
        {
            pbBrowse.Image = Properties.Resources.browse_br;
        }
            
        private void btnStart_Click(object sender, EventArgs e)
        {
			stopwatch.Reset();
            OleDbConnection ocn = null;
            try
            {
                strXLSFile = txtFileName.Text.Trim();
                if (string.IsNullOrEmpty(strXLSFile))
                {
                    MessageBox.Show("Plese select PRs list file.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
                fileExt = Path.GetExtension(strXLSFile).ToUpper();
                if (string.IsNullOrEmpty(fileExt) || (fileExt != ".XLS" && fileExt != ".XLSX"))
                {
                    MessageBox.Show("Please select valid Excel file.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
                if (!File.Exists(strXLSFile))
                {
                    MessageBox.Show("Excel file does not exists in physical location.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                //Closing the excel file if it is already opened.
                if (clsStaticMethods.closeOpenedExcel(strXLSFile))
                    System.Threading.Thread.Sleep(1000);

                if (fileExt == ".XLS")
                {
                    OcnStr = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + strXLSFile + ";Extended Properties=Excel 8.0;"; //HDR=Yes;IMEX=2"
                }
                else if (fileExt == ".XLSX")
                {
                    //OcnStr = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" & strXLSFile & ";Extended Properties=Excel 12.0 Xml;HDR=Yes;IMEX=2"
                    OcnStr = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + strXLSFile + ";Extended Properties=Excel 12.0 ;";
                }
                ocn = new OleDbConnection(OcnStr);
                try
                {
                    if ((ocn.State == ConnectionState.Closed))
                    {
                        ocn.Open();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error in opening connection to read data from Excel: " + Environment.NewLine + ex.Message, strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                //Get the name of the First Sheet.
                DataTable dtExcelSchema = ocn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
                string sheetName = dtExcelSchema.Rows[0]["TABLE_NAME"].ToString();

                string Query = "Select [PR Link],[Status] from [" + sheetName + "] where [PR Link]  IS NOT NULL";
                //clsStaticMethods.WriteLog(Query)
                using (OleDbDataAdapter oda = new OleDbDataAdapter(Query, ocn))
                {
                    try
                    {
                        ods = new DataSet();
                        oda.Fill(ods, "tblData");
                        //odtbl = New DataTable["tblData"]
                        //oda.Fill(odtbl)
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error in reading data from Excel: " + Environment.NewLine + ex.Message, strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        return;
                    }
                }
                //clsStaticMethods.WriteLog("Columns count in Excel file: " & CInt(ods.Tables("tblData").Columns.Count))
                int recCount = ods.Tables["tblData"].Rows.Count;
                if (!(recCount > 0))
                {
                    MessageBox.Show("No records found in Excel file.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                else
                {
                    if(!clsStaticMethods.isFireforExists())
                    {
                        MessageBox.Show("It is required to have Mozilla Firefox browser, Install it and try again.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    if (cmbReQBuild.Text.Trim().ToLower() == "yes")
                    {
                        clsStaticMethods.isRequeue = true;
                        if(chkFailed.Checked)
                            clsStaticMethods.isRequeueFailed = true ;
                        else
                            clsStaticMethods.isRequeueFailed = false;
                        if (chkExpired.Checked)
                            clsStaticMethods.isRequeueExpired = true;
                        else
                            clsStaticMethods.isRequeueExpired = false;
                    }
                    else
                    {
                        clsStaticMethods.isRequeue = false ;
                        clsStaticMethods.isRequeueFailed = false;
                        clsStaticMethods.isRequeueExpired = false;
                    }

                    isControlsEnable(false);

                    lstBxLog.Items.Clear();
                    ProgressBar1.Visible = true;
                    ProgressBar1.Maximum = recCount;
                    ProgressBar1.Value = 0;
                    lblProgressPercent.Text = "";
                    lblElapsedTime.Text = "";
                    lblProgressPercent.BackColor = SystemColors.ControlLight;
                    lblProgressPercent.Visible = true;
                    isFirstPR = true;

                    BackgroundWorker1.RunWorkerAsync();
                }
            }
            catch (Exception ex)
            {
                clsStaticMethods.WriteLog("btnStart_Click()-->" + ex.Message);
            }
            finally
            {
                if (ocn != null)
                {
                    if (ocn.State == ConnectionState.Open)
                    {
                        ocn.Close();
                    }
                    ocn = null;
                }
            }
        }

        private void isControlsEnable(bool isEnable)
        {
            btnBrowse.Enabled = isEnable;
            pbBrowse.Enabled = isEnable;
            btnStart.Enabled = isEnable;
            btnClose.Enabled = isEnable;
            btnClear.Enabled = isEnable;
            //getPRsFromQueryToolStripMenuItem.Enabled = false;
            //helpToolStripMenuItem.Enabled = false;
            menuStrip1.Enabled = isEnable;
            cmbReQBuild.Enabled = isEnable;
            chkFailed.Enabled = isEnable;
            chkExpired.Enabled = isEnable;
            if (isEnable)
            {
                txtFileName.ReadOnly = false;
                pictureBox1.Visible = false;
            }
            else
            {
                txtFileName.ReadOnly = true;
                pictureBox1.Visible = true;
            }
        }
        private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            var driverService = FirefoxDriverService.CreateDefaultService();
            driverService.HideCommandPromptWindow = true;
            IWebDriver driver = new FirefoxDriver(driverService);
            //IWebDriver driver = new FirefoxDriver();
            driver.Manage().Window.Maximize();

            try
            {
                if ((ods != null) && (ods.Tables[0] != null) && ods.Tables[0].Rows.Count > 0)
                {
                    odtbl = ods.Tables[0];
                    int i = 0;
                    int recCount = 0;
                    recCount = odtbl.Rows.Count;
                    addlog("######## Total PRs count: " + recCount);
                    string strPRID = "";
                    string strStatus = "";
                    string strPRLink = "";
                    string[] strPRArr = null;
					stopwatch.Start();
                    startTime = DateTime.Now;

                    foreach (DataRow dr in odtbl.Rows)
                    {
                        i += 1;
                        strPRLink = dr["PR Link"].ToString();
                        if(!strPRLink.ToLower().Contains("pullrequest"))
                        {
                            strStatus = "Invalid PRLink - It doesn't contains pullrequest in it.";
                            dr["Status"] = strStatus;
                            addlog(i + ") PR# Status: Invalid PRLink - It doesn't contains pullrequest in it.");
                            BackgroundWorker1.ReportProgress(i);
                            //UpdateSinglePRBuildStatusInExcel(strStatus, strPRLink);
                            System.Threading.Thread.Sleep(100);
                            continue;
                        }
                        strPRArr = System.Text.RegularExpressions.Regex.Split(strPRLink.ToLower(), "pullrequest/");
                        if (strPRArr.Length == 2)
                        {
                            strPRLink = strPRArr[0];
                            strPRID = strPRArr[1];
                            strPRLink = strPRArr[0];
                            strPRID = strPRArr[1];
                            ArrayList splChars = new ArrayList();
                            splChars.Add("!"); splChars.Add("@"); splChars.Add("$");
                            splChars.Add("&"); splChars.Add("("); splChars.Add(")");
                            splChars.Add("?"); splChars.Add("#"); splChars.Add("^");
                            splChars.Add("%"); splChars.Add("/"); splChars.Add("*");
                            foreach (string chr in splChars)
                            {
                                if (strPRID.Contains(chr))
                                    strPRID = strPRID.Substring(0, strPRID.IndexOf(chr));
                            }
                           // if (strPRID.Contains("?"))
                           //     strPRID = strPRID.Substring(0, strPRID.IndexOf("?"));
                           //else if(strPRID.Contains("#"))
                           //     strPRID = strPRID.Substring(0, strPRID.IndexOf("#"));
                            strPRLink = strPRLink + "pullrequest/" + strPRID + "?_a=overview";
                            strStatus = startProcess(driver, strPRLink);
                            if (strStatus == "remotewebdriver")
                            {
                                strStatus = "Browser closed unexpectedly, opening new browser.";
                                driver = new FirefoxDriver(driverService);
								stopwatch.Start();
                            }
                            dr["Status"] = strStatus;
                            addlog(i + ") PR#" + strPRID + "  Status: " + strStatus);

                            BackgroundWorker1.ReportProgress(i);
                            //UpdateSinglePRBuildStatusInExcel(strStatus, strPRLink);
                            System.Threading.Thread.Sleep(100);
                        }
                        else
                        {
                            strStatus = "Invalid PRLink, split with pullrequest issue.";
                            dr["Status"] = strStatus;
                            addlog(i + ") PR# Status: Invalid PRLink, split with pullrequest issue.");
                            BackgroundWorker1.ReportProgress(i);
                            //UpdateSinglePRBuildStatusInExcel(strStatus, strPRLink);
                            System.Threading.Thread.Sleep(100);
                            continue;
                        }
                    }
                    UpdateAllPsRBuildStatusInExcel(ods);
                    //clsStaticMethods.WriteLog("Total Elapsed time: " + strTimeElapsed);
                }
                else
                {
                    MessageBox.Show("No records in Excel file.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Information);
					
                }
            }
            catch (Exception ex)
            {
                clsStaticMethods.WriteLog("BackgroundWorker1_DoWork-->" + ex.Message);
				stopwatch.Stop();
            }
            finally
            {
                if (driver != null)
                    driver.Quit();
            }
        }

        public string startProcess(IWebDriver driver, string strPRLink)
        {
            string strPRStatus = string.Empty;
            try
            {
                //IWebDriver driver = new FirefoxDriver();
                //IList<IWebElement> elements = null;
                //System.setProperty("webdriver.gecko.driver", "path of geckodriver.exe");

                //System.Environment.SetEnvironmentVariable("webdriver.gecko.driver", "path of geckodriver.exe");
                // if above property is not working or not opening the application in browser then try below property
                //System.Environment.SetEnvironmentVariable("webdriver.firefox.marionette","G:\\Selenium\\Firefox driver\\geckodriver.exe");

                //FirefoxDriverService service = FirefoxDriverService.CreateDefaultService();
                //service.FirefoxBinaryPath = @"C:\Program Files\Mozilla Firefox\firefox.exe";
                //IWebDriver driver = new FirefoxDriver(service);

                driver.Url = strPRLink;

                if (isFirstPR)
                {
                    addlog("Waiting for page load...");
                    isFirstPR = false;
                }
                System.Threading.Thread.Sleep(5000);
                
                string tempURL = strPRLink.Substring(0, strPRLink.IndexOf("?"));
                string pageTitle = driver.Title.Trim().ToLower();

                if (!string.IsNullOrEmpty(pageTitle) && (pageTitle.StartsWith("sign in") || pageTitle.Contains("authentication options") || pageTitle.Contains("multi-factor authentication ") || pageTitle.Contains("waiting for response") || pageTitle.Contains("sign out") || pageTitle.StartsWith("error")))
                {
                    //MessageBox.Show("Please login through microsoft credentials and phone authentication in the opened browser." + Environment.NewLine + "After successful login please click on Ok to proceed.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    //MessageBox.Show(new Form { TopMost = true }, "Please login through microsoft credentials and phone authentication in the opened browser." + Environment.NewLine + "After successful login please click on Ok to proceed.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    stopwatch.Stop();
                    this.Invoke((Func<DialogResult>)(() => MessageBox.Show("Please login through microsoft credentials and phone authentication in the opened browser." + Environment.NewLine + "After successful login please click on OK to proceed.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)));
                    stopwatch.Start();
                    pageTitle = driver.Title.Trim().ToLower();
                    if (pageTitle.StartsWith("working"))
                        System.Threading.Thread.Sleep(1000);
                    if (pageTitle.StartsWith("error"))
                        return "Login failed.";
                }
                pageTitle = driver.Title.Trim().ToLower();
                if (!pageTitle.StartsWith("pull request") && (pageTitle.Contains("page not found") || pageTitle.Contains("pull request doesn't exist")))
                {
                    return "Invalid PRLink, doesn't exist.";
                }
                if (driver.Url.ToLower().Contains(tempURL.ToLower()))
                {
                    strPRStatus = clsStaticMethods.getPRStatus(driver);
                }
                else
                {
                    //MessageBox.Show("Login failed, unable to open PR Link." + Environment.NewLine + "Please login with valid credentials and try again.");
                    return "Login failed, unable to open PR Link." + Environment.NewLine + "Please login with valid credentials and try again.";
                }
            }
            catch (Exception ex)
            {
				stopwatch.Stop();
                if (ex.Message.ToLower().Contains("failed to decode response from marionette") || ex.Message.ToLower().Contains("tried to run command without establishing a connection"))
                    return "remotewebdriver";
                clsStaticMethods.WriteLog("startProcess()-->" + ex.Message);
            }
            return strPRStatus;
        }

        private void UpdateAllPsRBuildStatusInExcel(DataSet myds)
        {
            OleDbConnection cn = new OleDbConnection(OcnStr);
            OleDbCommand cmd = default(OleDbCommand);
            try
            {
                string query = "Update [Sheet1$] set [Status]=@statusval where [PR Link]=@prlink";
                cmd = new OleDbCommand(query, cn);
                cmd.Parameters.Add("@statusval", OleDbType.LongVarChar, 201, "Status");
                cmd.Parameters.Add("@prlink", OleDbType.LongVarChar,2048, "PR link");
                using (OleDbDataAdapter odas = new OleDbDataAdapter("Select [PR Link],[Status] from [Sheet1$]", cn))
                {
                    odas.UpdateCommand = cmd;
                    odas.Update(myds, "tbldata");
                }
            }
            catch (Exception ex)
            {
                clsStaticMethods.WriteLog("UpdatePRBuildStatusInExcel()-->" + ex.Message);
                MessageBox.Show("Error occured while updating the PR status in excel." + Environment.NewLine + ex.Message,strCaption, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void UpdateSinglePRBuildStatusInExcel(string strStatus, string strPRLink)
        {
            OleDbConnection ocn = new OleDbConnection(OcnStr);
            try
            {
                if (ocn.State == ConnectionState.Closed)
                {
                    ocn.Open();
                }
                string Query = "Update [Sheet1$] set [Status]='" + strStatus + "' where [PR Link] ='" + strPRLink + "'";
                OleDbCommand cmd = new OleDbCommand(Query, ocn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                clsStaticMethods.WriteLog("UpdateSinglePRBuildStatusInExcel()-->" + ex.Message);
                //MessageBox.Show("Error occured while updating the PR status in excel" + Environment.NewLine + ex.Message);
            }
            finally
            {
                if (ocn != null)
                {
                    if (ocn.State == ConnectionState.Open)
                        ocn.Close();
                    ocn = null;
                }
            }
        }

        private void BackgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            try
            {
                ProgressBar1.Value = e.ProgressPercentage;
                double percent = Math.Round(((float)ProgressBar1.Value / ProgressBar1.Maximum * 100));
                lblProgressPercent.Text = percent.ToString() + "%";
                lblProgressPercent.BackColor = SystemColors.ControlLight; //Color.FromArgb(198, 199, 201);
                if (percent > 40)
                {
                    lblProgressPercent.BackColor = Color.FromArgb(5, 176, 36);
                }
                lblProgressPercent.Refresh();
            }
            catch (Exception ex)
            {
                clsStaticMethods.WriteLog("BackgroundWorker1_ProgressChanged-->" + ex.Message);
            }
        }

        private void BackgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
			stopwatch.Stop();
            TimeSpan ts = stopwatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
            try
            {
                lblElapsedTime.Text = "Total Elapsed Time: " + elapsedTime;
                if (string.IsNullOrEmpty(strFailedLst))
                {
                    addlog("########## PR status verification completed successfully. ########");
                    MessageBox.Show("PR status verification completed successfully. Please check logs for more details.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    clsStaticMethods.WriteLog("####Status verification failed PR list####" + Environment.NewLine + strFailedLst);
                    addlog("##### Status verification failed for some PRs. Please check logs for more details.");
                    MessageBox.Show("Status verification failed for some PRs. Please check logs for more details.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                isControlsEnable(true);

            }
            catch (Exception ex)
            {
                clsStaticMethods.WriteLog("BackgroundWorker1_RunWorkerCompleted-->" + ex.Message);

            }
            finally
            {
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtFileName.Text = "";
            lstBxLog.Items.Clear();
            ProgressBar1.Value = 0;
            pictureBox1.Visible = false;
            ProgressBar1.Visible = false;
            lblProgressPercent.Visible = false;
            lblProgressPercent.Text = "";
            lblElapsedTime.Text = "";
            lblProgressPercent.BackColor = SystemColors.ControlLight;
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void tsmItemSave_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "Text Files (*.txt*)|*.txt";
            saveFileDialog1.Title = "Save PR status log";
            saveFileDialog1.FileName = "PRStatusLog_" + DateTime.Now.ToString("ddMMyyyy_hhmmss");
            if (saveFileDialog1.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(saveFileDialog1.FileName))
            {
                using (StreamWriter streamwriter = new StreamWriter(saveFileDialog1.FileName))
                {
                    foreach (string str in lstBxLog.Items)
                    {
                        streamwriter.WriteLine(str);
                    }
                }
            }
        }
        private void lstBxLog_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && lstBxLog.Items.Count > 0 && btnStart.Enabled)
                lstBxLog.ContextMenuStrip = ContextMenuStrip1;
            else
                lstBxLog.ContextMenuStrip = null;
        }

        private void lbtnGetPRsByQuery_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                clsStaticMethods.queryExcelPath = "";
                frmGetPRsByQuery objFrm = new frmGetPRsByQuery();
                objFrm.ShowDialog();
                Application.DoEvents();
                objFrm.Dispose();

                if(!string.IsNullOrEmpty(clsStaticMethods.queryExcelPath) && File.Exists(clsStaticMethods.queryExcelPath))
                    txtFileName.Text = clsStaticMethods.queryExcelPath;
            }
            catch (Exception ex)
            {
               clsStaticMethods.WriteLog("lbtnGetPRsByQuery_LinkClicked()-->" + ex.Message);
            }
        }

        private void lstBxLog_DrawItem(object sender, DrawItemEventArgs e)
        {
            
            if (e.Index < 0)
                return;

            string strText = lstBxLog.Items[e.Index].ToString().ToLower();

            // Draw the background of the ListBox control for each item.
            // Create a new Brush and initialize to a Black colored brush by default.
            e.DrawBackground();
            Brush myBrush = Brushes.Black;
           
            // Determine the color of the brush to draw each item based on the index of the item to draw.
            if(strText.Contains("completed"))
                myBrush = Brushes.Green;
            else if (strText.Contains("abandoned"))
                myBrush = Brushes.Maroon;
            else if (strText.Contains("invalid") || strText.Contains("failed") || strText.Contains("block") || strText.Contains("unable to queue") || strText.Contains("status not available") || strText.Contains("closed") || strText.Contains("waiting for merge") || strText.Contains("closed") || strText.Contains("closed"))
                myBrush = Brushes.Red;
            
            // Draw the current item text based on the current Font and the custom brush settings.
            e.Graphics.DrawString(((ListBox)sender).Items[e.Index].ToString(), e.Font, myBrush, e.Bounds, StringFormat.GenericDefault);
            // If the ListBox has focus, draw a focus rectangle around the selected item.
            e.DrawFocusRectangle();
        }

        private void getPRsFromQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                clsStaticMethods.queryExcelPath = "";
                frmGetPRsByQuery objFrm = new frmGetPRsByQuery();
                objFrm.ShowDialog();
                Application.DoEvents();
                objFrm.Dispose();

                if (!string.IsNullOrEmpty(clsStaticMethods.queryExcelPath) && File.Exists(clsStaticMethods.queryExcelPath))
                    txtFileName.Text = clsStaticMethods.queryExcelPath;
            }
            catch (Exception ex)
            {
                clsStaticMethods.WriteLog("getPRsFromQueryToolStripMenuItem_Click()-->" + ex.Message);
            }
        }

        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                frmHelp objFrm = new frmHelp();
                objFrm.ShowDialog();
                Application.DoEvents();
                objFrm.Dispose();
            }
            catch (Exception ex)
            {
                clsStaticMethods.WriteLog("helpToolStripMenuItem_Click()-->" + ex.Message);
            }
        }
        private void cmbReQBuild_SelectedIndexChanged(object sender, EventArgs e)
        {
            string strRequeBuild = cmbReQBuild.Text.Trim();
            if(strRequeBuild == "Yes")
            {
                //chkFailed.Visible = true;
                //chkExpired.Visible = true;
                chkFailed.Checked = true;
                chkExpired.Checked = true;
                chkFailed.Enabled = true;
                chkExpired.Enabled = true;
            }
            else
            {
                chkFailed.Checked = false;
                chkExpired.Checked = false;
                //chkFailed.Visible = false;
                //chkExpired.Visible = false ;
                chkFailed.Enabled  = false;
                chkExpired.Enabled = false;
            }
        }

        private void chkFailed_CheckedChanged(object sender, EventArgs e)
        {
            string strRequeBuild = cmbReQBuild.Text.Trim();
            if (strRequeBuild =="Yes" && (!chkExpired.Checked && !chkFailed.Checked))
            {
                MessageBox.Show("At lease one option should be selected when Re-queue build is Yes.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                chkFailed.Checked = true;
            }
        }

        private void chkExpired_CheckedChanged(object sender, EventArgs e)
        {
            string strRequeBuild = cmbReQBuild.Text.Trim();
            if (strRequeBuild == "Yes" && (!chkExpired.Checked && !chkFailed.Checked))
            {
                MessageBox.Show("At lease one option should be selected when Re-queue build is Yes.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                chkExpired.Checked = true;
            }
        }
    }
}
