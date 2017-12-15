using Microsoft.Win32;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace GetPRStatus_Selenium
{
   static class clsStaticMethods
    {
        public static string queryExcelPath = "";
        public static bool isRequeue = false;
        public static bool isRequeueFailed = false;
        public static bool isRequeueExpired = false;
        public static string getPRStatus(IWebDriver driver)
        {
            string strPRStatus = string.Empty;
            bool queBuild = false;
            try
            {
                //Wait until left pan load, max time is 8sec.
                if (!waitForOverviewLeftPaneLoad(driver))
                    return "Invalid PRLink/taking long time to load.";

                //1. Check if PR is COMPLETED
                //elements = driver.FindElements(By.CssSelector(".status-indicator.abandoned")); // It is giving all the elements with class "status-indicator abandoned"
                IList<IWebElement> elements = driver.FindElements(By.CssSelector("span.status-indicator.completed"));  // only span elements
                if (elements != null && elements.Count > 0)
                    strPRStatus = "COMPLETED";
                else
                {
                    //2. Check if PR is ABANDONED
                    elements = driver.FindElements(By.CssSelector("span.status-indicator.abandoned"));
                    if (elements != null && elements.Count > 0)
                        strPRStatus = "ABANDONED";
                    else
                    {
                        //3. Check if PR is ACTIVE, then check the build status and reque if failed/expired/expiring
                        elements = driver.FindElements(By.CssSelector("a.actionLink")); //driver.FindElements(By.ClassName("actionLink"));
                        if (elements != null && elements.Count > 0)
                        {
                            strPRStatus = elements[0].Text.ToString();
                            if(isRequeue)
                            {
                                if (strPRStatus.ToLower().Contains("failed") && isRequeueFailed)
                                    queBuild = true;
                                else if (strPRStatus.ToLower().Contains("expire") && isRequeueExpired)
                                    queBuild = true;
                                    
                                if (queBuild)
                                {
                                    if (reQueueBuild(driver))
                                        strPRStatus = "REQUEUED";
                                }
                            }
                        }
                        else
                        {
                            //4. If there is no actionlink, we need to get the statusText like 
                            strPRStatus = getStatusText(driver);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                strPRStatus = ex.Message;
                WriteLog("getPRStatus()-->" + ex.Message);
            }
            return strPRStatus;
        }

        public static bool waitForOverviewLeftPaneLoad(IWebDriver driver)
        {
            bool retVal = false;
            string strException = string.Empty;
            try
            {
                //Id don’t wait for the max time out. Instead it waits for the time till the condition specified in .until(YourCondition) method becomes true.
                WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(8));
                IWebElement option = wait.Until<IWebElement>((d) =>
                {
                    try
                    {
                        IList<IWebElement> dynamicEle = d.FindElements(By.CssSelector("div.overview-tab-left-pane"));
                        if (dynamicEle != null && dynamicEle.Count > 0)
                        {
                            return dynamicEle[0];
                        }
                    }
                    catch (NoSuchElementException) { }
                    catch (StaleElementReferenceException) { }
                    catch (TimeoutException) { }
                    return null;
                });

                if (option != null)
                    retVal = true;
            }
            catch (TimeoutException ex2)
            {
                strException = ex2.Message;                
            }
            catch (NoSuchElementException ex)
            {
                strException = ex.Message;
            }
            catch (Exception ex)
            {
                strException = ex.Message;
            }
            return retVal;
        }
        private static bool reQueueBuild(IWebDriver driver)
        {
            bool retVal = false;
            try
            {
                //Reque the build
                IList<IWebElement> elements = driver.FindElements(By.XPath("//i[contains(@data-icon-name, 'More')]")); ////driver.FindElements(By.CssSelector("i.ms-Icon.css-liugll.vss-Icon"))
                if (elements != null && elements.Count > 0)
                {
                    elements[1].Click();
                    System.Threading.Thread.Sleep(1300);
                    //elements = driver.FindElements(By.Name("Queue build"));
                    elements = driver.FindElements(By.XPath("//button[contains(@name, 'Queue build')]"));

                    if (elements != null && elements.Count > 0)
                    {
                        elements[0].Click();
                        System.Threading.Thread.Sleep(1300);
                        retVal = true;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog("reQueueBuild()-->" + ex.Message);
            }
            return retVal;
        }

        private static string getStatusText(IWebDriver driver)
        {
            string strStatusText = string.Empty;
            string strText1 = string.Empty;
            string strText2 = string.Empty;
            string strText3 = string.Empty;
            try
            {
                //if there is no actionlink, we need to get the statusText like 
                //IList<IWebElement> elements = driver.FindElements(By.ClassName("statusText"));
                IList<IWebElement> elements = driver.FindElements(By.XPath("//span[contains(@class, 'statusText')]"));
                if (elements != null && elements.Count > 0)
                {
                    int elmCount = elements.Count;
                    for (int i = 0; i < elmCount; i++)
                    {
                        if (i == 0)
                            strText1 = elements[0].Text.ToString().ToLower();
                        else if (i == 1)
                            strText2 = elements[1].Text.ToString().ToLower();
                        else if (i == 2)
                            strText3 = elements[2].Text.ToString().ToLower();
                    }

                    if((!string.IsNullOrEmpty(strText1) && strText1.Contains("build waiting for merge")) || (!string.IsNullOrEmpty(strText2) && strText2.Contains("build waiting for merge")) || (!string.IsNullOrEmpty(strText3) && strText3.Contains("build waiting for merge")))
                    {
                        strStatusText = "Build waiting for merge";

                        //Checking for merge conflict
                        elements = driver.FindElements(By.ClassName("conflict-header-text"));
                        //elements = driver.FindElements(By.XPath("//div[contains(@class, 'conflict-header-text')]"));
                        if (elements != null && elements.Count > 0)
                        {
                            strStatusText = strStatusText + " : " + elements[0].Text.ToString(); //Merge conflicts
                        }
                    }
                    else if ((!string.IsNullOrEmpty(strText1) && strText1.Contains("reviewer is block")) || (!string.IsNullOrEmpty(strText2) && strText2.Contains("reviewer is block")) || (!string.IsNullOrEmpty(strText3) && strText3.Contains("reviewer is block")))
                        strStatusText = "Reviewer is blocking";
                    else
                    {
                        string callOutBlockedText = string.Empty;
                        //Checking for callout-text
                        elements = driver.FindElements(By.CssSelector("div.callout-text"));
                        //elements = driver.FindElements(By.ClassName("callout-text"));
                        //elements = driver.FindElements(By.XPath("//div[contains(@class, 'callout-text')]"));
                        if (elements != null && elements.Count > 0)
                        {
                            callOutBlockedText = elements[0].Text.ToString();
                            if ((!string.IsNullOrEmpty(strText1) && strText1.Contains("unable to queue build")) || (!string.IsNullOrEmpty(strText2) && strText2.Contains("unable to queue build")) || (!string.IsNullOrEmpty(strText3) && strText3.Contains("unable to queue build")))
                                strStatusText = "Unable to queue Build" + " : " + callOutBlockedText;
                            else if (elmCount == 3 && !string.IsNullOrEmpty(strText3))
                                strStatusText = strText3 + " : " + callOutBlockedText;
                            else
                                strStatusText = "PR Build status not available.";
                        }
                        else
                            strStatusText = "PR Build status not available.";
                    }
                }
                else
                {
                    //there is no build status div
                    strStatusText = "PR Build status not available.";
                }
            }
            catch (Exception ex)
            {
                strStatusText = ex.Message;
            }
            return strStatusText;
        }
        public static bool closeOpenedExcel(string fPath)
        {
            bool retVal = false;
            FileStream fs = null;
            try
            {
                fs = new FileStream(fPath, FileMode.Open, FileAccess.Read);
            }
            catch (Exception ex)
            {
                retVal = true;
                string fileName = Path.GetFileNameWithoutExtension(fPath).ToUpper().Trim();
                Process[] oProcess = Process.GetProcessesByName("EXCEL");
                foreach (Process item in oProcess)
                {
                    if (item.MainWindowTitle.ToUpper().Contains(fileName))
                        item.Kill();
                }
                //WriteLog("closeOpenedExcel()-->" + ex.Message);
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }
            return retVal;
        }

        public static bool isFireforExists()
        {
            bool retVal = false;
            try
            {
                //Check if your using x64 system first if return is null your on a x86 system.
                RegistryKey browserKeys = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Clients\StartMenuInternet");
                if (browserKeys == null)
                    browserKeys = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Clients\StartMenuInternet");

                // Lets get our keys!
                string[] browserNames = browserKeys.GetSubKeyNames();
                // Loop through all the subkeys for the information you want then display it on the console.
                for (int i = 0; i < browserNames.Length; i++)
                {
                    RegistryKey browserKey = browserKeys.OpenSubKey(browserNames[i]);
                    string browserName = (string)browserKey.GetValue(null);
                    if (browserName.ToLower().Contains("firefox"))
                    {
                        retVal = true;
                        break;
                    }
                    RegistryKey browserKeyPath = browserKey.OpenSubKey(@"shell\open\command");
                    string browserPath = (string)browserKeyPath.GetValue(null);
                    RegistryKey browserIconPath = browserKey.OpenSubKey(@"DefaultIcon");
                    string strBrowserIconPath = (string)browserIconPath.GetValue(null);
                    Console.WriteLine("Name: {0}\r\nPath: {1}\r\nIconPath: {2}", browserName, browserPath, strBrowserIconPath);
                }
            }
            catch (Exception ex)
            {
                WriteLog("isFireforExists()-->" + ex.Message);
            }
            return retVal;
        }
        public static void WriteLog(string strError)
        {
            try
            {
                string AppPath = AppDomain.CurrentDomain.BaseDirectory;
                string strLog = @"LOG\";
                string strFilePath = AppPath + strLog;

                if (!(Directory.Exists(strFilePath)))
                {
                    Directory.CreateDirectory(strFilePath);
                }
                string fn = string.Format("{0}{1}.txt", strFilePath, DateTime.Now.ToString("ddMMyyyy"));
                FileStream fs = new FileStream(fn, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

                StreamWriter writer = new StreamWriter(fs);
                //writer.Write("[ " + DateTime.Now.Hour.ToString() + ":" + DateTime.Now.Minute.ToString() + ":" + DateTime.Now.Second.ToString() + " ]");
                //writer.WriteLine(strError);
                //   writer.WriteLine("--------------------------------------------------------------------------");
                writer.WriteLine(string.Format("[ {0} ] {1}", DateTime.Now.ToString("HH:mm:ss"), strError));
                writer.Close();
                fs.Close();
            }
            finally
            {
                //nothing
            }
        }
        public static void CopyUserManual()
        {
            try
            {
                string filePath = AppDomain.CurrentDomain.BaseDirectory + "UserManual.pdf";
                if (!File.Exists(filePath))
                {
                    string sourcePath = Directory.GetParent(Environment.CurrentDirectory).ToString().Replace("bin", "Docs");
                    if ((Directory.Exists(sourcePath)))
                    {
                        string strSourceFilePath = sourcePath + "\\UserManual.pdf";
                        if (File.Exists(strSourceFilePath))
                        {
                            File.Copy(strSourceFilePath, filePath);
                        }
                        else
                            WriteLog("CopyUserManual-->usermanual.pdf file does not exist in Docs folder");
                    }
                    else
                        WriteLog("CopyUserManual-->Docs folder does not exist");
                }
            }
            catch (Exception ex)
            {
                WriteLog("CopyUserManual-->" + ex.Message);
            }
        }
    }
}
