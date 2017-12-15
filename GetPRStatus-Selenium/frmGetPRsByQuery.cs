using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using System.IO;
using Excel = Microsoft.Office.Interop.Excel;
using Microsoft.Office.Interop.Excel;
using System.Text.RegularExpressions;

namespace GetPRStatus_Selenium
{
    public partial class frmGetPRsByQuery : Form
    {
        string strCaption = "CRM :: Pull Request Status";

        System.Data.DataTable ExcelDataTable;
        DataSet dsWorkItems;
        VssConnection connection;
        string strXLSFilePath = "";

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
        private void btnStart_Click(object sender, EventArgs e)
        {
            string strQueryURL = txtQueryLink.Text.Trim();
            try
            {
                if (string.IsNullOrEmpty(strQueryURL))
                {
                    MessageBox.Show("Please enter query link.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
                if (!strQueryURL.ToLower().Contains("http://vstfmbs:8080/tfs/crm/engineering/_workitems"))
                {
                    MessageBox.Show("Please enter valid query link.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                string temp = System.Net.WebUtility.UrlDecode(strQueryURL);

                string queryFullPath = Regex.Split(temp, "#path=", RegexOptions.IgnoreCase)[1]; //strQueryURL.Split(new string[] { "#path=" }, StringSplitOptions.None)[1];
                queryFullPath = queryFullPath.Substring(0, queryFullPath.ToLower().IndexOf("&_a=query"));

                string[] queryArr = Regex.Split(queryFullPath, "/", RegexOptions.IgnoreCase);

                string rootFolderName = queryArr[0];
                string strQueryName = queryArr[queryArr.Length - 1];
                int qFolderCount = queryArr.Length;

                if (!(qFolderCount > 1) && (rootFolderName.ToLower() != "my queries" || rootFolderName.ToLower() != "shared queries"))
                {
                    MessageBox.Show("Please enter valid query link, query should present under My Queries/Shared Queries.", strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                DataSet ds = GetAllBugs(queryFullPath);
                if (ds != null && ds.Tables.Count > 0 && ds.Tables["tblWorkITems"].Rows.Count > 0)
                {
                    SaveFileDialog saveFileDialog = new SaveFileDialog();
                    saveFileDialog.Filter = "Execl files (*.xlsx)|*.xlsx";
                    saveFileDialog.Title = "Enter file name to save Query Data";
                    //saveFileDialog.FileName = "PRList_" + DateTime.Now.ToString("ddMMyyyy_hhmmss");
                    saveFileDialog.FilterIndex = 0;
                    saveFileDialog.RestoreDirectory = true;
                    saveFileDialog.CreatePrompt = false;

                    DialogResult dlgRes = DialogResult.OK;
                    DialogResult dlgCancelRes = DialogResult.No;
                    do
                    {
                        dlgRes = saveFileDialog.ShowDialog();
                        if (dlgRes == DialogResult.OK)
                            break;
                        else
                        {
                            if (MessageBox.Show("Are you sure you want to cancel save operation?.", strCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                                dlgCancelRes = DialogResult.Yes;
                        }
                    } while (dlgCancelRes == DialogResult.No);

                    if (dlgRes == DialogResult.OK)
                    {
                        addlog("Writing bugs details in excel...");
                        strXLSFilePath = saveFileDialog.FileName;
                        if (File.Exists(strXLSFilePath))
                            File.Delete(strXLSFilePath);

                        SaveInExcel(ds, strXLSFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                clsStaticMethods.WriteLog("frmGetPRsByQuery-btnStart_Click()-->" + ex.Message);
                MessageBox.Show(ex.Message, strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        public DataSet GetAllBugs(string queryFullPath)
        {
            string warningMsg = string.Empty;
            try
            {
				dsWorkItems.Tables["tblWorkITems"].Rows.Clear();
                string teamProjectName = "Engineering";
                string WorkItemDetails = string.Empty;
                string[] queryArr = Regex.Split(queryFullPath, "/", RegexOptions.IgnoreCase);
                string rootFolderName = queryArr[0];
                string strQueryName = queryArr[queryArr.Length - 1];

                addlog("Connecting to TFS to get the query data...");

                // Create instance of WorkItemTrackingHttpClient using VssConnection
                WorkItemTrackingHttpClient witClient = connection.GetClient<WorkItemTrackingHttpClient>();
                // Get 1 levels of query hierarchy items
                List<QueryHierarchyItem> queryHierarchyItems = witClient.GetQueriesAsync(teamProjectName, depth: 1).Result;
                // Search for root folder ( My Queries/Shared Queries)
                QueryHierarchyItem myQueriesFolder = queryHierarchyItems.FirstOrDefault(qhi => qhi.Name.Equals(rootFolderName));
                if (myQueriesFolder == null)
                {
                    warningMsg = "This folder " + rootFolderName + " is empty, please create a query to run the program.";
                    addlog(warningMsg);
                    MessageBox.Show(warningMsg, strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return null;
                }

                // See if our query already exists under 'My Queries/Shared Queries' folder.
                QueryHierarchyItem query = witClient.GetQueryAsync(teamProjectName, queryFullPath).Result;
                // Run the query
                WorkItemQueryResult result = witClient.QueryByIdAsync(query.Id).Result;
                if (result.WorkItems.Any())
                {
                    addlog("Getting list of bugs from query...");
                    int skip = 0;
                    const int batchSize = 100;
                    IEnumerable<WorkItemReference> workItemRefs;
                    do
                    {
                        workItemRefs = result.WorkItems.Skip(skip).Take(batchSize);
                        if (workItemRefs.Any())
                        {
                            // get details for each work item in the batch
                            List<WorkItem> workItems = witClient.GetWorkItemsAsync(workItemRefs.Select(wir => wir.Id)).Result;

                            foreach (WorkItem workItem in workItems)
                            {
                                if (workItem.Fields.ContainsKey("Microsoft.CRM.PRLink"))
                                {
                                    if (workItem.Fields["Microsoft.CRM.PRLink"].ToString() != "" || workItem.Fields["Microsoft.CRM.PRLink"].ToString() != "NA")
                                    {
                                        WorkItemDetails = "Bug--" + workItem.Id + " | PR Link---" + workItem.Fields["Microsoft.CRM.PRLink"].ToString();
                                        addlog(WorkItemDetails);
                                        dsWorkItems.Tables["tblWorkITems"].Rows.Add(workItem.Id, workItem.Fields["Microsoft.CRM.PRLink"].ToString(), "");
                                    }
                                }
                                else
                                {
                                    WorkItemDetails = "Bug--" + workItem.Id + " | PR Link--- Not Present";
                                    addlog(WorkItemDetails);
                                    dsWorkItems.Tables["tblWorkITems"].Rows.Add(workItem.Id, "", "");
                                }
                            }
                        }
                        skip += batchSize;
                    }
                    while (workItemRefs.Count() == batchSize);

                    return dsWorkItems;
                }
                else
                {
                    warningMsg = "No work items were returned from query.";
                    addlog(warningMsg);
                    MessageBox.Show(warningMsg, strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return null;
                }

            }
            catch (Exception ex)
            {
                warningMsg = ex.Message;
                if (ex.InnerException != null)
                {
                    warningMsg = ex.InnerException.Message;
                    if (warningMsg.Contains(":"))
                        warningMsg = warningMsg.Split(':')[1].Trim();
                }
                clsStaticMethods.WriteLog("GetAllBugs()-->" + warningMsg);
                MessageBox.Show(warningMsg, strCaption, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private void SaveInExcel(DataSet ds, string filePath)
        {
            Excel.Application excelApp = new Excel.Application();
            Excel.Workbook workbook;
            Excel.Worksheet worksheet;
            try
            {
                workbook = excelApp.Workbooks.Add(1);
                worksheet = workbook.Sheets[1];
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                workbook.SaveAs(filePath, Excel.XlFileFormat.xlOpenXMLWorkbook, Type.Missing, Type.Missing, false, false, XlSaveAsAccessMode.xlNoChange, XlSaveConflictResolution.xlLocalSessionChanges, Type.Missing, Type.Missing);
                workbook.Close();
                Excel.Workbook excelWorkBook = excelApp.Workbooks.Open(filePath);
                Excel.Worksheet excelWorkSheet = excelWorkBook.ActiveSheet;
                foreach (System.Data.DataTable table in ds.Tables)
                {
                    for (int i = 1; i < table.Columns.Count + 1; i++)
                    {
                        excelWorkSheet.Cells[1, i] = table.Columns[i - 1].ColumnName;
                        excelWorkSheet.Cells[1, i].Font.Bold = true;
                        excelWorkSheet.Cells[1, i].Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.Green);
                        //excelWorkSheet.Cells[1, i].Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                        excelWorkSheet.Cells[1, i].BorderAround(Excel.XlLineStyle.xlContinuous, Excel.XlBorderWeight.xlThin, Excel.XlColorIndex.xlColorIndexAutomatic, Excel.XlColorIndex.xlColorIndexAutomatic);
                    }
                    for (int j = 0; j < table.Rows.Count; j++)
                    {
                        for (int k = 0; k < table.Columns.Count; k++)
                        {
                            excelWorkSheet.Cells[j + 2, k + 1] = table.Rows[j].ItemArray[k].ToString();
                            excelWorkSheet.Cells[j + 2, k + 1].BorderAround(Excel.XlLineStyle.xlContinuous, Excel.XlBorderWeight.xlThin, Excel.XlColorIndex.xlColorIndexAutomatic, Excel.XlColorIndex.xlColorIndexAutomatic);
                        }
                    }
                }
                excelWorkSheet.Columns[2].ColumnWidth = 130;
                excelWorkSheet.Columns[2].WrapText = true;
                excelWorkBook.Save();
                excelWorkBook.Close();

                clsStaticMethods.queryExcelPath = filePath;
                MessageBox.Show("Your Excel is saved  in below Location." + Environment.NewLine + filePath, strCaption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                clsStaticMethods.WriteLog("SaveInExcel()-->" + ex.Message);
                MessageBox.Show(ex.Message, strCaption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            finally
            {
                excelApp.Quit();
            }
        }
        private void btnClear_Click(object sender, EventArgs e)
        {
            txtQueryLink.Text = string.Empty;
            lstBxLog.Items.Clear();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

#region "For Related"

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

        public frmGetPRsByQuery()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.None;
            //adjust these parameters to get the lookyou want.
            //Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
            Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(4, 4, Width - 3, Height - 3, 20, 20));

            connection = new VssConnection(new Uri($"http://vstfmbs:8080/tfs/CRM/"), new VssAadCredential());
            ExcelDataTable = new System.Data.DataTable("tblWorkITems");
            ExcelDataTable.Columns.Add("BugId", typeof(int));
            ExcelDataTable.Columns.Add("PR Link", typeof(string));
            ExcelDataTable.Columns.Add("Status", typeof(string));
            dsWorkItems = new DataSet();
            dsWorkItems.Tables.Add(ExcelDataTable);
        }

        private void pBMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }
        private void pBMinimize_MouseHover(object sender, EventArgs e)
        {
            pBMinimize.Image = Properties.Resources.PBMin_b;
        }
        private void pBMinimize_MouseLeave(object sender, EventArgs e)
        {
            pBMinimize.Image = Properties.Resources.PBmin_a;
        }
        private void PBClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        private void PBClose_MouseHover(object sender, EventArgs e)
        {
            PBClose.Image = Properties.Resources.PBclose_b;
        }
        private void PBClose_MouseLeave(object sender, EventArgs e)
        {
            PBClose.Image = Properties.Resources.PBclose_a;
        }

        System.Drawing.Point mouse_offset;
        private void frmGetPRsByQuery_MouseDown(object sender, MouseEventArgs e)
        {
            mouse_offset = new System.Drawing.Point(-e.X, -e.Y);
        }
        private void frmGetPRsByQuery_MouseMove(object sender, MouseEventArgs e)
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

        #endregion

    }
}
