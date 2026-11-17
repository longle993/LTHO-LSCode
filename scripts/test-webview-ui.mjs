// Browser interaction tests use an explicit fake native transport. Real .NET/SQL is tested separately.
import {createRequire} from 'node:module';
import {mkdirSync,writeFileSync} from 'node:fs';
import path from 'node:path';
const require=createRequire(path.resolve(process.argv[2]??'.','package.json'));
const {chromium}=require('playwright');
let browser;try{browser=await chromium.launch();}catch{browser=await chromium.launch({channel:'chrome'});}
const page=await browser.newPage({viewport:{width:1440,height:1000}});
const errors=[];page.on('pageerror',e=>errors.push(e.stack??e.message));
page.on('dialog',dialog=>dialog.accept());
await page.addInitScript(()=>{
  const listeners=[];let serial=0,clipboard='';
  window.__nativeCalls=[];
  window.__traceBatch=[];
  const doc=(id,text='',name='fixture.xml',filePath='C:\\Fixture\\fixture.xml')=>({id,path:filePath,name,text,encoding:'utf-8',bom:false,newline:'CRLF',version:1});
  const projectRoot='C:\\Fixture';
  const otherProject={id:'other',name:'Dự án lịch sử',server:'.',database:'Other_App',sourcePath:'C:\\Other'};
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
      case 'studio.bootstrap':result={settings,projects:[{id:'fixture',name:'Dự án thử UI',server:'.',database:'Fixture_App',sourcePath:'C:\\Fixture'},otherProject],snippets:[{key:'select',category:'SQL',body:'SELECT * FROM {{table}};',description:'Mẫu SELECT',position:0,param:''}],version:'ui-test'};break;
      case 'project.open':result=request.payload.id==='other'?{project:otherProject,targets:[]}:
        {project:{id:'fixture',name:'Dự án thử UI',server:'.',database:'Fixture_App',sourcePath:'C:\\Fixture'},targets:[{id:'project-app',server:'.',database:'Fixture_App'},{id:'project-sys',server:'.',database:'Fixture_Sys'}]};break;
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
      case 'project.commands':result=[{entry:{id:'menu1',caption:'Nhân viên',sysId:'HR',commandId:'Dir',link:'nhanvien.aspx'},children:[]},{entry:{id:'menu2',caption:'Bộ phận',sysId:'HR',commandId:'Dept',link:'bophan.aspx'},children:[]},{entry:{id:'menu3',caption:'Thông tin nhân viên',sysId:'HR',commandId:'Info',link:'hrEmployeeInformation.aspx'},children:[]},{entry:{id:'separator',caption:'-',sysId:'',commandId:'',link:''},children:[]}];break;
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
      case 'document.save':result={...doc(request.payload.id,request.payload.text,request.payload.name??'Untitled.sql',request.payload.path??'C:\\Fixture\\saved.sql'),version:request.payload.version+1};break;
      case 'settings.read':result=settings;break;
      case 'document.changed':result=false;break;
      case 'clipboard.read':result=clipboard;break;
      case 'clipboard.write':clipboard=request.payload.text;result=true;break;
      case 'document.close':case 'recovery.save':case 'settings.save':case 'operation.cancel':case 'profiler.stop':result=true;break;
      case 'sql.connect':result={id:'target-'+(++serial),server:request.payload.server,database:request.payload.database};break;
      case 'sql.execute':result={tables:[{columns:['Mã','Tên','Giá trị'],rows:Array.from({length:5000},(_,i)=>[String(i),'Tiếng Việt '+i,i===0?null:'1000000000.123456789'])},{columns:['Bảng phụ'],rows:[['Dòng bảng hai']]}],messages:['Query fixture'],durationMs:12,truncated:true};break;
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
      case 'project.list':result=[{id:'fixture',name:'Dự án thử UI',server:'.',database:'Fixture_App',sourcePath:'C:\\Fixture'},otherProject];break;
      case 'catalog.references':result={columns:['Schema','Table','Column','Type'],rows:[]};break;
      case 'catalog.load':result={signature:'fixture-schema',columns:[{name:'code',type:'nvarchar',nullable:false,identity:false,computed:false,generated:0,managed:false,hasDefault:false},{name:'label',type:'nvarchar',nullable:true,identity:false,computed:false,generated:0,managed:false,hasDefault:false},...['user_id0','user_id2','datetime0','datetime2'].map(name=>({name,type:name.startsWith('user')?'int':'datetime',nullable:true,identity:false,computed:false,generated:0,managed:true,hasDefault:false}))],data:{columns:['code','label','user_id0','user_id2','datetime0','datetime2'],rows:[['one',null,null,null,null,null],[':null','literal',null,null,null,null]]}};break;
      case 'catalog.clipboard.write':result=request.payload.rows.map(r=>r.map(v=>'"'+(v??'').replaceAll('"','""')+'"').join('\t')).join('\r\n');break;
      default:error={code:'fixture',message:'Fixture không hỗ trợ '+request.method};
    }
    const reply=()=>listeners.forEach(fn=>fn({data:{protocolVersion:1,requestId:request.requestId,result,error}}));
    if (request.method==='table.catalog'&&window.__holdTableCatalog) window.__releaseTableCatalog=reply;
    else setTimeout(reply,10);
  }};
  window.chrome=window.chrome??{};window.chrome.webview=native;
});
const out='artifacts/webview-ui';mkdirSync(out,{recursive:true});
let checks=0;const check=(value,name)=>{if(!value)throw Error('FAIL '+name);checks++;console.log('PASS '+name);};
try{
  await page.goto('http://127.0.0.1:4173');await page.getByRole('dialog',{name:'Dự án của bạn'}).waitFor();check(true,'Project picker opens before workbench');
  check((await page.locator('.project-row').first().textContent()).includes('Dự án lịch sử'),'Project picker sorts by last access');
  await page.keyboard.press('Control+Shift+n');
  await page.waitForFunction(()=>window.__nativeCalls.filter(call=>call.method==='window.new').length===1);
  await page.getByRole('button',{name:'Xóa profile Dự án thử UI',exact:true}).click();
  await page.getByRole('button',{name:'Xóa profile',exact:true}).waitFor();
  await page.getByRole('button',{name:'Hủy',exact:true}).click();
  await page.getByRole('button',{name:'Đóng',exact:true}).click();await page.locator('[data-ready=true]').waitFor();
  await page.keyboard.press('Control+Shift+n');
  await page.waitForFunction(()=>window.__nativeCalls.filter(call=>call.method==='window.new').length===2);
  check(true,'New-window shortcut reaches the native bridge from both the picker and the workbench');
  await page.getByRole('button',{name:'SQL mới',exact:true}).click();
  await page.getByRole('button',{name:'Kết nối SQL',exact:true}).click();
  await page.getByRole('textbox',{name:'Server',exact:true}).fill('.');
  await page.getByRole('textbox',{name:'Database',exact:true}).fill('Fixture_App');
  await page.getByRole('button',{name:'Kết nối',exact:true}).click();
  await page.locator('dialog').waitFor({state:'detached'});
  check((await page.getByRole('button',{name:'Database của tab',exact:true}).textContent()).includes('Fixture_App'),'Connection assigned to active tab');
  await page.locator('.monaco-editor').first().click({position:{x:170,y:35}});await page.keyboard.insertText("SELECT N'Tiếng Việt';");
  await page.keyboard.press('Control+s');await page.waitForTimeout(100);
  check(await page.evaluate(()=>window.__nativeCalls.some(c=>c.method==='document.save'&&c.payload.text.includes('Tiếng Việt'))),'Ctrl+S sends current Unicode buffer to native boundary');
  await page.getByRole('button',{name:'Chia editor',exact:true}).click();
  await page.waitForFunction(()=>document.querySelectorAll('.code-editor').length===2);
  await page.waitForFunction(()=>Array.from(document.querySelectorAll('.code-editor')).every(e=>e.textContent.replace(/\s+/g,' ').includes('Tiếng Việt')));
  check((await page.locator('.code-editor').allTextContents()).every(t=>t.replace(/\s+/g,' ').includes('Tiếng Việt')),'Split panes show shared text');
  const completionCount=await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='sql.complete').length);
  await page.getByRole('button',{name:'Chia editor',exact:true}).click();await page.waitForFunction(()=>document.querySelectorAll('.code-editor').length===1);await page.locator('.monaco-editor').first().click({position:{x:160,y:35}});await page.keyboard.press('Control+Space');await page.waitForFunction(n=>window.__nativeCalls.filter(c=>c.method==='sql.complete').length>n,completionCount);check(await page.evaluate(()=>Boolean(window.__nativeCalls.filter(c=>c.method==='sql.complete').at(-1)?.payload.targetId)),'Closing split preserves completion target and active editor');await page.getByRole('button',{name:'Chia editor',exact:true}).click();
  await page.getByRole('button',{name:'Chạy F5',exact:true}).click();await page.locator('.data-grid tbody tr').first().waitFor();
  const resultPicker=page.getByRole('group',{name:'Bảng kết quả'});
  await resultPicker.getByRole('button',{name:'Kết quả 2 · 1 dòng',exact:true}).click();
  check((await page.locator('.data-grid').textContent()).includes('Dòng bảng hai'),'Second SQL table opens with one click');
  await resultPicker.getByRole('button',{name:'Kết quả 1 · 5000 dòng',exact:true}).click();
  check(await resultPicker.getByRole('button',{pressed:true}).count()===1,'Result picker marks the selected table');
  check(await page.locator('.data-grid tbody tr').count()<100,'5000 SQL rows render through bounded virtual grid');
  await page.locator('.data-grid').evaluate(el=>{el.scrollTop=100000;});await page.waitForTimeout(100);
  check(await page.locator('.data-grid').evaluate(el=>Math.abs(el.querySelector('thead').getBoundingClientRect().top-el.getBoundingClientRect().top)<3),
    'Grid header stays pinned after scrolling');
  check((await page.locator('.data-grid').textContent()).includes('Tiếng Việt 3'),'Virtual grid updates visible rows on scroll');
  await page.screenshot({path:out+'/desktop-code-light.png'});
  await page.getByRole('button',{name:'Cài đặt',exact:true}).click();
  await page.getByRole('button',{name:'Theme',exact:true}).click();await page.getByRole('option',{name:'Dark',exact:true}).click();
  await page.getByLabel('Màu nhấn',{exact:true}).fill('#ad82ee');
  await page.getByRole('button',{name:'Lưu cài đặt',exact:true}).click();await page.waitForFunction(()=>document.documentElement.dataset.theme==='Dark');
  check(await page.evaluate(()=>document.documentElement.style.getPropertyValue('--primary')==='#ad82ee' &&
    document.documentElement.style.getPropertyValue('--accent')==='#ad82ee' &&
    window.__nativeCalls.some(call=>call.method==='settings.save'&&call.payload.settings.accentColor==='#ad82ee')),
    'Accent color applies and persists in workspace settings');
  check(true,'Theme setting updates workbench and Monaco');await page.screenshot({path:out+'/desktop-code-dark.png'});
  await page.getByRole('button',{name:'Cài đặt',exact:true}).click();
  await page.getByRole('tab',{name:'Phím tắt',exact:true}).click();
  check(await page.getByRole('heading',{name:'File & cửa sổ',exact:true}).isVisible() &&
    await page.getByRole('heading',{name:'SQL & dữ liệu',exact:true}).isVisible(),
    'Shortcut settings use a dedicated tab grouped by function');
  await page.getByRole('searchbox',{name:'Tìm phím tắt',exact:true}).fill('Generate Update');
  check(await page.getByText('Generate Update',{exact:true}).isVisible(),'Shortcut settings can filter commands');
  await page.getByRole('button',{name:'Hủy',exact:true}).click();
  await page.keyboard.press('Control+Shift+p');
  check(await page.getByRole('textbox',{name:'Tìm lệnh hoặc file'}).evaluate(element=>element===document.activeElement),
    'Ctrl+Shift+P focuses command search immediately');
  await page.keyboard.press('Escape');
  await page.keyboard.press('Control+Shift+4');
  await page.getByRole('tabpanel',{name:'Generate Update'}).waitFor();
  check(true,'Ctrl+Shift+4 opens Generate Update when Shift+4 reports a symbol');
  await page.getByRole('button',{name:'Đóng Generate Update',exact:true}).click();
  await page.keyboard.press('Control+n');
  await page.getByRole('button',{name:'Kết nối SQL',exact:true}).click();await page.getByRole('textbox',{name:'Database',exact:true}).fill('Fixture_Sys');await page.getByRole('button',{name:'Kết nối',exact:true}).click();await page.locator('dialog').waitFor({state:'detached'});
  await page.getByRole('tab').first().click();check((await page.getByRole('button',{name:'Database của tab',exact:true}).textContent()).includes('Fixture_App'),'Old tab keeps App target after new connection');
  await page.getByRole('tab').last().click();check((await page.getByRole('button',{name:'Database của tab',exact:true}).textContent()).includes('Fixture_Sys'),'New tab keeps Sys target');await page.getByRole('tab').first().click();
  await page.getByRole('button',{name:'Cài đặt',exact:true}).click();await page.getByRole('button',{name:'Theme',exact:true}).click();await page.getByRole('option',{name:'High Contrast',exact:true}).click();await page.getByRole('button',{name:'Lưu cài đặt',exact:true}).click();await page.waitForFunction(()=>document.documentElement.dataset.theme==='High Contrast');await page.setViewportSize({width:1920,height:1080});await page.screenshot({path:out+'/desktop-code-high-contrast-1920.png'});check(true,'High Contrast is available at 1920px');
  await page.getByRole('button',{name:'Cài đặt',exact:true}).click();await page.getByRole('button',{name:'Theme',exact:true}).click();await page.getByRole('option',{name:'Dark',exact:true}).click();await page.getByRole('button',{name:'Lưu cài đặt',exact:true}).click();await page.setViewportSize({width:1440,height:1000});
  for(const title of ['Snippets','So sánh văn bản / thư mục','XML: Outline / XPath','Dịch văn bản','Beautify SQL','Danh mục']){
    await page.keyboard.press('Control+Shift+p');await page.getByRole('textbox',{name:'Tìm lệnh hoặc file'}).fill(title);await page.getByRole('dialog').getByRole('button',{name:title,exact:true}).click();await page.locator('dialog[open]').waitFor();check(await page.locator('dialog[open]').isVisible(),'Tool opens: '+title);if(title==='Danh mục'){await page.getByRole('textbox',{name:'Tên bảng',exact:true}).fill('catalog_fixture');await page.getByRole('button',{name:'Mở bảng',exact:true}).click();await page.getByRole('button',{name:'Dữ liệu',exact:true}).click();await page.getByRole('button',{name:'Chọn dòng 1',exact:true}).click();await page.getByRole('button',{name:'Sao chép dòng chọn',exact:true}).click();await page.getByRole('button',{name:'Dán nhiều dòng',exact:true}).click();await page.getByRole('textbox',{name:'Dòng 1 label',exact:true}).waitFor();check(await page.getByRole('textbox',{name:'Dòng 1 label',exact:true}).inputValue()===':null','Catalog copy/paste preserves SQL NULL through native clipboard boundary');}await page.getByRole('button',{name:'Đóng',exact:true}).click();
  }

  await page.getByRole('button',{name:'SQL Profiler',exact:true}).click();
  const profiler=page.getByRole('tabpanel',{name:'SQL Profiler'});
  check(await profiler.isVisible() && await page.locator('.editor-toolbar').getByRole('tab',{name:'SQL Profiler'}).isVisible()
    && !(await page.locator('dialog[open]').count()),'Profiler opens on the editor toolbar without a dialog');
  check(await profiler.getByRole('textbox',{name:'Profiler user'}).inputValue()==='profile'&&await profiler.locator('input[type=password]').inputValue()==='fsd','Profiler defaults to dedicated SQL login');
  check(await profiler.getByRole('button',{name:/^(Profiler engine|Lưu trace|Mở trace)$/}).count()===0 &&
    await profiler.getByRole('textbox',{name:'Đường dẫn template'}).count()===0,
    'Profiler hides the engine selector, template field and trace file buttons');
  const executions=await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='sql.execute').length);
  await page.keyboard.press('F5');
  check(await page.evaluate(before=>window.__nativeCalls.filter(c=>c.method==='sql.execute').length===before,executions),'Profiler tab shortcuts do not run the hidden editor query');
  await page.evaluate(()=>window.__traceBatch.push(...Array.from({length:60},(_,i)=>({time:String(i),event:'Event '+i,database:'Fixture_App',login:'profile',durationMs:1,reads:0,text:'SELECT '+i}))));
  await profiler.getByRole('button',{name:'Chạy',exact:true}).click();
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='profiler.start' &&
    c.payload.engine==='ssms18' && !Object.hasOwn(c.payload,'templatePath')));
  await page.waitForFunction(()=>document.querySelector('.trace-sql')?.value==='SELECT 59');
  check(await profiler.locator('.trace-sql').evaluate(element=>element.clientHeight>=110),'Profiler SQL area has room for more lines');
  const traceGrid=profiler.locator('.data-grid');
  check(await traceGrid.evaluate(el=>el.scrollTop>0),'Profiler follows the newest live event');
  await traceGrid.evaluate(el=>el.scrollTop=0);
  const firstTraceCell=traceGrid.locator('[data-grid-row="0"][data-grid-column="1"]');
  const lastTraceCell=traceGrid.locator('[data-grid-row="1"][data-grid-column="2"]');
  await firstTraceCell.waitFor();
  const from=await firstTraceCell.boundingBox(),to=await lastTraceCell.boundingBox();
  await page.mouse.move(from.x+from.width/2,from.y+from.height/2);
  await page.mouse.down();await page.mouse.move(to.x+to.width/2,to.y+to.height/2,{steps:8});await page.mouse.up();
  await page.keyboard.press('Control+c');
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='clipboard.write'&&c.payload.text==='Event 0\tFixture_App\r\nEvent 1\tFixture_App'));
  check(await traceGrid.locator('td.grid-selected').count()===4&&!(await traceGrid.getByRole('button',{name:/Sao chép/}).count()),'Profiler drag copies a rectangular TSV range without copy buttons');
  await page.evaluate(()=>window.__traceBatch.push({time:'60',event:'Event 60',database:'Fixture_App',login:'profile',durationMs:1,reads:0,text:'SELECT 60'}));
  await profiler.getByText(/61 sự kiện/).waitFor();
  check(await profiler.locator('.trace-sql').inputValue()==='SELECT 0'&&await traceGrid.locator('td.grid-selected').count()===4,'Profiler preserves the selected row and range while live events arrive');
  await profiler.getByRole('button',{name:'Sao chép SQL',exact:true}).click();
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='clipboard.write'&&c.payload.text==='SELECT 0'));
  await profiler.locator('.trace-sql').focus();
  await profiler.locator('.trace-sql').evaluate(el=>el.setSelectionRange(0,6));
  await page.keyboard.press('Control+c');
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='clipboard.write'&&c.payload.text==='SELECT'));
  check(true,'Profiler copies full SQL by button and selected text with Ctrl+C');
  await profiler.getByRole('button',{name:'Theo dõi cuối lưới'}).click();
  await page.waitForFunction(()=>document.querySelector('.trace-sql')?.value==='SELECT 60');
  await page.screenshot({path:out+'/profiler-tab-dark.png'});
  await page.locator('.document-tabs').getByRole('tab').first().click();
  check(!(await profiler.isVisible())&&await page.evaluate(()=>!window.__nativeCalls.some(c=>c.method==='profiler.stop')),'Switching editor tabs keeps the Profiler session running');
  await page.getByRole('button',{name:'Đóng SQL Profiler'}).click();
  await page.getByRole('tab',{name:'SQL Profiler'}).waitFor({state:'detached'});
  check(await page.evaluate(()=>window.__nativeCalls.some(c=>c.method==='profiler.stop')),'Closing the Profiler tab stops the session');

  await page.keyboard.press('F5');await page.waitForTimeout(150);await page.locator('.data-grid').first().evaluate(e=>e.scrollTop=0);await page.waitForTimeout(80);
  const cell=page.locator('.data-grid tbody tr').filter({has:page.locator('td[title="Tiếng Việt 0"]')}).locator('td[title="Tiếng Việt 0"]');
  await cell.click();await page.keyboard.press('Control+c');await page.waitForTimeout(50);
  check(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='clipboard.write').at(-1)?.payload.text)==='Tiếng Việt 0','Grid copies individual Unicode cell');
  await cell.locator('..').locator('.row-number').click();await page.keyboard.press('Control+c');
  await page.waitForFunction(()=>window.__nativeCalls.filter(c=>c.method==='clipboard.write').at(-1)?.payload.text.includes('0\tTiếng Việt 0'));
  check(true,'Grid copies full row with Ctrl+C');
  await page.locator('.data-grid th').filter({hasText:'Tên'}).click();await page.keyboard.press('Control+c');
  await page.waitForFunction(()=>window.__nativeCalls.filter(c=>c.method==='clipboard.write').at(-1)?.payload.text.includes('Tiếng Việt 4999'));
  check(true,'Grid copies a virtualized column with Ctrl+C');
  await page.getByRole('button',{name:'Chọn dự án',exact:true}).click();
  await page.getByRole('button',{name:'Đồng bộ dự án',exact:true}).click();
  await page.getByRole('textbox',{name:'Tên dự án',exact:true}).fill('Project Fixture');
  await page.getByRole('button',{name:'Tìm ứng dụng'}).click();
  await page.getByRole('button',{name:/Fixture.*Application/}).click();
  await page.getByRole('button').filter({hasText:'Dự án thử UI'}).waitFor();
  check(await page.evaluate(()=>window.__nativeCalls.some(c=>c.method==='project.sync'&&c.payload.name==='Project Fixture')),'Project picker syncs and returns to the catalog');
  await page.getByRole('button',{name:'Đóng',exact:true}).click();
  const openTabsBeforeProject=await page.locator('.document-tabs [role="tab"]').count();
  await page.getByRole('button',{name:'Chọn dự án',exact:true}).click();await page.getByRole('button').filter({hasText:'Dự án thử UI'}).click({button:'right'});await page.getByRole('textbox',{name:'Đường dẫn chương trình',exact:true}).waitFor();check(await page.getByRole('textbox',{name:'Đường dẫn chương trình',exact:true}).inputValue().then(v=>v.endsWith('program.exe')),'Project right click opens editable details');await page.getByRole('button',{name:'Danh sách',exact:true}).click();await page.getByRole('button').filter({hasText:'Dự án thử UI'}).click();await page.locator('dialog').waitFor({state:'detached'});await page.waitForTimeout(150);
  check(await page.locator('.document-tabs [role="tab"]').count()===openTabsBeforeProject &&
    await page.getByRole('button',{name:'SQL mới',exact:true}).isVisible(),'Opening a project starts with an empty editor and creates no query tab');
  check(await page.evaluate(()=>window.__nativeCalls.some(c=>c.method==='project.commands'&&c.payload.targetId==='project-sys')),'Project selection automatically loads SYS functions');
  await page.getByRole('button',{name:'Làm mới web.config',exact:true}).click();
  await page.getByText('Đã lưu lại web.config',{exact:true}).waitFor();
  await page.getByRole('button',{name:'Xóa Structure/App',exact:true}).click();
  const clearDialog=page.getByRole('dialog',{name:'Xóa file trong Structure/App'});
  await clearDialog.getByText(/Xóa 2 file/).waitFor();
  check(await page.evaluate(()=>!window.__nativeCalls.some(call=>call.method==='project.structure.clear')),
    'Structure cleanup previews the target before deleting');
  await clearDialog.getByRole('button',{name:'Xóa file',exact:true}).click();
  await page.getByText('Đã xóa 2 file trong Structure/App',{exact:true}).waitFor();
  const workspacePath=page.locator('.workspace-path-action');
  await workspacePath.click();
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='workspace.reveal'&&c.payload.path==='C:\\Fixture'));
  await workspacePath.click({button:'right'});
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='clipboard.write'&&c.payload.text==='C:\\Fixture'));
  check(true,'Project folder path opens on click and copies on right click');
  check(await page.evaluate(()=>!window.__nativeCalls.some(c=>c.method==='schema.objects'&&c.payload.targetId==='project-app')),'DB stays unloaded until its sidebar is opened');
  await page.getByRole('button',{name:'DB',exact:true}).click();await page.waitForTimeout(100);check(await page.evaluate(()=>window.__nativeCalls.some(c=>c.method==='schema.objects'&&c.payload.targetId==='project-app')),'Opening DB lazily loads current App target');
  const countBefore=await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='schema.objects').length);await page.getByRole('button',{name:'File',exact:true}).click();await page.getByRole('button',{name:'DB',exact:true}).click();await page.waitForTimeout(50);check(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='schema.objects').length)===countBefore,'Reopening DB retains already loaded objects');
  await page.getByRole('button',{name:'SQL mới',exact:true}).click();
  await page.keyboard.press('Control+Alt+d');await page.waitForTimeout(100);check((await page.getByRole('button',{name:'Database của tab',exact:true}).textContent()).includes('Fixture_Sys'),'Ctrl+Alt+D switches to SYS');check(await page.evaluate(()=>window.__nativeCalls.some(c=>c.method==='schema.objects'&&c.payload.targetId==='project-sys')),'Visible DB follows the current database after switching');
  const objectSearch=page.getByRole('textbox',{name:'Tên hoặc nội dung object'});
  await objectSearch.fill('D');await objectSearch.fill('Da');await objectSearch.fill('Danh');
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='schema.objects'&&c.payload.query==='Danh'));
  check(await page.evaluate(()=>!window.__nativeCalls.some(c=>c.method==='schema.objects'&&['D','Da'].includes(c.payload.query))),'Live DB search debounces intermediate text');
  const openedBefore=await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='document.open').length);
  await page.getByRole('button',{name:'Chức năng',exact:true}).click();
  check(await page.locator('.function-separator').count()===1,'Dash function renders as horizontal separator');
  await page.getByRole('button',{name:'Nhân viên',exact:true}).click();
  const fileTree=page.locator('.project-file-tree');
  const dirPath='C:\\Fixture\\App_Data\\Controllers\\Dir';
  const gridPath='C:\\Fixture\\App_Data\\Controllers\\Grid';
  const dirFile=fileTree.getByTitle(dirPath+'\\nhanvien.xml',{exact:true});
  const gridFile=fileTree.getByTitle(gridPath+'\\nhanvien.xml',{exact:true});
  await dirFile.waitFor();await gridFile.waitFor();
  const workspaceResize=page.getByRole('separator',{name:'Đổi chiều rộng Không gian dự án'});
  const sidebar=page.locator('#project-sidebar');
  const initialWidth=(await sidebar.boundingBox()).width;
  const grip=await workspaceResize.boundingBox();
  await page.mouse.move(grip.x+3,grip.y+80);
  await page.mouse.down();
  await page.mouse.move(grip.x+83,grip.y+80,{steps:4});
  await page.mouse.up();
  await page.waitForFunction(width=>document.querySelector('#project-sidebar').getBoundingClientRect().width>=width+70,initialWidth);
  await workspaceResize.press('ArrowLeft');
  await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='settings.save'&&call.payload.settings.explorerWidth));
  check(true,'Workspace width supports pointer, keyboard and saved settings');
  await page.getByRole('button',{name:'Cài đặt',exact:true}).click();
  await page.getByRole('button',{name:'Cỡ chữ Không gian dự án',exact:true}).click();
  await page.getByRole('option',{name:'14 px',exact:true}).click();
  await page.getByRole('button',{name:'Cỡ chữ editor',exact:true}).click();
  await page.getByRole('option',{name:'18 px',exact:true}).click();
  await page.getByRole('button',{name:'Lưu cài đặt',exact:true}).click();
  await page.locator('dialog[open]').waitFor({state:'detached'});
  check(await dirFile.evaluate(row=>getComputedStyle(row).fontSize==='14px'),
    'Workspace font size applies to the file tree');
  check(await fileTree.getByRole('button',{name:'App_Data',exact:true}).getAttribute('aria-expanded')==='true' &&
    await fileTree.getByRole('button',{name:'Controllers',exact:true}).getAttribute('aria-expanded')==='true',
    'ERP function reveals actual App_Data and Controllers ancestors');
  check(await page.evaluate(before=>window.__nativeCalls.filter(c=>c.method==='document.open').length===before,openedBefore),
    'Opening a function expands folders without opening a document');
  check(await fileTree.getByTitle(dirPath+'\\bophan.xml',{exact:true}).count()===0,
    'The controller tree excludes files from other controllers in the same directory');
  check(await fileTree.locator('.category-heading').allTextContents().then(names=>
    ['Main','Dir','Filter','Grid','Report','Templates'].every(name=>names.includes(name))),
    'Real folder tree keeps the colored categories');
  check(await fileTree.locator('.category-heading').first().evaluate(element => getComputedStyle(element).borderTopWidth==='2px'),
    'The category color stripe sits above the folder heading');
  await dirFile.click();await gridFile.click();
  check(await page.locator('.monaco-editor .view-lines').first().evaluate(lines=>getComputedStyle(lines).fontSize==='18px'),
    'Editor font size applies to Monaco');
  await sidebar.screenshot({path:out+'/workspace-tree-design.png'});
  check(await page.locator('.document-tabs .document-tab.active').evaluate(element => {
    const style = getComputedStyle(element);
    return style.borderBottomWidth === '2px' && style.borderTopWidth === '0px';
  }), 'The active editor tab keeps its color stripe below the label');
  await page.getByRole('tab',{name:'nhanvien.xml — Dir',exact:true}).waitFor();
  await page.getByRole('tab',{name:'nhanvien.xml — Grid',exact:true}).waitFor();
  check(true,'Same-name file tabs still show their distinct folders');
  await page.getByRole('button',{name:'Xem trước Grid / Dir',exact:true}).click();
  const previewDialog=page.getByRole('dialog',{name:'Xem trước Grid'});
  await previewDialog.getByRole('heading',{name:'Cập nhật thông tin lương nhân viên'}).waitFor();
  check(await page.locator('dialog[open]').count()===1, 'Grid preview opens one dialog');
  check(await previewDialog.getByRole('columnheader').allTextContents().then(headers=>headers.join('|')==='Mã nhân viên|Họ và tên|Bộ phận') &&
    await previewDialog.getByRole('columnheader',{name:'Mã nhân viên'}).evaluate(cell=>Math.abs(cell.getBoundingClientRect().width-110)<2),
    'Grid preview renders ordered XML columns at their configured widths');
  await page.screenshot({path:out+'/grid-preview.png'});
  await previewDialog.getByRole('button',{name:'Đóng',exact:true}).click();
  await page.evaluate(()=>{window.__previewDir=true;});
  await page.getByRole('button',{name:'Xem trước Grid / Dir',exact:true}).click();
  const dirDialog=page.getByRole('dialog',{name:'Xem trước Dir',exact:true});
  await dirDialog.getByRole('heading',{name:'Khai báo nhân viên'}).waitFor();
  check(await dirDialog.getByText('Năm',{exact:true}).isVisible() &&
    await dirDialog.getByRole('textbox',{name:'Năm',exact:true}).evaluate(input=>input.parentElement.style.gridColumn==='2 / span 2') &&
    await dirDialog.getByRole('textbox',{name:'Mã nhân viên',exact:true}).evaluate(input=>input.parentElement.style.gridColumn==='4 / span 1'),
    'Dir preview shows labels and keeps field spans and positions');
  const dirTabs=dirDialog.getByRole('tablist',{name:'Nhóm Dir'});
  check((await dirTabs.getByRole('tab').allTextContents()).join('|')==='Chi tiết|Phụ cấp',
    'Dir categories follow view order');
  await dirTabs.getByRole('tab',{name:'Chi tiết'}).click();
  check(await dirDialog.getByText('1 - Theo thời gian',{exact:true}).isVisible() &&
    await dirDialog.getByRole('textbox',{name:'Năm',exact:true}).isVisible() &&
    await dirDialog.getByRole('table',{name:'Bảng lương'}).getByRole('columnheader',{name:'Mức lương'}).isVisible(),
    'Dir preview shows only the active category fields and detail Grid');
  await dirTabs.getByRole('tab',{name:'Chi tiết'}).press('ArrowRight');
  check(await dirDialog.getByRole('textbox',{name:'Mức phụ cấp'}).isVisible() &&
    await dirDialog.getByRole('textbox',{name:'Năm',exact:true}).isVisible() &&
    !await dirDialog.getByRole('table',{name:'Bảng lương'}).count(),
    'Keyboard switches Dir categories without retaining the previous Grid');
  await dirDialog.getByRole('button',{name:'Đóng',exact:true}).click();
  const colored=()=>page.evaluate(()=>{
    const lines=[...document.querySelectorAll('.editor-group.focused .view-line')];
    const color=(line,token)=>[...line.querySelectorAll('span[class^="mtk"]')]
      .find(span=>span.textContent.trim()===token)?.className;
    return ['42','43'].every(value=>{
      const line=lines.find(element=>element.textContent.includes(value));
      return line && color(line,'SELECT') && color(line,'SELECT')!==color(line,value);
    });
  });
  await page.waitForFunction(()=>[...document.querySelectorAll('.editor-group.focused .view-line span[class^="mtk"]')]
    .filter(span=>span.textContent.trim()==='SELECT').length>=2);
  check(await colored(),'SQL keywords and numbers are colored in XML CDATA and inline queries');
  check(await page.locator('.editor-group.focused .view-line').evaluateAll(lines => lines.some(line =>
    [...line.querySelectorAll('span')].some(span => span.textContent === '<![CDATA[' && /rgb\(99, 217, 144\)/.test(getComputedStyle(span).color)))),
    'CDATA opening delimiter is highlighted in green');
  const xmlEditor=page.locator('.editor-group.focused .monaco-editor').first();
  await xmlEditor.click({position:{x:125,y:25}});
  await page.keyboard.press('Control+Home');
  for(let i=0;i<3;i++) await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Home');
  for(let i=0;i<'<command><text><![CDATA[SELECT '.length;i++) await page.keyboard.press('ArrowRight');
  await page.keyboard.type('@@');
  const partitionSuggestion=xmlEditor.locator('.suggest-widget .monaco-list-row').filter({hasText:'@@prime$partition$current'});
  await partitionSuggestion.waitFor();
  await page.keyboard.type('prime$partition$c');
  await xmlEditor.locator('.suggest-widget .monaco-list-row.focused').filter({hasText:'@@prime$partition$current'}).waitFor();
  await page.keyboard.press('Enter');
  await page.waitForFunction(()=>document.querySelector('.editor-group.focused .view-lines')?.textContent.includes('@@prime$partition$current'));
  check(await xmlEditor.locator('.view-lines').textContent().then(text=>text.includes('@@prime$partition$current')),
    'Embedded XML SQL suggests and inserts FAST session variables with dollar signs');
  await page.keyboard.press('Control+z');
  await page.keyboard.press('Control+z');
  await page.getByRole('button',{name:/^Vấn đề/}).click();
  await page.getByText(/Cảnh báo · Field 'missing'/).waitFor();
  check(true,'ERP XML warnings are listed with their severity');
  await page.getByRole('button',{name:'Kết quả',exact:true}).click();
  await page.keyboard.press('Control+g');
  await page.getByRole('dialog',{name:'Đi đến phần trong file'}).getByRole('button',{name:'toolbar',exact:true}).click();
  await page.waitForFunction(()=>document.querySelector('.statusbar')?.textContent.includes('Dòng 3'));
  check(true,'Ctrl+G jumps to an XML section in the current buffer');
  await dirFile.focus();await page.keyboard.press('Control+c');
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='clipboard.write'&&c.payload.text==='nhanvien.xml'));
  await dirFile.click({button:'right'});
  const fileMenu=page.getByRole('menu',{name:'Thao tác file và thư mục'});
  await fileMenu.getByRole('menuitem',{name:'Mở thư mục chứa file',exact:true}).click();
  await page.waitForFunction(path=>window.__nativeCalls.some(c=>c.method==='workspace.reveal'&&c.payload.path===path),dirPath+'\\nhanvien.xml');
  await dirFile.click({button:'right'});await fileMenu.getByRole('menuitem',{name:'Nhân bản file',exact:true}).click();
  await page.getByRole('tab',{name:'nhanvien_copy.xml',exact:true}).waitFor();
  await fileTree.getByTitle(dirPath+'\\nhanvien_copy.xml',{exact:true}).waitFor();
  check(true,'Duplicate appears in the real directory and opens in a tab');
  await page.getByRole('tab',{name:'nhanvien.xml — Dir',exact:true}).click();
  await page.locator('.editor-group .monaco-editor').first().click({position:{x:125,y:25}});
  await page.keyboard.press('Control+End');
  await page.keyboard.insertText('<!-- giữ nội dung chưa lưu -->');
  await dirFile.press('F2');
  const renameFile=page.getByRole('dialog',{name:'Đổi tên file'});
  await renameFile.getByRole('textbox',{name:'Tên mới'}).fill('nhanvien-doi.xml');
  await renameFile.getByRole('button',{name:'Đổi tên',exact:true}).click();
  await page.getByRole('tab',{name:'nhanvien-doi.xml',exact:true}).waitFor();
  await fileTree.getByTitle(dirPath+'\\nhanvien-doi.xml',{exact:true}).waitFor();
  check(!(await dirFile.count()),'F2 renames the file and updates the open tab and tree');
  const dirFolder=fileTree.getByTitle(dirPath,{exact:true});
  await dirFolder.click({button:'right'});
  await fileMenu.getByRole('menuitem',{name:/Đổi tên/}).click();
  const renameFolder=page.getByRole('dialog',{name:'Đổi tên thư mục'});
  await renameFolder.getByRole('textbox',{name:'Tên mới'}).fill('DirERP');
  await page.screenshot({path:out+'/erp-rename.png'});
  await renameFolder.getByRole('button',{name:'Đổi tên',exact:true}).click();
  const renamedPath='C:\\Fixture\\App_Data\\Controllers\\DirERP\\nhanvien-doi.xml';
  await fileTree.getByTitle(renamedPath,{exact:true}).waitFor();
  check(await page.getByRole('tab',{name:'nhanvien-doi.xml',exact:true}).getAttribute('title')===renamedPath &&
    await page.getByRole('tab',{name:'nhanvien_copy.xml',exact:true}).getAttribute('title')==='C:\\Fixture\\App_Data\\Controllers\\DirERP\\nhanvien_copy.xml',
    'Folder rename updates all open descendant tab paths');
  await page.keyboard.press('Control+s');
  await page.waitForFunction(path=>window.__nativeCalls.some(c=>c.method==='document.save'&&c.payload.path===path&&c.payload.text.includes('giữ nội dung chưa lưu')),renamedPath);
  check(true,'Unsaved editor content saves to the renamed descendant path');
  await page.screenshot({path:out+'/erp-folder-tree.png'});
  await page.getByRole('button',{name:'Ẩn không gian dự án',exact:true}).click();
  check(!(await page.locator('.sidebar').isVisible()),'Project sidebar can be hidden');
  await page.getByRole('button',{name:'File dự án',exact:true}).click();
  await page.locator('.project-file-tree').getByRole('button',{name:'App_Data',exact:true}).waitFor();
  await page.getByRole('button',{name:'Chức năng',exact:true}).click();
  await page.getByRole('button',{name:'Bộ phận',exact:true}).click();
  await fileTree.getByTitle(dirPath+'\\bophan.xml',{exact:true}).waitFor();
  const employeeGroup=page.locator('.controller-group-title').filter({hasText:'nhanvien'});
  const departmentGroup=page.locator('.controller-group-title').filter({hasText:'bophan'});
  check(await fileTree.getByTitle(renamedPath,{exact:true}).count()===0 &&
    await employeeGroup.getAttribute('aria-expanded')==='false' &&
    await departmentGroup.getAttribute('aria-expanded')==='true' &&
    await page.getByRole('tab',{name:'nhanvien-doi.xml',exact:true}).count()===1,
    'Opening another controller retains the collapsed group and open editor tabs');
  await employeeGroup.click();
  await fileTree.getByTitle(renamedPath,{exact:true}).waitFor();
  check(await departmentGroup.getAttribute('aria-expanded')==='false','Reopening a group collapses the other controller');
  await departmentGroup.click();
  await page.getByRole('button',{name:'Đóng controller nhanvien',exact:true}).click();
  check(await employeeGroup.count()===0 && await page.getByRole('tab',{name:'nhanvien-doi.xml',exact:true}).count()===1,
    'Closing a controller removes its group and preserves its editor tabs');
  await page.screenshot({path:out+'/erp-controller-groups.png'});
  await page.getByRole('button',{name:'Chức năng',exact:true}).click();
  await page.getByRole('button',{name:'Thông tin nhân viên',exact:true}).click();
  const functionTabs=sidebar.getByRole('button',{name:'Tab chức năng',exact:true});
  await functionTabs.click();
  const passport=sidebar.getByRole('button',{name:'Hộ chiếu, thị thực',exact:true});
  await passport.click();
  await fileTree.getByTitle(dirPath+'\\hrPIPassportVisa.xml',{exact:true}).waitFor();
  check(await passport.getAttribute('aria-current')==='true' &&
    await page.evaluate(()=>window.__nativeCalls.some(c=>c.method==='project.employeeTabs'&&c.payload.targetId==='project-sys')),
    'Employee tabs load from the project SYS connection and switch the file tree by sysid');
  await sidebar.getByRole('button',{name:'Thông tin chung',exact:true}).click();
  await fileTree.getByTitle(dirPath+'\\hrPIGeneralInformation.xml',{exact:true}).waitFor();
  await departmentGroup.click();
  check(await functionTabs.count()===0,'Employee tabs hide when a different ERP function is selected');
  await page.keyboard.press('Control+Shift+f');
  await page.getByRole('button',{name:'Phạm vi tìm kiếm',exact:true}).click();
  await page.getByRole('option',{name:/Controllers.*Dir$/}).click();
  await page.getByRole('textbox',{name:'Nội dung tìm'}).fill('controller');
  await page.getByRole('button',{name:'Tìm',exact:true}).click();
  await page.waitForFunction(path=>window.__nativeCalls.some(call=>call.method==='workspace.search'&&call.payload.path===path),dirPath);
  check(true,'Folder search starts at the selected controller directory');
  await page.evaluate(()=>{window.__holdTableCatalog=true;});
  await page.keyboard.press('Control+Shift+t');
  const firstTableTab=page.getByRole('tab',{name:/^Cấu trúc và dữ liệu(?: ·.*)?$/});
  const tablePanel=page.locator('#'+await firstTableTab.getAttribute('aria-controls'));
  await tablePanel.waitFor();
  check(await page.locator('.editor-toolbar .workspace-tools').getByRole('tab',{name:'Cấu trúc và dữ liệu'}).isVisible(),
    'Table tool tab is on the editor toolbar next to the breadcrumb');
  check(!(await page.locator('dialog[open]').count())&&!(await tablePanel.getByLabel('Schema',{exact:true}).count()),'Table inspection opens as a tab with a single table field');
  const tableInput=tablePanel.getByRole('combobox',{name:'Bảng',exact:true});
  check(await tableInput.evaluate(input=>input===document.activeElement),'Opening table inspection focuses the table field');
  for(const name of ['Cột SELECT','WHERE','ORDER BY']){
    await page.keyboard.press('Tab');
    check(await tablePanel.getByRole('combobox',{name,exact:true}).evaluate(input=>input===document.activeElement),
      'Tab focuses '+name+' in input order');
  }
  await tableInput.fill('danh');
  await tablePanel.getByRole('option',{name:'DanhMuc',exact:true}).waitFor();
  check(await page.evaluate(()=>window.__nativeCalls.some(call=>call.method==='table.names'&&call.payload.query==='danh')),
    'Quick table lookup suggests matches while the full catalog is pending');
  await page.evaluate(()=>{window.__holdTableCatalog=false;window.__releaseTableCatalog();});
  await tablePanel.getByRole('option',{name:'DanhMuc_Archive',exact:true}).waitFor();
  await tableInput.fill('DanhMuc');
  check(await tablePanel.getByRole('option').first().textContent()==='DanhMuc','Exact table match ranks above prefix matches');
  await tableInput.press('Enter');
  await tablePanel.locator('.data-grid td[title="1"]').waitFor();
  const firstTableCell=await tablePanel.locator('.data-grid td[title="1"]').boundingBox();
  const lastTableCell=await tablePanel.locator('.data-grid td[title="y"]').boundingBox();
  await page.mouse.move(firstTableCell.x+firstTableCell.width/2,firstTableCell.y+firstTableCell.height/2);
  await page.mouse.down();
  await page.mouse.move(lastTableCell.x+lastTableCell.width/2,lastTableCell.y+lastTableCell.height/2,{steps:8});
  await page.mouse.up();
  await page.keyboard.press('Control+c');
  await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='clipboard.write'&&call.payload.text==='1\tx\r\n2\ty'));
  check(await tablePanel.locator('.grid-copy-menu').count()===0,'Table grid copies a dragged cell range with Ctrl+C');
  check(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='table.catalog').length===1 &&
    !window.__nativeCalls.some(c=>c.method==='table.structure'&&c.payload.table==='danh')),
    'Table search reuses one catalog and opens the selected table immediately');
  await tablePanel.getByRole('combobox',{name:'WHERE',exact:true}).fill('co');
  await tablePanel.getByRole('option',{name:'code',exact:true}).waitFor();
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Tab');
  check(await tablePanel.getByRole('combobox',{name:'WHERE',exact:true}).inputValue()==='code' &&
    await tablePanel.getByRole('combobox',{name:'ORDER BY',exact:true}).evaluate(input=>input===document.activeElement),
    'Tab inserts a selected ordinary column without brackets and moves to the next field');
  const selectInput=tablePanel.getByRole('combobox',{name:'Cột SELECT',exact:true});
  await selectInput.fill('cou');
  await selectInput.press('Escape');
  await selectInput.press('Control+Space');
  await tablePanel.getByRole('option',{name:'COUNT',exact:true}).click();
  await page.waitForFunction(()=>document.querySelector('input[aria-label="Cột SELECT"]')?.selectionStart===6);
  check(await selectInput.inputValue()==='COUNT()','SQL function completion inserts parentheses and places the caret inside');
  await selectInput.fill('COUNT(*) AS total');
  const whereInput=tablePanel.getByRole('combobox',{name:'WHERE',exact:true});
  await whereInput.fill('code IS NOT N');
  await tablePanel.getByRole('option',{name:'IS NOT NULL',exact:true}).click();
  const orderInput=tablePanel.getByRole('combobox',{name:'ORDER BY',exact:true});
  await orderInput.fill('total DE');
  await tablePanel.getByRole('option',{name:'DESC',exact:true}).click();
  await page.screenshot({path:out+'/table-catalog-sql.png'});
  await tablePanel.getByRole('button',{name:'Đọc dữ liệu',exact:true}).click();
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='table.view' &&
    c.payload.select==='COUNT(*) AS total' && c.payload.where==='code IS NOT NULL' && c.payload.orderBy==='total DESC'));
  check(true,'SELECT, WHERE and ORDER BY suggestions produce SQL expressions in the query');
  await tablePanel.getByRole('button',{name:'Cấu trúc',exact:true}).click();
  await tablePanel.getByText('PK · PK_DanhMuc',{exact:true}).waitFor();
  await tablePanel.getByText('label',{exact:true}).click({button:'right'});
  await tablePanel.getByRole('button',{name:'Gen ADD COLUMN',exact:true}).click();
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='clipboard.write'&&c.payload.text.includes('ADD [label]')));
  await tablePanel.getByText('label',{exact:true}).click({button:'right'});
  await tablePanel.getByRole('button',{name:'Gen ALTER COLUMN',exact:true}).click();
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='clipboard.write'&&c.payload.text.includes('ALTER COLUMN [label]')));
  check(true,'Separate ADD and ALTER actions copy the script for the selected column');
  await tablePanel.getByText('label',{exact:true}).click({button:'right'});
  await tablePanel.getByRole('button',{name:'DROP COLUMN',exact:true}).click();
  check(await page.evaluate(()=>!window.__nativeCalls.some(c=>c.method==='table.dropColumns')),'Drop column waits for explicit confirmation');
  await page.getByRole('dialog',{name:'Xóa cột khỏi bảng'}).getByRole('button',{name:'Xóa cột',exact:true}).click();
  await tablePanel.getByText('Đã xóa cột.',{exact:true}).waitFor();
  check(await tablePanel.getByText('label',{exact:true}).count()===0,'Confirmed drop reloads the table structure');
  await tablePanel.getByRole('button',{name:'Dữ liệu',exact:true}).click();
  await tablePanel.getByRole('button',{name:'Đọc dữ liệu',exact:true}).click();
  await tablePanel.locator('.data-grid td[title="1"]').waitFor();
  await tablePanel.getByRole('button',{name:'Script',exact:true}).click();
  await tablePanel.getByRole('button',{name:'Script INSERT dữ liệu đang hiển thị'}).click();
  await page.getByRole('tab',{name:'DanhMuc-insert.sql'}).waitFor();
  await firstTableTab.click();
  await tablePanel.getByRole('button',{name:'Script',exact:true}).click();
  await tablePanel.getByRole('button',{name:'Script tạo bảng',exact:true}).click();
  await page.getByRole('tab',{name:'DanhMuc-create.sql'}).waitFor();
  check(true,'Both table scripts open in editor tabs');
  await page.keyboard.press('Control+Shift+t');
  const secondTableTab=page.getByRole('tab',{name:/^Cấu trúc và dữ liệu 2/});
  const secondTablePanel=page.locator('#'+await secondTableTab.getAttribute('aria-controls'));
  const secondTableInput=secondTablePanel.getByRole('combobox',{name:'Bảng',exact:true});
  await secondTableInput.fill('danh');
  await secondTablePanel.getByRole('option',{name:'DanhMuc_Archive',exact:true}).click();
  await secondTablePanel.locator('.data-grid td[title="1"]').waitFor();
  const secondTableName=await secondTableInput.inputValue();
  await firstTableTab.click();
  check(await tableInput.inputValue()==='DanhMuc' && secondTableName==='DanhMuc_Archive',
    'Independent inspection tabs retain their selected tables');
  await tablePanel.getByRole('button',{name:'Script',exact:true}).click();
  await tablePanel.getByRole('button',{name:'Script INSERT dữ liệu đang hiển thị'}).click();
  await page.waitForFunction(()=>window.__nativeCalls.filter(call=>call.method==='table.insertScript').length===2);
  check(await page.evaluate(()=>new Set(window.__nativeCalls.filter(call=>call.method==='table.view').map(call=>call.payload.scope)).size===2),
    'Inspection data previews use independent native scopes');
  check(await page.evaluate(()=>window.__nativeCalls.filter(call=>call.method==='table.catalog').length===1),
    'Inspection tabs reuse the cached catalog for the same connection');
  await page.getByRole('button',{name:'SQL Profiler',exact:true}).click();
  await secondTableTab.click();
  const tableDatabase=secondTablePanel.getByRole('button',{name:'Database của cấu trúc và dữ liệu',exact:true});
  const nextDb=(await tableDatabase.textContent()).includes('Fixture_App')?'Fixture_Sys':'Fixture_App';
  await tableDatabase.click();
  await secondTablePanel.getByRole('option',{name:new RegExp(nextDb)}).click();
  check(await secondTablePanel.locator('.data-grid').count()===0 &&
    await secondTableInput.inputValue()==='DanhMuc_Archive','Switching App/Sys keeps the table name and clears the old preview');
  await secondTablePanel.getByRole('button',{name:'Đọc dữ liệu',exact:true}).click();
  await page.waitForFunction(db=>window.__nativeCalls.filter(c=>c.method==='table.view').at(-1)?.payload.targetId===db,
    nextDb==='Fixture_Sys'?'project-sys':'project-app');
  check(true,'Table inspection reads from the selected App or Sys connection');
  await page.screenshot({path:out+'/sql-workspace-tabs.png'});
  await page.setViewportSize({width:375,height:600});
  check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),
    'Multiple SQL tools fit within the narrow workbench');
  await page.setViewportSize({width:1440,height:1000});
  await page.getByRole('button',{name:'Đóng SQL Profiler'}).click();
  await page.getByRole('button',{name:/^Đóng Cấu trúc và dữ liệu 2/}).click();
  await page.getByRole('button',{name:/^Đóng Cấu trúc và dữ liệu ·/}).click();
  const separator=page.getByRole('separator',{name:'Đổi chiều cao panel kết quả'});
  const panel=page.locator('#results-panel');
  const previousHeight=(await panel.boundingBox()).height;
  const handle=await separator.boundingBox();
  await page.mouse.move(handle.x+handle.width/2,handle.y+handle.height/2);
  await page.mouse.down();await page.mouse.move(handle.x+handle.width/2,handle.y-90,{steps:12});await page.mouse.up();
  check((await panel.boundingBox()).height>previousHeight+50,'Dragging the top separator increases results height');
  await separator.press('ArrowDown');
  check((await panel.boundingBox()).height<previousHeight+90,'Keyboard separator adjustment reduces results height');
  writeFileSync(out+'/startup-timing.json',JSON.stringify(await page.evaluate(()=>performance.getEntriesByName('startup.picker').map(e=>({durationMs:e.duration}))),null,2));
  check(await page.locator('.document-tabs').evaluate(e=>e.getBoundingClientRect().height)<=32,'Document tabs use compact 32px height');
  for(const [width,height] of [[1440,900],[1280,600],[960,480],[375,600]]){await page.setViewportSize({width,height});await page.waitForTimeout(80);check(await page.evaluate(()=>{const f=document.querySelector('.statusbar').getBoundingClientRect(),w=document.querySelector('.workbench').getBoundingClientRect(),a=document.querySelector('.editor-area').getBoundingClientRect();return f.bottom<=innerHeight+1&&f.height>=27&&w.bottom<=f.top+1&&a.bottom<=f.top+1;}),'Bottom status and workbench remain within '+width+'x'+height);}
  await page.setViewportSize({width:375,height:900});await page.getByRole('button',{name:'File dự án',exact:true}).click();await page.waitForTimeout(100);
  check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'375px workbench does not overflow page');
  await page.screenshot({path:out+'/mobile-code-dark.png'});
  await page.setViewportSize({width:1440,height:900});
  await page.evaluate(()=>{window.__bulkOpenCount=36;});
  await page.keyboard.press('Control+o');
  await page.getByRole('tab',{name:'bulk-35.xml',exact:true,selected:true}).waitFor();
  await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='xml.diagnostics'&&call.payload.text==='<root>bulk-35</root>'));
  check(await page.evaluate(()=>!window.__nativeCalls.some(call=>call.method==='xml.diagnostics'&&call.payload.text==='<root>bulk-0</root>')),
    'Bulk open defers loading and validating inactive files');
  await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='project.history.record'&&call.payload.path.endsWith('\\bulk-35.xml')));
  check(await page.evaluate(()=>!window.__nativeCalls.some(call=>call.method==='project.history.record'&&call.payload.path.endsWith('\\bulk-0.xml'))),
    'History records the displayed file without adding background tabs');
  await page.getByRole('tab',{name:'bulk-0.xml',exact:true}).click();
  await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='xml.diagnostics'&&call.payload.text==='<root>bulk-0</root>'));
  await page.getByRole('button',{name:'Lịch sử',exact:true}).click();
  const otherHistory=page.locator('.history-group').filter({has:page.locator('summary').getByText('Dự án lịch sử',{exact:true})});
  await otherHistory.getByText('Main',{exact:true}).waitFor();
  check(await otherHistory.getByText('Controller',{exact:true}).count()===0,'History shows only the immediate folder');
  const historySearch=page.getByRole('searchbox',{name:'Tìm file hoặc thư mục trong lịch sử'});
  await historySearch.fill('PAST.XML');
  await otherHistory.getByRole('button',{name:'past.xml',exact:true}).waitFor();
  check(await page.locator('.project-file-tree').getByText('Dự án thử UI',{exact:true}).count()===0,
    'History filters by filename across projects without flattening the result');
  await historySearch.fill('main');
  await otherHistory.getByRole('button',{name:'past.xml',exact:true}).waitFor();
  await historySearch.fill('unmatched-history-text');
  await page.getByText('Không tìm thấy file hoặc thư mục.',{exact:true}).waitFor();
  await historySearch.fill('main');
  await page.locator('.project-file-tree').getByRole('button',{name:'past.xml',exact:true}).click();
  const unsaved=page.getByRole('dialog').getByRole('button',{name:'Đóng không lưu',exact:true});
  const openedHistoryTab=page.getByRole('tab',{name:'past.xml',exact:true,selected:true});
  await unsaved.or(openedHistoryTab).waitFor();
  if(await unsaved.isVisible()) await unsaved.click();
  await openedHistoryTab.waitFor();
  await page.getByRole('tab',{name:'bulk-0.xml',exact:true}).waitFor({state:'detached'});
  check(await page.evaluate(()=>window.__nativeCalls.some(call=>call.method==='project.open'&&call.payload.id==='other')),
    'History opens another project and closes the previous project tabs');
  await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='project.history.record'&&call.payload.path.endsWith('past.xml')));
  await otherHistory.getByRole('button',{name:'Xóa past.xml khỏi lịch sử',exact:true}).click();
  await otherHistory.waitFor({state:'detached'});
  check(await page.getByRole('tab',{name:'past.xml',exact:true,selected:true}).isVisible(),
    'Removing the last history entry hides the project and leaves the editor open');
  if(errors.length)console.log(JSON.stringify(errors,null,2));check(errors.length===0,'No uncaught browser errors');
  writeFileSync(out+'/interaction-report.json',JSON.stringify({passed:true,checks,transport:'explicit browser fixture; not a .NET/SQL integration test',errors},null,2));
}finally{await browser.close();}
