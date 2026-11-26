// .NET Framework x86 bridge for the SSMS 18 SQL Trace reader. Credentials use stdin only.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
class Program
{
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
    static readonly object OutputLock = new object();
    static object reader;
    static volatile bool stopping;
    static long sequence;
    static string DescribeError(Exception error, string stage, string password)
    {
        var message = new StringBuilder("SQL Trace — " + stage);
        for (var current = error; current != null; current = current.InnerException)
        {
            if (current is TargetInvocationException && current.InnerException != null) continue;
            message.Append("\n").Append(current.GetType().Name).Append(" [0x").Append(current.HResult.ToString("X8")).Append("]: ").Append(current.Message);
            var sql = current as SqlException;
            if (sql != null) foreach (SqlError detail in sql.Errors) message.Append("\nSQL ").Append(detail.Number).Append(" / state ").Append(detail.State).Append(": ").Append(detail.Message);
        }
        var result = message.ToString();
        return string.IsNullOrEmpty(password) ? result : result.Replace(password, "[redacted]");
    }
    static void Emit(object value) { lock(OutputLock) { Console.WriteLine(Json.Serialize(value)); Console.Out.Flush(); } }
    static object Call(string method) { return reader.GetType().GetMethod(method, Type.EmptyTypes).Invoke(reader, null); }
    static void Set(object obj, string name, object value) { obj.GetType().GetProperty(name).SetValue(obj, value, null); }
    static string Text(Dictionary<string,object> row, string key) { object v; return row.TryGetValue(key,out v) && v != null && v != DBNull.Value ? Convert.ToString(v) : ""; }
    static double Number(Dictionary<string,object> row,string key) { double n; return double.TryParse(Text(row,key),out n) ? n : 0; }
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern bool SetDllDirectory(string path);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr process, uint exitCode);
    [STAThread]
    static int Main(string[] commandArgs)
    {
        Console.InputEncoding = new UTF8Encoding(false); Console.OutputEncoding = new UTF8Encoding(false);
        bool initialized = false; int code = 0; string stage = "đọc cấu hình", password = ""; Thread stopThread = null; Exception stopError = null;
        try
        {
            var input = Json.Deserialize<Dictionary<string,string>>((Console.ReadLine() ?? "").TrimStart('\uFEFF'));
            password = input["Password"];
            var builder = new SqlConnectionStringBuilder {
                DataSource=input["Server"], IntegratedSecurity=bool.Parse(input["IntegratedSecurity"]),
                UserID=input["User"], Password=input["Password"],
                TrustServerCertificate=bool.Parse(input["TrustServerCertificate"]), Encrypt=bool.Parse(input["Encrypt"])
            };
            if (commandArgs.Length == 1 && commandArgs[0] == "--validate-request") {
                Emit(new { Kind="validated", Server=builder.DataSource, User=builder.UserID, TrustServerCertificate=builder.TrustServerCertificate, Encrypt=builder.Encrypt, IntegratedSecurity=builder.IntegratedSecurity, PasswordLength=builder.Password.Length }); return 0;
            }
            if (commandArgs.Length == 1 && commandArgs[0] == "--validate-error") {
                Emit(new { Kind="validated-error", Message=DescribeError(new TargetInvocationException(new InvalidOperationException("Failed to initialize object as reader", new System.Runtime.InteropServices.COMException("native cause " + password, unchecked((int)0x80004005)))), "khởi tạo reader", password) }); return 0;
            }
            stage = "kết nối server " + builder.DataSource;
            builder.InitialCatalog="master"; builder.ConnectTimeout=10; builder.ApplicationName="LSCode.Profiler";
            using (var connection = new SqlConnection(builder.ConnectionString)) {
                connection.Open();
                stage = "kiểm tra ALTER TRACE trên " + builder.DataSource;
                using (var permission = connection.CreateCommand()) {
                    permission.CommandText="SELECT HAS_PERMS_BY_NAME(NULL,NULL,'ALTER TRACE')";
                    permission.CommandTimeout=0;
                    if (Convert.ToString(permission.ExecuteScalar()) != "1")
                        throw new InvalidOperationException("Tài khoản kết nối được nhưng thiếu quyền ALTER TRACE trên server " + builder.DataSource + ". Chọn tài khoản Profiler có quyền này trong Kết nối… hoặc nhờ DBA cấp quyền.");
                }
            }
            string folder = input["LibraryFolder"]; stage = "nạp thư viện SSMS: " + folder;
            SetDllDirectory(Path.GetDirectoryName(folder)); Environment.CurrentDirectory=Path.GetDirectoryName(folder); Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(folder),"PFCLNT.DLL"));
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args) { string file = Path.Combine(folder,new AssemblyName(args.Name).Name+".dll"); return File.Exists(file) ? Assembly.LoadFrom(file) : null; };
            var common = Assembly.LoadFrom(Path.Combine(folder,"Microsoft.SqlServer.ConnectionInfo.dll"));
            var trace = Assembly.LoadFrom(Path.Combine(folder,"Microsoft.SqlServer.ConnectionInfoExtended.dll"));
            var info = builder.IntegratedSecurity
                ? Activator.CreateInstance(common.GetType("Microsoft.SqlServer.Management.Common.SqlConnectionInfo"),new object[]{builder.DataSource})
                : Activator.CreateInstance(common.GetType("Microsoft.SqlServer.Management.Common.SqlConnectionInfo"),new object[]{builder.DataSource,builder.UserID,builder.Password});
            Set(info,"ApplicationName","LSCode.Profiler"); Set(info,"ConnectionTimeout",10); Set(info,"TrustServerCertificate",builder.TrustServerCertificate); Set(info,"EncryptConnection",builder.Encrypt);
            reader = Activator.CreateInstance(trace.GetType("Microsoft.SqlServer.Management.Trace.TraceServer"));
            stage = "khởi tạo reader trên " + builder.DataSource + " — template " + input["TemplatePath"];
            reader.GetType().GetMethod("InitializeAsReader").Invoke(reader,new object[] {info,input["TemplatePath"]}); initialized = true;
            stage = "đọc sự kiện trace";
            Emit(new { Kind="ready" });
            stopThread = new Thread(delegate() { Console.ReadLine(); stopping=true; try { Call("Stop"); } catch(Exception ex) { stopError=ex; } }); stopThread.IsBackground = true; stopThread.Start();
            while (!stopping && (bool)Call("Read"))
            {
                var row = new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
                int count = (int)reader.GetType().GetProperty("FieldCount").GetValue(reader,null);
                for (int i=0;i<count;i++) { string name=(string)reader.GetType().GetMethod("GetName").Invoke(reader,new object[]{i}); row[name]=reader.GetType().GetMethod("GetValue").Invoke(reader,new object[]{i}); }
                if (Text(row,"ApplicationName")=="LSCode.Profiler") continue;
                string eventClass = Text(row,"EventClass");
                bool included =
                    eventClass == "RPC:Completed" || eventClass == "RPCCompleted" || eventClass == "10" ||
                    eventClass == "RPC:Starting" || eventClass == "RPCStarting" || eventClass == "11" ||
                    eventClass == "SQL:BatchCompleted" || eventClass == "BatchCompleted" || eventClass == "SQLBatchCompleted" || eventClass == "12" ||
                    eventClass == "SQL:BatchStarting" || eventClass == "BatchStarting" || eventClass == "SQLBatchStarting" || eventClass == "13" ||
                    eventClass == "User Error Message" || eventClass == "UserErrorMessage" || eventClass == "162";
                if (!included) continue;
                string database=Text(row,"DatabaseName");
                if (input["Database"].Length>0 && !row.ContainsKey("DatabaseName"))
                    throw new InvalidOperationException("Template không thu cột DatabaseName nên không thể lọc theo DB. Chọn template SQL2014 của LS Code hoặc thêm DatabaseName trong SQL Profiler.");
                // Match the shared project prefix to include both Sys and App databases.
                if (input["Database"].Length>0 && !LSCode.Core.ProfilerDatabaseFilter.Matches(database, input["Database"])) continue;
                DateTime time; if (!DateTime.TryParse(Text(row,"StartTime"),out time)) time=DateTime.Now;
                Emit(new { Kind="event", Sequence=++sequence, Time=time.ToString("o"), Event=Text(row,"EventClass"), Database=database, Login=Text(row,"LoginName"), Host=Text(row,"HostName"), Application=Text(row,"ApplicationName"), Session=Text(row,"SPID"), DurationMs=Number(row,"Duration")/1000d, Reads=(long)Number(row,"Reads"), Writes=(long)Number(row,"Writes"), CpuMs=Number(row,"CPU"), Severity=(int)Number(row,"Severity"), Text=Text(row,"TextData") });
            }
        }
        catch(Exception ex) { if (!stopping) { Emit(new {Kind="error",Message=DescribeError(ex,stage,password)}); code=1; } }
        finally
        {
            if (reader!=null && initialized) { try {
                if (stopping && stopThread!=null) { if (!stopThread.Join(10000)) throw new TimeoutException("Stop chưa hoàn tất."); if(stopError!=null) throw stopError; }
                else Call("Stop");
                Call("Close"); ((IDisposable)reader).Dispose(); Emit(new { Kind="stopped" });
            } catch(Exception ex) { Emit(new {Kind="error",Message="SQL Trace cleanup failed: "+ex.GetBaseException().Message}); code=2; } }
        }
        // PFCLNT's mixed-mode shutdown crashes/hangs after successful disposal on SSMS 18.
        // Only terminate this isolated helper after Stop/Close/Dispose and flushed acknowledgement.
        if (initialized && code == 0) TerminateProcess(GetCurrentProcess(), 0);
        return code;
    }
}
