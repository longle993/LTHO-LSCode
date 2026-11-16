// UI regression checks use fake native transport; SQL integration is tested by LSCode.WebView.Tests.
import {createRequire} from 'node:module';import path from 'node:path';const require=createRequire(path.resolve(process.argv[2]??'artifacts/ui-test-tools','package.json'));const {chromium}=require('playwright');const browser=await chromium.launch({channel:'chrome'});const page=await browser.newPage({viewport:{width:1440,height:1000}});page.setDefaultTimeout(15000);const errors=[];page.on('pageerror',e=>errors.push(e.message));page.on('dialog',d=>d.accept());
await page.addInitScript(()=>{
  const listeners=[];let serial=0,clipboard='';
  window.__nativeCalls=[];
  window.__traceBatch=[];
  const doc=(id,text='',name='fixture.xml',filePath='C:\\Fixture\\fixture.xml')=>({id,path:filePath,name,text,encoding:'utf-8',bom:false,newline:'CRLF',version:1});
  const projectRoot='C:\\Fixture';
  const otherProject={id:'other',name:'Dự án lịch sử',code:'NGANANH',title:'Dự án lịch sử',active:true,programPath:'C:\\Other',server:'.',database:'Other_App',sourcePath:'C:\\Other'};
  const pastFile='C:\\Other\\Controller\\Main\\past.xml';
  const history=new Map([['other',[pastFile]]]);
  const controllerFiles=name=>[
    projectRoot+'\\Main\\'+name+'.aspx',
    ...['Dir','Filter','Grid','Report','Templates'].flatMap(folder=>['f','xml'].map(ext=>projectRoot+'\\App_Data\\Controllers\\'+folder+'\\'+name+'.'+ext)),
    projectRoot+'\\App_Data\\Controllers\\Templates\\Upload\\'+name+'.xml',
  ];
  let workspaceFiles=[projectRoot+'\\fixture.xml',projectRoot+'\\long.sql',...controllerFiles('nhanvien'),...controllerFiles('bophan')];
  const settings={theme:'Light',workspace:'C:\\Fixture',server:'.',database:'Fixture_App',integratedSecurity:true,sqlUser:'',trustCertificate:true,crystalPath:'',profiler18Path:'',shortcuts:{},activityCommands:null,projectLastAccess:{other:'2026-11-12T03:00:00Z'}};
  const native={addEventListener:(_,fn)=>listeners.push(fn),postMessage:request=>{window.__nativeCalls.push(request);let result,error;
    switch(request.method){
      case 'studio.bootstrap':result={settings,projects:[{id:'fixture',name:'Dự án thử UI',code:'BACHVIET / FBISP24',title:'Dự án thử UI',active:true,programPath:'C:\\Fixture',server:'.',database:'Fixture_App',sourcePath:'C:\\Fixture'},otherProject],snippets:[{key:'select',category:'SQL',body:'SELECT * FROM {{table}};',description:'Mẫu SELECT',position:0,param:''}],version:'ui-test'};break;
      case 'project.open':result=request.payload.id==='other'?{project:otherProject,targets:[]}:
        {project:{id:'fixture',name:'Dự án thử UI',code:'BACHVIET / FBISP24',title:'Dự án thử UI',active:true,programPath:'C:\\Fixture',server:'.',database:'Fixture_App',sourcePath:'C:\\Fixture'},targets:[{id:'project-app',server:'.',database:'Fixture_App'},{id:'project-sys',server:'.',database:'Fixture_Sys'}]};break;
      case 'project.history':result=history.get(request.payload.id)??[];break;
      case 'project.history.list':result=[...history].filter(([,files])=>files.length).map(([projectId,files])=>({projectId,files}));break;
      case 'project.history.remove':{
        const {id,path}=request.payload;
        history.set(id,(history.get(id)??[]).filter(file=>file!==path));result=true;break;
      }
      case 'project.history.record':{
        const {id,path}=request.payload;
        history.set(id,[...new Set([path,...(history.get(id)??[])])]);result=true;break;
      }
      case 'project.refreshConfig':result=projectRoot+'\\web.config';break;
      case 'project.structure.preview':result={path:projectRoot+'\\Controller\\Structure\\App',count:2};break;
      case 'project.structure.clear':result=2;break;
      case 'project.menu.details':case 'project.menu.new':result={mode:request.method.endsWith('new')?'new':'edit',fields:['wmenu_id','wmenu_id0','bar','bar2','menu_id','link','sysid','status'].map(name=>({name,type:'varchar',maxLength:128,nullable:true,readOnly:false})),values:{wmenu_id:'03.10.01',bar:'Nhân viên',menu_id:request.method.endsWith('new')?'AA.00.00':'02.01.01'},version:'1',canSave:true};break;
case 'project.menu.resources':result={controllers:['Employee'],tables:['employee'],resources:[]};break;
case 'sql.debug.available':result={available:true};break;case 'sql.debug.open':result={path:'C:/Fixture/debug.sql'};break;case 'project.commands':result=[{entry:{id:'menu1',caption:'Nhân viên',sysId:'HR',commandId:'Dir',link:'nhanvien.aspx'},children:[]},{entry:{id:'menu2',caption:'Bộ phận',sysId:'HR',commandId:'Dept',link:'bophan.aspx'},children:[]},{entry:{id:'menu3',caption:'Thông tin nhân viên',sysId:'HR',commandId:'Info',link:'hrEmployeeInformation.aspx'},children:[]},{entry:{id:'separator',caption:'-',sysId:'',commandId:'',link:''},children:[]}];break;
      case 'project.employeeTabs':result=[{sysId:'hrPIGeneralInformation',caption:'Thông tin chung'},{sysId:'hrPIPassportVisa',caption:'Hộ chiếu, thị thực'}];break;
      case 'project.controller':{
        const link=request.payload.link;
        const files=controllerFiles(link==='hrEmployeeInformation.aspx'?'hrPIGeneralInformation':link.replace(/\.aspx$/,''));
        if(link==='hrEmployeeInformation.aspx') files[0]=projectRoot+'\\Main\\hrEmployeeInformation.aspx';
        result={file:files[0],files};break;
      }
      case 'project.details':result={programPath:'C:\\Fixture\\program.exe',updatePath:'C:\\Fixture\\update',metadata:'<project><FolderName>Dự án thử UI</FolderName></project>'};break;
      case 'window.new':result=true;break;
      case 'recovery.load':result=[];break;
      case 'workspace.list':{
        const prefix=request.payload.path+'\\', entries=new Map();
        for (const path of workspaceFiles.filter(file=>file.startsWith(prefix))) {
          const [name,...children]=path.slice(prefix.length).split('\\');
          entries.set(name,{name,path:prefix+name,directory:children.length>0});
        }
        result=[...entries.values()].sort((a,b)=>Number(b.directory)-Number(a.directory)||a.name.localeCompare(b.name));break;
      }
      case 'workspace.files':result=['C:\\Fixture\\fixture.xml','C:\\Fixture\\long.sql'];break;
      case 'workspace.search':result=[];break;
      case 'workspace.reveal':result=true;break;
      case 'document.openMany':result=window.__bulkOpenCount
        ? Array.from({length:window.__bulkOpenCount},(_,index)=>doc('open-'+(++serial),'<root>bulk-'+index+'</root>','bulk-'+index+'.xml',projectRoot+'\\bulk-'+index+'.xml'))
        : [doc('open-'+(++serial),'<root>Tiếng Việt</root>')];break;
      case 'document.open':result=doc('open-'+(++serial),request.payload.path?.endsWith('nhanvien.xml')?'<root>\r\n<fields/>\r\n<toolbar/>\r\n<command><text><![CDATA[SELECT 42 AS x]]></text></command>\r\n<query>SELECT 43 AS y</query>\r\n<script><![CDATA[const z = 1;]]></script>\r\n</root>':'<root>Tiếng Việt</root>',request.payload.path?.split(/[\\/]/).at(-1)??'fixture.xml',request.payload.path??'C:\\Fixture\\fixture.xml');break;
      case 'workspace.duplicate':result=request.payload.path.replace(/(\.[^.]+)$/, '_copy$1');workspaceFiles.push(result);break;
      case 'workspace.rename':{
        const {path,name}=request.payload;
        result=path.slice(0,path.lastIndexOf('\\')+1)+name;
        workspaceFiles=workspaceFiles.map(file=>file===path||file.startsWith(path+'\\')?result+file.slice(path.length):file);
        break;
      }
      case 'document.save':if(window.__cancelSave){result=null;break;}result={...doc(request.payload.id,request.payload.text,request.payload.name??'Untitled.sql',request.payload.path??'C:\\Fixture\\saved.sql'),version:request.payload.version+1};break;
      case 'settings.read':result=settings;break;
      case 'document.changed':result=false;break;
      case 'clipboard.read':result=clipboard;break;
      case 'clipboard.write':clipboard=request.payload.text;result=true;break;
      case 'document.close':case 'recovery.save':case 'settings.save':case 'operation.cancel':case 'profiler.stop':result=true;break;
      case 'sql.connect':result={id:'target-'+(++serial),server:request.payload.server,database:request.payload.database};break;
      case 'sql.execute':if(request.payload.sql.includes('RAISE_FIXTURE')){error={message:'Watch SQL error',code:'sql'};break;}result={tables:[{columns:['Mã','Tên','Giá trị'],rows:Array.from({length:5000},(_,i)=>[String(i),'Tiếng Việt '+i,i===0?null:'1000000000.123456789'])},{columns:['Bảng phụ'],rows:[['Dòng bảng hai']]}],messages:['Query fixture'],durationMs:12,truncated:true};break;
      case 'sql.complete':result=request.payload.text===''?['COUNT','AS','IS NOT NULL','AND','BETWEEN','DESC','ASC'].map(text=>({text,isSnippet:false})):[];break;
      case 'javascript.complete':result=[];break;
      case 'xml.validate':case 'xml.viewWidth':result=null;break;
      case 'xml.diagnostics':result={diagnostic:null,warnings:request.payload.text.includes('SELECT 42 AS x')?
        [{line:2,column:2,message:"Field 'missing' đã khai báo nhưng chưa có trong <views>."}]:[]};break;
      case 'xml.controllerPreview':result=window.__previewDir ? {
        kind:'dir', title:'Khai báo nhân viên', subtitle:'Thông tin nhân viên', toolbar:['Lưu'],
        views:[{name:'Dir', columns:[], layout:{widths:[120,40,60,320],rows:[
          [{name:'nam',label:'Năm',isLabel:true,column:1,span:1},{name:'nam',label:'Năm',isLabel:false,column:2,span:2}],
          [{name:'ma_nv',label:'Mã nhân viên',isLabel:false,column:4,span:1}]
        ],categories:[{label:'Chi tiết',widths:[540],rows:[
          [{name:'method',label:'Cách tính',description:'1 - Theo thời gian',column:1,span:1}],
          [{name:'salary',label:'Bảng lương',column:1,span:1,detail:{controller:'SalaryDetail',columns:[
            {name:'rate',label:'Mức lương',width:90}
          ]}}]
        ]},{label:'Phụ cấp',widths:[540],rows:[
          [{name:'allowance',label:'Mức phụ cấp',column:1,span:1}]
        ]}]}}]
      } : {kind:'grid',title:'Cập nhật thông tin lương nhân viên',subtitle:'Cập nhật thông tin lương, tài khoản ngân hàng…',
        toolbar:['Sửa','Tìm','Xem'],views:[{name:'Grid',columns:[{name:'ma_nv',label:'Mã nhân viên',width:110},
          {name:'ten',label:'Họ và tên',width:160},{name:'ma_bp',label:'Bộ phận',width:240}]}]};break;
      case 'schema.objects':result={columns:['Schema','Name','Type','Id'],rows:[['dbo','DanhMuc','USER_TABLE','1']]};break;
      case 'table.names':result={columns:['Schema','Name'],rows:[['dbo','DanhMuc']]};break;
      case 'table.catalog':result={columns:['Schema','Name'],rows:[['dbo','DanhMuc_Archive'],['dbo','DanhMuc'],['hr','NhanVien']]};break;
      case 'table.structure':result={columns:['Cột','Kiểu dữ liệu','Khóa'],rows:[['code','INT','PK · PK_DanhMuc'],...(!window.__columnDropped?[['label','NVARCHAR(20)','']]:[])]};break;
      case 'table.addScript':result='ALTER TABLE [dbo].[DanhMuc] ADD [label] NVARCHAR(20) NULL;';break;
      case 'table.alterScript':result='ALTER TABLE [dbo].[DanhMuc] ALTER COLUMN [label] NVARCHAR(20) NULL;';break;
      case 'table.dropColumns':window.__columnDropped=true;result=true;break;
      case 'table.createScript':result='CREATE TABLE [dbo].[DanhMuc] ([code] INT);';break;
      case 'table.view':result={data:{columns:['code','label'],rows:[['1','x'],['2','y']]},snapshotId:'fixture-preview'};break;
      case 'table.insertScript':result='INSERT INTO [dbo].[DanhMuc] ([code]) VALUES (1);';break;
      case 'profiler.start':result=true;break;
      case 'profiler.read':result={events:window.__traceBatch.splice(0),dropped:0,truncated:false};break;
      case 'project.discover':result=[{name:'Project Fixture',folder:'C:\\Fixture\\Application',configPath:'C:\\Fixture\\Application\\web.config'}];break;
      case 'project.sync':result=true;break;
      case 'project.list':result=[{id:'fixture',name:'Dự án thử UI',code:'BACHVIET / FBISP24',title:'Dự án thử UI',active:true,programPath:'C:\\Fixture',server:'.',database:'Fixture_App',sourcePath:'C:\\Fixture'},otherProject];break;
      case 'catalog.references':result={columns:['Schema','Table','Column','Type'],rows:[]};break;
      case 'catalog.load':result={signature:'fixture-schema',columns:[{name:'code',type:'nvarchar',nullable:false,identity:false,computed:false,generated:0,managed:false,hasDefault:false},{name:'label',type:'nvarchar',nullable:true,identity:false,computed:false,generated:0,managed:false,hasDefault:false},...['user_id0','user_id2','datetime0','datetime2'].map(name=>({name,type:name.startsWith('user')?'int':'datetime',nullable:true,identity:false,computed:false,generated:0,managed:true,hasDefault:false}))],data:{columns:['code','label','user_id0','user_id2','datetime0','datetime2'],rows:[['one',null,null,null,null,null],[':null','literal',null,null,null,null]]}};break;
      case 'catalog.clipboard.write':result=request.payload.rows.map(r=>r.map(v=>'"'+(v??'').replaceAll('"','""')+'"').join('\t')).join('\r\n');break;
      default:error={code:'fixture',message:'Fixture không hỗ trợ '+request.method};
    }
    const reply=()=>listeners.forEach(fn=>fn({data:{protocolVersion:1,requestId:request.requestId,result,error}}));
    if(request.method==='sql.execute'&&window.__holdWatch){window.__heldWatch=request;return;}
    if(request.method==='operation.cancel'&&window.__heldWatch){const held=window.__heldWatch;window.__heldWatch=null;listeners.forEach(fn=>fn({data:{requestId:held.requestId,error:{code:'cancelled',message:'cancelled'}}}));}
    if (request.method==='table.catalog'&&window.__holdTableCatalog) window.__releaseTableCatalog=reply;
    else setTimeout(reply,10);
  }};
  window.chrome=window.chrome??{};window.chrome.webview=native;
});

