## -Module--08211513-60F5-41D7-BF6B-844E8C0DA47A-.cs (4 lines)
1: internal class _003CModule_003E_007B08211513_002D60F5_002D41D7_002DBF6B_002D844E8C0DA47A_007D
## -Module--0cc50ae3-8d4c-4e1f-bc5b-e4ac35e14361-.cs (345 lines)
1: internal sealed class _003CModule_003E_007B0cc50ae3_002D8d4c_002D4e1f_002Dbc5b_002De4ac35e14361_007D
230: internal static void j154990f08da1438b83a7d12983521708()
## -PrivateImplementationDetails-.cs (1 lines)
## VB-AnonymousDelegate_0.cs (7 lines)
1: using System.Diagnostics;
2: using System.Runtime.CompilerServices;
## BCFGibpKMkGSOOvTg4ne\h1Y8XspKD2oRFrpIWpS1.cs (21 lines)
1: using System;
5: internal class h1Y8XspKD2oRFrpIWpS1
9: internal static void ODN5ptdTxoW()
## FCode\AddLookup.cs (567 lines)
1: using System;
2: using System.Data;
3: using System.Diagnostics;
4: using System.Drawing;
5: using System.Runtime.CompilerServices;
6: using System.Threading;
7: using System.Windows.Forms;
9: using fcontrol;
10: using fsystem;
12: using Microsoft.VisualBasic;
13: using Microsoft.VisualBasic.CompilerServices;
17: public class AddLookup
117: public AddLookup()
130: public void AddLookup(TextBox txtSource, string cName, DataView tbl, bool isBlank, string nW = "")
186: public void HideList()
301: public void SelectWithName(string name)
378: public void ShowForm()
## FCode\clsForm.cs (897 lines)
1: using System;
2: using System.Data;
3: using System.Diagnostics;
4: using System.Drawing;
5: using System.Runtime.CompilerServices;
6: using System.Threading;
7: using System.Windows.Forms;
10: using Microsoft.VisualBasic;
11: using Microsoft.VisualBasic.CompilerServices;
15: public class clsForm : Form
302: public clsForm()
329: public void ItemUpDown(Keys oKey)
409: public void RefreshData()
443: protected override void OnPaint(PaintEventArgs e)
793: protected override void OnMouseDown(MouseEventArgs e)
804: protected override void OnMouseMove(MouseEventArgs e)
814: protected override void OnMouseLeave(EventArgs e)
820: protected override void OnClick(EventArgs e)
825: protected override void OnDoubleClick(EventArgs e)
## FCode\clsGenSource.cs (134 lines)
1: using System;
2: using System.IO;
3: using System.Text;
5: using fsystem;
7: using Microsoft.VisualBasic;
8: using Microsoft.VisualBasic.CompilerServices;
12: public class clsGenSource
14: public clsGenSource()
20: public static void WriteFile(string folderPath, string path, string content)
79: public static void RemoveTag(ref string fileContent, string tagName)
98: public static string AddMoreTab(string data, short ntab, string content = "", string tagName = "")
## FCode\clsMain.cs (1593 lines)
1: using System;
2: using System.Data;
3: using System.Data.SqlClient;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Drawing.Text;
7: using System.IO;
8: using System.Runtime.CompilerServices;
9: using System.Text;
10: using System.Windows.Forms;
12: using fcontrol;
13: using fsystem;
15: using Microsoft.VisualBasic;
16: using Microsoft.VisualBasic.CompilerServices;
21: public class clsMain
44: public clsMain()
50: public urLookup AddTabLookup()
59: urLookup urLookup3 = new urLookup();
67: public urLookup AddTabLookupForce()
69: urLookup urLookup2 = new urLookup();
76: public urTables AddTabTable()
78: urTables urTables2 = new urTables();
84: public urQuery AddTabQuery()
90: urQuery2 = new urQuery();
100: public urCommand AddTabCommand()
106: urCommand urCommand2 = new urCommand();
112: public urWCommand AddTabWCommand()
118: urWCommand urWCommand2 = new urWCommand();
124: public urFileLookup AddTabFileLookup()
140: urFileLookup urFileLookup4 = new urFileLookup();
148: public urFileLookup AddTabFileLookupForce()
150: urFileLookup urFileLookup2 = new urFileLookup();
157: public urFileReference AddTabFileReference()
166: urFileReference urFileReference3 = new urFileReference();
174: public urChangeOwner AddTabChangeOwner()
180: urChangeOwner urChangeOwner2 = new urChangeOwner();
186: public urNote AddTabNote()
194: urNote urNote3 = new urNote();
201: public urNoteX AddTabNoteX()
207: urNoteX urNoteX2 = new urNoteX();
213: public urImportExportExcel AddTabImportExportExcel()
219: urImportExportExcel urImportExportExcel2 = new urImportExportExcel();
225: public urClearTables AddTabClearTables()
227: urClearTables urClearTables2 = new urClearTables();
232: public urUpdate AddTabGenUpdate()
234: urUpdate urUpdate2 = new urUpdate();
243: public urCompareDatabase AddTabCompareDatabase()
249: urCompareDatabase urCompareDatabase2 = new urCompareDatabase();
255: public urCompareDataRow AddTabCompareData()
261: urCompareDataRow urCompareDataRow2 = new urCompareDataRow();
267: public urCreateFileRpt AddTabCreateRPT()
273: urCreateFileRpt urCreateFileRpt2 = new urCreateFileRpt();
279: public urCreateList AddTabCreateList()
285: urCreateList urCreateList2 = new urCreateList();
291: public urCreateGridDetail AddTabCreateGridDetail()
297: urCreateGridDetail urCreateGridDetail2 = new urCreateGridDetail();
303: public urCreateReport AddTabCreateReport()
309: urCreateReport urCreateReport2 = new urCreateReport();
315: public urCreateVoucher AddTabCreateVoucher()
321: urCreateVoucher urCreateVoucher2 = new urCreateVoucher();
327: public urCreateProcessing AddTabCreateProcessing()
333: urCreateProcessing urCreateProcessing2 = new urCreateProcessing();
339: public urCreateFilter AddTabCreateFilter()
345: urCreateFilter urCreateFilter2 = new urCreateFilter();
351: public urSourceRetrieve AddTabCreateRetrieve()
357: urSourceRetrieve urSourceRetrieve2 = new urSourceRetrieve();
363: public urChangeHeader AddTabChangeHeader()
369: urChangeHeader urChangeHeader2 = new urChangeHeader();
375: public urChangeColumnWidth AddTabChangeColumnWidth()
381: urChangeColumnWidth urChangeColumnWidth2 = new urChangeColumnWidth();
387: public urCheckMail AddTabCheckMail()
393: urCheckMail urCheckMail2 = new urCheckMail();
399: public urCheckRpt AddTabCheckRPT()
405: urCheckRpt urCheckRpt2 = new urCheckRpt();
411: public urDecryptSQLObject AddTabDecryptSQLObject()
417: urDecryptSQLObject urDecryptSQLObject2 = new urDecryptSQLObject();
423: public urCompareText AddTabCompareText()
430: urCompareText urCompareText2 = new urCompareText();
437: public urQueryCompare AddTabCompareQueryText()
444: urQueryCompare urQueryCompare2 = new urQueryCompare();
451: public urCompareFolder AddTabCompareFolder()
458: urCompareFolder urCompareFolder2 = new urCompareFolder();
465: public urStringBeauty AddTabStringBeauty()
471: urStringBeauty urStringBeauty2 = new urStringBeauty();
477: public urMinifier AddTabMinifier()
483: urMinifier urMinifier2 = new urMinifier();
489: public urLibrary AddTabLibrary()
495: urLibrary urLibrary2 = new urLibrary();
501: public urExploreCommander AddTabExploreCommander()
507: urExploreCommander urExploreCommander2 = new urExploreCommander();
513: public urSetupEInvoice AddTabSetupEInvoice()
519: urSetupEInvoice urSetupEInvoice2 = new urSetupEInvoice();
525: public urRptFEC AddTabRptFEC()
531: urRptFEC urRptFEC2 = new urRptFEC();
537: public static TabPageAV AddTab(zurInheritsControl ur, string cName, string tag, int index, Color clTab = default(Color))
741: public void ViewFileConfig()
743: frmViewConfigTables frmViewConfigTables2 = new frmViewConfigTables();
748: public void OpenAVTool()
750: Process.Start(Application.ExecutablePath);
753: public void OpenFCodeViewer()
756: Process.Start(fileName);
759: public void CheckKeyCode()
761: frmCheckKeyCode frmCheckKeyCode2 = new frmCheckKeyCode();
766: public void WhatIDo()
774: public static void ChooseServerName()
776: frmServerName frmServerName2 = new frmServerName();
920: public static SqlConnection ChangeDatabase(SqlConnection connection, string db)
943: public static void ChangeAppDB(SqlConnection connection)
956: public static void ChangeSysDB(SqlConnection connection)
969: public static void SwitchDatabase()
1008: public static void RunSQLProfiler()
1018: urPRofiler urPRofiler2 = new urPRofiler();
1027: public static void RunSQLProfilerSingle()
1098: Process.Start(fileName, arguments);
1134: public static void RunSQLSMS()
1186: Process.Start(fileName, arguments);
1197: public static void LoadDatabase(DataRow rowInfo = null)
1357: public void BackupDatabase()
1359: frmBackup frmBackup2 = new frmBackup();
1364: public void RestoreDatabase()
1366: frmRestore frmRestore2 = new frmRestore();
1371: public void DatabaseInfo()
1373: frmDBInfo frmDBInfo2 = new frmDBInfo();
1378: public void AttachDatabase()
1380: frmAttach frmAttach2 = new frmAttach();
1385: public void RefreshData()
1428: public void AddToScript()
1455: public void ViewScript()
1457: frmScript frmScript2 = new frmScript();
1464: public void ClearScript()
1472: public void SaveScript()
1488: public void AddToScript2Clipboard()
1515: public static string getIDSourcePath(Control cParent, byte xtype = 0)
1560: buttonP.Click += PvJpHGro2d;
1561: buttonP2.Click += TQIpI7uYql;
## FCode\Defaults.cs (56 lines)
1: using System;
2: using System.Collections;
3: using System.Drawing;
8: public class Defaults
29: public Defaults()
35: public static Hashtable DefineParserColors()
51: public static bool CheckLineComment(string s_Text__1)
## FCode\frmAddSource.cs (984 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.IO;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
9: using System.Xml;
11: using fcontrol;
12: using fsystem;
14: using Microsoft.VisualBasic;
15: using Microsoft.VisualBasic.CompilerServices;
21: public class frmAddSource : FormP
143: buttonP.Click += value2;
244: buttonP.Click += value2;
405: label.Click += value2;
410: public frmAddSource()
421: protected override void Dispose(bool disposing)
662: frmSourceVersionConfig frmSourceVersionConfig2 = new frmSourceVersionConfig();
743: fws11WCssP.ReadXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
748: fws11WCssP.ReadXml(stream);
## FCode\frmAddWFile.cs (1283 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Data.SqlClient;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Runtime.CompilerServices;
9: using System.Text;
10: using System.Text.RegularExpressions;
11: using System.Windows.Forms;
13: using fcontrol;
14: using fsystem;
16: using Microsoft.VisualBasic;
17: using Microsoft.VisualBasic.CompilerServices;
24: public class frmAddWFile : FormP
151: buttonP.Click += value2;
252: buttonP.Click += value2;
383: label.Click += value2;
403: public frmAddWFile()
415: protected override void Dispose(bool disposing)
661: frmWFileVersionConfig frmWFileVersionConfig2 = new frmWFileVersionConfig();
772: pvqePeDqeY.ReadXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
777: pvqePeDqeY.ReadXml(stream);
1007: string text8 = File.ReadAllText(text6);
## FCode\frmAttach.cs (412 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fcontrol;
10: using fsystem;
12: using Microsoft.VisualBasic.CompilerServices;
17: public class frmAttach : FormP
133: buttonP.Click += value2;
198: public frmAttach()
207: protected override void Dispose(bool disposing)
## FCode\frmBackup.cs (540 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fcontrol;
10: using fsystem;
12: using Microsoft.VisualBasic;
13: using Microsoft.VisualBasic.CompilerServices;
18: public class frmBackup : FormP
89: buttonP.Click += value2;
199: public frmBackup()
208: protected override void Dispose(bool disposing)
534: public bool CheckExistsFolder(string path)
## FCode\frmCheckFieldsBetweenTables.cs (620 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class frmCheckFieldsBetweenTables : FormP
153: buttonP.Click += value2;
218: public frmCheckFieldsBetweenTables()
227: protected override void Dispose(bool disposing)
## FCode\frmCheckKeyCode.cs (338 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Diagnostics;
4: using System.Drawing;
5: using System.Runtime.CompilerServices;
6: using System.Windows.Forms;
8: using fcontrol;
9: using fsystem;
11: using Microsoft.VisualBasic.CompilerServices;
16: public class frmCheckKeyCode : FormP
111: buttonP.Click += value2;
191: public frmCheckKeyCode()
201: protected override void Dispose(bool disposing)
## FCode\frmDBInfo.cs (825 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fcontrol;
10: using fsystem;
12: using Microsoft.VisualBasic;
13: using Microsoft.VisualBasic.CompilerServices;
19: public class frmDBInfo : FormP
161: buttonP.Click += value2;
397: buttonP.Click += value2;
417: public frmDBInfo()
426: protected override void Dispose(bool disposing)
## FCode\frmDescriptionColumns.cs (651 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Linq;
8: using System.Runtime.CompilerServices;
9: using System.Text.RegularExpressions;
10: using System.Windows.Forms;
12: using fcontrol;
13: using fsystem;
15: using Microsoft.VisualBasic;
16: using Microsoft.VisualBasic.CompilerServices;
21: public class frmDescriptionColumns : FormP
216: buttonP.Click += value2;
242: buttonP.Click += value2;
277: public frmDescriptionColumns()
286: protected override void Dispose(bool disposing)
497: public void InitGrid()
642: dsDescriptionColumn.WriteXml(pathSave);
## FCode\frmFileLookupCopyFiles.cs (529 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.IO;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic.CompilerServices;
18: public class frmFileLookupCopyFiles : FormP
148: buttonP.Click += value2;
174: buttonP.Click += value2;
254: public frmFileLookupCopyFiles()
263: protected override void Dispose(bool disposing)
## FCode\frmFilterTemplate.cs (714 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fcontrol;
10: using fsystem;
12: using Microsoft.VisualBasic;
13: using Microsoft.VisualBasic.CompilerServices;
18: public class frmFilterTemplate : FormP
266: public frmFilterTemplate()
276: protected override void Dispose(bool disposing)
450: dataSet.ReadXml(Application.StartupPath + <obfuscated-string>);
626: frmFilterTemplate_Update frmFilterTemplate_Update3 = new frmFilterTemplate_Update();
647: frmFilterTemplate_Update frmFilterTemplate_Update2 = new frmFilterTemplate_Update();
682: dataSet.WriteXml(Application.StartupPath + <obfuscated-string>);
710: dataSet.WriteXml(Application.StartupPath + <obfuscated-string>);
## FCode\frmFilterTemplate_Update.cs (447 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fcontrol;
10: using fsystem;
12: using Microsoft.VisualBasic.CompilerServices;
18: public class frmFilterTemplate_Update : FormP
94: buttonP.Click += value2;
120: buttonP.Click += value2;
247: label.Click += value2;
252: public frmFilterTemplate_Update()
261: protected override void Dispose(bool disposing)
## FCode\frmGetResourceDLL.cs (743 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Reflection;
9: using System.Runtime.CompilerServices;
10: using System.Windows.Forms;
12: using fcontrol;
13: using fsystem;
15: using Microsoft.VisualBasic.CompilerServices;
20: public class frmGetResourceDLL : FormP
138: buttonP.Click += value2;
323: public frmGetResourceDLL()
333: protected override void Dispose(bool disposing)
## FCode\frmMain.cs (5941 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.Drawing.Drawing2D;
9: using System.IO;
10: using System.Runtime.CompilerServices;
11: using System.Windows.Forms;
12: using System.Xml;
14: using fcontrol;
15: using fsystem;
17: using Microsoft.VisualBasic;
18: using Microsoft.VisualBasic.CompilerServices;
20: using ScintillaNET;
25: public class frmMain : Form
3001: public frmMain()
3018: protected override void Dispose(bool disposing)
3174: ctrMainControl = new urMainControl();
3866: internal virtual ToolStripContentPanel N7cpdaIYI6x()
3874: internal virtual void OEnpdn13x4U(ToolStripContentPanel WithEventsValue)
3879: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
3980: L7YfXtFySt.DoubleClick += RUWotcpiyJ;
4204: tlsFCodeTranslator.Click += stEowdYKth;
4272: frmTranslatorServerSetting frmTranslatorServerSetting2 = new frmTranslatorServerSetting();
4331: mniQuickAccess.Click += vivoxGWhGW;
4347: tlsQuickAdd.Click += DmtoQ2cXNr;
4448: frmQuickAccess frmQuickAccess2 = new frmQuickAccess();
4478: mniTheme.Click += XCRlkElpxl;
4800: Process.Start(Application.ExecutablePath);
4806: Process.Start(fileName);
4811: Process.Start(Application.StartupPath);
4817: Process.Start(fileName);
4832: tlsCaptureScreen.Click += h53lYZqpaL;
4833: mniNewAVTool.Click += WcilNfFKJv;
4834: mniNewFCodeViewer.Click += Wq3lPCha7N;
4835: mniOpenFCodeLocation.Click += bYolccBTwM;
4870: mniToolCopyFile.Click += SI4llVB2se;
4953: mniShowObject.Click += vgYlVL8P51;
5016: urQuery2.txtSQL.Text = File.ReadAllText(array[0]);
5024: urQuery3.txtSQL.Text = File.ReadAllText(array[i]);
5081: public void UpdatePosition(int n, int count)
5096: Process.Start(<obfuscated-string>, <obfuscated-string> + text);
5100: Process.Start(text);
5172: Process.Start(<obfuscated-string>, <obfuscated-string> + text);
5176: Process.Start(text);
5217: public void COPYPROJECTINFO()
5240: public void CREATEMENU()
5255: text = Sec.Decrypt(text, <obfuscated-string>);
5295: Process.Start(text);
5377: P_0.Click += TFWldDnqsO;
## FCode\frmMenu_Duplicate.cs (184 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fcontrol;
10: using fsystem;
12: using Microsoft.VisualBasic.CompilerServices;
17: public class frmMenu_Duplicate : FormP
83: public frmMenu_Duplicate()
92: protected override void Dispose(bool disposing)
## FCode\frmQueryMore.cs (811 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class frmQueryMore : FormP
208: buttonP.Click += value2;
234: buttonP.Click += value2;
286: label.Click += value2;
312: label.Click += value2;
373: public frmQueryMore()
382: protected override void Dispose(bool disposing)
580: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
## FCode\frmQuickAccess.cs (612 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Diagnostics;
4: using System.Drawing;
5: using System.Runtime.CompilerServices;
6: using System.Windows.Forms;
8: using fcontrol;
9: using fsystem;
11: using Microsoft.VisualBasic;
12: using Microsoft.VisualBasic.CompilerServices;
18: public class frmQuickAccess : FormP
94: buttonP.Click += value2;
120: buttonP.Click += value2;
230: public frmQuickAccess()
241: protected override void Dispose(bool disposing)
370: cmdAdd.Click += RMlcqfvOJM;
371: cmdReturn.Click += qcocDraf8c;
372: cmdClear.Click += TKxcM7tARn;
## FCode\frmRestore.cs (581 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Data.SqlClient;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class frmRestore : FormP
113: buttonP.Click += value2;
279: public frmRestore()
291: protected override void Dispose(bool disposing)
## FCode\frmScript.cs (476 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Diagnostics;
4: using System.Drawing;
5: using System.IO;
6: using System.Runtime.CompilerServices;
7: using System.Text;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic.CompilerServices;
14: using ScintillaNET;
19: public class frmScript : FormP
195: public frmScript()
208: protected override void Dispose(bool disposing)
360: public int FindAll(int posStart = -1)
404: public void HighlightWord(string strSelected)
## FCode\frmServerName.cs (761 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Data.SqlClient;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
20: public class frmServerName : FormP
138: public frmServerName()
153: protected override void Dispose(bool disposing)
265: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
271: frmServerName_SyncProject frmServerName_SyncProject2 = new frmServerName_SyncProject();
398: Process.Start(processStartInfo);
471: Process.Start(processStartInfo);
643: frmServerName_Update frmServerName_Update2 = new frmServerName_Update();
667: frmServerName_Update frmServerName_Update2 = new frmServerName_Update();
## FCode\frmServerName_SyncProject.cs (521 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Data.SqlClient;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class frmServerName_SyncProject : FormP
118: buttonP.Click += value2;
144: buttonP.Click += value2;
220: public frmServerName_SyncProject()
229: protected override void Dispose(bool disposing)
390: text = Sec.Decrypt(text, <obfuscated-string>);
## FCode\frmServerName_Update.cs (1602 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Data.SqlClient;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Runtime.CompilerServices;
9: using System.Windows.Forms;
11: using fcontrol;
12: using fsystem;
14: using Microsoft.VisualBasic;
15: using Microsoft.VisualBasic.CompilerServices;
20: public class frmServerName_Update : FormP
364: buttonP.Click += value2;
405: buttonP.Click += value2;
431: buttonP.Click += value2;
757: label.Click += value2;
762: public frmServerName_Update()
773: protected override void Dispose(bool disposing)
1264: dataSet.ReadXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
1269: dataSet.ReadXml(stream);
1570: frmRestore frmRestore2 = new frmRestore();
## FCode\frmSourceVersionConfig.cs (644 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.IO;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic.CompilerServices;
18: public class frmSourceVersionConfig : FormP
123: buttonP.Click += value2;
149: buttonP.Click += value2;
205: buttonP.Click += value2;
291: buttonP.Click += value2;
296: public frmSourceVersionConfig()
309: protected override void Dispose(bool disposing)
629: IZjWqFgBhI.ReadXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
633: IZjWqFgBhI.ReadXml(stream);
639: dataSet.WriteXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
## FCode\frmTableCheckStruct.cs (389 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class frmTableCheckStruct : FormP
125: public frmTableCheckStruct()
134: protected override void Dispose(bool disposing)
## FCode\frmTableEditColumn.cs (981 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.Runtime.CompilerServices;
9: using System.Text;
10: using System.Windows.Forms;
12: using fcontrol;
13: using fsystem;
15: using Microsoft.VisualBasic;
16: using Microsoft.VisualBasic.CompilerServices;
22: public class frmTableEditColumn : FormP
129: buttonP.Click += value2;
209: public frmTableEditColumn()
220: protected override void Dispose(bool disposing)
419: public void LoadColumns()
## FCode\frmTranslatorServerSetting.cs (940 lines)
1: using System;
2: using System.Collections.Generic;
3: using System.ComponentModel;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fcontrol;
10: using fsystem;
12: using Microsoft.VisualBasic;
13: using Microsoft.VisualBasic.CompilerServices;
18: public class frmTranslatorServerSetting : FormP
173: buttonP.Click += value2;
349: buttonP.Click += value2;
504: public frmTranslatorServerSetting()
513: protected override void Dispose(bool disposing)
## FCode\frmUpdateTopBottomScript.cs (436 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fcontrol;
10: using fsystem;
12: using Microsoft.VisualBasic.CompilerServices;
13: using ScintillaNET;
18: public class frmUpdateTopBottomScript : FormP
230: public frmUpdateTopBottomScript()
241: protected override void Dispose(bool disposing)
379: label.Click += YiCUcfanD1;
## FCode\frmViewConfigTables.cs (949 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic.CompilerServices;
18: public class frmViewConfigTables : FormP
294: buttonP.Click += value2;
445: public frmViewConfigTables()
454: protected override void Dispose(bool disposing)
661: internal virtual ColumnHeader wMTpBx2kYWR()
669: internal virtual void fMspBQ40ltp(ColumnHeader WithEventsValue)
848: dataSet2.WriteXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
882: dataSet3.WriteXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
917: dataSet.WriteXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
## FCode\frmWFileVersionConfig.cs (645 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.IO;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic.CompilerServices;
18: public class frmWFileVersionConfig : FormP
123: buttonP.Click += value2;
149: buttonP.Click += value2;
205: buttonP.Click += value2;
291: buttonP.Click += value2;
296: public frmWFileVersionConfig()
309: protected override void Dispose(bool disposing)
630: bR9UToxEa0.ReadXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
634: bR9UToxEa0.ReadXml(stream);
640: dataSet.WriteXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
## FCode\Functions.cs (113 lines)
1: using System.Drawing;
2: using System.Text;
5: using Microsoft.VisualBasic;
9: public class Functions
11: public Functions()
22: public static string ReplaceHtml(string s_Text)
69: public static string ReplaceRtf(string s_Text)
107: public static string GetHtmlColor(Color c_Col)
## FCode\Parser.cs (558 lines)
1: using System;
2: using System.Collections;
3: using System.Drawing;
4: using System.Windows.Forms;
7: using Microsoft.VisualBasic;
8: using Microsoft.VisualBasic.CompilerServices;
12: public class Parser
229: public ParseItem(Parser i_Parser, string s_Text)
250: public void Remove(bool b_IgnorePermanent)
292: public void InsertAfter(ParseItem i_Item)
303: public void InsertBefore(ParseItem i_Item)
314: public void GetText(RtfHtmlBuilder i_Builder)
341: public Parser()
353: public void Abort()
358: public bool Parse(string s_SQL, bool b_ModifyLinebreaks, bool b_Append_LF)
364: public bool Parse(string s_SQL, bool b_ModifyLinebreaks, bool b_Append_LF, ref int s32_CursorPos)
382: protected static eType _getType(ParseItem i_Item)
553: public string GetRtf(bool _EnableColor, Font i_Font)
## FCode\RtfHtmlBuilder.cs (144 lines)
1: using System;
2: using System.Collections;
3: using System.Drawing;
4: using System.Text;
7: using Microsoft.VisualBasic;
8: using Microsoft.VisualBasic.CompilerServices;
12: public class RtfHtmlBuilder
58: public RtfHtmlBuilder()
65: public void Clear()
73: public void AppendText(string s_Text)
103: public string BuildRtf(bool _EnableColor, Font i_Font)
130: public static string Unicode2RTF(string UnicodeString)
## FCode\Serializable.cs (22 lines)
5: public class Serializable
7: public Serializable()
13: public virtual string Serialize()
18: public virtual void Deserialize(string s_Serial)
## FCode\SQLEditor.cs (600 lines)
1: using System;
2: using System.Drawing;
3: using System.Runtime.InteropServices;
4: using System.Windows.Forms;
6: using fsystem;
8: using Microsoft.VisualBasic;
9: using Microsoft.VisualBasic.CompilerServices;
13: public class SQLEditor : RichTextBox
27: public RtfData(SQLEditor i_Box)
36: public void SetRtf(SQLEditor i_Box)
41: public bool Equals(RtfData i_Data)
55: public UndoBuffer(SQLEditor i_Box)
64: protected bool TextChanged()
74: public void SetNewText()
83: public void Undo()
98: public void Redo()
257: public SQLEditor()
280: protected override void OnCreateControl()
286: protected void LockWindow(bool b_Lock)
308: public override bool PreProcessMessage(ref Message msg)
313: protected override void WndProc(ref Message m)
321: protected override void OnKeyDown(KeyEventArgs e)
420: public void CommentSelection()
451: public void UnCommentSelection()
493: protected override void OnKeyUp(KeyEventArgs e)
523: public void SetColor()
533: public void Scroll(int cX, int cY)
538: public void SetSelection(int s32_Start, int s32_Length)
549: public int GetLineFromChar(int s32_Pos)
554: public int GetLineIndex(int s32_Line)
559: public void GetLineAndCharPos(ref int s32_Line, ref int s32_CharPos)
565: public void ReplaceRtf(string s_Rtf, int s32_SelectionStart, int s32_SelectionLength)
576: protected void SetRtf(string s_Rtf, int s32_SelectionStart, int s32_SelectionLength)
584: protected override void OnSelectionChanged(EventArgs e)
592: protected override void OnMouseWheel(MouseEventArgs e)
## FCode\StackEx.cs (116 lines)
1: using System;
2: using System.Collections;
3: using System.Reflection;
4: using System.Runtime.CompilerServices;
5: using System.Threading;
7: using Microsoft.VisualBasic.CompilerServices;
11: public class StackEx : ArrayList
34: public StackEx()
41: public void PushUnique(object o_Value)
47: public void Push(object o_Value)
53: public object Pop()
67: public object Peek()
76: public string Serialize(char c_Delimiter, int MaxCount)
98: public void DeSerialize(string s_Serial, Type t_ValueType, char c_Delimiter)
## FCode\urChangeColumnWidth.cs (50 lines)
1: using System.ComponentModel;
2: using System.Diagnostics;
3: using System.Drawing;
4: using System.Windows.Forms;
7: using Microsoft.VisualBasic.CompilerServices;
12: public class urChangeColumnWidth : zurInheritsControl
16: public urChangeColumnWidth()
24: protected override void Dispose(bool disposing)
## FCode\urChangeHeader.cs (485 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Runtime.CompilerServices;
9: using System.Text;
10: using System.Text.RegularExpressions;
11: using System.Windows.Forms;
13: using fcontrol;
14: using fsystem;
16: using Microsoft.VisualBasic;
17: using Microsoft.VisualBasic.CompilerServices;
22: public class urChangeHeader : zurInheritsControl
116: buttonP.Click += value2;
121: public urChangeHeader()
130: protected override void Dispose(bool disposing)
431: string text = File.ReadAllText(P_0);
## FCode\urChangeOwner.cs (809 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fcontrol;
10: using fsystem;
12: using Microsoft.VisualBasic.CompilerServices;
17: public class urChangeOwner : zurInheritsControl
216: buttonP.Click += value2;
242: buttonP.Click += value2;
268: buttonP.Click += value2;
294: buttonP.Click += value2;
380: toolStripMenuItem.Click += value2;
421: buttonP.Click += value2;
447: buttonP.Click += value2;
482: public urChangeOwner()
491: protected override void Dispose(bool disposing)
796: public override void OnRefresh()
803: public override string OnAddScript()
## FCode\urCheckMail.cs (1332 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Diagnostics;
4: using System.Drawing;
5: using System.IO;
6: using System.Net;
7: using System.Net.Mail;
8: using System.Runtime.CompilerServices;
9: using System.Text;
10: using System.Windows.Forms;
11: using System.Xml;
13: using fcontrol;
14: using fsystem;
16: using Microsoft.VisualBasic;
17: using Microsoft.VisualBasic.CompilerServices;
22: public class urCheckMail : zurInheritsControl
582: buttonP.Click += value2;
710: public urCheckMail()
719: protected override void Dispose(bool disposing)
## FCode\urCheckRpt.cs (1061 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Runtime.CompilerServices;
9: using System.Text;
10: using System.Text.RegularExpressions;
11: using System.Windows.Forms;
12: using System.Xml;
14: using CrystalDecisions.CrystalReports.Engine;
15: using CrystalDecisions.Shared;
16: using fcontrol;
17: using fsystem;
19: using Microsoft.VisualBasic;
20: using Microsoft.VisualBasic.CompilerServices;
26: public class urCheckRpt : zurInheritsControl
297: buttonP.Click += value2;
368: label.Click += value2;
424: public urCheckRpt()
433: protected override void Dispose(bool disposing)
694: Process.Start(txtRptPath.Text);
752: string input = File.ReadAllText(text2);
811: urCheckRPTData urCheckRPTData2 = new urCheckRPTData();
## FCode\urCheckRPTData.cs (833 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Runtime.CompilerServices;
7: using System.Text;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class urCheckRPTData : UserControl
285: public urCheckRPTData()
294: protected override void Dispose(bool disposing)
521: public void InitGrid(DataTable tblParam, DataTable tblFormula, DataTable tblSource, DataTable tblColumn, string tabName)
582: public string Check(DataSet ds, DataTable dsParam)
825: public void GotoDispose()
## FCode\urClearTables.cs (1843 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class urClearTables : zurInheritsControl
753: public urClearTables()
766: protected override void Dispose(bool disposing)
1145: internal virtual ContextMenuStripP vg7pF5cKcBr()
1153: internal virtual void owEpFAw2pQ9(ContextMenuStripP WithEventsValue)
1160: internal virtual ContextMenuStripP BpSpFC1gHET()
1168: internal virtual void amKpFXHNZtn(ContextMenuStripP WithEventsValue)
1692: public override void OnRefresh()
1698: public override string OnAddScript()
1821: buIpk2N37hx = new frmScript();
## FCode\urCommand.cs (3467 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Runtime.CompilerServices;
9: using System.Threading;
10: using System.Windows.Forms;
11: using System.Xml;
13: using fcontrol;
14: using fsystem;
16: using Microsoft.VisualBasic;
17: using Microsoft.VisualBasic.CompilerServices;
18: using Microsoft.Win32;
24: public class urCommand : zurInheritsControl
964: public urCommand()
980: protected override void Dispose(bool disposing)
1322: public override string OnAddNote()
1332: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
1368: public override void OnRefresh()
1374: public override string OnAddScript()
1411: ctxRunExeByProcess.Click += pDxpk9hb7B5;
1988: public Bitmap GetIconForFileOrFolder(string FilePath)
2714: Process.Start(processStartInfo);
2867: dataSet.ReadXml(Application.StartupPath + <obfuscated-string>);
2942: dataSet.WriteXml(Application.StartupPath + <obfuscated-string>);
2959: RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(text, writable: true);
2962: Registry.CurrentUser.CreateSubKey(text);
2963: registryKey = Registry.CurrentUser.OpenSubKey(text, writable: true);
2997: dataSet.ReadXml(Application.StartupPath + <obfuscated-string>);
3009: RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(text2, writable: true);
3012: registryKey = Registry.CurrentUser.CreateSubKey(text2, writable: true);
3020: dataSet.WriteXml(Application.StartupPath + <obfuscated-string>);
3047: dataSet.ReadXml(Application.StartupPath + <obfuscated-string>);
3052: RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(text2, writable: true);
3055: Registry.CurrentUser.CreateSubKey(text2);
3056: registryKey = Registry.CurrentUser.OpenSubKey(text2, writable: true);
3080: process.StartInfo.WorkingDirectory = text3;
3082: process.StartInfo.FileName = fileName;
3083: process.Start();
3134: frmAddSource frmAddSource2 = new frmAddSource();
3308: process.StartInfo.FileName = strVal;
3312: process.StartInfo.Arguments = arguments;
3313: process.Start();
3345: result = File.ReadAllText(P_0);
## FCode\urCommander.cs (2068 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Generic;
4: using System.Collections.Specialized;
5: using System.ComponentModel;
6: using System.Data;
7: using System.Diagnostics;
8: using System.Drawing;
9: using System.Drawing.Drawing2D;
10: using System.Drawing.Imaging;
11: using System.IO;
12: using System.Runtime.CompilerServices;
13: using System.Security.Permissions;
14: using System.Windows.Forms;
16: using fcontrol;
17: using fsystem;
19: using Microsoft.VisualBasic;
20: using Microsoft.VisualBasic.CompilerServices;
21: using Microsoft.VisualBasic.FileIO;
26: public class urCommander : UserControl
53: public jfUsdmpDjoUN6PRmTkGP()
59: protected override void OnMouseEnter(EventArgs P_0)
66: protected override void OnMouseLeave(EventArgs P_0)
73: protected override void OnPaint(PaintEventArgs P_0)
501: public urCommander()
512: protected override void Dispose(bool disposing)
919: cmdBack.Click += hc0hQrD5F8;
920: cmdOrg.Click += RPIhuPb05n;
923: pnPath.DoubleClick += fUshwO0ggl;
2037: Process.Start(<obfuscated-string>, <obfuscated-string> + text);
2041: public void CreateNewFolder()
## FCode\urCompareDatabase.cs (1901 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Data.SqlClient;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Text;
9: using System.Text.RegularExpressions;
10: using System.Windows.Forms;
12: using fcontrol;
13: using fsystem;
15: using Microsoft.VisualBasic;
16: using Microsoft.VisualBasic.CompilerServices;
17: using ScintillaNET;
22: public class urCompareDatabase : zurInheritsControl
633: label.Click += value2;
923: public urCompareDatabase()
934: protected override void Dispose(bool disposing)
955: uCompare = new urControlCompareText();
1461: lblTableDiff.Click += QjYypp15rW;
1462: lblTableNot1.Click += QjYypp15rW;
1463: lblTableNot2.Click += QjYypp15rW;
1587: frmScript frmScript2 = new frmScript();
1625: frmScript frmScript3 = new frmScript();
## FCode\urCompareDataRow.cs (2656 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.Runtime.CompilerServices;
9: using System.Text;
10: using System.Windows.Forms;
12: using fcontrol;
13: using fsystem;
15: using Microsoft.VisualBasic;
16: using Microsoft.VisualBasic.CompilerServices;
21: public class urCompareDataRow : zurInheritsControl
644: label.Click += value2;
1117: public urCompareDataRow()
1130: protected override void Dispose(bool disposing)
1738: lblTableMatch.Click += cLvdfv6lKQ;
1739: lblTableDiff.Click += cLvdfv6lKQ;
1740: lblTableNot1.Click += cLvdfv6lKQ;
1741: lblTableNot2.Click += cLvdfv6lKQ;
1742: lblDataDiff.Click += xXUdvs68bO;
1743: lblDataNot1.Click += xXUdvs68bO;
1744: lblDataNot2.Click += xXUdvs68bO;
2050: frmScript frmScript2 = new frmScript();
2059: public bool CheckValueDiff(object s1, object s2)
## FCode\urCompareFolder.cs (2408 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Drawing.Imaging;
8: using System.IO;
9: using System.Runtime.CompilerServices;
10: using System.Windows.Forms;
12: using fcontrol;
13: using fsystem;
15: using Microsoft.VisualBasic;
16: using Microsoft.VisualBasic.CompilerServices;
17: using Newtonsoft.Json;
22: public class urCompareFolder : zurInheritsControl
349: label.Click += value2;
600: button.Click += value2;
641: label.Click += value2;
667: label.Click += value2;
743: public urCompareFolder()
755: protected override void Dispose(bool disposing)
1121: cmdChangePath.Click += srZsrd6538;
1279: button.Click += NTosxss1K3;
1796: public string CheckDataFile(DataRow dr1, DataRow dr2)
1826: left = File.ReadAllText(path);
1830: left2 = File.ReadAllText(path2);
1876: urCompareText urCompareText2 = new urCompareText();
2142: Process.Start(_path);
2162: Process.Start(_path);
2169: Process.Start(<obfuscated-string>, _path);
2201: Process.Start(_path);
2250: txtPath1.Text = File.ReadAllText(txtPath1.Text);
2258: txtPath2.Text = File.ReadAllText(txtPath2.Text);
## FCode\urCompareText.cs (2431 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Generic;
4: using System.ComponentModel;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Linq;
9: using System.Runtime.CompilerServices;
10: using System.Text.RegularExpressions;
11: using System.Windows.Forms;
12: using System.Xml.Linq;
14: using DiffPlex;
15: using DiffPlex.DiffBuilder;
16: using DiffPlex.DiffBuilder.Model;
17: using fcontrol;
18: using fsystem;
20: using Microsoft.VisualBasic;
21: using Microsoft.VisualBasic.CompilerServices;
23: using ScintillaNET;
28: public class urCompareText : zurInheritsControl
248: buttonP.Click += value2;
604: label.Click += value2;
654: public urCompareText()
669: protected override void Dispose(bool disposing)
990: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
1101: string text = File.ReadAllText(textBoxP.Text);
1120: string text = File.ReadAllText(openFileDialog.FileName);
1132: public void FirstScreen()
1161: SideBySideDiffBuilder sideBySideDiffBuilder = new SideBySideDiffBuilder(differ);
1162: SideBySideDiffModel sideBySideDiffModel = sideBySideDiffBuilder.BuildDiffModel(txtContentONE.Text.Trim(), txtContentTWO.Text.Trim());
2174: public void OpenSearch(object sender)
2178: fSearch = new urSearch();
2372: public void SelectFullRow(Scintilla txt)
2418: NewLateBinding.LateSet(P_0, null, <obfuscated-string>, new object[1] { File.ReadAllText(array[0]) }, null, null);
## FCode\urControlCompareText.cs (1732 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Generic;
4: using System.ComponentModel;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Linq;
9: using System.Runtime.CompilerServices;
10: using System.Text.RegularExpressions;
11: using System.Windows.Forms;
12: using System.Xml.Linq;
14: using DiffPlex;
15: using DiffPlex.DiffBuilder;
16: using DiffPlex.DiffBuilder.Model;
17: using fcontrol;
18: using fsystem;
20: using Microsoft.VisualBasic;
21: using Microsoft.VisualBasic.CompilerServices;
23: using ScintillaNET;
28: public class urControlCompareText : UserControl
174: label.Click += value2;
269: public urControlCompareText()
284: protected override void Dispose(bool disposing)
401: public void HideResult(bool hidden = true, bool force = false)
469: public void CompareText(string s1, string s2, Lexer nowlexer = Lexer.Null)
473: SideBySideDiffBuilder sideBySideDiffBuilder = new SideBySideDiffBuilder(differ);
474: SideBySideDiffModel sideBySideDiffModel = sideBySideDiffBuilder.BuildDiffModel(s1.Trim(), s2.Trim());
1499: public void OpenSearch(object sender)
1503: fSearch = new urSearch();
1697: public void SelectFullRow(Scintilla txt)
## FCode\urCreateFileRpt.cs (4776 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Generic;
4: using System.ComponentModel;
5: using System.Data;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.IO;
9: using System.Linq;
10: using System.Net;
11: using System.Reflection;
12: using System.Runtime.CompilerServices;
13: using System.Runtime.InteropServices;
14: using System.Text.RegularExpressions;
15: using System.Windows.Forms;
16: using System.Xml;
17: using System.Xml.XPath;
19: using CrystalDecisions.CrystalReports.Engine;
20: using CrystalDecisions.Shared;
21: using fcontrol;
22: using fsystem;
24: using Microsoft.VisualBasic;
25: using Microsoft.VisualBasic.CompilerServices;
31: public class urCreateFileRpt : zurInheritsControl
412: buttonP.Click += value2;
438: buttonP.Click += value2;
569: buttonP.Click += value2;
595: buttonP.Click += value2;
1104: label.Click += value2;
1130: label.Click += value2;
1212: label.Click += value2;
1238: label.Click += value2;
1524: public urCreateFileRpt()
1534: protected override void Dispose(bool disposing)
3029: ds.ReadXmlSchema(txtXsdPath.Text);
3582: buttonP.Click += enmpAILAbnV;
3756: ds.ReadXmlSchema(txtXsdPath.Text);
3783: ds.WriteXmlSchema(text2);
3971: Process.Start(text);
4649: Process.Start(text);
4733: dataSet.WriteXmlSchema(text2);
4747: Process.Start(text2);
## FCode\urCreateFilter.cs (974 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Generic;
4: using System.ComponentModel;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic.CompilerServices;
18: public class urCreateFilter : zurInheritsControl
194: buttonP.Click += value2;
220: buttonP.Click += value2;
411: label.Click += value2;
437: label.Click += value2;
513: public urCreateFilter()
524: protected override void Dispose(bool disposing)
543: ctrStep = new urStepByStep();
828: ed1jog8S3V = new urCreateFilterGenerate();
848: uDesignF = new urGenerateViewDesign();
864: uFilter = new urListDeclearField();
923: urListDeclearField urListDeclearField2 = new urListDeclearField();
## FCode\urCreateFilterGenerate.cs (376 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Diagnostics;
4: using System.Drawing;
5: using System.IO;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fsystem;
11: using Microsoft.VisualBasic;
12: using Microsoft.VisualBasic.CompilerServices;
17: public class urCreateFilterGenerate : UserControl
147: public urCreateFilterGenerate()
156: protected override void Dispose(bool disposing)
246: lblPath.Click += gQajOwIBkr;
252: Process.Start(fileName);
255: public void GenerateSource()
## FCode\urCreateGridDetail.cs (1982 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Runtime.CompilerServices;
9: using System.Windows.Forms;
11: using fcontrol;
12: using fsystem;
14: using Microsoft.VisualBasic;
15: using Microsoft.VisualBasic.CompilerServices;
20: public class urCreateGridDetail : zurInheritsControl
310: buttonP.Click += value2;
336: buttonP.Click += value2;
504: label.Click += value2;
530: label.Click += value2;
661: label.Click += value2;
702: label.Click += value2;
728: buttonP.Click += value2;
754: buttonP.Click += value2;
926: label.Click += value2;
1017: public urCreateGridDetail()
1029: protected override void Dispose(bool disposing)
1048: ctrStep = new urStepByStep();
1552: rbrNBECF85 = new urCreateGridDetailGenerate();
1565: uImport = new urListDeclearField();
1608: uGridMain = new urListDeclearField();
## FCode\urCreateGridDetailGenerate.cs (394 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.IO;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fsystem;
12: using Microsoft.VisualBasic;
13: using Microsoft.VisualBasic.CompilerServices;
18: public class urCreateGridDetailGenerate : UserControl
148: public urCreateGridDetailGenerate()
157: protected override void Dispose(bool disposing)
247: lblPath.Click += HXhNUECHB3;
253: Process.Start(fileName);
256: public void GenerateGridDetail()
## FCode\urCreateList.cs (2176 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Generic;
4: using System.ComponentModel;
5: using System.Data;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.Runtime.CompilerServices;
9: using System.Windows.Forms;
11: using fcontrol;
12: using fsystem;
14: using Microsoft.VisualBasic;
15: using Microsoft.VisualBasic.CompilerServices;
20: public class urCreateList : zurInheritsControl
30: public StepEx(int index, string code, string name)
346: buttonP.Click += value2;
372: buttonP.Click += value2;
861: label.Click += value2;
887: label.Click += value2;
1094: public urCreateList()
1107: protected override void Dispose(bool disposing)
1126: ctrStep = new urStepByStep();
1687: uFilter = new urListDeclearField();
1705: uGridMain = new urListDeclearField();
1778: uMaster = new urListDeclearField();
1836: uImport = new urListDeclearField();
1882: uDesignM = new urGenerateViewDesign();
1900: uDesignF = new urGenerateViewDesign();
1916: Vlpq1EjD5w = new urWCommand();
1943: Qb9qhuOxrT = new urCreateListGenerate();
2140: urListDeclearField urListDeclearField2 = new urListDeclearField();
## FCode\urCreateListGenerate.cs (1470 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.IO;
7: using System.Reflection;
8: using System.Runtime.CompilerServices;
9: using System.Text;
10: using System.Text.RegularExpressions;
11: using System.Windows.Forms;
13: using fsystem;
15: using Microsoft.VisualBasic;
16: using Microsoft.VisualBasic.CompilerServices;
21: public class urCreateListGenerate : UserControl
159: public urCreateListGenerate()
169: protected override void Dispose(bool disposing)
271: lblPath.Click += mh8HApAIOX;
277: Process.Start(fileName);
280: public void GenerateList()
345: string text3 = File.ReadAllText(path);
## FCode\urCreateProcessing.cs (921 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fcontrol;
10: using fsystem;
12: using Microsoft.VisualBasic.CompilerServices;
17: public class urCreateProcessing : zurInheritsControl
186: buttonP.Click += value2;
212: buttonP.Click += value2;
403: label.Click += value2;
429: label.Click += value2;
479: public urCreateProcessing()
490: protected override void Dispose(bool disposing)
509: ctrStep = new urStepByStep();
772: zirLfEZJm0 = new urCreateProcessingGenerate();
785: xT2Ll1EbcA = new urWCommand();
811: uDesign = new urGenerateViewDesign();
827: uFilter = new urListDeclearField();
## FCode\urCreateProcessingGenerate.cs (368 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Diagnostics;
4: using System.Drawing;
5: using System.IO;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fsystem;
11: using Microsoft.VisualBasic;
12: using Microsoft.VisualBasic.CompilerServices;
17: public class urCreateProcessingGenerate : UserControl
147: public urCreateProcessingGenerate()
156: protected override void Dispose(bool disposing)
246: lblPath.Click += LhhDyJaJ4F;
252: Process.Start(fileName);
255: public void GenerateSource()
## FCode\urCreateReport.cs (3339 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Generic;
4: using System.ComponentModel;
5: using System.Data;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.Drawing.Drawing2D;
9: using System.IO;
10: using System.Runtime.CompilerServices;
11: using System.Windows.Forms;
13: using fcontrol;
14: using fsystem;
16: using Microsoft.VisualBasic;
17: using Microsoft.VisualBasic.CompilerServices;
22: public class urCreateReport : zurInheritsControl
477: buttonP.Click += value2;
503: buttonP.Click += value2;
900: label.Click += value2;
926: label.Click += value2;
1057: label.Click += value2;
1098: buttonP.Click += value2;
1199: label.Click += value2;
1225: buttonP.Click += value2;
1657: public urCreateReport()
1668: protected override void Dispose(bool disposing)
1688: ctrStep = new urStepByStep();
2613: lblPaintKy.Click += IApLLr2ibo;
2699: uFilter = new urListDeclearField();
2715: uGridMain = new urListDeclearField();
2733: uDesign = new urGenerateViewDesign();
2770: uPrint = new urListDeclearField();
2792: uDrillDown = new urListDeclearField();
2822: CNe3xrwEys = new urWCommand();
2849: UO23SW1yqY = new urCreateReportGenerate();
## FCode\urCreateReportGenerate.cs (1583 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.IO;
7: using System.Runtime.CompilerServices;
8: using System.Text;
9: using System.Text.RegularExpressions;
10: using System.Windows.Forms;
12: using fsystem;
14: using Microsoft.VisualBasic;
15: using Microsoft.VisualBasic.CompilerServices;
21: public class urCreateReportGenerate : UserControl
182: label.Click += value2;
208: label.Click += value2;
249: label.Click += value2;
275: label.Click += value2;
355: public urCreateReportGenerate()
364: protected override void Dispose(bool disposing)
529: lblPath.Click += R3BvaMxMNH;
535: Process.Start(fileName);
789: public void GenerateReport()
841: string text3 = File.ReadAllText(path);
1079: string text = File.ReadAllText(path);
## FCode\urCreateReportPrint.cs (1544 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Generic;
4: using System.ComponentModel;
5: using System.Data;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.Runtime.CompilerServices;
9: using System.Windows.Forms;
11: using fcontrol;
12: using fsystem;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class urCreateReportPrint : zurInheritsControl
274: buttonP.Click += value2;
300: buttonP.Click += value2;
764: label.Click += value2;
790: label.Click += value2;
840: public urCreateReportPrint()
851: protected override void Dispose(bool disposing)
871: ctrStep = new urStepByStep();
1365: OnF0kRl0eL = new urCreateReportGenerate();
1377: qWi0puL41t = new urWCommand();
1401: uDesign = new urGenerateViewDesign();
1417: uGridMain = new urListDeclearField();
1432: uFilter = new urListDeclearField();
## FCode\urCreateRetrieve.cs (646 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Data.SqlClient;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Linq;
9: using System.Net;
10: using System.Runtime.CompilerServices;
11: using System.Text.RegularExpressions;
12: using System.Windows.Forms;
13: using System.Xml;
15: using fcontrol;
16: using fsystem;
18: using Microsoft.VisualBasic;
19: using Microsoft.VisualBasic.CompilerServices;
25: public class urCreateRetrieve : zurInheritsControl
263: public urCreateRetrieve()
274: protected override void Dispose(bool disposing)
451: dataSet.ReadXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
456: dataSet.ReadXml(stream);
## FCode\urCreateRetrieveCalc.cs (56 lines)
1: using System.ComponentModel;
2: using System.Diagnostics;
3: using System.Drawing;
4: using System.Windows.Forms;
7: using Microsoft.VisualBasic.CompilerServices;
12: public class urCreateRetrieveCalc : UserControl
18: public urCreateRetrieveCalc()
26: protected override void Dispose(bool disposing)
52: public void GenerateRetrieve()
## FCode\urCreateRetrieveGenerate.cs (209 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Diagnostics;
4: using System.Drawing;
5: using System.Runtime.CompilerServices;
6: using System.Windows.Forms;
8: using fsystem;
10: using Microsoft.VisualBasic.CompilerServices;
15: public class urCreateRetrieveGenerate : UserControl
121: public urCreateRetrieveGenerate()
130: protected override void Dispose(bool disposing)
## FCode\urCreateVoucher.cs (2218 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.IO;
9: using System.Net;
10: using System.Runtime.CompilerServices;
11: using System.Runtime.InteropServices;
12: using System.Text.RegularExpressions;
13: using System.Threading;
14: using System.Windows.Forms;
15: using System.Xml;
17: using fcontrol;
18: using fsystem;
20: using Microsoft.VisualBasic;
21: using Microsoft.VisualBasic.CompilerServices;
26: public class urCreateVoucher : zurInheritsControl
367: label.Click += value2;
393: label.Click += value2;
614: label.Click += value2;
970: buttonP.Click += value2;
996: buttonP.Click += value2;
1048: public urCreateVoucher()
1065: protected override void Dispose(bool disposing)
1084: ctrStep = new urStepByStep();
1696: string text2 = ReadXML(path, ref doc);
1799: public string ReadXML(string path, [Optional][DefaultParameterValue(null)] ref XmlDocument doc)
1876: uDPMwBYhLg = new urWCommand();
1903: uSQL = new urCreateVoucherSQL();
1920: uReference = new urCreateVoucherReference();
1940: uSource = new urCreateVoucherSource();
1957: uGen = new urCreateVoucherGenerate();
## FCode\urCreateVoucherGenerate.cs (2597 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.IO;
9: using System.Linq;
10: using System.Runtime.CompilerServices;
11: using System.Text;
12: using System.Text.RegularExpressions;
13: using System.Windows.Forms;
14: using System.Xml;
16: using fcontrol;
17: using fsystem;
19: using Microsoft.VisualBasic;
20: using Microsoft.VisualBasic.CompilerServices;
25: public class urCreateVoucherGenerate : UserControl
533: label.Click += value2;
598: public urCreateVoucherGenerate()
607: protected override void Dispose(bool disposing)
962: lblPath.Click += NrgKAPKUCX;
981: lblCopyApp.Click += X6sK5JXLsB;
982: lblCopySys.Click += X6sK5JXLsB;
983: lblCopyAprroveApp.Click += X6sK5JXLsB;
984: lblCopyAprroveSys.Click += X6sK5JXLsB;
1023: Process.Start(text);
1027: public void GenScript()
1572: text = File.ReadAllText(Conversions.ToString(item[<obfuscated-string>]));
1794: frmScript frmScript2 = new frmScript();
1805: public void GenerateVoucher()
1861: string text2 = File.ReadAllText(P_0);
2032: string text2 = File.ReadAllText(Conversions.ToString(item[<obfuscated-string>]));
2173: string content = File.ReadAllText(text2);
2196: string text4 = File.ReadAllText(text2);
2264: string input = step1.ReadXML(P_0, ref doc);
2265: string text = File.ReadAllText(P_0);
2299: text5 = File.ReadAllText(path);
## FCode\urCreateVoucherReference.cs (353 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.IO;
7: using System.Runtime.CompilerServices;
8: using System.Text.RegularExpressions;
9: using System.Windows.Forms;
10: using System.Xml;
12: using fcontrol;
13: using fsystem;
15: using Microsoft.VisualBasic;
16: using Microsoft.VisualBasic.CompilerServices;
21: public class urCreateVoucherReference : UserControl
77: public urCreateVoucherReference()
86: protected override void Dispose(bool disposing)
157: public void InitGrid()
189: string text = urCreateVoucher2.ReadXML(path, ref doc);
194: string text2 = File.ReadAllText(Conversions.ToString(Operators.AddObject(Operators.AddObject(Operators.AddObject(srcPath, item[<obfuscated-string>]), <obfuscated-string>), item[<obfuscated-string>])));
240: string input = File.ReadAllText(srcPath + <obfuscated-string>);
255: string input = File.ReadAllText(pathDetail);
285: input = File.ReadAllText(path);
## FCode\urCreateVoucherSource.cs (861 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.IO;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class urCreateVoucherSource : UserControl
218: buttonP.Click += value2;
274: label.Click += value2;
300: label.Click += value2;
341: label.Click += value2;
367: label.Click += value2;
372: public urCreateVoucherSource()
382: protected override void Dispose(bool disposing)
599: PTcJqLtigx.ReadXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
604: PTcJqLtigx.ReadXml(stream);
708: public void cboVersion_SelectedValueChanged(object sender, EventArgs e)
## FCode\urCreateVoucherSQL.cs (2236 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.Runtime.CompilerServices;
9: using System.Windows.Forms;
11: using fcontrol;
12: using fsystem;
14: using Microsoft.VisualBasic;
15: using Microsoft.VisualBasic.CompilerServices;
20: public class urCreateVoucherSQL : UserControl
1146: public urCreateVoucherSQL()
1155: protected override void Dispose(bool disposing)
1848: public void InitGrid()
## FCode\urDecryptSQLObject.cs (2144 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.IO;
9: using System.Linq;
10: using System.Net.Http;
11: using System.Net.Http.Headers;
12: using System.Runtime.CompilerServices;
13: using System.Text.RegularExpressions;
14: using System.Threading;
15: using System.Threading.Tasks;
16: using System.Windows.Forms;
18: using fcontrol;
19: using fsystem;
21: using Microsoft.SqlServer.TransactSql.ScriptDom;
22: using Microsoft.VisualBasic;
23: using Microsoft.VisualBasic.CompilerServices;
24: using Newtonsoft.Json;
30: public class urDecryptSQLObject : zurInheritsControl
76: public ResultValue()
90: public _Closure_0024__170_002D0()
97: internal Task _Lambda_0024__0()
132: public VB_0024StateMachine_173_DecryptFromHost()
139: internal void MoveNext()
157: _0024VB_0024ResumableLocal_client_00241.BaseAddress = new Uri(_0024VB_0024ResumableLocal_url_00240);
668: buttonP.Click += value2;
712: buttonP.Click += value2;
783: label.Click += value2;
809: label.Click += value2;
1045: label.Click += value2;
1065: public urDecryptSQLObject()
1075: protected override void Dispose(bool disposing)
1517: string[] array = Sec.Decrypt(cipherText).Split(Conversions.ToChar(tVUgAe7bCe));
1696: BaseAddress = new Uri(txtProxy.Text.Trim()),
1898: SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(<obfuscated-string> + lstDatabase.Control.CheckedItems[0].Text + <obfuscated-string> + P_0 + <obfuscated-string>, sqlConnection);
1900: sqlDataAdapter.Fill(dataTable);
1901: sqlDataAdapter.Dispose();
2058: public string SqlBeauty(string query)
2063: TSql120Parser tSql120Parser = new TSql120Parser(initialQuotedIdentifiers: false);
2064: TSqlFragment scriptFragment = tSql120Parser.Parse(new StringReader(query), out var errors);
2077: Sql120ScriptGenerator sql120ScriptGenerator = new Sql120ScriptGenerator(new SqlScriptGeneratorOptions
2140: Process.Start(text);
## FCode\urExploreCommander.cs (596 lines)
1: using System;
2: using System.Collections.Generic;
3: using System.ComponentModel;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fcontrol;
10: using fsystem;
12: using Microsoft.VisualBasic;
13: using Microsoft.VisualBasic.CompilerServices;
18: public class urExploreCommander : zurInheritsControl
292: public urExploreCommander()
301: protected override void Dispose(bool disposing)
491: tmvs9feK1O = new urCommander();
506: PEMsmLChyl = new urCommander();
526: o4ussYLWIJ = new urCommander();
546: xjfstWrvYe = new urCommander();
## FCode\urFileLookup.cs (6058 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Generic;
4: using System.ComponentModel;
5: using System.Data;
6: using System.Data.SqlClient;
7: using System.Diagnostics;
8: using System.Drawing;
9: using System.Drawing.Drawing2D;
10: using System.IO;
11: using System.Linq;
12: using System.Net;
13: using System.Runtime.CompilerServices;
14: using System.Runtime.InteropServices;
15: using System.Text.RegularExpressions;
16: using System.Threading;
17: using System.Threading.Tasks;
18: using System.Windows.Forms;
19: using System.Xml;
20: using System.Xml.XPath;
22: using fcontrol;
23: using fsystem;
25: using Microsoft.VisualBasic;
26: using Microsoft.VisualBasic.CompilerServices;
27: using Microsoft.VisualBasic.FileIO;
28: using Newtonsoft.Json;
30: using ScintillaNET;
36: public class urFileLookup : zurInheritsControl
48: public CYf7eKpDO98nU85mKGXX(string P_0, string P_1, string P_2, string P_3)
64: public _Closure_0024__364_002D0(_Closure_0024__364_002D0 arg0)
75: internal bool _Lambda_0024__0(FileInfo f)
890: label.Click += value2;
1328: buttonP.Click += value2;
1504: label.Click += value2;
1586: label.Click += value2;
1702: label.Click += value2;
1768: protected override void Dispose(bool disposing)
2442: internal virtual BackgroundWorker lfIpxox7DoW()
2450: internal virtual void C3UpxlpDMG2(BackgroundWorker WithEventsValue)
2455: public override string OnAddNote()
2460: public urFileLookup()
2487: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
2535: public int FindAll(int posStart = -1)
2579: public void HighlightWord(string strSelected)
2646: lblFindText.Click += ccdpXpn3lcC;
2837: dataSet.ReadXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
2842: dataSet.ReadXml(stream);
2877: lblUpdateVersion.Click += KvhpXXatoFM;
2890: dataSet.ReadXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
2895: dataSet.ReadXml(stream);
2906: dataSet.WriteXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
3006: string src = File.ReadAllText(trvFile.Control.SelectedNode.Name);
3350: buttonP.Click += aKrpXLWvT0j;
3351: buttonP2.Click += Gk2pX3PvP7P;
3419: frmScript frmScript2 = new frmScript();
3902: frmScript frmScript2 = new frmScript();
3998: public override void OnRefresh()
4026: public override string OnAddScript()
4238: Process.Start(trvFile.Control.SelectedNode.Name);
4258: Process.Start(trvFile.Control.SelectedNode.Name);
4265: Process.Start(<obfuscated-string>, _path);
4515: frmFileLookupCopyFiles frmFileLookupCopyFiles2 = new frmFileLookupCopyFiles();
4613: frmAddWFile frmAddWFile2 = new frmAddWFile();
4834: text2 = File.ReadAllText(P_0.Name);
4976: dataSet.ReadXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
4984: dataSet.ReadXml(stream);
5026: text4 = File.ReadAllText(path);
5234: public string getDefaultSrcPath()
5264: frmGetResourceDLL frmGetResourceDLL2 = new frmGetResourceDLL();
5287: public void ReLoadFile(bool moreOption = false)
5672: jRDpNCm2Gwb.Click += B2Xp753WS5b;
5897: txtContent.Text = Cmd.BeautyLine(File.ReadAllText(text));
## FCode\urFileReference.cs (1788 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Linq;
9: using System.Runtime.CompilerServices;
10: using System.Threading;
11: using System.Windows.Forms;
13: using fcontrol;
14: using fsystem;
16: using Microsoft.VisualBasic;
17: using Microsoft.VisualBasic.CompilerServices;
19: using ScintillaNET;
25: public class urFileReference : zurInheritsControl
37: public a6nLqPpDLmrHZ9UUGo7O(string P_0, string P_1, string P_2, string P_3)
457: label.Click += value2;
568: protected override void Dispose(bool disposing)
808: public urFileReference()
825: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
896: public int FindAll(int posStart = -1)
940: public void HighlightWord(string strSelected)
976: lblFindText.Click += tB3pNlMLCHp;
999: txtContent.Click += LckpNfHED2R;
1129: JY6pYNqpO2j.ReadXml(Cmd.FullPath(Application.StartupPath) + <obfuscated-string>);
1134: JY6pYNqpO2j.ReadXml(stream);
1166: public override void OnRefresh()
1189: public override string OnAddScript()
1279: Process.Start(trvFile.Control.SelectedNode.Name);
1303: Process.Start(trvFile.Control.SelectedNode.Name);
1436: public void ReLoadFile()
## FCode\urFilterGrid.cs (610 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Globalization;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class urFilterGrid : UserControl
212: protected override void Dispose(bool disposing)
329: public urFilterGrid(urFilterWrapper w)
353: lblAndOr.Click += t9HBejhYvg;
354: lblOperation.Click += t9HBejhYvg;
355: lblDelete.Click += t9HBejhYvg;
473: public void LoadKey(urQuery.FilterKeyArgs item)
520: public void SetValueFilter(string value, string dType, string operation, bool isBlank = false)
555: public string GetKeyString()
582: public urQuery.FilterKeyArgs GetKey()
584: urQuery.FilterKeyArgs filterKeyArgs = new urQuery.FilterKeyArgs();
## FCode\urFilterWrapper.cs (776 lines)
1: using System;
2: using System.Collections.Generic;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class urFilterWrapper : UserControl
120: label.Click += value2;
161: label.Click += value2;
187: label.Click += value2;
213: label.Click += value2;
329: label.Click += value2;
371: public urFilterWrapper()
381: protected override void Dispose(bool disposing)
588: public void LoadKey(ref Label _lblFilter)
624: urFilterGrid urFilterGrid2 = new urFilterGrid(this);
666: public void Delete(urFilterGrid u)
## FCode\urGenerateViewDesign.cs (1636 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Generic;
4: using System.ComponentModel;
5: using System.Data;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.Runtime.CompilerServices;
9: using System.Runtime.InteropServices;
10: using System.Windows.Forms;
12: using fcontrol;
13: using fsystem;
15: using Microsoft.VisualBasic;
16: using Microsoft.VisualBasic.CompilerServices;
22: public class urGenerateViewDesign : UserControl
355: label.Click += value2;
559: button.Click += value2;
585: urGridDesign urGridDesign2 = new urGridDesign();
603: public urGenerateViewDesign()
613: protected override void Dispose(bool disposing)
641: ctrHeader = new urGridDesign();
651: ctrFooter = new urGridDesign();
1145: urGridDesign urGridDesign2 = new urGridDesign();
1237: public string GenerateXML()
1242: public bool Check()
1371: public string GenerateView([Optional][DefaultParameterValue(0)] ref int nHeightDetail, bool GenCategory5forFilter = false)
1444: public string GetFirstControlInAllTab()
1465: public string GenerateDetail(urListDeclearField urMaster, int nHeight = 218)
1504: public void UpdateCategoryIndex(urListDeclearField urMaster)
## FCode\urGridDesign.cs (1794 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class urGridDesign : UserControl
551: public urGridDesign()
577: protected override void Dispose(bool disposing)
818: public void ConvertToGrid(string controller, string id, string title, string title2, string loading)
1130: public void tlsHeader_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
1184: public void ClearAll()
1566: public void InvalidatePanel()
1597: public string GenMain()
1613: public int GetHeight()
1626: public int GetWidth()
1644: public string GenGrid()
1649: public string GetFirstControlName()
1671: public string GenCategory(string index, string header, string header2, int totalGridWidth = 0)
1692: public string GenItem()
## FCode\urImportExportExcel.cs (1914 lines)
1: using System;
2: using System.Collections.Generic;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.IO;
9: using System.Linq;
10: using System.Net;
11: using System.Runtime.CompilerServices;
12: using System.Windows.Forms;
13: using System.Xml;
14: using System.Xml.XPath;
16: using fcontrol;
17: using fsystem;
19: using Microsoft.VisualBasic;
20: using Microsoft.VisualBasic.CompilerServices;
25: public class urImportExportExcel : zurInheritsControl
276: buttonP.Click += value2;
489: label.Click += value2;
605: label.Click += value2;
730: public urImportExportExcel()
743: protected override void Dispose(bool disposing)
1123: public override void OnRefresh()
## FCode\urLibrary.cs (1248 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Diagnostics;
4: using System.Drawing;
5: using System.IO;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fcontrol;
10: using fsystem;
12: using Microsoft.VisualBasic;
13: using Microsoft.VisualBasic.CompilerServices;
19: public class urLibrary : zurInheritsControl
382: public urLibrary()
396: protected override void Dispose(bool disposing)
570: internal virtual ColumnHeader wMTpBx2kYWR()
578: internal virtual void fMspBQ40ltp(ColumnHeader WithEventsValue)
631: stsTimeExec.Click += ghpujn6kE7;
680: Process.Start(stsTimeExec.Text);
983: Process.Start(text);
## FCode\urListDeclearField.cs (2951 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.IO;
9: using System.Runtime.CompilerServices;
10: using System.Text;
11: using System.Text.RegularExpressions;
12: using System.Windows.Forms;
14: using fcontrol;
15: using fsystem;
17: using Microsoft.VisualBasic;
18: using Microsoft.VisualBasic.CompilerServices;
24: public class urListDeclearField : UserControl
505: button.Click += value2;
585: public urListDeclearField()
597: protected override void Dispose(bool disposing)
895: public void Init(string id, string title, DeclareStyle style)
1015: public void CellValueChangedEvent(object sender, DataGridViewCellEventArgs e)
1654: dataSet.ReadXml(Application.StartupPath + <obfuscated-string>);
1687: frmFilterTemplate frmFilterTemplate2 = new frmFilterTemplate();
1714: dataSet.ReadXml(Application.StartupPath + <obfuscated-string>);
2001: public void PrintTemplate(object sender, EventArgs e)
2175: public void txtTable_Leave(object sender, EventArgs e)
2468: public string GenView()
2497: public string GenGrid(bool forceImport = false, bool forceImportTemplate = false, bool showImportErrorCode = false)
2869: public bool Check()
## FCode\urLookup.cs (2348 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.Runtime.CompilerServices;
9: using System.Text.RegularExpressions;
10: using System.Threading;
11: using System.Windows.Forms;
13: using fcontrol;
14: using fsystem;
16: using Microsoft.VisualBasic;
17: using Microsoft.VisualBasic.CompilerServices;
19: using ScintillaNET;
24: public class urLookup : zurInheritsControl
761: protected override void Dispose(bool disposing)
1161: public urLookup()
1179: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
1237: public int FindAll(int posStart = -1)
1281: public void HighlightWord(string strSelected)
1329: lblFindText.Click += iq6pYH2y1sE;
1487: public override void OnRefresh()
1514: public override string OnAddScript()
## FCode\urMainCommand.cs (2372 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.IO;
9: using System.Runtime.CompilerServices;
10: using System.Windows.Forms;
11: using System.Xml;
13: using fcontrol;
14: using fsystem;
16: using Microsoft.VisualBasic;
17: using Microsoft.VisualBasic.CompilerServices;
18: using Microsoft.Win32;
24: public class urMainCommand : UserControl
694: public urMainCommand()
705: protected override void Dispose(bool disposing)
929: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
943: public void Init()
983: public void Clear()
988: public bool LoadCommand(urMainControl _parent, bool force_yn = false)
1181: public void CloseEditForm()
1313: urViewTableData urViewTableData2 = new urViewTableData();
1705: Process.Start(processStartInfo);
1895: dataSet.ReadXml(Application.StartupPath + <obfuscated-string>);
1970: dataSet.WriteXml(Application.StartupPath + <obfuscated-string>);
1984: RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(text, writable: true);
1987: Registry.CurrentUser.CreateSubKey(text);
1988: registryKey = Registry.CurrentUser.OpenSubKey(text, writable: true);
2018: dataSet.ReadXml(Application.StartupPath + <obfuscated-string>);
2030: RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(text2, writable: true);
2033: registryKey = Registry.CurrentUser.CreateSubKey(text2, writable: true);
2041: dataSet.WriteXml(Application.StartupPath + <obfuscated-string>);
2064: dataSet.ReadXml(Application.StartupPath + <obfuscated-string>);
2069: RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(text2, writable: true);
2072: Registry.CurrentUser.CreateSubKey(text2);
2073: registryKey = Registry.CurrentUser.OpenSubKey(text2, writable: true);
2098: process.StartInfo.WorkingDirectory = text3;
2100: process.StartInfo.FileName = fileName;
2101: process.Start();
2147: frmAddSource frmAddSource2 = new frmAddSource();
2181: process.StartInfo.FileName = strVal;
2185: process.StartInfo.Arguments = arguments;
2186: process.Start();
2218: result = File.ReadAllText(P_0);
2333: fDetailCommand = new urMainCommand_Menu();
## FCode\urMainCommand_Menu.cs (1092 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Data.SqlClient;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class urMainCommand_Menu : UserControl
495: public urMainCommand_Menu()
504: protected override void Dispose(bool disposing)
719: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
819: public void RefreshData(string cAction)
## FCode\urMainControl.cs (600 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.Runtime.CompilerServices;
9: using System.Threading.Tasks;
10: using System.Windows.Forms;
12: using fcontrol;
13: using fsystem;
15: using Microsoft.VisualBasic.CompilerServices;
20: public class urMainControl : UserControl
174: public urMainControl()
190: protected override void Dispose(bool disposing)
267: public void RefreshObject()
273: public void SetConnection(SqlConnection _conn, bool force_yn = false)
441: public void Init()
482: public void RefreshControl()
498: uLookup = new urMainLookup();
546: uWCommand = new urMainWCommand();
566: uDVCommand = new urMainDvCommand();
586: uCommand = new urMainCommand();
## FCode\urMainDvCommand.cs (774 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.Runtime.CompilerServices;
9: using System.Text;
10: using System.Windows.Forms;
12: using fcontrol;
13: using fsystem;
15: using Microsoft.VisualBasic;
16: using Microsoft.VisualBasic.CompilerServices;
22: public class urMainDvCommand : UserControl
162: public urMainDvCommand()
172: protected override void Dispose(bool disposing)
267: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
281: public void Init()
341: public void Clear()
346: public bool LoadDvCommand(urMainControl _parent, bool force_yn = false)
597: public void CloseEditForm()
664: frmScript frmScript2 = new frmScript();
722: frmAddWFile frmAddWFile2 = new frmAddWFile();
735: fDetailDvCommand = new urMainDVCommand_Menu();
## FCode\urMainDVCommand_Menu.cs (1263 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Data.SqlClient;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Runtime.CompilerServices;
9: using System.Text.RegularExpressions;
10: using System.Windows.Forms;
12: using fcontrol;
13: using fsystem;
15: using Microsoft.VisualBasic;
16: using Microsoft.VisualBasic.CompilerServices;
21: public class urMainDVCommand_Menu : UserControl
539: public urMainDVCommand_Menu()
549: protected override void Dispose(bool disposing)
785: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
905: public void RefreshData(string cAction)
936: public string getDefaultSrcPath()
974: string input = File.ReadAllText(path);
999: public void ItemClickedEvent(object sender, ToolStripItemClickedEventArgs e)
## FCode\urMainLookup.cs (829 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Data.SqlClient;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Text.RegularExpressions;
9: using System.Windows.Forms;
11: using fcontrol;
12: using fsystem;
14: using Microsoft.VisualBasic;
15: using Microsoft.VisualBasic.CompilerServices;
21: public class urMainLookup : UserControl
127: public urMainLookup()
142: protected override void Dispose(bool disposing)
217: public void Init()
254: label2.Click += klM2dlMygO;
281: public void Clear()
286: public DataView LoadObject(urMainControl _parent, DataView tblTmp = null)
## FCode\urMainWCommand.cs (1017 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.Runtime.CompilerServices;
9: using System.Text;
10: using System.Windows.Forms;
12: using fcontrol;
13: using fsystem;
15: using Microsoft.VisualBasic;
16: using Microsoft.VisualBasic.CompilerServices;
22: public class urMainWCommand : UserControl
162: public urMainWCommand()
172: protected override void Dispose(bool disposing)
267: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
287: public bool SearchSysCode(string voucherID)
297: public bool SearchWMenu_id(string wmenu_id)
307: public void Init()
395: public void Clear()
400: public bool LoadWCommand(urMainControl _parent, bool force_yn = false)
590: public void trvCommand_MouseDoubleClick(object sender, MouseEventArgs e)
685: public void CloseEditForm()
693: public void EditCommand()
753: Process.Start(<obfuscated-string>, <obfuscated-string> + text);
757: Process.Start(text);
803: Process.Start(<obfuscated-string>, <obfuscated-string> + text4);
807: Process.Start(text4);
832: frmMenu_Duplicate frmMenu_Duplicate2 = new frmMenu_Duplicate();
884: frmScript frmScript2 = new frmScript();
892: urViewTableData urViewTableData2 = new urViewTableData();
961: frmAddWFile frmAddWFile2 = new frmAddWFile();
978: fDetailWCommand = new urMainWCommand_Menu();
## FCode\urMainWCommand_Menu.cs (1875 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Data.SqlClient;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Runtime.CompilerServices;
9: using System.Text.RegularExpressions;
10: using System.Windows.Forms;
11: using System.Xml;
13: using fcontrol;
14: using fsystem;
16: using Microsoft.VisualBasic;
17: using Microsoft.VisualBasic.CompilerServices;
23: public class urMainWCommand_Menu : UserControl
655: label.Click += value2;
681: label.Click += value2;
707: label.Click += value2;
714: public urMainWCommand_Menu()
724: protected override void Dispose(bool disposing)
1033: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
1105: label.Click += tBE9NsTCB2;
1153: public void RefreshData(string cAction)
1199: public string getDefaultSrcPath()
1244: string input = File.ReadAllText(path);
1280: string input2 = File.ReadAllText(path);
1393: public void ItemClickedEvent(object sender, ToolStripItemClickedEventArgs e)
1767: public object ChangeDatabase(urTables uTables)
1849: Process.Start(text2);
## FCode\urMinifier.cs (781 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Diagnostics;
4: using System.Drawing;
5: using System.IO;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
9: using fcontrol;
10: using fsystem;
12: using Microsoft.Ajax.Utilities;
13: using Microsoft.VisualBasic.CompilerServices;
18: public class urMinifier : zurInheritsControl
199: label.Click += value2;
225: label.Click += value2;
320: public urMinifier()
330: protected override void Dispose(bool disposing)
514: txtContent.Text = File.ReadAllText(Conversions.ToString(P_1.Node.Tag));
712: string text2 = File.ReadAllText(Conversions.ToString(P_0.Tag));
## FCode\urNote.cs (579 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Diagnostics;
4: using System.Drawing;
5: using System.IO;
6: using System.Runtime.CompilerServices;
7: using System.Text;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
16: using ScintillaNET;
21: public class urNote : zurInheritsControl
182: public urNote()
195: protected override void Dispose(bool disposing)
288: internal virtual Timer mHwpQ7epkUh()
296: internal virtual void qEGpQjgU5L9(Timer WithEventsValue)
301: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
316: public void Save(bool mess = true)
350: public override void OnControlRemove()
412: txtNote.Text = File.ReadAllText(wMupP31GQlv, Encoding.Unicode) + <obfuscated-string>;
475: public override void OnRefresh()
488: txtNote.Text = File.ReadAllText(vdqpPL9K81X) + <obfuscated-string>;
496: public override string OnAddScript()
531: txtNote.Text += File.ReadAllText(array[i]);
## FCode\urNoteX.cs (6882 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Specialized;
4: using System.ComponentModel;
5: using System.Data;
6: using System.Data.SqlClient;
7: using System.Diagnostics;
8: using System.Drawing;
9: using System.Drawing.Drawing2D;
10: using System.IO;
11: using System.Linq;
12: using System.Runtime.CompilerServices;
13: using System.Text;
14: using System.Text.RegularExpressions;
15: using System.Threading;
16: using System.Windows.Forms;
18: using fcontrol;
19: using fsystem;
21: using Microsoft.VisualBasic;
22: using Microsoft.VisualBasic.CompilerServices;
24: using ScintillaNET;
30: public class urNoteX : zurInheritsControl
1679: buttonP.Click += value2;
1881: label.Click += value2;
1993: buttonP.Click += value2;
2184: label.Click += value2;
2189: public urNoteX()
2211: protected override void Dispose(bool disposing)
3177: internal virtual ContextMenuStripP Yn3pT9PE05T()
3185: internal virtual void PnKpTmAAGD1(ContextMenuStripP WithEventsValue)
3190: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
3266: lblErrorMessage.Click += H5twKOv04a;
3351: FifFpO2NVp = Sec.Decrypt(FifFpO2NVp, <obfuscated-string>);
3665: r26FAEauyy.ReadXml(cf6FCgO3Pg);
4199: public void Save(bool mess = true)
4223: dataSet.WriteXml(cf6FCgO3Pg, XmlWriteMode.WriteSchema);
4232: dataSet.WriteXml(w5tFPIRupV, XmlWriteMode.WriteSchema);
4276: Process.Start(P_1.LinkText);
5513: string input = File.ReadAllText(text12);
5634: public void ReLoadFile(bool isThreading = true, string strKey = "")
5733: string text4 = File.ReadAllText(Conversions.ToString(item2[<obfuscated-string>]));
5751: text4 = File.ReadAllText(path);
5763: string text4 = File.ReadAllText(Conversions.ToString(array7[0][<obfuscated-string>]));
5879: string text3 = File.ReadAllText(text2);
5890: Process.Start(text2);
6410: public void GetSource()
6426: public void GetSourceMobile()
6524: dataSet.ReadXml(openFileDialog.FileName);
6826: Process.Start(trvFile.Control.SelectedNode.Name);
6846: Process.Start(trvFile.Control.SelectedNode.Name);
6853: Process.Start(<obfuscated-string>, _path);
## FCode\urPRofiler.cs (492 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Data.SqlClient;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Text.RegularExpressions;
9: using System.Threading;
10: using System.Windows.Forms;
12: using fcontrol;
13: using fsystem;
15: using Microsoft.VisualBasic;
16: using Microsoft.VisualBasic.CompilerServices;
22: public class urPRofiler : zurInheritsControl
26: public pnNOPaint()
76: public urPRofiler()
88: protected override void Dispose(bool disposing)
199: pProfiler = Process.Start(fileName, text6);
292: label2.Click += q1PuigKHph;
303: label3.Click += IsLurEDKDV;
462: public static void ToggleTitleBar(long hwnd, bool showTitle)
## FCode\urQuery.cs (6865 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Generic;
4: using System.ComponentModel;
5: using System.Data;
6: using System.Data.SqlClient;
7: using System.Diagnostics;
8: using System.Drawing;
9: using System.Globalization;
10: using System.IO;
11: using System.Linq;
12: using System.Net;
13: using System.Runtime.CompilerServices;
14: using System.Text;
15: using System.Text.RegularExpressions;
16: using System.Windows.Forms;
17: using System.Xml;
18: using System.Xml.XPath;
20: using fcontrol;
21: using fsystem;
23: using Microsoft.SqlServer.TransactSql.ScriptDom;
24: using Microsoft.VisualBasic;
25: using Microsoft.VisualBasic.CompilerServices;
27: using ScintillaNET;
32: public class urQuery : zurInheritsControl
42: public CellBackground(int r, int c, Color clor)
138: public FilterKeyArgs()
1113: splitter.DoubleClick += value2;
1460: label.Click += value2;
1967: public urQuery()
2000: protected override void Dispose(bool disposing)
2567: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
2638: public override string OnAddNote()
2647: public override void OnControlRemove()
2721: protected override void OnParentChanged(EventArgs e)
2934: public override void OnRefresh()
2962: public override string OnAddScript()
3047: public void FillBackgroundCell()
3152: gridView.Click += gP6pPnggdbv;
3329: label.Click += zuOpor5Xprs;
4000: P_0 = <obfuscated-string> + Sec.Decrypt(array3[0], array3[1]) + <obfuscated-string>;
4054: SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(sqlCommand);
4055: sqlDataAdapter.ReturnProviderSpecificTypes = ReturnProviderSpecificTypesItem.Checked;
4056: sqlDataAdapter.Fill(dsResult, <obfuscated-string> + P_1);
4057: sqlDataAdapter.Dispose();
4429: dataSet.ReadXml(text);
4434: dataSet.ReadXml(stream);
4804: public void ctxMain_ItemClickEvent(object sender, ToolStripItemClickedEventArgs e)
4908: SqlScriptGeneratorOptions sqlScriptGeneratorOptions = new SqlScriptGeneratorOptions();
4909: sqlScriptGeneratorOptions.NewLineBeforeFromClause = true;
4910: sqlScriptGeneratorOptions.NewLineBeforeWhereClause = true;
4911: sqlScriptGeneratorOptions.NewLineBeforeOrderByClause = true;
4912: sqlScriptGeneratorOptions.MultilineSelectElementsList = true;
4913: sqlScriptGeneratorOptions.MultilineSetClauseItems = true;
4914: string text4 = urStringBeauty.SQLBeautyFormat(text3, alert: true, sqlScriptGeneratorOptions);
5131: frmQueryMore frmQueryMore2 = new frmQueryMore();
5215: txtSQL.AppendText(File.ReadAllText(openFileDialog.FileName));
5529: dsResult.WriteXmlSchema(saveFileDialog.FileName);
5613: label2.Click += UrGpoBM2aWb;
5621: koWpOjjn65j.Click += M2Zpoe3pDSg;
5659: frmCheckFieldsBetweenTables frmCheckFieldsBetweenTables2 = new frmCheckFieldsBetweenTables();
6629: label2.Click += zuOpor5Xprs;
6726: wOBpOl8q0Tk = new urFilterWrapper();
6743: public Point GetPositionInForm(Control ctrl)
## FCode\urQueryCompare.cs (2241 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Generic;
4: using System.ComponentModel;
5: using System.Data;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.Drawing.Drawing2D;
9: using System.IO;
10: using System.Linq;
11: using System.Runtime.CompilerServices;
12: using System.Text.RegularExpressions;
13: using System.Windows.Forms;
14: using System.Xml.Linq;
16: using DiffPlex;
17: using DiffPlex.DiffBuilder;
18: using DiffPlex.DiffBuilder.Model;
19: using fcontrol;
20: using fsystem;
22: using Microsoft.VisualBasic;
23: using Microsoft.VisualBasic.CompilerServices;
25: using ScintillaNET;
30: public class urQueryCompare : zurInheritsControl
271: label.Click += value2;
462: buttonP.Click += value2;
542: public urQueryCompare()
559: protected override void Dispose(bool disposing)
855: public void Init(DataView dv)
1005: SideBySideDiffBuilder sideBySideDiffBuilder = new SideBySideDiffBuilder(differ);
1006: SideBySideDiffModel sideBySideDiffModel = sideBySideDiffBuilder.BuildDiffModel(P_0.Trim(), P_1.Trim());
2008: public void OpenSearch(object sender)
2012: fSearch = new urSearch();
2206: public void SelectFullRow(Scintilla txt)
## FCode\urRptFEC.cs (1262 lines)
1: using System;
2: using System.Collections.Generic;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Runtime.CompilerServices;
9: using System.Threading.Tasks;
10: using System.Windows.Forms;
12: using CrystalDecisions.CrystalReports.Engine;
13: using CrystalDecisions.Shared;
14: using fcontrol;
15: using fsystem;
17: using Microsoft.VisualBasic;
18: using Microsoft.VisualBasic.CompilerServices;
19: using OfficeOpenXml;
25: public class urRptFEC : zurInheritsControl
34: public _Closure_0024__121_002D0(_Closure_0024__121_002D0 arg0)
45: internal bool _Lambda_0024__0()
267: buttonP.Click += value2;
572: public urRptFEC()
583: protected override void Dispose(bool disposing)
887: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
915: lblRefresh.Click += opNFglfS66;
916: lblExport.Click += UvBFTlnHiH;
1064: Process.Start(<obfuscated-string>, <obfuscated-string> + text2);
## FCode\urSetupEInvoice.cs (1393 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Net;
7: using System.Net.Http;
8: using System.Runtime.CompilerServices;
9: using System.Text;
10: using System.Threading.Tasks;
11: using System.Windows.Forms;
13: using fcontrol;
14: using fsystem;
15: using fsystem.FastEInvoice.CryptoExt;
17: using Microsoft.VisualBasic;
18: using Microsoft.VisualBasic.CompilerServices;
19: using Newtonsoft.Json;
25: public class urSetupEInvoice : zurInheritsControl
55: public reqClient()
79: public updateKeyResult()
438: buttonP.Click += value2;
689: label.Click += value2;
715: label.Click += value2;
765: public urSetupEInvoice()
776: protected override void Dispose(bool disposing)
1232: frmScript frmScript2 = new frmScript();
## FCode\urSourceRetrieve.cs (2149 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Runtime.CompilerServices;
9: using System.Text.RegularExpressions;
10: using System.Windows.Forms;
11: using System.Xml;
13: using fcontrol;
14: using fsystem;
16: using Microsoft.VisualBasic;
17: using Microsoft.VisualBasic.CompilerServices;
22: public class urSourceRetrieve : zurInheritsControl
315: label.Click += value2;
341: label.Click += value2;
562: buttonP.Click += value2;
588: buttonP.Click += value2;
949: public urSourceRetrieve()
963: protected override void Dispose(bool disposing)
996: ctrStep = new urStepByStep();
1454: uFilter = new urListDeclearField();
1771: string text = File.ReadAllText(path);
1840: uDesign = new urGenerateViewDesign();
1856: uCondition = new urSourceRetrieveCondition();
1872: uLookup = new urSourceRetrieveLookup();
1888: uGrid = new urSourceRetrieveGrid();
1904: uFrom = new urSourceRetrieveForm();
1920: uGen = new urSourceRetrieveGenerate();
2020: public void AnalysisCode(ref DataTable tbl, string path, string category = "")
2127: public DataTable DefaultFilter()
## FCode\urSourceRetrieveCondition.cs (669 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
8: using System.Xml;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class urSourceRetrieveCondition : UserControl
269: public urSourceRetrieveCondition()
279: protected override void Dispose(bool disposing)
483: public void InitGrid()
## FCode\urSourceRetrieveForm.cs (886 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.IO;
7: using System.Runtime.CompilerServices;
8: using System.Text.RegularExpressions;
9: using System.Windows.Forms;
11: using fcontrol;
12: using fsystem;
14: using Microsoft.VisualBasic;
15: using Microsoft.VisualBasic.CompilerServices;
20: public class urSourceRetrieveForm : UserControl
100: label.Click += value2;
231: label.Click += value2;
236: public urSourceRetrieveForm()
247: protected override void Dispose(bool disposing)
420: public void InitGrid()
439: string input = File.ReadAllText(fileInfo.FullName);
587: string text = File.ReadAllText(Application.StartupPath + <obfuscated-string>);
738: string text = File.ReadAllText(path);
## FCode\urSourceRetrieveGenerate.cs (310 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Diagnostics;
4: using System.Drawing;
5: using System.IO;
6: using System.Runtime.CompilerServices;
7: using System.Text.RegularExpressions;
8: using System.Windows.Forms;
10: using fsystem;
12: using Microsoft.VisualBasic.CompilerServices;
17: public class urSourceRetrieveGenerate : UserControl
147: public urSourceRetrieveGenerate()
156: protected override void Dispose(bool disposing)
245: public void GenerateRetrieve()
300: string input = File.ReadAllText(text);
## FCode\urSourceRetrieveGrid.cs (698 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.IO;
7: using System.Runtime.CompilerServices;
8: using System.Windows.Forms;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class urSourceRetrieveGrid : UserControl
109: label.Click += value2;
144: public urSourceRetrieveGrid()
154: protected override void Dispose(bool disposing)
264: public void InitGrid()
456: public bool Check()
510: public string Create()
512: string text = File.ReadAllText(Application.StartupPath + <obfuscated-string>);
## FCode\urSourceRetrieveLookup.cs (545 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Runtime.CompilerServices;
7: using System.Windows.Forms;
8: using System.Xml;
10: using fcontrol;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
19: public class urSourceRetrieveLookup : UserControl
153: label.Click += value2;
158: public urSourceRetrieveLookup()
168: protected override void Dispose(bool disposing)
284: public void InitGrid()
## FCode\urStringBeauty.cs (1252 lines)
1: using System;
2: using System.Collections.Generic;
3: using System.ComponentModel;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.IO;
7: using System.Linq;
8: using System.Runtime.CompilerServices;
9: using System.Text;
10: using System.Windows.Forms;
11: using System.Xml;
12: using System.Xml.Linq;
14: using fcontrol;
15: using fsystem;
16: using fsystem.JSBeautyLib;
18: using Microsoft.SqlServer.TransactSql.ScriptDom;
19: using Microsoft.VisualBasic.CompilerServices;
20: using Newtonsoft.Json.Linq;
22: using ScintillaNET;
27: public class urStringBeauty : zurInheritsControl
339: public urStringBeauty()
348: protected override void Dispose(bool disposing)
487: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
682: public void OpenSearch(string searchText = "")
686: fSearch = new urSearch();
1046: public void SelectFullRow(Scintilla txt)
1063: public void JsonBeauty()
1083: public void XmlBeauty()
1124: public static string SQLBeautyFormat(string query, bool alert = true, SqlScriptGeneratorOptions dfOption = null)
1129: TSql150Parser tSql150Parser = new TSql150Parser(initialQuotedIdentifiers: false);
1130: TSqlFragment scriptFragment = tSql150Parser.Parse(new StringReader(query), out var errors);
1149: dfOption = new SqlScriptGeneratorOptions
1192: public void SqlBeauty()
1219: txtPlainText.Text = File.ReadAllText(array[0]);
## FCode\urTables.cs (5693 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Generic;
4: using System.ComponentModel;
5: using System.Data;
6: using System.Data.SqlClient;
7: using System.Diagnostics;
8: using System.Drawing;
9: using System.Globalization;
10: using System.IO;
11: using System.Linq;
12: using System.Runtime.CompilerServices;
13: using System.Runtime.InteropServices;
14: using System.Text;
15: using System.Threading;
16: using System.Threading.Tasks;
17: using System.Windows.Forms;
19: using fcontrol;
20: using fsystem;
22: using Microsoft.VisualBasic;
23: using Microsoft.VisualBasic.CompilerServices;
29: public class urTables : zurInheritsControl
38: public _Closure_0024__261_002D0()
45: internal void _Lambda_0024__0()
51: internal void _Lambda_0024__1()
57: internal void _Lambda_0024__2()
74: public _Closure_0024__316_002D0(_Closure_0024__316_002D0 arg0)
87: internal bool _Lambda_0024__0()
102: public _Closure_0024__316_002D1(_Closure_0024__316_002D1 arg0)
571: buttonP.Click += value2;
1451: public urTables()
1470: protected override void Dispose(bool disposing)
1953: internal virtual ColumnHeader DGNpzbE58Kf()
1961: internal virtual void xH0pzy1sAHW(ColumnHeader WithEventsValue)
1966: public override string OnAddNote()
1975: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
2346: lblTen_ct.Click += PhXpOLeDBYu;
2347: lblSource.Click += mWtpO366IWd;
2359: tlsRenderGrid.Click += dqdpOFXoj7L;
2360: tlsRenderDir.Click += dqdpOFXoj7L;
2361: tlsRenderGridFilter.Click += dqdpOFXoj7L;
2511: sqlCommand.ExecuteNonQuery();
2586: Process.Start(text);
3034: WARpvdBVNOg.ReadXml(XOZpvEc1XdY);
3058: frmDescriptionColumns frmDescriptionColumns2 = new frmDescriptionColumns();
3123: frmTableCheckStruct frmTableCheckStruct2 = new frmTableCheckStruct();
3155: public override void OnControlRemove()
4229: public override void OnRefresh()
4242: public override string OnAddScript()
4699: frmTableEditColumn frmTableEditColumn2 = new frmTableEditColumn();
4768: frmTableEditColumn frmTableEditColumn3 = new frmTableEditColumn();
4847: frmScript frmScript2 = new frmScript();
5320: SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(JWLpvF04l71);
5321: sqlDataAdapter.Fill(ooRpvS5qHec);
5322: sqlDataAdapter.Dispose();
5543: public void LoadColumns()
5639: public override void OnRefreshForm()
## FCode\urUpdate.cs (6832 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.IO;
9: using System.Linq;
10: using System.Runtime.CompilerServices;
11: using System.Text;
12: using System.Text.RegularExpressions;
13: using System.Threading;
14: using System.Windows.Forms;
16: using fcontrol;
17: using fsystem;
19: using Microsoft.VisualBasic;
20: using Microsoft.VisualBasic.CompilerServices;
22: using ScintillaNET;
27: public class urUpdate : zurInheritsControl
799: buttonP.Click += value2;
855: buttonP.Click += value2;
881: buttonP.Click += value2;
952: buttonP.Click += value2;
1124: buttonP.Click += value2;
1150: buttonP.Click += value2;
1386: buttonP.Click += value2;
1813: label.Click += value2;
1904: public urUpdate()
1927: protected override void Dispose(bool disposing)
2871: lblNote.Click += J0ypVXygMaB;
3087: public void GetSource()
3103: public void GetSourceMobile()
3347: txtNote4Update.Text = File.ReadAllText(path);
4090: frmUpdateTopBottomScript frmUpdateTopBottomScript2 = new frmUpdateTopBottomScript();
4829: string input = File.ReadAllText(text17);
5023: dataSet3.WriteXml(text5);
5118: dataSet2.WriteXml(text4);
5294: dataSet.ReadXml(P_0);
5812: frmScript frmScript2 = new frmScript();
5841: string text3 = File.ReadAllText(text2);
5852: Process.Start(text2);
5903: public override void OnRefresh()
6016: public override string OnAddScript()
6343: public void ReLoadFile(bool isThreading = true, string strKey = "")
6468: string text4 = File.ReadAllText(Conversions.ToString(item2[<obfuscated-string>]));
6486: string text4 = File.ReadAllText(Conversions.ToString(array6[0][<obfuscated-string>]));
## FCode\urViewTableData.cs (1230 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Data.SqlClient;
6: using System.Diagnostics;
7: using System.Drawing;
8: using System.Runtime.CompilerServices;
9: using System.Windows.Forms;
11: using fcontrol;
12: using fsystem;
14: using Microsoft.VisualBasic;
15: using Microsoft.VisualBasic.CompilerServices;
20: public class urViewTableData : zurInheritsControl
332: label.Click += value2;
423: public urViewTableData()
433: protected override void Dispose(bool disposing)
467: urLeft = new urLeftSide();
709: label.Click += aAi9RKOxXe;
967: public DataView LoadColumns(string tableName, SqlConnection conn)
## FCode\urWCommand.cs (2632 lines)
1: using System;
2: using System.Collections;
3: using System.ComponentModel;
4: using System.Data;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.IO;
8: using System.Runtime.CompilerServices;
9: using System.Text.RegularExpressions;
10: using System.Threading;
11: using System.Windows.Forms;
13: using fcontrol;
14: using fsystem;
16: using Microsoft.VisualBasic;
17: using Microsoft.VisualBasic.CompilerServices;
23: public class urWCommand : zurInheritsControl
889: public urWCommand()
905: protected override void Dispose(bool disposing)
1241: public override string OnAddNote()
1251: protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
1282: public override void OnRefresh()
1288: public override string OnAddScript()
1320: ctxOpenWithIE.Click += dwnp3a9YijJ;
1321: ctxLoginWithIE.Click += usZp3nK9PgT;
1449: label.Click += ReJp3zdHckJ;
1697: string input = File.ReadAllText(path);
1724: public string getDefaultSrcPath()
1796: public void ItemClickedEvent(object sender, ToolStripItemClickedEventArgs e)
2231: Process.Start(HCQpHiX2ELL);
2312: Process.Start(<obfuscated-string>, FRCpHxI7PWN);
2349: Process.Start(<obfuscated-string>, <obfuscated-string> + text);
2353: Process.Start(text);
2382: Process.Start(<obfuscated-string>, text);
2445: frmAddWFile frmAddWFile2 = new frmAddWFile();
## FCode\zurInheritsControl.cs (498 lines)
1: using System;
2: using System.ComponentModel;
3: using System.Data;
4: using System.Data.SqlClient;
5: using System.Diagnostics;
6: using System.Drawing;
7: using System.Runtime.CompilerServices;
8: using System.Threading;
9: using System.Windows.Forms;
11: using fsystem;
13: using Microsoft.VisualBasic;
14: using Microsoft.VisualBasic.CompilerServices;
20: public class zurInheritsControl : UserControl
373: protected override void Dispose(bool disposing)
401: public zurInheritsControl()
419: public void CloseConnection()
436: protected override void OnLoad(EventArgs e)
446: protected override void OnParentChanged(EventArgs e)
472: public virtual void OnControlRemove()
476: public virtual void OnRefresh()
480: public virtual string OnAddScript()
485: public virtual void OnRefreshForm()
489: public virtual void ShowHelp()
493: public virtual string OnAddNote()
## FCode.My\MySettings.cs (30 lines)
1: using System.CodeDom.Compiler;
2: using System.ComponentModel;
3: using System.Configuration;
4: using System.Runtime.CompilerServices;
12: internal sealed class MySettings : ApplicationSettingsBase
24: public MySettings()
## FCode.My\MySettingsProperty.cs (18 lines)
1: using System.ComponentModel.Design;
2: using System.Diagnostics;
3: using System.Runtime.CompilerServices;
4: using Microsoft.VisualBasic;
5: using Microsoft.VisualBasic.CompilerServices;
13: internal sealed class MySettingsProperty
## FCode.My.Resources\Resources.cs (51 lines)
1: using System.CodeDom.Compiler;
2: using System.ComponentModel;
3: using System.Diagnostics;
4: using System.Globalization;
5: using System.Resources;
6: using System.Runtime.CompilerServices;
8: using Microsoft.VisualBasic;
9: using Microsoft.VisualBasic.CompilerServices;
18: internal sealed class Resources
## JlMVHopDdkBZUX4G8Nnn\rCBXNfpDyOI9shWiA00d.cs (3049 lines)
1: using System;
2: using System.Collections;
3: using System.Collections.Generic;
4: using System.Diagnostics;
5: using System.IO;
6: using System.Reflection;
7: using System.Reflection.Emit;
8: using System.Runtime.CompilerServices;
9: using System.Runtime.InteropServices;
10: using System.Security.Cryptography;
11: using System.Text;
16: internal class rCBXNfpDyOI9shWiA00d
24: public mjmESfpKAVGtJTpkjxNn()
31: public zRDfdopK5tQJaCqD3luG(object P_0)
38: internal static string KwepK7lSNYe(string P_0, string P_1)
77: public kUvnYXpKoZ0WiJIy3Utd(Stream P_0)
83: internal Stream nW4lBacjpc()
88: internal byte[] tJTpKlsbMML(int P_0)
93: internal int eNxpK8pIjIo(byte[] P_0, int P_1, int P_2)
98: internal int CV6pKOBNQVv()
103: internal void RAEpKfa9N8F()
278: internal static byte[] LBjpDEelbKi(byte[] P_0)
416: internal static bool m1kpDGKHE9Y()
426: internal rCBXNfpDyOI9shWiA00d()
530: internal static SymmetricAlgorithm ltlpDnOAZkm()
554: internal static void UySpD97D6mc()
574: internal static byte[] XrDpDmq8q0X(byte[] P_0)
583: internal static void WohpDsnfPHO(HashAlgorithm P_0, Stream P_1, uint P_2, byte[] P_3)
594: internal static void B6PpDtp9792(HashAlgorithm P_0, byte[] P_1, int P_2, int P_3)
599: internal static uint TljpDRi16fW(uint P_0, int P_1, long P_2, BinaryReader P_3)
616: public static void KjwpDgGsgPG(RuntimeTypeHandle P_0)
782: internal static void PYEpDi5Sh8P()
2654: internal static string gnxpDSNRMCT(int P_0)
2695: internal static string T9fpDxDPkW1(string P_0)
2727: internal static object YeypMkfmUIr(object P_0)
2849: internal static Stream xtHpMcqBiwb()
2854: internal static byte[] IO0pMPLfwbm(Stream P_0)
2921: internal byte[] a4vpM04i9YF()
2928: internal byte[] w9EpMLr0Oba()
2935: internal byte[] gTopM3pHsqy()
2942: internal byte[] dTVpMHVY9Gn()
2949: internal static object m6MV3t5phhRsfXtBlaTP(object P_0)
2954: internal static void MNIEGV5p6H33pR2kYseX(object P_0, long P_1)
2959: internal static long f2q4715p1CPqlglPT9Qk(object P_0)
2964: internal static object NDfkSV5peQ57lQTSa50y(object P_0, int P_1)
2969: internal static void wWcd7Z5pBeybj3B44WaI(object P_0)
2974: internal static void d6KA7v5pbcYkODjPP79R(object P_0)
2979: internal static object jL6klr5pywMjDwEbfdlJ(object P_0)
2984: internal static object ETO4p85pdnlgHd0jDEl6(object P_0)
2989: internal static object DcOxrT5pEpaTV3gXTaZr()
2994: internal static void KXmcti5pZUOnaOdLBg1e(object P_0, CipherMode P_1)
2999: internal static object hg6Cfp5p4UC540XG1FFN(object P_0, object P_1, object P_2)
3004: internal static object a6BgnK5pWm6i69xxSKcn()
3009: internal static void CyjCxo5pUI6DWTYTHrW6(object P_0, object P_1, int P_2, int P_3)
3014: internal static void qW0W8j5p2MB8qwgxH86H(object P_0)
3019: internal static object ltgtXt5pGk93f8qA9gcw(object P_0)
3024: internal static void CERNw75paXvwxoA0eWLV(object P_0)
3029: internal static object SLIHtQ5pnWniHQ0XX5LG(object P_0)
3034: internal static bool MFjjVA5p91SXYY8hmbrG(object P_0, object P_1)
3039: internal static bool bfovvV5pK5dV3cdaK3cy()
3044: internal static object MTtDbp5pJCuGHSGpZ4xj()
## L2iWoI8hpBl5GWiqp4\mQVhwOlnP3WecXFawe.cs (18 lines)
1: using System.CodeDom.Compiler;
2: using System.ComponentModel;
4: using Microsoft.VisualBasic.ApplicationServices;
10: internal class mQVhwOlnP3WecXFawe : ConsoleApplicationBase
12: public mQVhwOlnP3WecXFawe()
## OVGykjfLYGfCVNwSTTa\mR6aHYf0Y9ldHD1I9ah.cs (345 lines)
1: using System;
2: using System.Collections;
3: using System.Data;
4: using System.Diagnostics;
5: using System.Drawing;
6: using System.Globalization;
7: using System.IO;
8: using System.Reflection;
9: using System.Runtime.CompilerServices;
10: using System.Threading;
11: using System.Windows.Forms;
13: using FCode;
14: using fcontrol;
15: using fsystem;
17: using Microsoft.VisualBasic.CompilerServices;
18: using Microsoft.Win32;
23: internal sealed class mR6aHYf0Y9ldHD1I9ah
86: public static void Main(string[] args)
129: Process.Start(args[0]);
140: RegistryKey registryKey = Registry.ClassesRoot.OpenSubKey(<obfuscated-string>, writable: false);
151: sF2fKtO5em = new frmMain();
184: public static object JwufH8N5kU(string P_0, params object[] arr)
225: public static void DicfIAPlG6(ToolStrip P_0, ToolStripItemDisplayStyle P_1)
248: public static void iuNfqFfMTB(FileInfo P_0)
287: Process.Start(text2);
296: string text = Sec.Decrypt(File.ReadAllText(path), Cmd.GetClientCode());
## Properties\AssemblyInfo.cs (18 lines)
1: using System.Diagnostics;
2: using System.Reflection;
3: using System.Runtime.CompilerServices;
4: using System.Runtime.InteropServices;
5: using System.Runtime.Versioning;
## sd42ZNVGGvhGoL1dHd\LweuRwvGBqKnEtb6BD.cs (993 lines)
1: using System;
2: using System.CodeDom.Compiler;
3: using System.Collections;
4: using System.ComponentModel;
5: using System.ComponentModel.Design;
6: using System.Diagnostics;
7: using System.Reflection;
8: using System.Runtime.CompilerServices;
9: using System.Runtime.InteropServices;
10: using System.Windows.Forms;
12: using FCode;
15: using Microsoft.VisualBasic;
16: using Microsoft.VisualBasic.ApplicationServices;
17: using Microsoft.VisualBasic.CompilerServices;
25: internal sealed class LweuRwvGBqKnEtb6BD
807: public cscu94pIvPFITQbdgaeY()
814: public override bool Equals(object P_0)
820: public override int GetHashCode()
826: internal Type FSupIIroYFW()
832: public override string ToString()
844: public override bool Equals(object P_0)
851: public override int GetHashCode()
858: internal Type G4Tpqg9haaI()
865: public override string ToString()
888: public A3Y4SZpqREQdvMSrmu90()
897: internal sealed class wHOGNJpqSnopBiDc2Bex<sL1nZSpqxKBMLECEVJr7> where sL1nZSpqxKBMLECEVJr7 : new()
905: internal sL1nZSpqxKBMLECEVJr7 T1WpqQKv42D()
916: public wHOGNJpqSnopBiDc2Bex()
## T3HYZUfM2bsJe8Uoon\mEKnkxOaQBi1TTlYtS.cs (21 lines)
1: using System.CodeDom.Compiler;
2: using System.ComponentModel;
3: using System.Diagnostics;
5: using Microsoft.VisualBasic.Devices;
11: internal class mEKnkxOaQBi1TTlYtS : Computer
15: public mEKnkxOaQBi1TTlYtS()
## uJ2fx9pDeW6hC2DME9kk\pO07jppD1SbAKV0vdSiK.cs (36 lines)
1: using System;
2: using System.Reflection;
7: internal class pO07jppD1SbAKV0vdSiK
13: internal static void mHg5pmlco9n(int typemdt)
24: public pO07jppD1SbAKV0vdSiK()
