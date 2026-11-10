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
  const doc=(id,text='',name='fixture.xml',filePath='C:\\Fixture\\fixture.xml')=>({id,path:filePath,name,text,encoding:'utf-8',bom:false,newline:'CRLF',version:1});
  const settings={theme:'Light',workspace:'C:\\Fixture',server:'.',database:'Fixture_App',integratedSecurity:true,sqlUser:'',trustCertificate:true,crystalPath:'',profiler18Path:'',shortcuts:{},activityCommands:null};
  const native={addEventListener:(_,fn)=>listeners.push(fn),postMessage:request=>{window.__nativeCalls.push(request);let result,error;
    switch(request.method){
      case 'studio.bootstrap':result={settings,projects:[{id:'fixture',name:'Dự án thử UI',server:'.',database:'Fixture_App',sourcePath:'C:\\Fixture'}],snippets:[{key:'select',category:'SQL',body:'SELECT * FROM {{table}};',description:'Mẫu SELECT',position:0,param:''}],version:'ui-test'};break;
      case 'project.open':result={project:{id:'fixture',name:'Dự án thử UI',server:'.',database:'Fixture_App',sourcePath:'C:\\Fixture'},targets:[{id:'project-app',server:'.',database:'Fixture_App'},{id:'project-sys',server:'.',database:'Fixture_Sys'}]};break;
      case 'project.commands':result=[{entry:{id:'menu1',caption:'Nhân viên',sysId:'HR',commandId:'Dir',link:'nhanvien.aspx'},children:[]},{entry:{id:'menu2',caption:'Bộ phận',sysId:'HR',commandId:'Dept',link:'bophan.aspx'},children:[]},{entry:{id:'separator',caption:'-',sysId:'',commandId:'',link:''},children:[]}];break;
      case 'project.controller':{const name=request.payload.link==='bophan.aspx'?'bophan':'nhanvien';const root='C:\\Fixture\\';result={file:root+'Main\\'+name+'.aspx',files:[root+'Main\\'+name+'.aspx',...['Dir','Filter','Grid','Report','Templates'].flatMap(folder=>['f','xml'].map(ext=>root+'App_Data\\Controllers\\'+folder+'\\'+name+'.'+ext)),root+'App_Data\\Controllers\\Templates\\Upload\\'+name+'.xml']};break;}
      case 'project.details':result={programPath:'C:\\Fixture\\program.exe',metadata:'<project><FolderName>Dự án thử UI</FolderName></project>'};break;
      case 'recovery.load':result=[];break;
      case 'workspace.list':result=request.payload.path==='C:\\Fixture'?[{name:'fixture.xml',path:'C:\\Fixture\\fixture.xml',directory:false},{name:'truy-van-ten-rat-dai-kiem-tra-tieng-viet.sql',path:'C:\\Fixture\\long.sql',directory:false}]:[];break;
      case 'workspace.files':result=['C:\\Fixture\\fixture.xml','C:\\Fixture\\long.sql'];break;
      case 'document.openMany':result=[doc('open-'+(++serial),'<root>Tiếng Việt</root>')];break;
      case 'document.open':result=doc('open-'+(++serial),'<root>Tiếng Việt</root>');break;
      case 'document.save':result={...doc(request.payload.id,request.payload.text,request.payload.name??'Untitled.sql',request.payload.path??'C:\\Fixture\\saved.sql'),version:request.payload.version+1};break;
      case 'settings.read':result=settings;break;
      case 'document.changed':result=false;break;
      case 'clipboard.read':result=clipboard;break;
      case 'clipboard.write':clipboard=request.payload.text;result=true;break;
      case 'document.close':case 'recovery.save':case 'settings.save':case 'operation.cancel':case 'profiler.stop':result=true;break;
      case 'sql.connect':result={id:'target-'+(++serial),server:request.payload.server,database:request.payload.database};break;
      case 'sql.execute':result={tables:[{columns:['Mã','Tên','Giá trị'],rows:Array.from({length:5000},(_,i)=>[String(i),'Tiếng Việt '+i,i===0?null:'1000000000.123456789'])}],messages:['Query fixture'],durationMs:12,truncated:true};break;
      case 'sql.complete':case 'javascript.complete':result=[];break;
      case 'xml.validate':case 'xml.viewWidth':result=null;break;
      case 'schema.objects':result={columns:['Schema','Name','Type','Id'],rows:[['dbo','DanhMuc','USER_TABLE','1']]};break;
      case 'profiler.read':result={events:[],dropped:0,truncated:false};break;
      case 'catalog.references':result={columns:['Schema','Table','Column','Type'],rows:[]};break;
      case 'catalog.load':result={signature:'fixture-schema',columns:[{name:'code',type:'nvarchar',nullable:false,identity:false,computed:false,generated:0,managed:false,hasDefault:false},{name:'label',type:'nvarchar',nullable:true,identity:false,computed:false,generated:0,managed:false,hasDefault:false},...['user_id0','user_id2','datetime0','datetime2'].map(name=>({name,type:name.startsWith('user')?'int':'datetime',nullable:true,identity:false,computed:false,generated:0,managed:true,hasDefault:false}))],data:{columns:['code','label','user_id0','user_id2','datetime0','datetime2'],rows:[['one',null,null,null,null,null],[':null','literal',null,null,null,null]]}};break;
      case 'catalog.clipboard.write':result=request.payload.rows.map(r=>r.map(v=>'"'+(v??'').replaceAll('"','""')+'"').join('\t')).join('\r\n');break;
      default:error={code:'fixture',message:'Fixture không hỗ trợ '+request.method};
    }
    setTimeout(()=>listeners.forEach(fn=>fn({data:{protocolVersion:1,requestId:request.requestId,result,error}})),10);
  }};
  window.chrome=window.chrome??{};window.chrome.webview=native;
});
const out='artifacts/webview-ui';mkdirSync(out,{recursive:true});
let checks=0;const check=(value,name)=>{if(!value)throw Error('FAIL '+name);checks++;console.log('PASS '+name);};
try{
  await page.goto('http://127.0.0.1:4173');await page.getByRole('dialog',{name:'Dự án của bạn'}).waitFor();check(true,'Project picker opens before workbench');await page.getByRole('button',{name:'Đóng',exact:true}).click();await page.locator('[data-ready=true]').waitFor();
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
  check(await page.locator('.data-grid tbody tr').count()<100,'5000 SQL rows render through bounded virtual grid');
  await page.locator('.data-grid').evaluate(el=>{el.scrollTop=100000;});await page.waitForTimeout(100);
  check((await page.locator('.data-grid').textContent()).includes('Tiếng Việt 3'),'Virtual grid updates visible rows on scroll');
  await page.screenshot({path:out+'/desktop-code-light.png'});
  await page.getByRole('button',{name:'Cài đặt',exact:true}).click();
  await page.getByRole('button',{name:'Theme',exact:true}).click();await page.getByRole('option',{name:'Dark',exact:true}).click();
  await page.getByRole('button',{name:'Lưu cài đặt',exact:true}).click();await page.waitForFunction(()=>document.documentElement.dataset.theme==='Dark');
  check(true,'Theme setting updates workbench and Monaco');await page.screenshot({path:out+'/desktop-code-dark.png'});
  await page.keyboard.press('Control+n');
  await page.getByRole('button',{name:'Kết nối SQL',exact:true}).click();await page.getByRole('textbox',{name:'Database',exact:true}).fill('Fixture_Sys');await page.getByRole('button',{name:'Kết nối',exact:true}).click();await page.locator('dialog').waitFor({state:'detached'});
  await page.getByRole('tab').first().click();check((await page.getByRole('button',{name:'Database của tab',exact:true}).textContent()).includes('Fixture_App'),'Old tab keeps App target after new connection');
  await page.getByRole('tab').last().click();check((await page.getByRole('button',{name:'Database của tab',exact:true}).textContent()).includes('Fixture_Sys'),'New tab keeps Sys target');await page.getByRole('tab').first().click();
  await page.getByRole('button',{name:'Cài đặt',exact:true}).click();await page.getByRole('button',{name:'Theme',exact:true}).click();await page.getByRole('option',{name:'High Contrast',exact:true}).click();await page.getByRole('button',{name:'Lưu cài đặt',exact:true}).click();await page.waitForFunction(()=>document.documentElement.dataset.theme==='High Contrast');await page.setViewportSize({width:1920,height:1080});await page.screenshot({path:out+'/desktop-code-high-contrast-1920.png'});check(true,'High Contrast is available at 1920px');
  await page.getByRole('button',{name:'Cài đặt',exact:true}).click();await page.getByRole('button',{name:'Theme',exact:true}).click();await page.getByRole('option',{name:'Dark',exact:true}).click();await page.getByRole('button',{name:'Lưu cài đặt',exact:true}).click();await page.setViewportSize({width:1440,height:1000});
  for(const title of ['Snippets','So sánh văn bản / thư mục','SQL Profiler','XML: Outline / XPath','Dịch văn bản','Beautify SQL','Danh mục']){
    await page.keyboard.press('Control+Shift+p');await page.getByRole('textbox',{name:'Tìm lệnh hoặc file'}).fill(title);await page.getByRole('dialog').getByRole('button',{name:title,exact:true}).click();await page.locator('dialog[open]').waitFor();check(await page.locator('dialog[open]').isVisible(),'Tool opens: '+title);if(title==='SQL Profiler'){check(await page.getByRole('textbox',{name:'Profiler user',exact:true}).inputValue()==='profiler'&&await page.locator('input[aria-label="Profiler password"]').inputValue()==='fsd','Profiler defaults to dedicated SQL login');}if(title==='SQL Profiler')check(await page.getByRole('button',{name:'Profiler engine',exact:true}).count()===1,'Profiler provider selector available');if(title==='Danh mục'){await page.getByRole('textbox',{name:'Tên bảng',exact:true}).fill('catalog_fixture');await page.getByRole('button',{name:'Mở bảng',exact:true}).click();await page.getByRole('button',{name:'Dữ liệu',exact:true}).click();await page.getByRole('button',{name:'Chọn dòng 1',exact:true}).click();await page.getByRole('button',{name:'Sao chép dòng chọn',exact:true}).click();await page.getByRole('button',{name:'Dán nhiều dòng',exact:true}).click();await page.getByRole('textbox',{name:'Dòng 1 label',exact:true}).waitFor();check(await page.getByRole('textbox',{name:'Dòng 1 label',exact:true}).inputValue()===':null','Catalog copy/paste preserves SQL NULL through native clipboard boundary');}await page.getByRole('button',{name:'Đóng',exact:true}).click();
  }

  await page.keyboard.press('F5');await page.waitForTimeout(150);await page.locator('.data-grid').first().evaluate(e=>e.scrollTop=0);await page.waitForTimeout(80);
  const cell=page.locator('.data-grid tbody tr').filter({has:page.locator('td[title="Tiếng Việt 0"]')}).locator('td[title="Tiếng Việt 0"]');
  await cell.click();await page.keyboard.press('Control+c');await page.waitForTimeout(50);
  check(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='clipboard.write').at(-1)?.payload.text)==='Tiếng Việt 0','Grid copies individual Unicode cell');
  await page.getByRole('button',{name:'Sao chép dòng',exact:true}).click();await page.waitForTimeout(50);check(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='clipboard.write').at(-1)?.payload.text.includes('0\tTiếng Việt 0')),'Grid copies full row');
  await page.getByRole('button',{name:'Sao chép cột',exact:true}).click();await page.waitForTimeout(50);check(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='clipboard.write').at(-1)?.payload.text.includes('Tiếng Việt 4999')),'Grid copies virtualized column beyond visible rows');
  await page.getByRole('button',{name:'Chọn dự án',exact:true}).click();await page.getByRole('button').filter({hasText:'Dự án thử UI'}).click({button:'right'});await page.getByRole('textbox',{name:'Đường dẫn chương trình',exact:true}).waitFor();check(await page.getByRole('textbox',{name:'Đường dẫn chương trình',exact:true}).inputValue().then(v=>v.endsWith('program.exe')),'Project right click opens editable details');await page.getByRole('button',{name:'Danh sách',exact:true}).click();await page.getByRole('button').filter({hasText:'Dự án thử UI'}).click();await page.locator('dialog').waitFor({state:'detached'});await page.waitForTimeout(150);
  check(await page.evaluate(()=>window.__nativeCalls.some(c=>c.method==='project.commands'&&c.payload.targetId==='project-sys')),'Project selection automatically loads SYS functions');
  check(await page.evaluate(()=>!window.__nativeCalls.some(c=>c.method==='schema.objects'&&c.payload.targetId==='project-app')),'DB stays unloaded until its sidebar is opened');
  await page.getByRole('button',{name:'DB',exact:true}).click();await page.waitForTimeout(100);check(await page.evaluate(()=>window.__nativeCalls.some(c=>c.method==='schema.objects'&&c.payload.targetId==='project-app')),'Opening DB lazily loads current App target');
  const countBefore=await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='schema.objects').length);await page.getByRole('button',{name:'File',exact:true}).click();await page.getByRole('button',{name:'DB',exact:true}).click();await page.waitForTimeout(50);check(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='schema.objects').length)===countBefore,'Reopening DB retains already loaded objects');
  await page.keyboard.press('Control+Alt+d');await page.waitForTimeout(100);check((await page.getByRole('button',{name:'Database của tab',exact:true}).textContent()).includes('Fixture_Sys'),'Ctrl+Alt+D switches to SYS');check(await page.evaluate(()=>window.__nativeCalls.some(c=>c.method==='schema.objects'&&c.payload.targetId==='project-sys')),'Visible DB follows the current database after switching');
  await page.getByRole('button',{name:'Chức năng',exact:true}).click();check(await page.locator('.function-separator').count()===1,'Dash function renders as horizontal separator');await page.getByRole('button',{name:'Nhân viên',exact:true}).dblclick();await page.waitForTimeout(150);check(await page.locator('.controller-heading').count()===1,'Controller group opens in explorer');check(await page.locator('.controller-group .file-f').count()>=1&&await page.locator('.controller-group .file-xml').count()>=1,'Controller files have distinct extension icons');
  check(await page.locator('.controller-icon').count()===1,'Controller uses its own workflow symbol');
  check(await page.locator('.controller-group').first().innerText().then(t=>!t.includes('App_Data')&&!t.includes('Controllers')),'Explorer hides infrastructure folder layers');
  check(await page.locator('.category-heading').allTextContents().then(names=>['Main','Dir','Filter','Grid','Report','Templates'].every(n=>names.includes(n))),'Explorer exposes logical controller categories');
  check(await page.evaluate(()=>new Set(['.folder-icon','.file-f','.file-xml'].map(q=>getComputedStyle(document.querySelector('.controller-group '+q)).color)).size===3),'Folder, .f and XML icons have distinct colors');
  check(await page.evaluate(()=>new Set([...document.querySelectorAll('.controller-group .category-heading .folder-icon')].map(e=>getComputedStyle(e).color)).size===6),'All six controller categories use different colors');check(await page.evaluate(()=>{const colors=['.file-f','.file-xml'].map(q=>getComputedStyle(document.querySelector('.controller-group '+q)).color);return [...document.querySelectorAll('.controller-group .folder-icon')].every(e=>!colors.includes(getComputedStyle(e).color));}),'Folder category colors remain different from file extension colors');
  await page.getByRole('button',{name:'Chức năng',exact:true}).click();await page.getByRole('button',{name:'Bộ phận',exact:true}).dblclick();await page.waitForTimeout(100);check(await page.locator('.controller-heading').count()===2,'Explorer keeps multiple controller groups');await page.locator('.controller-heading').last().getByRole('button').last().click();check(await page.locator('.controller-heading').count()===1,'Closing controller group preserves other groups');
  check(await page.getByRole('button',{name:'File editor 1',exact:true}).count()===1&&await page.getByRole('button',{name:'File editor 2',exact:true}).count()===1,'Split editor exposes independent file selectors');
  await page.getByRole('button',{name:'File editor 1',exact:true}).click();const firstOption=page.getByRole('option').last();await firstOption.click();await page.waitForTimeout(100);check((await page.getByRole('button',{name:'File editor 1',exact:true}).textContent())!==(await page.getByRole('button',{name:'File editor 2',exact:true}).textContent()),'Split panes can select different files');
  await page.screenshot({path:out+'/controller-split-revision.png'});
  writeFileSync(out+'/startup-timing.json',JSON.stringify(await page.evaluate(()=>performance.getEntriesByName('startup.picker').map(e=>({durationMs:e.duration}))),null,2));
  check(await page.locator('.document-tabs').evaluate(e=>e.getBoundingClientRect().height)<=32,'Document tabs use compact 32px height');
  for(const [width,height] of [[1440,900],[1280,600],[960,480],[375,600]]){await page.setViewportSize({width,height});await page.waitForTimeout(80);check(await page.evaluate(()=>{const f=document.querySelector('.statusbar').getBoundingClientRect(),w=document.querySelector('.workbench').getBoundingClientRect(),a=document.querySelector('.editor-area').getBoundingClientRect();return f.bottom<=innerHeight+1&&f.height>=27&&w.bottom<=f.top+1&&a.bottom<=f.top+1;}),'Bottom status and workbench remain within '+width+'x'+height);}
  await page.setViewportSize({width:375,height:900});await page.getByRole('button',{name:'File dự án',exact:true}).click();await page.waitForTimeout(100);
  check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'375px workbench does not overflow page');
  await page.screenshot({path:out+'/mobile-code-dark.png'});
  if(errors.length)console.log(JSON.stringify(errors,null,2));check(errors.length===0,'No uncaught browser errors');
  writeFileSync(out+'/interaction-report.json',JSON.stringify({passed:true,checks,transport:'explicit browser fixture; not a .NET/SQL integration test',errors},null,2));
}finally{await browser.close();}