let checks=0;const check=(ok,name)=>{if(!ok)throw Error(name);console.log('PASS '+name);checks++;};
try {
await page.goto('http://127.0.0.1:4173');await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
check(await page.locator('.edition,.connection-state').count()===0,'Header removes WebView and Desktop labels');
await page.getByRole('button',{name:'Chức năng',exact:true}).click();check(await page.getByRole('button',{name:'Tải chức năng',exact:true}).count()===0&&await page.getByRole('button',{name:'Đồng bộ',exact:true}).count()===0,'ERP removes duplicate actions');
await page.getByRole('button',{name:'SQL Debug / Watch',exact:true}).click();const panel=page.locator('#sql-watch-panel');
await panel.getByLabel('SQL ngữ cảnh',{exact:true}).fill('DECLARE @id int = 7;');await panel.getByLabel('Nội dung Watch').fill('@id + 1');
await panel.getByRole('button',{name:'Chạy Watch',exact:true}).click();await panel.locator('.data-grid').waitFor();
const call=await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='sql.execute').at(-1));check(call.payload.targetId==='project-app'&&call.payload.sql.includes('DECLARE @id int = 7;\n;\nSELECT (@id + 1) AS [Watch];'),'Watch executes context and scalar together on selected database');
await panel.getByRole('button',{name:'Kết quả 1 · 5000 dòng',exact:true}).click();check(await panel.locator('.data-grid tbody tr').count()<100,'Watch uses virtualized result grid');
await panel.getByRole('button',{name:'Thêm Watch',exact:true}).click();await panel.getByRole('button',{name:'Loại Watch',exact:true}).click();await page.getByRole('option',{name:'Query xem dữ liệu',exact:true}).click();await panel.getByLabel('Nội dung Watch').fill('SELECT * FROM @items');
await panel.getByRole('button',{name:'Chạy Watch',exact:true}).click();await panel.locator('.data-grid').waitFor();
check((await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='sql.execute').at(-1))).payload.sql.endsWith('SELECT * FROM @items'),'Watch executes full data query');
await panel.getByRole('button',{name:'Debug bằng SSDT',exact:true}).click();await panel.getByRole('status').filter({hasText:'Đã mở SSDT'}).waitFor();check((await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='sql.debug.open').at(-1))).payload.sql==='DECLARE @id int = 7;','SSDT receives editor script and selected target');
await panel.getByLabel('Nội dung Watch').fill('RAISE_FIXTURE');await panel.getByRole('button',{name:'Chạy Watch',exact:true}).click();await panel.getByRole('alert').waitFor();check((await panel.getByRole('alert').innerText()).includes('Watch SQL error'),'Watch shows SQL failures');
await panel.getByRole('tab',{name:'Watch 1',exact:true}).click();check(await panel.getByLabel('Nội dung Watch').inputValue()==='@id + 1','Watch preserves each expression');
await page.evaluate(()=>window.__holdWatch=true);await panel.getByRole('button',{name:'Chạy Watch',exact:true}).click();await panel.getByRole('button',{name:'Dừng Watch',exact:true}).waitFor();await page.getByRole('button',{name:'Đóng SQL Watch',exact:true}).click();check(await panel.isVisible(),'Running Watch cannot be closed accidentally');await panel.getByRole('button',{name:'Dừng Watch',exact:true}).click();await panel.getByRole('alert').filter({hasText:'Đã dừng Watch'}).waitFor();check(true,'Watch cancellation clears running state');await page.evaluate(()=>window.__holdWatch=false);
await panel.getByRole('button',{name:'Chạy Watch',exact:true}).click();await panel.locator('.data-grid').waitFor();await page.screenshot({path:'artifacts/workbench-watch.png'});
await page.getByRole('button',{name:'Đổi theme',exact:true}).click();await page.waitForTimeout(200);await page.screenshot({path:'artifacts/workbench-watch-dark.png'});await page.getByRole('button',{name:'Đổi theme',exact:true}).click();await page.getByRole('button',{name:'Đóng SQL Watch',exact:true}).click();
await page.evaluate(()=>window.__bulkOpenCount=2);await page.keyboard.press('Control+o');await page.getByRole('tab',{name:/bulk-0.xml/}).waitFor();
await page.getByRole('tab',{name:/bulk-0.xml/}).click();await page.locator('.editor-group.focused .monaco-editor').click();await page.keyboard.press('Control+End');await page.keyboard.type('draft');await page.waitForTimeout(200);
await page.keyboard.press('Control+n');await page.getByRole('button',{name:'Lịch sử',exact:true}).click();const summary=page.locator('summary').filter({hasText:'Dự án thử UI'});await summary.waitFor();
await summary.click({button:'right'});await page.getByRole('menuitem',{name:'Close project',exact:true}).click();const prompt=page.getByRole('dialog',{name:'Lưu thay đổi trước khi đóng?',exact:true});await prompt.waitFor();await prompt.getByRole('button',{name:'Hủy',exact:true}).click();check(await page.getByRole('tab',{name:/bulk-0.xml/}).count()===1,'Close project cancel keeps dirty file open');
await summary.click({button:'right'});await page.getByRole('menuitem',{name:'Close project',exact:true}).click();await prompt.getByRole('button',{name:'Đóng không lưu',exact:true}).click();await page.getByRole('tab',{name:/bulk-0.xml/}).waitFor({state:'detached'});check(await page.getByRole('tab',{name:/bulk-1.xml/}).count()===0&&await page.locator('.document-tabs [role=tab]').count()===1,'Close project closes group and preserves unrelated scratch tab');
await page.keyboard.press('Control+o');await page.getByRole('tab',{name:/bulk-0.xml/}).waitFor();await summary.click({button:'right'});await page.getByRole('menuitem',{name:'Close all projects',exact:true}).click();await page.getByRole('tab',{name:/bulk-0.xml/}).waitFor({state:'detached'});check(await page.locator('.document-tabs [role=tab]').count()===1,'Close all projects closes project files');
await page.keyboard.press('Control+o');await page.getByRole('tab',{name:/bulk-0.xml/}).click();await page.locator('.editor-group.focused .monaco-editor').click();await page.keyboard.press('Control+End');await page.keyboard.type('saved draft');await page.waitForTimeout(100);await summary.click({button:'right'});await page.getByRole('menuitem',{name:'Close project',exact:true}).click();await page.evaluate(()=>window.__cancelSave=true);await prompt.getByRole('button',{name:'Lưu thay đổi',exact:true}).click();await page.waitForTimeout(100);check(await page.getByRole('tab',{name:/bulk-0.xml/}).count()===1,'Cancelled save leaves group open');await page.evaluate(()=>window.__cancelSave=false);await summary.click({button:'right'});await page.getByRole('menuitem',{name:'Close project',exact:true}).click();await prompt.getByRole('button',{name:'Lưu thay đổi',exact:true}).click();await page.getByRole('tab',{name:/bulk-0.xml/}).waitFor({state:'detached'});check(await page.evaluate(()=>window.__nativeCalls.some(c=>c.method==='document.save'&&c.payload.text.includes('saved draft'))),'Close project saves edited files before closing');
check(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='project.history.remove').length)===0,'Bulk close preserves history');
await summary.click({button:'right'});await page.screenshot({path:'artifacts/workbench-history.png'});await page.keyboard.press('Escape');
if(errors.length)throw Error(errors.join('\n'));console.log(checks+' workbench UI checks passed');
}catch(e){await page.screenshot({path:'artifacts/workbench-failure.png'});console.log(await page.locator('body').innerText());throw e;}finally{await browser.close();}
