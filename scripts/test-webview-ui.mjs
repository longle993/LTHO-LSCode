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
  const entityMainPath=projectRoot+'\\entity-main.xml';
  const entityDtdPath=projectRoot+'\\graph.dtd';
  const entityBodyPath=projectRoot+'\\Include\\XML\\UnitBeforeInsert.txt';
  const entityChildValue="((ma_vv like rtrim(@ma_vv) + '%'))";
  const entityDtdText="<!ENTITY UnitBeforeInsert SYSTEM 'UnitBeforeInsert'>\r\n<!ENTITY Job.Nested.Key.a \""+entityChildValue+"\">";
  const entityBodyText='if exists(select 1 where &Job.Nested.Key.a;)';
  const entityMainText="<!DOCTYPE root [<!ENTITY % graph SYSTEM 'graph.dtd'>%graph;]><root>&UnitBeforeInsert;</root>";
  const fixtureProject={id:'fixture',name:'Dự án thử UI',code:'Dự án thử UI',title:'Dự án thử UI',active:true,server:'.',database:'Fixture_App',sourcePath:projectRoot};
  const otherProject={id:'other',name:'Dự án lịch sử',code:'Dự án lịch sử',title:'Dự án lịch sử',active:true,server:'.',database:'Other_App',sourcePath:'C:\\Other'};
  const requestFixture=new URLSearchParams(location.search).has('requestFixture');
  if(requestFixture){
    localStorage.setItem('lscode.qlyc.roster',[
      'ma_bp\tma_nv\tten_nv','LTHO\tDEV01\tNhân viên A','LTHO\tDEV02\tNhân viên B','FSD\tDEV03\tNhân viên C',
    ].join('\n'));
  }
  const pastFile='C:\\Other\\Controller\\Main\\past.xml';
  const controllerDiagnosticPath=projectRoot+'\\App_Data\\Controllers\\Filter\\nhanvien.xml';
  const history=new Map([['other',[pastFile]]]);
  const controllerFiles=name=>[
    projectRoot+'\\Main\\'+name+'.aspx',
    ...['Dir','Filter','Grid','Report','Templates'].flatMap(folder=>['f','xml'].map(ext=>projectRoot+'\\App_Data\\Controllers\\'+folder+'\\'+name+'.'+ext)),
    projectRoot+'\\App_Data\\Controllers\\Templates\\Upload\\'+name+'.xml',
  ];
  let workspaceFiles=[projectRoot+'\\fixture.xml',projectRoot+'\\long.sql',
    projectRoot+'\\App_Data\\Controllers\\Dashboard\\Template\\unused.xml',
    projectRoot+'\\App_Data\\Controllers\\About.xml',...controllerFiles('nhanvien'),...controllerFiles('bophan')];
  const settings={theme:'Light',workspace:'C:\\Fixture',server:'.',database:'Fixture_App',integratedSecurity:true,sqlUser:'',trustCertificate:true,crystalPath:'',profiler18Path:'',shortcuts:{},activityCommands:null,projectLastAccess:{other:'2026-11-12T03:00:00Z'}};
  if(requestFixture){
    settings.standardCatalogUser='DEV01';
    settings.activityCommands=['request.command'];
  }
  const native={addEventListener:(_,fn)=>listeners.push(fn),postMessage:request=>{window.__nativeCalls.push(request);let result,error;
    switch(request.method){
      case 'studio.bootstrap':result={settings,projects:[fixtureProject,otherProject],snippets:[{key:'select',category:'SQL',body:'SELECT * FROM {{table}};',description:'Mẫu SELECT',position:0,param:''}],version:'ui-test'};break;
      case 'project.open':result=window.__holdProjectTargets
        ? {project:fixtureProject,targets:[],targetsDeferred:true}
        : request.payload.id==='other'?{project:otherProject,targets:[]}:
        {project:fixtureProject,targets:[{id:'project-app',server:'.',database:'Fixture_App'},{id:'project-sys',server:'.',database:'Fixture_Sys'}]};break;
      case 'project.targets':result=[{id:'project-app',server:'.',database:'Fixture_App'}];break;
      case 'project.history.clear':
        if(request.payload.id)history.delete(request.payload.id);else history.clear();
        result=true;break;
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
      case 'project.commands':
        result=request.payload.kind==='mobile'
          ? [{entry:{id:'mobile1',caption:'Mobile bán hàng',sysId:'MobileSales',commandId:'mobile1',link:''},children:[]}]
          : [
              {entry:{id:'menu-parent',caption:'Hệ thống',sysId:'',commandId:'',link:''},children:[
                {entry:{id:'menu-child',caption:'Cấu hình hệ thống',sysId:'HR',commandId:'SystemConfig',link:'systemconfig.aspx'},children:[]},
              ]},
              {entry:{id:'menu1',caption:'Nhân viên',sysId:'HR',commandId:'Dir',link:'nhanvien.aspx'},children:[]},
              {entry:{id:'menu2',caption:'Bộ phận',sysId:'HR',commandId:'Dept',link:'bophan.aspx'},children:[]},
              {entry:{id:'menu3',caption:'Thông tin nhân viên',sysId:'HR',commandId:'Info',link:'hrEmployeeInformation.aspx'},children:[]},
              {entry:{id:'separator',caption:'-',sysId:'',commandId:'',link:''},children:[]},
              ...(request.payload.refresh?[{entry:{id:'menu4',caption:'Menu vừa thêm',sysId:'HR',commandId:'NewMenu',link:'newmenu.aspx'},children:[]}]:[]),
            ];
        break;
      case 'project.menu.details':{
        const values={wmenu_id0:'03.69.02',wmenu_id:'03.69.43',menu_id:'Dir',bar:'Nhân viên',bar2:'Employee',link:'nhanvien.aspx',sysid:'HR',parameter:'',syscode:'',type:'D',status:'1',icon_url:'',icon:''};
        result={mode:'edit',fields:Object.keys(values).map(name=>({name,type:'varchar',maxLength:80})),values,version:'fixture',canSave:true};break;
      }
      case 'project.menu.resources':result={controllers:['HR'],tables:['DanhMuc','hr.NhanVien'],resources:[
        {kind:'Dir',paths:['App_Data\\Controllers\\Dir\\nhanvien.xml'],files:[
          {path:'App_Data\\Controllers\\Dir\\nhanvien.xml',tables:['hr.NhanVien']},
        ]},
        {kind:'Filter',paths:[]},
        {kind:'Grid',paths:['App_Data\\Controllers\\Grid\\nhanvien.f']},
        {kind:'Main',paths:['Main\\nhanvien.aspx']},
      ]};break;
      case 'catalogSource.discover':result={controllers:['HR'],tables:['DanhMuc'],resources:[
        {kind:'Grid',paths:['App_Data\\Controllers\\Grid\\HR.xml']},
      ]};break;
      case 'catalogSource.preview':result={fingerprint:'catalog-fixture',hasMain:true,
        files:[{path:'App_Data\\Controllers\\Grid\\CatalogDemo.xml',text:"<grid table='DanhMucDemo' />"}]};break;
      case 'project.menu.new':{
        const values={wmenu_id0:'03.69.02',wmenu_id:'03.69.44',menu_id:'AA.00.01',bar:'',bar2:'',link:'',sysid:'',parameter:'',syscode:'',type:'',status:'1',icon_url:'',icon:''};
        result={mode:'new',fields:Object.keys(values).map(name=>({name,type:'varchar',maxLength:80})),values,version:'',canSave:true};break;
      }
      case 'catalogDescription.preview':result={fingerprint:'catalog-description-fixture',
        sql:'CREATE TABLE [dbo].['+request.payload.design.table+'] (...);',
        files:[{path:'App_Data\\Controllers\\Dir\\'+request.payload.design.source.controller+'.xml',text:'<controller />'}]};break;
      case 'catalogDescription.apply':result=['C:\\Fixture\\App_Data\\Controllers\\Dir\\zdm_diem_danh_gia_theo_he_so.xml'];break;
      case 'workspace.folder':result='C:\\Fixture\\Output';break;
      case 'templateStudio.open':result={project:{projectName:'Studio mẫu',templates:[
        {templateCode:'registered',execute:'dbo.usp_Registered',sampleJson:'{}',tables:[]},
        {templateCode:'existing',execute:'dbo.usp_Existing',sampleJson:'{}',tables:[
          {name:'#Master',from:'$',mode:'object',columns:[{name:'Code',path:'$.code',type:'varchar(16)',key:false,required:false}]},
        ]},
      ]},skippedTemplates:['invalid.json']};break;
      case 'templateStudio.save':{
        (window.__studioSaves??=[]).push(request.payload.project);
        const issues=request.payload.project.templates.flatMap(template=>template.tables.flatMap((table,tableIndex)=>
          table.columns.flatMap((field,fieldIndex)=>field.defaultJson==='{'?[{
            templateCode:template.templateCode,table:table.name,tableIndex,field:field.name,fieldIndex,
            location:'Default (JSON)',message:'Default/constant: Invalid JSON.',
          }]:[])));
        result={valid:issues.length===0,issues};break;
      }
      case 'templateStudio.renameTable':
      case 'templateStudio.renameField':{
        const {template,table,field,name}=request.payload;
        if(field===undefined) template.tables[table].name=name;
        else template.tables[table].columns[field].name=name;
        result=template;break;
      }
      case 'sourceCollection.versions':result=[
        {group:'FBO-FBI',version:'ZZZCHÉP SOURCE VÀO FOLDER App_Data',roots:['C:\\SourceCollection\\FBO-FBI\\ZZZCHÉP SOURCE']},
        {group:'FBO-FBI',version:'FBOR2SP22.9',roots:['C:\\SourceCollection\\FBO-FBI\\FBOR2SP22.9']},
        {group:'FAW',version:'FAW22',roots:['C:\\SourceCollection\\FAW\\FAW22']},
        {group:'FBO-FBI',version:'FBOR2SP24.2.2',roots:['C:\\SourceCollection\\FBO-FBI\\FBOR2SP24.2.2']},
        {group:'FAW',version:'FAW30',roots:['C:\\SourceCollection\\FAW\\FAW30']},
        {group:'FBO-FBI',version:'FBOR2SP23',roots:['C:\\SourceCollection\\FBO-FBI\\FBOR2SP23']},
      ];break;
      case 'sourceCollection.default.read':result='';break;
      case 'sourceCollection.default.save':result=request.payload.version;break;
      case 'catalogSource.export':result={folder:'C:\\Fixture\\Output\\CatalogDemo',files:['CatalogDemo.xml']};break;
      case 'project.menu.insertScript':result="-- dbo.wcommand\r\nINSERT INTO dbo.wcommand ([wmenu_id]) VALUES ('menu1')\r\nGO\r\n\r\n-- dbo.command\r\nINSERT INTO dbo.command ([menu_id]) VALUES ('Dir')\r\nGO\r\n";break;
      case 'project.employeeTabs':result=[{sysId:'hrPIGeneralInformation',caption:'Thông tin chung'},{sysId:'hrPIPassportVisa',caption:'Hộ chiếu, thị thực'}];break;
      case 'project.controller':{
        const link=request.payload.link;
        const files=controllerFiles(link==='hrEmployeeInformation.aspx'?'hrPIGeneralInformation':link.replace(/\.aspx$/,''));
        if(link==='hrEmployeeInformation.aspx') files[0]=projectRoot+'\\Main\\hrEmployeeInformation.aspx';
        result={file:files[0],files};break;
      }
      case 'project.controllerDiagnostics':{
        const text=request.payload.buffers?.[controllerDiagnosticPath]??'';
        const belongs=(request.payload.files??[]).some(file=>file.toLowerCase()===controllerDiagnosticPath.toLowerCase());
        result={diagnostics:belongs&&!text.includes('<!ENTITY MissingEntity ')?[{
          path:controllerDiagnosticPath,line:2,column:3,severity:'error',
          message:"Reference to undeclared entity 'MissingEntity'.",
        }]:[]};break;
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
      case 'workspace.search':
        result=request.payload.query==='needle' && request.payload.firstPerFile
          ? [{path:projectRoot+(request.payload.path.endsWith('\\Filter')
            ? '\\App_Data\\Controllers\\Filter\\fixture.xml' : '\\fixture.xml'),line:2,column:3,preview:'<root>needle</root>'}] : [];
        break;
      case 'workspace.reveal':result=true;break;
      case 'document.openMany':{
        const entityFixture=window.__entityFixture||new URLSearchParams(location.search).has('entityFixture');
        result=window.__bulkOpenCount
          ? Array.from({length:window.__bulkOpenCount},(_,index)=>doc('open-'+(++serial),'<root>bulk-'+index+'</root>','bulk-'+index+'.xml',projectRoot+'\\bulk-'+index+'.xml'))
          : entityFixture
            ? [doc('open-'+(++serial),entityMainText,'entity-main.xml',entityMainPath)]
            : [doc('open-'+(++serial),'<root>Tiếng Việt</root>')];break;
      }
      case 'document.open':{
        const requested=request.payload.path;
        const entityFixture=window.__entityFixture||new URLSearchParams(location.search).has('entityFixture');
        const filePath=requested??(entityFixture?entityMainPath:'C:\\Fixture\\fixture.xml');
        const text=filePath===entityBodyPath?entityBodyText:filePath===entityDtdPath?entityDtdText:
          entityFixture&&(filePath===entityMainPath||filePath.endsWith('fixture.xml'))?entityMainText:
          filePath.toLowerCase()===controllerDiagnosticPath.toLowerCase()?'<root>\r\n  &MissingEntity;\r\n</root>':
          filePath.endsWith('nhanvien.xml')?'<root>\r\n<fields/>\r\n<toolbar/>\r\n<command><text><![CDATA[SELECT 42 AS x]]></text></command>\r\n<query>SELECT 43 AS y</query>\r\n<script><![CDATA[const z = 1;]]></script>\r\n</root>':'<root>Tiếng Việt</root>';
        result=doc('open-'+(++serial),text,filePath.split(/[\\/]/).at(-1),filePath);break;
      }
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
      case 'window.close':case 'document.close':case 'recovery.save':case 'settings.save':case 'operation.cancel':case 'profiler.stop':result=true;break;
      case 'sql.connect':result={id:'target-'+(++serial),server:request.payload.server,database:request.payload.database};break;
      case 'sql.execute':
        if(window.__failSql){error={code:'sql_error',message:'Lỗi SQL fixture'};break;}
        if(request.payload.sql.includes('INVALID_SEQUENCE_FIXTURE')){
          const message="Unknown object type 'SEQUENCE' used in a CREATE, DROP, or ALTER statement.";
          error={code:'failed',message,details:[{line:1,message,procedure:''}]};break;
        }
        if(request.payload.sql.includes('CREATE TABLE dbo.BangMoiFixture')) window.__createdTableTarget=request.payload.targetId;
        if(/\b(?:CREATE|ALTER)\s+(?:PROC|PROCEDURE)\b/i.test(request.payload.sql)){
          result={tables:[],messages:[],durationMs:8,truncated:false};break;
        }
        result={tables:[{columns:['Mã','Tên','Giá trị'],rows:Array.from({length:5000},(_,i)=>[String(i),'Tiếng Việt '+i,i===0?null:'1000000000.123456789'])},{columns:['Bảng phụ'],rows:[['Dòng bảng hai']]}],messages:['Query fixture'],durationMs:12,truncated:true};break;
      case 'sql.complete':
        result=request.payload.beforeCaret?.endsWith('sel')&&location.search.includes('ghostFixture')
          ?[{text:'SELECT',insertText:'SELECT',description:'Fixture SQL',isSnippet:false}]
          :request.payload.text===''?['COUNT','AS','IS NOT NULL','AND','BETWEEN','DESC','ASC'].map(text=>({text,isSnippet:false})):[];
        break;
      case 'javascript.complete':result=[];break;
      case 'xml.complete':{
        const {text,offset}=request.payload;
        const prefix=text.slice(0,offset);
        const fixtures=[
          ['<loo','<lookup',location.search.includes('ghostFixture')
            ?'<items style="AutoComplete" controller="{{0-controller}}" />'
            :'<lookup field="{{0-ma_demo}}"/>','xml',true],
          ['Dec','Decimal','Decimal','xml',false],
          ['getI','getItem',"getItem('{{0-ma_demo}}')",'javascript',true],
          ['@@us','@@userID','@@userID','sql',false],
          ['backg','background-color','background-color','css',false],
        ];
        const partitionPrefix=prefix.match(/@@[\w$]*$/)?.[0];
        const partition='@@prime$partition$current';
        const fixture=fixtures.find(([suffix])=>prefix.endsWith(suffix))??
          (partitionPrefix&&partition.startsWith(partitionPrefix)?[partitionPrefix,partition,partition,'sql',false]:null);
        if(!fixture){result=null;break;}
        const [suffix,label,insertText,language,isSnippet]=fixture;
        const tail=text.slice(offset).match(/^[\w$-]*/)?.[0]??'';
        result={start:offset-suffix.length,end:offset+tail.length,language,
          suggestions:[{text:label,insertText,description:'Fixture IntelliSense',isSnippet}]};
        break;
      }
      case 'xml.validate':case 'xml.viewWidth':result=null;break;
      case 'xml.include':{
        if(request.payload.text.includes('&Job.Nested.Key.a;')){
          window.__nestedEntityPayload=request.payload;
          result={path:entityDtdPath,line:2,column:31,name:'Job.Nested.Key.a',start:entityDtdText.indexOf(entityChildValue),length:entityChildValue.length};
        }else if(request.payload.text.includes('&UnitBeforeInsert;')){
          result={path:entityBodyPath,line:1,column:1,name:'UnitBeforeInsert',start:0,length:entityBodyText.length};
        }else result=null;
        break;
      }
      case 'xml.diagnostics':{
        const missing=request.payload.text.includes('&MissingEntity;')&&
          !request.payload.text.includes('<!ENTITY MissingEntity ');
        result=missing
          ? {diagnostic:{line:2,column:3,message:"Reference to undeclared entity 'MissingEntity'."},warnings:[]}
          : {diagnostic:null,warnings:request.payload.text.includes('SELECT 42 AS x')?
            [{line:2,column:2,message:"Field 'missing' đã khai báo nhưng chưa có trong <views>."}]:[]};
        break;
      }
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
      case 'table.catalog':result={columns:['Schema','Name'],rows:[
        ['dbo','DanhMuc_Archive'],['dbo','DanhMuc'],['hr','NhanVien'],
        ...(window.__createdTableTarget===request.payload.targetId?[['dbo','BangMoiFixture']]:[]),
      ]};break;
      case 'table.structure':result={columns:['Cột','Kiểu dữ liệu','Khóa'],rows:[['code','INT','PK · PK_DanhMuc'],...(!window.__columnDropped?[['label','NVARCHAR(20)','']]:[])]};break;
      case 'table.metadata':result={columns:[{name:'code',sqlType:'INT',key:'PK · PK_DanhMuc',primaryKey:true,nullable:false,identity:false,computed:false,generated:0,writable:true},...(!window.__columnDropped?[{name:'label',sqlType:'NVARCHAR(20)',key:'',primaryKey:false,nullable:true,identity:false,computed:false,generated:0,writable:true}]:[])]};break;
      case 'table.addScript':result='ALTER TABLE [dbo].[DanhMuc] ADD [label] NVARCHAR(20) NULL;';break;
      case 'table.alterScript':result='ALTER TABLE [dbo].[DanhMuc] ALTER COLUMN [label] NVARCHAR(20) NULL;';break;
      case 'table.script':result=request.payload.action==='alter'?'ALTER TABLE [dbo].[DanhMuc] ALTER COLUMN [label] NVARCHAR(20) NULL;':request.payload.action==='drop'?'ALTER TABLE [dbo].[DanhMuc] DROP COLUMN [label];':request.payload.action==='create'?'CREATE TABLE [dbo].[DanhMuc] ([code] INT);':request.payload.action==='log'?'SELECT [label] FROM sys.columns;':request.payload.action==='addmanual'?'ALTER TABLE [dbo].[DanhMuc] ADD [new_column] NVARCHAR(16) NULL;':'ALTER TABLE [dbo].[DanhMuc] ADD [label] NVARCHAR(20) NULL;';break;
      case 'table.write':result=1;break;
      case 'table.writeBatch':result={conflict:request.payload.rows.some(row=>row.values.includes('duplicate-fixture'))?0:null};break;
      case 'table.dropColumns':window.__columnDropped=true;result=true;break;
      case 'table.createScript':result='CREATE TABLE [dbo].[DanhMuc] ([code] INT);';break;
      case 'table.view': {
        const offset=request.payload.offset??0;
        if(window.__pagedTable && request.payload.table==='BangMoiFixture'){
          const rows=Array.from({length:Math.min(200,1201-offset)},(_,index)=>[String(offset+index+1),'Dòng mẫu']);
          result={data:{columns:['code','label'],rows},snapshotId:'fixture-page-'+offset,hasMore:offset+rows.length<1201};
        } else {
          result={data:{columns:['code','label'],rows:request.payload.where?.includes("LIKE N'%ab%'")?[]:[['1','x'],['2','y']]},snapshotId:'fixture-preview'};
        }
        break;
      }
      case 'table.insertScript':result='INSERT INTO [dbo].[DanhMuc] ([code]) VALUES (1);';break;
      case 'profiler.defaults':result={filter:'Fixture',profilerPath:'C:\\Fixture\\PROFILER.EXE',authentication:'SQL Server · profile'};break;
      case 'file.pick':result='C:\\CustomSSMS\\PROFILER.EXE';break;
      case 'profiler.start':result={engine:'ssms18'};break;
      case 'profiler.read':result={events:window.__traceBatch.splice(0),dropped:0,truncated:false};break;
      case 'project.discover':result=[{name:'Project Fixture',folder:'C:\\Fixture\\Application',configPath:'C:\\Fixture\\Application\\web.config'}];break;
      case 'project.sync':result=true;break;
      case 'project.standard.sync':{
        settings.standardCatalogUser=request.payload.currentUser;
        result={currentUser:settings.standardCatalogUser,summary:{added:0,updated:0,unchanged:2,skipped:0},projects:[fixtureProject,otherProject]};
        break;
      }
      case 'requestTracking.info':result={legacyAvailable:true,defaultUser:settings.standardCatalogUser??''};break;
      case 'requestTracking.list':result={columns:['Khách hàng','Người xử lý','Số yêu cầu'],rows:[
        ['Yêu cầu mẫu A','DEV03','fixture-a'],['Yêu cầu mẫu B','DEV03','fixture-b'],
      ]};break;
      case 'requestTracking.categories':{
        const {requestId}=request.payload;
        if(window.__requestCategoryError){
          error={code:'fixture',message:'Nguồn thử chưa cấu hình Category.'};
          break;
        }
        const columns=['Mã đầu mục','Đầu mục','Chọn','Số lượng','Lập trình','Tester'];
        const rows=requestId==='fixture-a' ? [['01','Bổ sung chứng từ','1','2','DEV03','QA01']]
          : [['02','Thêm báo cáo','0','0','','']];
        result={columns,rows};
        break;
      }
      case 'requestTracking.statistics':{
        const months=Array.from({length:12},(_,index)=>[`T${index+1}`,`SL T${index+1}`]).flat();
        const makeRow=(code,jan,other,totals)=>[code,...jan,...other,...Array(20).fill('0'),...totals];
        result={columns:['Nhân viên',...months,'Tổng YC','Tổng SL','TB YC','TB SL'],rows:[
          makeRow('DEV01',['2','1.2500'],['1','0.5000'],['3','1.7500','1.50','0.88']),
          makeRow('DEV02',['4','2.3750'],[null,null],['4','2.3750','2.00','1.19']),
          makeRow('DEV03',['5','7.5000'],['0','0.0000'],['5','7.5000','2.50','3.75']),
        ]};
        break;
      }
      case 'project.list':result=[fixtureProject,otherProject];break;
      case 'catalog.references':result={columns:['Schema','Table','Column','Type'],rows:[]};break;
      case 'catalog.load':result={signature:'fixture-schema',columns:[{name:'code',type:'nvarchar',nullable:false,identity:false,computed:false,generated:0,managed:false,hasDefault:false},{name:'label',type:'nvarchar',nullable:true,identity:false,computed:false,generated:0,managed:false,hasDefault:false},...['user_id0','user_id2','datetime0','datetime2'].map(name=>({name,type:name.startsWith('user')?'int':'datetime',nullable:true,identity:false,computed:false,generated:0,managed:true,hasDefault:false}))],data:{columns:['code','label','user_id0','user_id2','datetime0','datetime2'],rows:[['one',null,null,null,null,null],[':null','literal',null,null,null,null]]}};break;
      case 'catalog.clipboard.write':result=request.payload.rows.map(r=>r.map(v=>'"'+(v??'').replaceAll('"','""')+'"').join('\t')).join('\r\n');break;
      case 'text.export':result=request.payload.name;break;
      default:error={code:'fixture',message:'Fixture không hỗ trợ '+request.method};
    }
    const reply=()=>listeners.forEach(fn=>fn({data:{protocolVersion:1,requestId:request.requestId,result,error}}));
    if (request.method==='workspace.search'&&window.__holdSearchResults) {
      window.__releaseSearchResults=reply;
      setTimeout(()=>listeners.forEach(fn=>fn({data:{type:'workspace.search.progress',requestId:request.requestId,hits:result}})),10);
    } else if (request.method==='project.targets'&&window.__holdProjectTargets) window.__releaseProjectTargets=reply;
    else if (request.method==='project.menu.resources'&&window.__holdMenuResources) window.__releaseMenuResources=reply;
    else if (request.method==='table.catalog'&&window.__holdTableCatalog) window.__releaseTableCatalog=reply;
    else if (request.method==='templateStudio.save'&&window.__holdStudioSave) window.__releaseStudioSave=reply;
    else if (request.method==='requestTracking.categories'&&request.payload.requestId==='fixture-a'&&window.__holdRequestCategories) window.__releaseRequestCategories=reply;
    else setTimeout(reply,10);
  }};
  window.chrome=window.chrome??{};window.chrome.webview=native;
});
const out='artifacts/webview-ui';mkdirSync(out,{recursive:true});
let checks=0;const check=(value,name)=>{if(!value)throw Error('FAIL '+name);checks++;console.log('PASS '+name);};
const waitForInputValue=async(locator,value)=>{
  for(let attempt=0;attempt<50;attempt++){
    if(await locator.inputValue()===value)return;
    await page.waitForTimeout(100);
  }
  throw Error(`Timed out waiting for input value ${value}`);
};
async function testRequestTracking(){
  await page.getByRole('button',{name:'DEV01',exact:true}).click();
  await page.getByLabel('Người sử dụng',{exact:true}).fill(' DEV02 ');
  await page.getByRole('button',{name:'Đồng bộ',exact:true}).click();
  await page.getByRole('button',{name:'DEV02',exact:true}).waitFor();
  await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
  await page.locator('dialog').waitFor({state:'detached'});
  await page.getByRole('button',{name:'Mở ô lệnh QLYC',exact:true}).click();
  await page.getByRole('dialog',{name:'Lệnh',exact:true}).getByLabel('Nhập lệnh').fill('@@');
  await page.getByRole('button',{name:'Mở',exact:true}).click();
  const requests=page.getByRole('dialog',{name:'QLYC',exact:true});
  const assignee=requests.getByLabel('Người xử lý',{exact:true});
  const source=requests.getByRole('button',{name:'Kết nối QLYC',exact:true});
  await waitForInputValue(assignee,'DEV02');
  check(true,'QLYC defaults to the latest assignee entered during project sync');
  await source.click();
  await page.getByRole('option',{name:'Fixture_App · .',exact:true}).click();
  await assignee.fill('DEV03');
  await page.evaluate(()=>{window.__holdRequestCategories=true;});
  await requests.getByRole('button',{name:'Tải dữ liệu',exact:true}).click();
  await requests.getByRole('cell',{name:'Yêu cầu mẫu A',exact:true}).waitFor();
  check(await page.evaluate(()=>window.__nativeCalls.some(call=>call.method==='requestTracking.list'&&
    call.payload.targetId==='project-app'&&call.payload.user==='DEV03'&&!call.payload.legacy)),
    'QLYC list uses the manually edited assignee and chosen connection');

  await page.waitForFunction(()=>typeof window.__releaseRequestCategories==='function');
  await requests.getByRole('cell',{name:'Yêu cầu mẫu B',exact:true}).click();
  await requests.getByText('Thêm báo cáo',{exact:true}).waitFor();
  await page.evaluate(()=>{window.__holdRequestCategories=false;window.__releaseRequestCategories();});
  check(await requests.getByText('Thêm báo cáo',{exact:true}).isVisible()&&
    await requests.getByText('Bổ sung chứng từ',{exact:true}).count()===0,
    'Category follows the selected request even when an earlier request completes late');
  await requests.getByRole('cell',{name:'Yêu cầu mẫu A',exact:true}).click();
  const categoryRow=requests.getByRole('row').filter({hasText:'Bổ sung chứng từ'});
  await categoryRow.waitFor();
  check(await categoryRow.getByRole('checkbox').isChecked()&&await categoryRow.getByRole('checkbox').isDisabled()&&
    await categoryRow.getByRole('cell',{name:'2',exact:true}).isVisible()&&
    await categoryRow.getByRole('cell',{name:'DEV03',exact:true}).isVisible()&&
    await categoryRow.getByRole('cell',{name:'QA01',exact:true}).isVisible(),
    'Category displays the selected flag, quantity, programmer and tester for the current request');
  await requests.screenshot({path:out+'/qlyc-category.png',animations:'disabled'});
  await page.setViewportSize({width:375,height:700});
  check(await requests.evaluate(dialog=>dialog.getBoundingClientRect().right<=innerWidth&&
    document.documentElement.scrollWidth<=innerWidth),'QLYC request and category panels fit the 375px viewport');
  await requests.screenshot({path:out+'/qlyc-category-narrow.png',animations:'disabled'});
  await page.setViewportSize({width:375,height:320});
  await categoryRow.scrollIntoViewIfNeeded();
  check(await categoryRow.evaluate(row=>{
    const bounds=row.getBoundingClientRect();
    const body=row.closest('.request-tracking-body');
    return body.scrollTop>0&&bounds.top>=body.getBoundingClientRect().top&&bounds.bottom<=innerHeight;
  }),'Category remains reachable by scrolling the dialog in a short window');
  await page.setViewportSize({width:1440,height:1000});
  await page.evaluate(()=>{window.__requestCategoryError=true;});
  await requests.getByRole('button',{name:'Tải dữ liệu',exact:true}).click();
  await requests.getByText('Nguồn thử chưa cấu hình Category.',{exact:false}).waitFor();
  check(await requests.getByRole('cell',{name:'Yêu cầu mẫu A',exact:true}).isVisible(),
    'A Category error leaves the request list usable');
  await page.evaluate(()=>{window.__requestCategoryError=false;});
  await requests.getByRole('button',{name:'Tải dữ liệu',exact:true}).click();
  await categoryRow.waitFor();
  check(true,'Reloading the same request retries its Category query');

  await requests.getByRole('button',{name:'View All',exact:true}).click();
  const totalRows=requests.locator('.request-department-total');
  const lthoTotal=totalRows.filter({hasText:'Tổng LTHO'});
  await lthoTotal.waitFor();
  check(await source.count()===0,'Statistics hides the connection dropdown');
  check(await page.evaluate(()=>window.__nativeCalls.some(call=>call.method==='requestTracking.statistics'&&
    call.payload.targetId==='project-app'&&!call.payload.legacy)),'Statistics keeps the connection selected on the request screen');
  const numbers=async (row)=>(await row.getByRole('cell').allTextContents())
    .flatMap(value=>/^\s*\d+(?:[.,]\d+)?\s*$/.test(value)?[Number(value.trim().replace(',','.'))]:[]);
  const ltho=await numbers(lthoTotal);
  check(ltho[0]===6&&ltho[1]===3.625&&ltho[2]===1&&ltho[3]===0.5&&ltho[24]===7&&ltho[25]===4.125,
    'Department totals sum request counts and decimal quantities for each month and the year');
  await requests.getByRole('button',{name:'Mã bộ phận',exact:true}).click();
  await page.getByRole('option',{name:'Tất cả bộ phận',exact:true}).click();
  const fsdTotal=totalRows.filter({hasText:'Tổng FSD'});
  await fsdTotal.waitFor();
  check(await totalRows.count()===2&&(await numbers(fsdTotal))[1]===7.5,
    'All departments display exactly one independent subtotal per group');
  await requests.getByRole('checkbox',{name:'Số lượng',exact:true}).uncheck();
  check((await numbers(lthoTotal)).length===14&&(await numbers(lthoTotal))[12]===7,
    'Department totals follow the quantity visibility setting');
  await requests.getByRole('checkbox',{name:'Số yêu cầu',exact:true}).uncheck();
  check((await numbers(lthoTotal)).length===0&&await lthoTotal.isVisible(),
    'A department subtotal remains aligned with the identity columns when both metrics are hidden');
  await requests.getByRole('checkbox',{name:'Số yêu cầu',exact:true}).check();
  await requests.getByRole('checkbox',{name:'Số lượng',exact:true}).check();
  const yearlyQuantity=lthoTotal.getByRole('cell',{name:'4.1250',exact:true});
  await yearlyQuantity.click();
  await page.keyboard.press('Control+c');
  await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='clipboard.write'&&call.payload.text==='4.1250'));
  check(true,'A department subtotal can be selected and copied through the statistics grid');
  await requests.screenshot({path:out+'/qlyc-statistics.png',animations:'disabled'});
  await page.setViewportSize({width:375,height:700});
  check(await requests.evaluate(dialog=>dialog.getBoundingClientRect().right<=innerWidth&&
    document.documentElement.scrollWidth<=innerWidth),'QLYC statistics table scrolls without widening the 375px viewport');
  await requests.screenshot({path:out+'/qlyc-statistics-narrow.png',animations:'disabled'});
  await page.setViewportSize({width:1440,height:1000});
  await requests.getByLabel('Năm',{exact:true}).fill('2025');
  await requests.getByRole('button',{name:'Xem thống kê',exact:true}).click();
  await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='requestTracking.statistics'&&
    call.payload.year===2025&&call.payload.targetId==='project-app'&&!call.payload.legacy));
  await requests.getByRole('button',{name:'Quay lại QLYC',exact:true}).click();
  check(await source.isVisible()&&await assignee.inputValue()==='DEV03',
    'Returning from statistics restores request controls and retains the manual assignee');
}
async function testProfiler(){
  await page.getByRole('button',{name:'SQL Profiler',exact:true}).click();
  const profiler=page.getByRole('tabpanel',{name:'SQL Profiler'});
  check(await profiler.isVisible() && await page.locator('.editor-toolbar').getByRole('tab',{name:'SQL Profiler'}).isVisible()
    && !(await page.locator('dialog[open]').count()),'Profiler opens on the editor toolbar without a dialog');
  await profiler.getByText(/Đang thu thập/).waitFor();
  check(await profiler.getByRole('textbox',{name:'Đường dẫn Profiler'}).inputValue()==='C:\\Fixture\\PROFILER.EXE' &&
    await profiler.getByRole('textbox',{name:'Bộ lọc database'}).inputValue()==='Fixture' &&
    await profiler.locator('input[type=password]').inputValue()==='',
    'Profiler prefills saved executable and database scope without exposing target credentials');
  check(await profiler.getByRole('button',{name:/^(Profiler engine|Lưu trace|Mở trace)$/}).count()===0 &&
    await profiler.getByRole('textbox',{name:'Đường dẫn template'}).count()===0,
    'Profiler hides the engine selector, template field and trace file buttons');
  const executions=await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='sql.execute').length);
  await page.keyboard.press('F5');
  check(await page.evaluate(before=>window.__nativeCalls.filter(c=>c.method==='sql.execute').length===before,executions),'Profiler tab shortcuts do not run the hidden editor query');
  const longProfilerSql=Array.from({length:112},(_,i)=>i===105
    ? "SET @warning = N'phần đầu'\0 + N' phần cuối dòng 106'"
    : `-- dòng ${i+1}`).join('\r\n');
  const normalizedLongProfilerSql=longProfilerSql.replaceAll('\0','');
  await page.evaluate(longSql=>window.__traceBatch.push(...Array.from({length:60},(_,i)=>({time:String(i),event:'Event '+i,database:'Fixture_App',login:'profile',application:i===0?'.Net SqlClient Data Provider':'Fixture.Client',durationMs:1,reads:0,text:i===0?longSql:'SELECT '+i}))),longProfilerSql);
  check(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='profiler.start').length===1 &&
    window.__nativeCalls.some(c=>c.method==='profiler.start' && c.payload.engine==='auto' &&
      c.payload.useTargetCredentials===false && c.payload.targetId && !Object.hasOwn(c.payload,'password'))),
    'Opening Profiler automatically uses the dedicated login without sending its password through the bridge');
  await page.waitForFunction(()=>document.querySelector('.trace-sql')?.value==='SELECT 59');
  check(await profiler.locator('.trace-sql').evaluate(element=>element.clientHeight>=110),'Profiler SQL area has room for more lines');
  const traceGrid=profiler.locator('.data-grid');
  check(await traceGrid.getByRole('columnheader',{name:'ID',exact:true}).isVisible(),'Profiler displays the ID column');
  check(await traceGrid.evaluate(el=>el.scrollTop>0),'Profiler follows the newest live event');
  await traceGrid.evaluate(el=>el.scrollTop=0);
  const firstTraceCell=traceGrid.locator('[data-grid-row="0"][data-grid-column="1"]');
  const lastTraceCell=traceGrid.locator('[data-grid-row="1"][data-grid-column="2"]');
  await firstTraceCell.waitFor();
  check(await traceGrid.locator('[data-grid-row="0"][data-grid-column="4"]').textContent()==='.Net SqlClient Data Provider',
    'Profiler maps ID to the captured application name');
  const from=await firstTraceCell.boundingBox(),to=await lastTraceCell.boundingBox();
  await page.mouse.move(from.x+from.width/2,from.y+from.height/2);
  await page.mouse.down();await page.mouse.move(to.x+to.width/2,to.y+to.height/2,{steps:8});await page.mouse.up();
  await page.keyboard.press('Control+c');
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='clipboard.write'&&c.payload.text==='Event 0\tFixture_App\r\nEvent 1\tFixture_App'));
  check(await traceGrid.locator('td.grid-selected').count()===4&&!(await traceGrid.getByRole('button',{name:/Sao chép/}).count()),'Profiler drag copies a rectangular TSV range without copy buttons');
  await page.evaluate(()=>window.__traceBatch.push({time:'60',event:'Event 60',database:'Fixture_App',login:'profile',durationMs:1,reads:0,text:'SELECT 60'}));
  await profiler.getByText(/61 sự kiện/).waitFor();
  const selectedLongSql=await profiler.locator('.trace-sql').inputValue();
  check(selectedLongSql.replaceAll('\0','').replaceAll('\r\n','\n')===normalizedLongProfilerSql.replaceAll('\r\n','\n')&&
    await traceGrid.locator('td.grid-selected').count()===4,'Profiler preserves the selected row and range while live events arrive');
  await profiler.getByRole('button',{name:'Sao chép SQL',exact:true}).click();
  await page.waitForFunction(sql=>window.__nativeCalls.some(c=>c.method==='clipboard.write'&&c.payload.text===sql),normalizedLongProfilerSql);
  await profiler.locator('.trace-sql').focus();
  const fullCopiesBefore=await page.evaluate(sql=>window.__nativeCalls.filter(c=>c.method==='clipboard.write'&&c.payload.text===sql).length,normalizedLongProfilerSql);
  await page.keyboard.press('Control+a');
  await page.keyboard.press('Control+c');
  await page.waitForFunction(({sql,before})=>window.__nativeCalls.filter(c=>c.method==='clipboard.write'&&c.payload.text===sql).length>before,
    {sql:normalizedLongProfilerSql,before:fullCopiesBefore});
  check((await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='clipboard.write').at(-1)?.payload.text.split(/\r?\n/).length))===112,
    'Profiler Ctrl+A and Ctrl+C copy all 112 SQL lines across an embedded NUL separator');
  await profiler.locator('.trace-sql').evaluate(el=>el.setSelectionRange(0,6));
  await page.keyboard.press('Control+c');
  await page.waitForFunction(text=>window.__nativeCalls.some(c=>c.method==='clipboard.write'&&c.payload.text===text),normalizedLongProfilerSql.slice(0,6));
  check(true,'Profiler copies full SQL by button and selected text with Ctrl+C');
  await profiler.getByRole('button',{name:'Theo dõi cuối lưới'}).click();
  await page.waitForFunction(()=>document.querySelector('.trace-sql')?.value==='SELECT 60');
  await profiler.getByRole('button',{name:'Dừng',exact:true}).click();
  await profiler.getByText(/Đã dừng/).waitFor();
  await page.evaluate(()=>window.__traceBatch.push({time:'61',event:'Event 61',database:'Fixture_App',login:'profile',durationMs:1,reads:0,text:'SELECT 61'}));
  await profiler.getByRole('button',{name:'Chạy',exact:true}).click();
  await profiler.getByText(/62 sự kiện/).waitFor();
  check(await profiler.locator('.data-grid tbody tr').count()>1 &&
    await profiler.locator('.trace-sql').inputValue()==='SELECT 61' &&
    await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='profiler.start').length===2),
    'Profiler stop and restart appends events without clearing captured data');
  const failedSql="SELECT zczc, N'Nội dung SQL đầy đủ không bị cắt' FROM dbo.dmct WHERE ma_ct = 'PN1'";
  await page.evaluate(sql=>window.__traceBatch.push(
    {sequence:63,time:'2026-11-26T10:49:22.523',event:'User Error Message',database:'Fixture_App',login:'profile',session:'71',durationMs:0,reads:0,severity:16,text:"Invalid column name 'zczc'."},
    {sequence:64,time:'2026-11-26T10:49:22.523',event:'SQL:BatchStarting',database:'Fixture_App',login:'profile',session:'71',durationMs:0,reads:0,text:sql},
  ),failedSql);
  await profiler.getByText(/64 sự kiện/).waitFor();
  const failedCell=traceGrid.locator('td').filter({hasText:failedSql}).last();
  await failedCell.waitFor();
  const failedRow=failedCell.locator('xpath=..');
  const errorRow=failedRow.locator('xpath=following-sibling::tr[1]');
  check((await failedRow.getAttribute('class'))?.includes('profiler-error-source-row') &&
    (await errorRow.getAttribute('class'))?.includes('profiler-error-message-row') &&
    (await errorRow.textContent()).includes("Invalid column name 'zczc'."),
    'Profiler gives failed SQL a red background and the following error message red text');
  const copiesBeforeClick=await page.evaluate(sql=>window.__nativeCalls.filter(c=>c.method==='clipboard.write'&&c.payload.text===sql).length,failedSql);
  await failedCell.click();
  await page.waitForTimeout(150);
  check(await page.evaluate(({sql,before})=>window.__nativeCalls.filter(c=>c.method==='clipboard.write'&&c.payload.text===sql).length===before,{sql:failedSql,before:copiesBeforeClick}),
    'A single click selects SQL without copying it');
  await failedCell.dblclick();
  await page.waitForFunction(sql=>window.__nativeCalls.some(c=>c.method==='clipboard.write'&&c.payload.text===sql),failedSql);
  await page.getByText('Đã sao chép SQL.',{exact:true}).waitFor();
  check(await profiler.locator('.trace-sql').inputValue()===failedSql,
    'Double-clicking the SQL cell copies the full statement and shows a success notice');
  await page.getByRole('button',{name:'Đóng thông báo'}).click();
  await profiler.getByRole('button',{name:'Theo dõi cuối lưới'}).click();
  const warningSql='SELECT SUM(nullable_value) FROM dbo.WarningFixture';
  const warningText='Warning: Null value is eliminated by an aggregate or other SET operation.';
  await page.evaluate(({sql,warning})=>window.__traceBatch.push(
    {sequence:65,time:'2026-11-26T11:03:38.300',event:'User Error Message',database:'Fixture_App',login:'profile',session:'72',durationMs:0,reads:0,severity:10,text:warning},
    {sequence:66,time:'2026-11-26T11:03:38.300',event:'SQL:BatchStarting',database:'Fixture_App',login:'profile',session:'72',durationMs:0,reads:0,text:sql},
    {sequence:67,time:'2026-11-26T11:03:38.400',event:'SQL:BatchCompleted',database:'Fixture_App',login:'profile',session:'72',durationMs:1,reads:1,text:sql},
  ),{sql:warningSql,warning:warningText});
  await profiler.getByText(/67 sự kiện/).waitFor();
  const warningRow=traceGrid.locator('tr').filter({hasText:warningText}).last();
  const warningSqlRow=traceGrid.locator('tr').filter({hasText:warningSql}).last();
  await warningRow.waitFor();await warningSqlRow.waitFor();
  check(!(await warningRow.getAttribute('class')??'').includes('profiler-error-') &&
    !(await warningSqlRow.getAttribute('class')??'').includes('profiler-error-'),
    'Profiler keeps severity 10 warnings and their SQL rows out of the red error style');
  await page.screenshot({path:out+'/profiler-tab-dark.png'});
  const stopsBeforeSwitch=await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='profiler.stop').length);
  await page.locator('.document-tabs').getByRole('tab').first().click();
  check(!(await profiler.isVisible())&&await page.evaluate(before=>window.__nativeCalls.filter(c=>c.method==='profiler.stop').length===before,stopsBeforeSwitch),'Switching editor tabs keeps the Profiler session running');
  await page.getByRole('button',{name:'Đóng SQL Profiler'}).click();
  await page.getByRole('tab',{name:'SQL Profiler'}).waitFor({state:'detached'});
  check(await page.evaluate(()=>window.__nativeCalls.some(c=>c.method==='profiler.stop')),'Closing the Profiler tab stops the session');
}
async function testMenuEditor(functionRow){
  await page.evaluate(()=>{window.__holdMenuResources=true;});
  await functionRow.focus();await functionRow.press('F3');
  const menuPanel=page.locator('#menu-editor-panel');
  await menuPanel.getByLabel('Main').waitFor();
  check(await menuPanel.getByRole('button',{name:'Lưu',exact:true}).count()===1 &&
    await menuPanel.getByRole('checkbox',{name:'Trạng thái'}).isChecked(),
    'ERP menu has one save action and renders status as a checked checkbox');
  await page.waitForFunction(()=>window.__releaseMenuResources);
  check(await menuPanel.getByText('Đang dò controller và file…',{exact:true}).isVisible() &&
    await menuPanel.getByLabel('Main').isEditable(), 'ERP menu can be edited before resources finish loading');
  await page.evaluate(()=>{window.__holdMenuResources=false;window.__releaseMenuResources();});
  await menuPanel.getByRole('button',{name:'hr.NhanVien',exact:true}).waitFor();
  const resourcePanel=menuPanel.getByRole('tabpanel');
  const dirFile=resourcePanel.getByRole('button',{name:/nhanvien\.xml.*hr\.NhanVien/});
  await dirFile.click({button:'right'});
  await page.getByRole('menuitem',{name:'Mở thư mục chứa file',exact:true}).click();
  await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='workspace.reveal'&&
    call.payload.path==='C:\\Fixture\\App_Data\\Controllers\\Dir\\nhanvien.xml'));
  await dirFile.focus();
  await dirFile.press('F2');
  const rename=page.getByRole('dialog',{name:'Đổi tên file',exact:true});
  check(await rename.getByRole('textbox',{name:'Tên mới',exact:true}).inputValue()==='nhanvien.xml',
    'ERP Dir rows show the file table, reveal that file and open rename with F2');
  await rename.getByRole('button',{name:'Hủy',exact:true}).click();
  await menuPanel.getByRole('tab',{name:'Grid (1)',exact:true}).click();
  await resourcePanel.getByRole('button',{name:/nhanvien\.f/}).waitFor();
  await menuPanel.getByRole('tab',{name:'Dir (1)',exact:true}).click();

  await menuPanel.getByRole('button',{name:'Thêm mới',exact:true}).click();
  const parentInput=menuPanel.getByLabel(/^Mã cha/);
  const treeInput=menuPanel.getByLabel(/^Mã cây/);
  await waitForInputValue(treeInput,'03.69.44');
  check(await parentInput.inputValue()==='03.69.02' && await treeInput.inputValue()==='03.69.44',
    'New ERP menu keeps the selected parent and increments the final tree-code suffix');
  await menuPanel.getByRole('button',{name:'Tải lại',exact:true}).click();
  await waitForInputValue(treeInput,'03.69.43');
  await menuPanel.getByRole('button',{name:'hr.NhanVien',exact:true}).waitFor();
  await menuPanel.screenshot({path:out+'/erp-menu-editor.png'});
  const shell=menuPanel.locator('.menu-editor-shell');
  await shell.evaluate(element=>{element.style.width='375px';});
  check(await shell.locator('.menu-editor-body').evaluate(element=>element.scrollWidth===element.clientWidth),
    'ERP menu form and resources fit a 375 px work area');
  await shell.evaluate(element=>{element.style.removeProperty('width');});
  await menuPanel.getByRole('button',{name:'Loại',exact:true}).click();
  await page.getByRole('option',{name:'V',exact:true}).click();
  check(await menuPanel.getByText('Có thay đổi chưa lưu',{exact:true}).isVisible(),
    'ERP menu shows changes in the inspection panel');
  await menuPanel.getByRole('button',{name:'hr.NhanVien',exact:true}).click();
  await page.waitForFunction(()=>['table.metadata','table.view'].every(method=>window.__nativeCalls.some(c=>
    c.method===method&&c.payload.targetId==='project-app'&&c.payload.schema==='hr'&&c.payload.table==='NhanVien')));
  check(true,'ERP menu opens the selected Dir table structure and data on the App target');
  await page.getByRole('button',{name:/^Đóng .*NhanVien/}).click();
  await page.locator('#menu-editor-tab').click();
  await menuPanel.getByRole('button',{name:'Đóng',exact:true}).click();
}
async function testTableRefreshAndNotice(){
  await page.getByRole('button',{name:'Đóng',exact:true}).click();
  await page.locator('[data-ready=true]').waitFor();
  await page.getByRole('button',{name:'SQL mới',exact:true}).click();
  await page.getByRole('button',{name:'Kết nối SQL',exact:true}).click();
  await page.getByRole('textbox',{name:'Server',exact:true}).fill('.');
  await page.getByRole('textbox',{name:'Database',exact:true}).fill('Fixture_App');
  await page.getByRole('button',{name:'Kết nối',exact:true}).click();
  await page.locator('dialog').waitFor({state:'detached'});
  const sqlTab=page.locator('.document-tabs').getByRole('tab',{selected:true});
  const sqlTabName=await sqlTab.getAttribute('aria-label') ?? await sqlTab.textContent();
  const sqlDocument=page.locator('.document-tabs').getByRole('tab',{name:sqlTabName.trim(),exact:true});
  await page.locator('.monaco-editor').first().click({position:{x:160,y:35}});
  await page.keyboard.insertText('CREATE TABLE dbo.BangMoiFixture (code int);');
  await page.keyboard.press('Control+Shift+t');
  const firstTableTab=page.getByRole('tab',{name:/^Cấu trúc và dữ liệu(?: ·.*)?$/});
  await firstTableTab.waitFor();
  const tablePanel=page.locator('#'+await firstTableTab.getAttribute('aria-controls'));
  const tableInput=tablePanel.getByRole('combobox',{name:'FROM',exact:true});
  await tableInput.fill('DanhMuc');
  await tablePanel.getByRole('option',{name:'DanhMuc',exact:true}).click();
  await tablePanel.locator('.data-grid td[title="1"]').waitFor();
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='table.catalog'));
  await sqlDocument.click();
  await page.getByRole('button',{name:'Chạy SQL',exact:true}).click();
  await page.waitForFunction(()=>!!window.__createdTableTarget);
  await firstTableTab.click();
  await tableInput.fill('BangMoiFixture');
  await tablePanel.getByRole('option',{name:'BangMoiFixture',exact:true}).click();
  await tablePanel.locator('.data-grid td[title="1"]').waitFor();
  await tablePanel.getByRole('button',{name:'Chọn tất cả',exact:true}).waitFor();
  await page.waitForFunction(()=>document.querySelector('.profiler-panel:not([hidden]) .table-column-panel-actions button:enabled'));
  check(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='table.view' && c.payload.table==='BangMoiFixture').length===1 &&
    window.__nativeCalls.filter(c=>c.method==='table.catalog').length>=2 &&
    window.__nativeCalls.some(c=>c.method==='table.metadata' && c.payload.table==='BangMoiFixture')),
    'A new table appears after running SQL and keeps its first data snapshot');
  const dataGrid=tablePanel.locator('.table-data-grid');
  const codeCell=dataGrid.locator('td[data-table-cell="0:0"]');
  const labelCell=dataGrid.locator('td[data-table-cell="0:1"]');
  await dataGrid.evaluate(element=>{
    Object.assign(element.style,{flex:'0 0 260px',maxWidth:'260px'});
    element.querySelector('table').style.width='620px';
  });
  await codeCell.click();
  await page.getByRole('button',{name:'Tập lệnh',exact:true}).click();
  await page.getByRole('menuitemcheckbox',{name:'Đóng băng đến cột code',exact:true}).click();
  await page.waitForFunction(()=>document.querySelector('.table-data-grid')?.getAttribute('data-frozen-through')==='code');
  const codeHeader=dataGrid.locator('thead th[data-table-column="code"]');
  const labelHeader=dataGrid.locator('thead th[data-table-column="label"]');
  const beforeScroll=await Promise.all([codeHeader,labelHeader].map(locator=>locator.evaluate(element=>element.getBoundingClientRect().left)));
  await dataGrid.evaluate(element=>{element.scrollLeft=180;});
  await page.waitForTimeout(80);
  const afterScroll=await Promise.all([codeHeader,labelHeader].map(locator=>locator.evaluate(element=>element.getBoundingClientRect().left)));
  check(await codeHeader.evaluate(element=>getComputedStyle(element).position==='sticky') &&
    Math.abs(beforeScroll[0]-afterScroll[0])<2 && beforeScroll[1]-afterScroll[1]>80,
    'Toolbar action freezes every column through the active column while later columns scroll');
  await page.getByRole('button',{name:'Tập lệnh',exact:true}).click();
  await page.getByRole('menuitemcheckbox',{name:'Bỏ đóng băng cột',exact:true}).click();
  await page.waitForFunction(()=>!document.querySelector('.table-data-grid')?.hasAttribute('data-frozen-through'));
  check(await codeHeader.evaluate(element=>getComputedStyle(element).position!=='sticky'),
    'Selecting the toolbar action again removes frozen columns');
  await dataGrid.evaluate(element=>{element.scrollLeft=0;});
  await labelCell.click({button:'right',position:{x:8,y:8}});
  const scriptMenu=page.getByRole('menu',{name:'Loại lệnh SQL'});
  await scriptMenu.waitFor();
  check(await scriptMenu.locator('[role="menuitem"], [role="menuitemcheckbox"]').count()===5 &&
    await scriptMenu.getByRole('menuitemcheckbox',{name:'Đóng băng đến cột label',exact:true}).isVisible() &&
    await scriptMenu.getByRole('menuitem',{name:'Thêm key vào SQL Data'}).isVisible() &&
    await scriptMenu.getByRole('menuitem',{name:'Tạo lệnh tạo bảng',exact:true}).isVisible() &&
    await scriptMenu.getByRole('menuitem',{name:'Tạo lệnh INSERT cho kết quả hiện tại'}).isVisible() &&
    await scriptMenu.getByRole('menuitem',{name:'Tạo lệnh xóa bảng',exact:true}).isVisible(),
    'Right click on a cell opens freeze together with every SQL script command');
  await scriptMenu.getByRole('menuitemcheckbox',{name:'Đóng băng đến cột label',exact:true}).click();
  await page.waitForFunction(()=>document.querySelector('.table-data-grid')?.getAttribute('data-frozen-through')==='label');
  await page.waitForFunction(()=>['code','label'].every(name=>{
    const header=document.querySelector(`.table-data-grid thead th[data-table-column="${name}"]`);
    return header&&getComputedStyle(header).position==='sticky';
  }));
  check(await codeHeader.evaluate(element=>getComputedStyle(element).position==='sticky') &&
    await labelHeader.evaluate(element=>getComputedStyle(element).position==='sticky'),
    'Context action freezes all columns from the first through the pointed column');
  await codeCell.click({button:'right',position:{x:8,y:8}});
  await page.getByRole('menuitemcheckbox',{name:'Bỏ đóng băng cột',exact:true}).click();
  await page.waitForFunction(()=>!document.querySelector('.table-data-grid')?.hasAttribute('data-frozen-through'));
  await dataGrid.evaluate(element=>{
    element.style.removeProperty('flex');element.style.removeProperty('max-width');
    element.querySelector('table').style.removeProperty('width');
  });

  await page.evaluate(()=>{window.__pagedTable=true;});
  await tablePanel.getByRole('button',{name:'Chạy',exact:true}).click();
  const grid=tablePanel.locator('.table-data-grid');
  const next=tablePanel.getByRole('button',{name:'Trang sau',exact:true});
  const previous=tablePanel.getByRole('button',{name:'Trang trước',exact:true});
  await tablePanel.getByText('Dòng 1–200',{exact:true}).waitFor();
  check(await grid.locator('.table-data-row').count()<60,'Table inspection mounts only the visible row window');
  await grid.evaluate(element=>{element.scrollTop=element.scrollHeight;});
  await grid.locator('td[title="200"]').waitFor();
  await page.screenshot({path:out+'/table-paged.png',animations:'disabled'});
  for(let index=1;index<=6;index++){
    await next.click();
    await tablePanel.getByText('Trang '+(index+1),{exact:true}).waitFor();
  }
  await grid.locator('td[title="1201"]').waitFor();
  check(await next.isDisabled(),'Table inspection reaches the last row beyond 1000');
  await previous.click();
  await grid.locator('td[title="1001"]').waitFor();
  await grid.locator('[data-table-cell="0:1"]').dblclick();
  await grid.locator('[data-edit-cell="0:1"]').fill('Bản sửa đang chờ');
  check(await next.isDisabled() && await previous.isDisabled(),'Paging protects unsaved cell edits');
  await grid.locator('[data-edit-cell="0:1"]').press('Escape');
  await next.click();
  await grid.locator('td[title="1201"]').waitFor();
  await page.getByRole('button',{name:'Ẩn không gian dự án',exact:true}).click();
  await page.setViewportSize({width:375,height:600});
  const pagination=tablePanel.getByRole('navigation',{name:'Phân trang dữ liệu bảng'});
  await pagination.scrollIntoViewIfNeeded();
  check(await pagination.evaluate(element=>{
    const {left,right,top,bottom}=element.getBoundingClientRect();
    const grid=element.closest('.table-inspection-content').querySelector('.table-data-grid');
    return left>=0 && right<=innerWidth && top>=0 && bottom<=innerHeight && grid.clientHeight>=120;
  }),'Paging controls fit within a narrow workbench');
  await page.screenshot({path:out+'/table-paged-narrow.png',animations:'disabled'});
  await page.setViewportSize({width:1440,height:1000});

  await sqlDocument.click();
  const workbenchTop=await page.locator('.workbench').evaluate(el=>el.getBoundingClientRect().top);
  await page.evaluate(()=>{window.__failSql=true;});
  await page.getByRole('button',{name:'Chạy SQL',exact:true}).click();
  await page.locator('.table-notice[role="alert"]').getByText('Lỗi SQL fixture').waitFor();
  check(await page.locator('.workbench').evaluate(el=>el.getBoundingClientRect().top)===workbenchTop,
    'SQL errors use a floating notice without moving the workbench');
  await page.screenshot({path:out+'/sql-error-toast.png',animations:'disabled'});
  await page.setViewportSize({width:375,height:600});
  const notice=page.locator('.table-notice[role="alert"]');
  check(await notice.evaluate(el=>{
    const {left,right}=el.getBoundingClientRect();
    return left>=0 && right<=innerWidth;
  }),'The error notice fits within a 375px workbench');
  await notice.getByRole('button',{name:'Đóng thông báo'}).click();
  await notice.waitFor({state:'detached'});
  await page.setViewportSize({width:1440,height:1000});
  await page.keyboard.press('Control+Shift+f');
  const search=page.getByRole('searchbox',{name:'Nội dung tìm'});
  await search.fill('needle');
  const searchResults=page.locator('#project-sidebar');
  await searchResults.getByText('Nhập tên file hoặc nội dung rồi bấm Tìm.',{exact:true}).waitFor();
  const scopeButton=searchResults.getByRole('button',{name:'Phạm vi tìm kiếm'});
  check((await scopeButton.textContent()).includes('Toàn dự án'),'Search defaults to the whole project');
  await scopeButton.click();
  await page.getByRole('option',{name:'Dashboard',exact:true}).waitFor();
  check(await page.getByRole('option',{name:'Template',exact:true}).count()===0 &&
    await page.getByRole('option',{name:'About.xml',exact:true}).count()===0,
    'Search offers first-level controller folders even without opening a function');
  await page.screenshot({path:out+'/controller-search-scope.png',animations:'disabled'});
  await page.getByRole('option',{name:'Filter',exact:true}).click();
  check(await page.evaluate(()=>!window.__nativeCalls.some(call=>call.method==='workspace.search')),
    'Opening and selecting the scope do not submit the search form');
  await page.evaluate(()=>window.__holdSearchResults=true);
  await search.press('Enter');
  await searchResults.getByRole('button',{name:/fixture\.xml:2/}).waitFor();
  check(await searchResults.getByRole('button',{name:'Dừng',exact:true}).isVisible(),
    'Sidebar displays streamed results before search completes');
  const branch = searchResults.locator('details').filter({has:page.locator('summary').getByText('App_Data',{exact:true})}).first();
  check(await branch.getByText('Controllers',{exact:true}).isVisible() &&
    await branch.getByText('Filter',{exact:true}).isVisible() &&
    await page.evaluate(()=>window.__nativeCalls.some(call=>call.method==='workspace.search' && call.payload.path==='C:\\Fixture\\App_Data\\Controllers\\Filter')),
    'Folder scope searches Filter and keeps the directory hierarchy');
  await page.evaluate(()=>{window.__holdSearchResults=false;window.__releaseSearchResults();});
  await searchResults.getByText('1 tệp khớp',{exact:true}).waitFor();
  await page.screenshot({path:out+'/workspace-content-search.png',animations:'disabled'});
  await page.setViewportSize({width:375,height:600});
  check(await search.evaluate(element=>element.getBoundingClientRect().right<=innerWidth),
    'Live content search fits in the sidebar at 375px');
  await searchResults.getByRole('button',{name:'Xóa nội dung tìm',exact:true}).click();
  await searchResults.getByText('Nhập tên file hoặc nội dung rồi bấm Tìm.',{exact:true}).waitFor();
}
try{
  const testUrl=new URL(process.env.LSCODE_UI_URL??'http://127.0.0.1:4173');
  if(process.argv.includes('--entity-peek-regression'))testUrl.searchParams.set('entityFixture','1');
  if(process.argv.includes('--request-tracking-regression'))testUrl.searchParams.set('requestFixture','1');
  if(process.argv.includes('--ghost-text-regression'))testUrl.searchParams.set('ghostFixture','1');
  await page.goto(testUrl.toString());await page.getByRole('dialog',{name:'Dự án của bạn'}).waitFor();check(true,'Project picker opens before workbench');
  if(process.argv.includes('--ghost-text-regression')){
    await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
    await page.locator('dialog').waitFor({state:'detached'});
    await page.getByRole('button',{name:'Mở file',exact:true}).first().click();
    const xmlTab=page.getByRole('tab',{name:'fixture.xml',exact:true});
    await xmlTab.waitFor();
    const editor=page.locator('.editor-group.focused .monaco-editor').first();
    const ghost=editor.locator('.ghost-text-decoration');
    const view=editor.locator('.view-lines');
    await editor.click({position:{x:110,y:24}});
    await page.keyboard.press('Control+a');
    await page.keyboard.insertText('<field name="{{demo}}" type=""/>');
    for(let index=0;index<3;index++)await page.keyboard.press('ArrowLeft');
    await page.keyboard.type('Dec');
    await ghost.filter({hasText:'imal'}).first().waitFor({timeout:5000});
    await editor.screenshot({path:out+'/xml-ghost-text.png',animations:'disabled'});
    await page.keyboard.press('Escape');
    await ghost.waitFor({state:'detached'});
    check((await view.textContent()).includes('type="Dec"'),'Escape dismisses XML ghost text without editing the model');
    await page.keyboard.press('Backspace');
    await page.keyboard.type('c');
    await ghost.first().waitFor();
    await page.keyboard.press('Tab');
    await ghost.waitFor({state:'detached'});
    check((await view.textContent()).includes('type="Decimal"'),'Tab accepts XML ghost text before navigating template tokens');
    await page.keyboard.press('Tab');
    await editor.locator('.selected-text').first().waitFor();
    check(true,'Tab still navigates to a template token after ghost text closes');

    await page.keyboard.press('Control+a');
    await page.keyboard.insertText('<field name="{{demo}}" type=""/>');
    for(let index=0;index<3;index++)await page.keyboard.press('ArrowLeft');
    await page.keyboard.type('Dec');
    await ghost.filter({hasText:'imal'}).first().waitFor();
    await page.keyboard.press('Control+Space');
    await editor.locator('.suggest-widget .monaco-list-row.focused').filter({hasText:'Decimal'}).first().waitFor();
    await page.keyboard.press('Tab');
    await editor.locator('.suggest-widget .monaco-list-row').filter({hasText:'Decimal'}).first().waitFor({state:'detached'});
    await ghost.waitFor({state:'detached'});
    check((await view.textContent()).includes('type="Decimal"'),'Ctrl+Space opens the popup over ghost text and Tab accepts its selection');

    await page.keyboard.press('Control+a');
    await page.keyboard.insertText('<query>SELECT @@u</query>');
    await page.keyboard.press('Control+Home');
    for(let index=0;index<'<query>SELECT @@u'.length;index++)await page.keyboard.press('ArrowRight');
    await page.keyboard.type('s');
    await ghost.filter({hasText:'erID'}).first().waitFor({timeout:5000}).catch(async error=>{
      throw Error(`Embedded SQL ghost text missing: ${await view.textContent()}`,{cause:error});
    });
    await page.keyboard.press('Tab');
    await ghost.waitFor({state:'detached'});
    check((await view.textContent()).replace(/\u00a0/g,' ').includes('SELECT @@userID</query>'),'Embedded SQL accepts a FAST session-variable ghost suggestion');

    await page.keyboard.press('Control+a');
    await page.keyboard.insertText('<lo');
    await page.keyboard.type('o');
    await editor.locator('.suggest-widget .monaco-list-row').filter({hasText:'<lookup'}).first().waitFor();
    check(await ghost.count()===0,'An XML snippet alias keeps its popup expansion instead of inserting an invalid literal tag');
    await page.keyboard.press('Escape');
    await page.getByRole('button',{name:'File mới',exact:true}).click();
    await editor.click({position:{x:110,y:24}});
    await page.keyboard.type('sel');
    await ghost.filter({hasText:'ECT'}).first().waitFor();
    await editor.screenshot({path:out+'/sql-ghost-text.png',animations:'disabled'});
    await page.keyboard.press('Tab');
    await ghost.waitFor({state:'detached'});
    check((await view.textContent()).includes('selECT'),'SQL ghost text preserves typed case when accepted');
    check(await page.evaluate(()=>window.__nativeCalls.some(call=>call.method==='sql.complete'&&call.payload.beforeCaret==='sel'&&!call.payload.targetId)),
      'Automatic SQL ghost text uses local completion without a database target');
    await page.keyboard.press('Control+a');
    await page.keyboard.insertText('se');
    await page.keyboard.type('l');
    await ghost.filter({hasText:'ECT'}).first().waitFor();
    await page.keyboard.press('Home');
    await ghost.waitFor({state:'detached'});
    check((await view.textContent()).includes('sel'),'Moving the caret dismisses visible SQL ghost text');
    await page.keyboard.press('Control+a');
    await page.keyboard.insertText('seNOT');
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.press('ArrowLeft');
    await page.keyboard.type('l');
    await page.waitForFunction(()=>document.querySelector('.editor-group.focused .view-lines')?.textContent.includes('selNOT'));
    check(await ghost.count()===0,
      'Typing within an existing SQL identifier leaves the suffix untouched by ghost completion');
    await page.keyboard.press('Escape');
    await page.keyboard.press('Control+a');
    await page.keyboard.insertText('se');
    await page.keyboard.type('l');
    await ghost.filter({hasText:'ECT'}).first().waitFor();
    await xmlTab.click();
    await page.waitForFunction(()=>document.querySelector('.editor-group.focused .view-lines')?.textContent.includes('<loo'));
    await ghost.waitFor({state:'detached'});
    check(true,'Changing tabs dismisses SQL ghost text and restores the XML model');
    if(errors.length)console.log(JSON.stringify(errors,null,2));
    check(errors.length===0,'No uncaught browser errors');
    writeFileSync(out+'/ghost-text-report.json',JSON.stringify({passed:true,checks,transport:'browser fixture',errors},null,2));
  } else if(process.argv.includes('--xml-intellisense-regression')){
    await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
    await page.locator('dialog').waitFor({state:'detached'});
    await page.getByRole('button',{name:'Mở file',exact:true}).first().click();
    const tab=page.getByRole('tab',{name:'fixture.xml',exact:true});
    await tab.waitFor();
    const editor=page.locator('.editor-group.focused .monaco-editor').first();
    async function complete(fragment, label, expected, accept='Enter'){
      await page.keyboard.press('Escape');
      await editor.click({position:{x:110,y:24}});
      await page.keyboard.press('Control+a');
      const [before,after='']=fragment.split('|');
      await page.keyboard.insertText(before+after);
      for(let index=0;index<after.length;index++)await page.keyboard.press('ArrowLeft');
      await page.keyboard.press('Control+Space');
      const item=editor.locator('.suggest-widget .monaco-list-row').filter({hasText:label}).first();
      await item.waitFor();
      await page.keyboard.press(accept);
      await page.waitForFunction(value=>document.querySelector('.editor-group.focused .view-lines')?.textContent.replace(/\u00a0/g,' ').includes(value),expected,{timeout:5000})
        .catch(async error=>{throw Error(`Expected ${expected} after ${label}; got ${await editor.locator('.view-lines').textContent()}`,{cause:error});});
    }
    await complete('<loo|kux','<lookup','<lookup field="ma_demo"/>');
    check(!(await editor.locator('.view-lines').textContent()).includes('kux'),
      'XML IntelliSense replaces the whole tag prefix and keeps snippet placeholders editable');
    await page.keyboard.insertText('ma_changed');
    await page.waitForFunction(()=>document.querySelector('.editor-group.focused .view-lines')?.textContent.includes('field="ma_changed"'));
    check(true,'XML snippet selects its first placeholder');
    await complete('<field type="Dec|imxl"/>','Decimal','<field type="Decimal"/>','Tab');
    check(true,'Tab accepts XML attribute value and preserves the surrounding quotes');
    await complete('<script>f.getI|temOld</script>','getItem',"f.getItem('ma_demo')");
    check(true,'Embedded JavaScript uses the incoming completion range and snippet expansion');
    await complete('<query>SELECT @@us|erOld</query>','@@userID','SELECT @@userID</query>');
    check(await page.evaluate(()=>window.__nativeCalls.some(call=>call.method==='xml.complete'&&
      call.payload.text.includes('SELECT @@userOld')&&call.payload.targetId==='project-app'&&
      call.payload.path==='C:\\Fixture\\fixture.xml')),
      'Embedded SQL replaces a FAST session variable and sends the active document and SQL target');
    await complete('<css>.field { backg|round-old: red; }</css>','background-color','background-color: red');
    check(true,'Embedded CSS completion replaces the existing property token');
    await editor.click({position:{x:110,y:24}});
    await page.keyboard.press('Control+a');
    await page.keyboard.insertText('<field type=""/>');
    for(let index=0;index<3;index++)await page.keyboard.press('ArrowLeft');
    await page.keyboard.type('Dec');
    await editor.locator('.ghost-text-decoration').filter({hasText:'imal'}).or(
      editor.locator('.suggest-widget .monaco-list-row').filter({hasText:'Decimal'})).first().waitFor();
    check(true,'Typing inside an XML attribute triggers a completion without Ctrl+Space');
    await editor.screenshot({path:out+'/xml-intellisense.png',animations:'disabled'});
    check(errors.length===0,'No uncaught browser errors');
  } else if(process.argv.includes('--request-tracking-regression')){
    await testRequestTracking();
    check(errors.length===0,'No uncaught browser errors');
  } else if(process.argv.includes('--catalog-source-regression')){
    await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
    await page.locator('dialog').waitFor({state:'detached'});
    await page.getByRole('button',{name:'Chức năng',exact:true}).click();
    await page.getByRole('button',{name:'Nhân viên',exact:true}).click({button:'right'});
    await page.getByRole('button',{name:'Dùng làm mẫu tạo danh mục'}).click();
    const catalog=page.getByRole('dialog',{name:'Tạo danh mục',exact:true});
    await catalog.getByRole('button',{name:'Đọc mẫu'}).click();
    check(await catalog.getByLabel('Mẫu ERP').inputValue()==='nhanvien.aspx','Menu seeds the ERP template in Create catalog');
    await catalog.getByLabel('Controller mới').fill('CatalogDemo');
    await catalog.getByLabel('DanhMuc',{exact:true}).fill('DanhMucDemo');
    await catalog.getByRole('button',{name:'Xem trước'}).click();
    await catalog.locator('pre').getByText("<grid table='DanhMucDemo' />").waitFor();
    await catalog.screenshot({path:out+'/catalog-source.png'});
    await catalog.getByRole('button',{name:'Xuất source'}).click();
    await page.locator('.table-notice').getByText('Đã xuất 1 file',{exact:false}).waitFor();
    check(await page.evaluate(()=>window.__nativeCalls.some(call=>call.method==='catalogSource.export'&&
      call.payload.fingerprint==='catalog-fixture'&&call.payload.tables.DanhMuc==='DanhMucDemo')),
      'Create catalog exports the reviewed configuration');
    await catalog.getByRole('button',{name:'Tạo từ mô tả'}).click();
    await catalog.getByLabel('Mô tả danh mục').fill(`Danh mục điểm đánh giá theo hệ số
Hiệu lực từ kỳ/năm: kiểu số, khoá chính
Mã điểm: kiểu chuỗi, khoá chính
Tên điểm: kiểu chuỗi, bắt buộc nhập
Tên khác: kiểu chuỗi
Hệ số đánh giá (%): kiểu số thập phân
Trạng thái: 1-Còn sử dụng, 0-Không còn sử dụng`);
    await catalog.getByRole('button',{name:'Tách trường'}).click();
    const fieldRows=catalog.locator('.catalog-description-table tbody tr');
    check(await fieldRows.count()===7,'Description rules expand period/year and keep every declared field');
    check((await Promise.all([0,1,2,3,4,5,6].map(index=>fieldRows.nth(index).locator('td').first().locator('input').inputValue()))).join(',')===
      'ky_hl,nam_hl,ma_diem,ten_diem,ten_diem2,he_so_danh_gia,status',
      'Description rules generate predictable ERP field names');
    check((await fieldRows.nth(2).getByRole('button',{name:'Kiểu SQL 3'}).textContent()).includes('char') &&
      await fieldRows.nth(2).getByLabel('Kích thước 3').inputValue()==='16' &&
      (await fieldRows.nth(5).getByRole('button',{name:'Kiểu SQL 6'}).textContent()).includes('numeric') &&
      await fieldRows.nth(5).getByLabel('Kích thước 6').inputValue()==='19,5',
      'Description rules apply char(16) to codes and numeric(19,5) to decimals');
    check(await fieldRows.nth(3).getByLabel('Bắt buộc 4').isChecked() &&
      await fieldRows.nth(6).getByLabel('Mặc định 7').inputValue()==='1',
      'Description rules preserve required and status defaults');
    await fieldRows.nth(2).getByRole('button',{name:'Kiểu SQL 3'}).click();
    check(await page.locator('.select-options-floating').isVisible(),'Catalog SQL types use the LSCode floating dropdown');
    await page.keyboard.press('Escape');
    const modeTop=await catalog.locator('.catalog-mode').evaluate(element=>element.getBoundingClientRect().top);
    const footerBottom=await catalog.locator('.catalog-description-footer').evaluate(element=>element.getBoundingClientRect().bottom);
    await catalog.locator('.catalog-source-scroll').evaluate(element=>element.scrollTop=element.scrollHeight);
    check(await catalog.locator('.catalog-mode').evaluate(element=>element.getBoundingClientRect().top)===modeTop &&
      await catalog.locator('.catalog-description-footer').evaluate(element=>element.getBoundingClientRect().bottom)===footerBottom,
      'Catalog mode bar and actions stay fixed while the content scrolls');
    await catalog.screenshot({path:out+'/catalog-description.png',animations:'disabled'});
    await page.setViewportSize({width:375,height:700});
    check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),
      'Create-from-description does not overflow the 375px viewport');
    await catalog.screenshot({path:out+'/catalog-description-mobile.png',animations:'disabled'});
    await page.setViewportSize({width:1440,height:1000});
    await catalog.getByRole('button',{name:'Thiết kế bảng SQL'}).click();
    const discard=page.getByRole('dialog',{name:'Bỏ bản nháp?'});
    await discard.waitFor();
    check(await discard.getByText('Bản nháp danh mục chưa được lưu',{exact:false}).isVisible(),
      'Catalog uses an LSCode confirmation instead of a system dialog');
    await discard.getByRole('button',{name:'Bỏ thay đổi'}).click();
    check(await catalog.getByRole('button',{name:'Thiết kế bảng SQL'}).getAttribute('aria-pressed')==='true',
      'Create catalog keeps the SQL designer available');
    check(errors.length===0,'No uncaught browser errors');
  } else if(process.argv.includes('--source-collection-regression')){
    await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
    await page.locator('dialog').waitFor({state:'detached'});
    await page.getByRole('button',{name:'Chức năng',exact:true}).click();
    await page.getByRole('button',{name:'Nhân viên',exact:true}).dblclick();
    const sourceFile=page.locator('.project-file-tree').getByTitle('C:\\Fixture\\App_Data\\Controllers\\Grid\\nhanvien.f',{exact:true});
    await sourceFile.waitFor();
    await sourceFile.click({button:'right'});
    await page.getByRole('menuitem',{name:'Source Collection',exact:true}).click();
    const collection=page.getByRole('dialog',{name:'Source Collection',exact:true});
    const versionSelect=collection.getByRole('button',{name:'Phiên bản',exact:true});
    await versionSelect.click();
    const versionList=collection.getByRole('tree',{name:'Phiên bản',exact:true});
    const groups=versionList.locator('.select-option-group-toggle');
    check((await groups.allTextContents()).join('|')==='FAW|FBO-FBI',
      'Source Collection renders one parent node for each major version group');
    check(await versionList.getByText(/ZZZCHÉP/).count()===0,
      'Source Collection excludes ZZZ instruction folders from the version tree');
    check(await groups.filter({hasText:'FAW'}).getAttribute('aria-expanded')==='true' &&
      await groups.filter({hasText:'FBO-FBI'}).getAttribute('aria-expanded')==='false' &&
      (await versionList.locator('[data-select-option]').allTextContents()).map(text=>text.trim()).join('|')==='FAW30|FAW22',
      'The selected major group starts expanded while other branches stay collapsed');
    await groups.filter({hasText:'FBO-FBI'}).click();
    check((await versionList.locator('.select-option-branch').filter({hasText:'FBO-FBI'}).locator('[data-select-option]').allTextContents())
      .map(text=>text.trim()).join('|')==='FBOR2SP24.2.2|FBOR2SP23|FBOR2SP22.9',
      'Expanded branches sort versions from newest to oldest');
    const groupStyle=await groups.first().evaluate(element=>({
      weight:Number.parseInt(getComputedStyle(element).fontWeight,10),
      background:getComputedStyle(element).backgroundColor,
    }));
    check(groupStyle.weight>=700 && !['transparent','rgba(0, 0, 0, 0)'].includes(groupStyle.background),
      'Major version parent nodes use bold text and a background');
    check((await collection.locator('.select-options-floating').boundingBox()).width>=400,
      'The version tree opens in a wider selection area');
    await collection.getByRole('searchbox',{name:'Tìm phiên bản'}).fill('FBO-FBI');
    check((await versionList.locator('[data-select-option]').allTextContents()).map(text=>text.trim()).join('|')===
      'FBOR2SP24.2.2|FBOR2SP23|FBOR2SP22.9' && (await groups.allTextContents()).join('|')==='FBO-FBI',
      'Version search can filter by the major version heading');
    await page.screenshot({path:out+'/source-collection-version-groups.png',animations:'disabled'});
    check(errors.length===0,'No uncaught browser errors');
  } else if(process.argv.includes('--entity-peek-regression')){
    await page.evaluate(()=>{window.__entityFixture=true;});
    await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
    await page.locator('dialog').waitFor({state:'detached'});
    await page.getByRole('button',{name:'Mở file',exact:true}).first().click();
    await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='document.openMany'));
    const mainEditor=page.locator('.editor-group.focused .monaco-editor');
    await mainEditor.waitFor();
    await page.waitForFunction(()=>document.querySelector('.editor-group.focused .view-lines')?.textContent.includes('UnitBeforeInsert'));
    await mainEditor.click({position:{x:180,y:35}});
    await page.keyboard.press('Control+f');
    await page.keyboard.insertText('&UnitBeforeInsert;');
    await page.keyboard.press('Escape');
    await page.keyboard.press('F12');
    await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='xml.include'));
    check(true,'F12 requests entity resolution from the active XML editor');
    check(await page.evaluate(()=>window.__nativeCalls.findLast(call=>call.method==='xml.include')?.payload.text.includes('&UnitBeforeInsert;')),
      'Entity resolution receives the active XML buffer');
    const peeks=page.locator('.entity-peek');
    const rootPeek=peeks.first();
    await rootPeek.waitFor();
    check(await page.locator('dialog[open]').count()===0 &&
      await rootPeek.getByText('UnitBeforeInsert',{exact:true}).isVisible() &&
      await rootPeek.getByText(/UnitBeforeInsert\.txt/).isVisible(),
      'F12 opens an anchored entity callout with its source path instead of a modal');
    check(await page.evaluate(()=>{
      const background=document.querySelector('.editor-group.focused .monaco-editor');
      const heading=document.querySelector('.entity-peek-heading');
      const backgroundBlocked=background&&!background.dispatchEvent(new WheelEvent('wheel',{deltaY:160,bubbles:true,cancelable:true}));
      const insideAllowed=heading&&heading.dispatchEvent(new WheelEvent('wheel',{deltaY:160,bubbles:true,cancelable:true}));
      return document.body.classList.contains('entity-peek-scroll-locked')&&
        Boolean(document.querySelector('.entity-peek-scroll-lock'))&&backgroundBlocked&&insideAllowed;
    }),'Entity callout locks background scrolling while keeping its own content scrollable');
    check(await rootPeek.locator('.view-lines').textContent().then(text=>
      text.includes('&Job.Nested.Key.a;')&&!text.includes('<!ENTITY')),
      'Entity callout renders only the resolved entity body');
    const rootPeekEditor=rootPeek.locator('.monaco-editor');
    await rootPeekEditor.click({position:{x:220,y:35}});
    await page.keyboard.press('Control+Home');
    for(let index=0;index<'if exists(select 1 where &J'.length;index++)await page.keyboard.press('ArrowRight');
    check(await page.evaluate(()=>document.activeElement instanceof HTMLElement&&Boolean(document.activeElement.closest('.entity-peek'))),
      'The nested entity editor keeps keyboard focus');
    await page.keyboard.press('F12');
    await page.waitForFunction(()=>window.__nativeCalls.filter(call=>call.method==='xml.include').length>=2);
    check(await page.evaluate(()=>window.__nativeCalls.filter(call=>call.method==='xml.include').at(-1)?.payload.text.includes('&Job.Nested.Key.a;')),
      'F12 in the callout resolves against the displayed entity source');
    await page.waitForFunction(()=>document.querySelectorAll('.entity-peek').length===2);
    const childPeek=peeks.nth(1);
    await childPeek.locator('.entity-peek-identity strong').filter({hasText:'Job.Nested.Key.a'}).waitFor();
    await page.waitForFunction(value=>[...document.querySelectorAll('.entity-peek')].at(-1)?.querySelector('.view-lines')?.textContent.replace(/\s/g,' ').includes(value),
      "((ma_vv like rtrim(@ma_vv) + '%'))");
    check(await rootPeek.getByText('UnitBeforeInsert',{exact:true}).isVisible() &&
      await childPeek.locator('.view-lines').textContent().then(text=>
        text.replace(/\s/g,' ').includes("((ma_vv like rtrim(@ma_vv) + '%'))")&&!text.includes('<!ENTITY')),
      'F12 inside a callout keeps its parent and opens the nested entity in a child callout');
    check(await page.evaluate(({path,text})=>window.__nestedEntityPayload?.contextPath===path&&
      window.__nestedEntityPayload?.contextText===text,{path:'C:\\Fixture\\entity-main.xml',text:"<!DOCTYPE root [<!ENTITY % graph SYSTEM 'graph.dtd'>%graph;]><root>&UnitBeforeInsert;</root>"}),
      'Nested F12 retains the declaration graph of the root XML');
    await page.locator('.editors').screenshot({path:out+'/entity-peek-tree.png',animations:'disabled'});
    await childPeek.getByLabel('Hiện toàn bộ file').check();
    await page.waitForFunction(()=>[...document.querySelectorAll('.entity-peek')].at(-1)?.querySelector('.view-lines')?.textContent.replace(/\s/g,' ').includes('<!ENTITY UnitBeforeInsert'));
    check(true,'The explicit option switches the callout to the entire source file');
    await childPeek.screenshot({path:out+'/entity-peek.png',animations:'disabled'});
    await page.locator('.entity-peek-scroll-lock').click({position:{x:5,y:100}});
    await page.waitForFunction(()=>document.querySelectorAll('.entity-peek').length===1);
    check(await rootPeek.getByText('UnitBeforeInsert',{exact:true}).isVisible(),
      'One outside click closes only the newest callout');
    await rootPeekEditor.click({position:{x:220,y:35}});
    await page.keyboard.press('Control+Home');
    for(let index=0;index<'if exists(select 1 where &J'.length;index++)await page.keyboard.press('ArrowRight');
    await page.keyboard.press('F12');
    await page.waitForFunction(()=>document.querySelectorAll('.entity-peek').length===2);
    await page.getByRole('button',{name:'Lệnh',exact:true}).click();
    await page.getByRole('dialog',{name:'Bảng lệnh'}).getByRole('button',{name:'Đóng',exact:true}).click();
    check(await page.locator('.entity-peek').count()===2,'Toolbar works while entity callouts are open');
    await page.keyboard.press('Escape');
    await page.waitForFunction(()=>document.querySelectorAll('.entity-peek').length===1);
    await page.keyboard.press('Escape');
    await page.waitForFunction(()=>document.querySelectorAll('.entity-peek').length===0);
    check(await page.evaluate(()=>!document.body.classList.contains('entity-peek-scroll-locked')),
      'Escape closes one callout at a time and releases the scroll lock after the last');
    check(errors.length===0,'No uncaught browser errors');
  } else if(process.argv.includes('--xml-panel-regression')){
    await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
    await page.locator('dialog').waitFor({state:'detached'});
    await page.getByRole('button',{name:'Kết quả',exact:true}).click();
    await page.getByRole('button',{name:'Mở file',exact:true}).first().click();
    const xmlTab=page.getByRole('tab',{name:'fixture.xml',exact:true});
    await xmlTab.waitFor();
    check(await page.locator('#results-panel').evaluate(element=>element.classList.contains('collapsed')),
      'Opening XML temporarily collapses the bottom panel');
    await page.getByRole('button',{name:'Kết quả',exact:true}).click();
    await page.getByRole('button',{name:'File mới',exact:true}).click();
    await xmlTab.click();
    check(await page.locator('#results-panel').evaluate(element=>element.classList.contains('collapsed')),
      'Returning to an existing XML tab collapses the bottom panel again');
    check(errors.length===0,'No uncaught browser errors');
  } else if(process.argv.includes('--controller-diagnostics-regression')){
    await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
    await page.locator('dialog').waitFor({state:'detached'});
    await page.getByRole('button',{name:'Chức năng',exact:true}).click();
    const menuEntry=page.getByRole('button',{name:'Nhân viên',exact:true});
    await menuEntry.click();
    check(await menuEntry.getAttribute('aria-current')==='page' &&
      await page.evaluate(()=>!window.__nativeCalls.some(c=>c.method==='project.controller')),
    'A single click selects the ERP menu without loading source');
    await menuEntry.dblclick();
    const diagnosticPath='C:\\Fixture\\App_Data\\Controllers\\Filter\\nhanvien.xml';
    const fileTree=page.locator('.project-file-tree');
    const diagnosticFile=fileTree.getByTitle(diagnosticPath,{exact:true});
    await diagnosticFile.waitFor();
    await page.waitForFunction(path=>[...document.querySelectorAll('button[title]')]
      .some(button=>button.title===path&&button.getAttribute('aria-invalid')==='true'),diagnosticPath);
    check(await diagnosticFile.getAttribute('data-diagnostic')==='error',
      'Controller scan marks the undeclared-entity file in red');
    const controllerNotice=page.locator('.table-notice').filter({hasText:'Khai báo controller chưa hợp lệ'});
    await controllerNotice.waitFor();
    check((await controllerNotice.textContent()).includes('MissingEntity')&&
      (await controllerNotice.textContent()).includes(diagnosticPath),
      'Function click reports the missing declaration and exact file immediately');
    await diagnosticFile.click();
    const banner=page.locator('.editor-diagnostic-banner').filter({hasText:'MissingEntity'});
    await banner.waitFor();
    check((await banner.textContent()).includes('Dòng 2, cột 3')&&
      (await banner.textContent()).includes(diagnosticPath),
      'Invalid controller shows a banner with entity, line, column, and path');
    await page.screenshot({path:out+'/controller-diagnostics-error.png',animations:'disabled'});
    await page.locator('.editor-group.focused .monaco-editor').click({position:{x:160,y:48}});
    await page.keyboard.press('Control+Home');
    await page.keyboard.insertText('<!DOCTYPE root [<!ENTITY MissingEntity "đã khai báo">]>\r\n');
    await page.waitForFunction(path=>window.__nativeCalls.some(call=>call.method==='xml.diagnostics'&&
      call.payload.path===path&&call.payload.text.includes('<!ENTITY MissingEntity ')),diagnosticPath);
    await banner.waitFor({state:'detached'});
    await page.waitForFunction(path=>[...document.querySelectorAll('button[title]')]
      .some(button=>button.title===path&&!button.hasAttribute('aria-invalid')),diagnosticPath);
    await controllerNotice.waitFor({state:'detached'});
    check(true,'Correct declaration clears the banner and red file state');
    await page.screenshot({path:out+'/controller-diagnostics.png',animations:'disabled'});
    check(errors.length===0,'No uncaught browser errors');
  } else if(process.argv.includes('--profiler-regression')){
    await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
    await page.locator('dialog').waitFor({state:'detached'});
    await page.getByRole('button',{name:'SQL mới',exact:true}).click();
    await testProfiler();
    await page.getByRole('button',{name:'Cài đặt',exact:true}).click();
    await page.getByRole('button',{name:'Chọn PROFILER.EXE',exact:true}).click();
    await page.waitForFunction(expected => Array.from(document.querySelectorAll('dialog input'))
      .some(input => input.value === expected), 'C:\\CustomSSMS\\PROFILER.EXE');
    check(await page.getByLabel('SQL Profiler (PROFILER.EXE)').inputValue()==='C:\\CustomSSMS\\PROFILER.EXE',
      'Settings still allows selecting another PROFILER.EXE');
    await page.getByRole('button',{name:'Lưu cài đặt',exact:true}).click();
    await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='settings.save' &&
      c.payload.settings.profiler18Path==='C:\\CustomSSMS\\PROFILER.EXE'));
    check(errors.length===0,'No uncaught browser errors');
  } else if(process.argv.includes('--sql-success-message-regression')){
    await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
    await page.locator('dialog').waitFor({state:'detached'});
    await page.getByRole('button',{name:'SQL mới',exact:true}).click();
    await page.locator('.monaco-editor').first().click({position:{x:160,y:35}});
    await page.keyboard.insertText('ALTER PROCEDURE dbo.StoreFixture AS SELECT 1;');
    await page.keyboard.press('F5');
    const messagePanel=page.locator('#results-panel .messages');
    await messagePanel.getByText('Các lệnh đã được thực hiện thành công.',{exact:true}).waitFor();
    check(await page.locator('#results-panel').getByRole('button',{name:'Thông báo',exact:true}).getAttribute('aria-selected')==='true',
      'F5 switches to Messages when a stored procedure script finishes without a result set');
    check((await messagePanel.textContent()).trim()==='Các lệnh đã được thực hiện thành công.',
      'Successful stored procedure execution shows the Vietnamese completion message');
    await page.locator('.monaco-editor').first().click({position:{x:160,y:35}});
    await page.keyboard.press('Control+a');
    await page.keyboard.insertText('SELECT 1 AS value;');
    await page.keyboard.press('F5');
    await page.locator('#results-panel .data-grid').waitFor();
    check(await page.locator('#results-panel .panel-tabs button').filter({hasText:'Kết quả'}).first().getAttribute('aria-selected')==='true',
      'A query with a result set still stays on the Results tab');
    await page.getByRole('button',{name:'Xuất CSV',exact:true}).click();
    await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='text.export' &&
      call.payload.name==='results.csv' && call.payload.text.charCodeAt(0)===0xfeff &&
      call.payload.text.includes('Tiếng Việt')));
    check(true,'CSV export sends a UTF-8 BOM and preserves Vietnamese text');
    check(errors.length===0,'No uncaught browser errors');
  } else if(process.argv.includes('--sql-error-routing-regression')){
    await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
    await page.locator('dialog').waitFor({state:'detached'});
    await page.getByRole('button',{name:'SQL mới',exact:true}).click();
    await page.locator('.monaco-editor').first().click({position:{x:160,y:35}});
    await page.keyboard.insertText('CREATE SEQUENCE INVALID_SEQUENCE_FIXTURE AS INT;');
    await page.keyboard.press('F5');
    const sqlError="Unknown object type 'SEQUENCE' used in a CREATE, DROP, or ALTER statement.";
    const resultsPanel=page.locator('#results-panel');
    await resultsPanel.locator('.messages').getByText(sqlError,{exact:true}).waitFor();
    check(await resultsPanel.getByRole('button',{name:'Thông báo',exact:true}).getAttribute('aria-selected')==='true' &&
      await page.locator('.editor-diagnostic-banner').count()===0 &&
      await page.locator('.table-notice[role="alert"]').count()===0,
      'SQL execution errors open Messages below without using an editor banner or global notice');
    await resultsPanel.getByRole('button',{name:/^Vấn đề/}).click();
    await resultsPanel.locator('.tool-list button').filter({hasText:sqlError}).waitFor();
    check(await resultsPanel.getByRole('button',{name:/^Vấn đề/}).textContent().then(text=>text.includes('1')),
      'SQL line diagnostics remain available in the Problems panel');
    await page.getByRole('button',{name:'Chức năng',exact:true}).click();
    await page.getByRole('button',{name:'Nhân viên',exact:true}).dblclick();
    const diagnosticPath='C:\\Fixture\\App_Data\\Controllers\\Filter\\nhanvien.xml';
    const diagnosticFile=page.locator('.project-file-tree').getByTitle(diagnosticPath,{exact:true});
    await diagnosticFile.waitFor();
    await diagnosticFile.click();
    const entityBanner=page.locator('.editor-diagnostic-banner').filter({hasText:'MissingEntity'});
    await entityBanner.waitFor();
    check((await entityBanner.textContent()).includes(diagnosticPath),
      'Undeclared XML entities still use the persistent editor banner');
    await page.getByRole('tab',{name:/^Untitled-.*\.sql/}).click();
    await resultsPanel.getByRole('button',{name:'Thông báo',exact:true}).click();
    await resultsPanel.locator('.messages').getByText(sqlError,{exact:true}).waitFor();
    check(await page.locator('.editor-diagnostic-banner').count()===0,
      'Returning to the SQL tab restores its error below without showing an editor banner');
    check(errors.length===0,'No uncaught browser errors');
  } else if(process.argv.includes('--shortcut-regression')){
    await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
    await page.locator('dialog').waitFor({state:'detached'});
    await page.getByRole('button',{name:'Lệnh',exact:true}).click();
    await page.getByRole('button',{name:'Chỉnh sửa phím tắt',exact:true}).click();
    await page.getByRole('searchbox',{name:'Tìm phím tắt'}).fill('Tạo file SQL');
    const shortcutInput=page.getByRole('textbox',{name:'Tạo file SQL phím 1',exact:true});
    await shortcutInput.click();
    await page.keyboard.press('Control+Alt+q');
    const capturedShortcut=await shortcutInput.inputValue();
    check(capturedShortcut==='Ctrl+Alt+Q','Shortcut editor captures the gesture: '+capturedShortcut);
    const shortcutSave=page.getByRole('button',{name:'Lưu cài đặt',exact:true});
    await shortcutSave.click();
    const shortcutError=page.locator('dialog .form-error');
    await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='settings.save' &&
      call.payload.saveShortcuts && call.payload.settings.shortcuts['file.new']?.[0]==='Ctrl+Alt+Q'),null,{timeout:4000})
      .catch(async err=>{throw new Error((await shortcutError.allTextContents()).join(' ') || err.message);});
    await page.locator('dialog').waitFor({state:'detached'});
    await page.getByRole('button',{name:'Lệnh',exact:true}).click();
    await page.getByRole('dialog',{name:'Bảng lệnh'}).getByText(/Ctrl\+Alt\+Q/).waitFor();
    check(errors.length===0,'Palette opens shortcut editing and shows the saved gesture');
  } else if(process.argv.includes('--menu-regression')){
    await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
    await page.locator('dialog').waitFor({state:'detached'});
    await page.getByRole('button',{name:'Chức năng',exact:true}).click();
    const menuSearch=page.getByRole('textbox',{name:'Tìm chức năng',exact:true});
    await menuSearch.fill('SYSTEMCONFIG.ASPX');
    await page.locator('.function-row .search-hit').getByText('Cấu hình hệ thống',{exact:true}).waitFor();
    check(await page.locator('.function-row .search-hit').count()===1,
      'Menu search matches the Main filename regardless of letter case');
    await menuSearch.clear();
    const menuParent=page.getByRole('button',{name:'Hệ thống',exact:true});
    await menuParent.click();
    const menuChild=page.getByRole('button',{name:'Cấu hình hệ thống',exact:true});
    await menuChild.dblclick();
    await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='project.controller'&&call.payload.link==='systemconfig.aspx'));
    const selectedMenuRow=menuChild.locator('xpath=..');
    const fileNav=page.getByRole('button',{name:'File',exact:true});
    check(await fileNav.getAttribute('aria-selected')==='true' && !(await menuChild.isVisible()),
      'Selecting a function switches to the File tab and shows its controller tree');
    await page.getByRole('button',{name:'Chức năng',exact:true}).click();
    check(await menuParent.getAttribute('aria-expanded')==='true' &&
      await selectedMenuRow.getAttribute('data-selected')==='true' &&
      await menuChild.evaluate(element=>Number.parseInt(getComputedStyle(element).fontWeight,10)>=700),
      'Returning to Functions keeps its parent expanded and emphasizes the selected menu row');
    await page.getByRole('button',{name:'menu-child',exact:true}).click();
    await page.getByRole('tab',{name:/Menu ERP.*menu-child/}).waitFor();
    check(await page.getByRole('tab',{name:'systemconfig.aspx',exact:true}).count()===0,
      'Clicking the Web menu ID opens the F3 editor');
    await menuChild.dblclick();
    const childMain=page.locator('.project-file-tree').getByTitle('C:\\Fixture\\Main\\systemconfig.aspx',{exact:true});
    await childMain.click();
    await page.getByRole('tab',{name:'systemconfig.aspx',exact:true}).waitFor();
    await page.getByRole('button',{name:'Chức năng',exact:true}).click();
    const opensBeforeMenuBrowse=await page.evaluate(()=>window.__nativeCalls.filter(call=>call.method==='document.open').length);
    await menuChild.dblclick();
    check(await fileNav.getAttribute('aria-selected')==='true' &&
      await childMain.getAttribute('aria-current')===null &&
      await page.evaluate(before=>window.__nativeCalls.filter(call=>call.method==='document.open').length===before,opensBeforeMenuBrowse),
      'Clicking the function name switches to File without selecting Main');
    await page.getByRole('button',{name:'Chức năng',exact:true}).click();
    await menuChild.click({button:'right'});
    const refreshMenu=page.getByRole('button',{name:'Làm mới menu',exact:true});
    await refreshMenu.waitFor();
    await refreshMenu.click();
    await page.getByRole('button',{name:'Menu vừa thêm',exact:true}).waitFor();
    check(await page.evaluate(()=>window.__nativeCalls.some(call=>call.method==='project.commands'&&call.payload.kind==='web'&&call.payload.refresh===true)) &&
      await menuParent.getAttribute('aria-expanded')==='true' &&
      await selectedMenuRow.getAttribute('data-selected')==='true',
      'Context refresh reloads newly added menus without resetting expansion or selection');
    await testMenuEditor(page.getByRole('button',{name:'Nhân viên',exact:true}));
    check(errors.length===0,'No uncaught browser errors');
  } else if(process.argv.includes('--studio-discovery-regression')){
    await page.getByRole('dialog',{name:'Dự án của bạn'}).getByRole('button',{name:'Đóng',exact:true}).click();
    await page.locator('.command-search').click();
    await page.getByRole('textbox',{name:'Tìm lệnh hoặc file'}).fill('Thiết kế template API');
    await page.getByRole('dialog').getByRole('button',{name:'Thiết kế template API',exact:true}).click();
    await page.getByRole('button',{name:'Mở project Studio',exact:true}).click();
    await page.getByRole('button',{name:'Template',exact:true}).click();
    await page.getByRole('option',{name:'existing',exact:true}).waitFor();
    check(await page.getByRole('option',{name:'registered',exact:true}).isVisible(),
      'Studio shows registered and discovered templates together');
    await page.screenshot({path:out+'/studio-discovered-templates.png',animations:'disabled'});
    await page.getByRole('searchbox',{name:'Tìm template',exact:true}).fill('existing');
    await page.getByRole('option',{name:'existing',exact:true}).click();
    check((await page.getByRole('button',{name:'Template',exact:true}).textContent()).includes('existing'),
      'Studio finds and opens a template discovered on disk');
    const notice=page.locator('.table-notice').filter({hasText:'invalid.json'});
    await notice.waitFor();
    await notice.getByRole('button',{name:'Đóng thông báo'}).click();
    await notice.waitFor({state:'detached'});
    const from=page.getByRole('textbox',{name:'From',exact:true});
    check((await from.getAttribute('placeholder')).includes('$.header'),'Studio shows a JSONPath example in From');
    await page.evaluate(()=>window.__holdStudioSave=true);
    await from.fill('$.header');
    await page.waitForFunction(()=>window.__releaseStudioSave);
    check(await from.isEditable(),'Studio keeps inputs editable while autosave is pending');
    await from.fill('$.edited');
    const tableHeading=page.locator('.studio-table-settings .studio-name');
    await tableHeading.getByRole('button',{name:'Đổi tên bảng',exact:true}).click();
    const tableName=tableHeading.getByRole('textbox',{name:'Tên bảng mới',exact:true});
    check(await tableName.evaluate(input=>input===document.activeElement),'Studio focuses the inline table rename input');
    await tableName.fill('#Edited');
    await tableName.press('Enter');
    await tableHeading.getByText('#Edited',{exact:true}).waitFor();
    const fieldCell=page.locator('#studio-field-0 td:nth-child(3)');
    await fieldCell.getByRole('button',{name:'Đổi tên field Code',exact:true}).click();
    const fieldName=fieldCell.getByRole('textbox',{name:'Tên field mới',exact:true});
    await fieldName.fill('ItemCode');
    await page.screenshot({path:out+'/studio-inline-rename.png',animations:'disabled'});
    await fieldName.press('Enter');
    await page.getByRole('button',{name:'Đổi tên field ItemCode',exact:true}).waitFor();
    await page.evaluate(()=>{window.__holdStudioSave=false;window.__releaseStudioSave();});
    await page.waitForFunction(()=>window.__studioSaves?.length===2 &&
      window.__studioSaves[1].templates[1].tables[0].from==='$.edited' &&
      window.__studioSaves[1].templates[1].tables[0].name==='#Edited' &&
      window.__studioSaves[1].templates[1].tables[0].columns[0].name==='ItemCode');
    await page.waitForFunction(()=>[...document.querySelectorAll('.studio-project-toolbar button')]
      .some(button=>button.textContent==='Lưu project' && button.disabled));
    check(true,'Autosave follows the first pending write with the new From and inline names');
    await page.getByRole('checkbox',{name:'Tự lưu',exact:true}).uncheck();
    check(await page.locator('.studio-design-sidebar .studio-settings').isVisible(),
      'Template properties appear beneath the table list in the left pane');
    await page.getByRole('button',{name:'Template',exact:true}).click();
    await page.getByRole('option',{name:'registered',exact:true}).click();
    await page.getByRole('button',{name:'Template',exact:true}).click();
    await page.getByRole('option',{name:'existing',exact:true}).click();
    await page.locator('.studio-inspector').waitFor({state:'detached'});
    await page.locator('#studio-field-0 td:nth-child(5)').click();
    const inspector=page.locator('.studio-inspector');
    const defaultJson=inspector.getByRole('textbox',{name:'Default (JSON)',exact:true});
    check(await defaultJson.isVisible() && await page.evaluate(()=>
      document.querySelector('.studio-inspector').getBoundingClientRect().bottom <=
      document.querySelector('.studio-grid-scroll').getBoundingClientRect().top),
      'Clicking a field row outside its name shows its properties above the grid');
    await defaultJson.fill('{');
    await page.getByRole('button',{name:'Lưu project',exact:true}).click();
    const issues=page.getByRole('region',{name:'Lỗi khi lưu',exact:true});
    await issues.waitFor();
    await page.getByRole('button',{name:'Template',exact:true}).click();
    await page.getByRole('option',{name:'registered',exact:true}).click();
    await issues.getByRole('button',{name:/existing › #Edited › ItemCode › Default \(JSON\)/}).click();
    await page.waitForFunction(()=>document.activeElement?.getAttribute('data-studio-location')==='Default (JSON)');
    check(await defaultJson.getAttribute('aria-invalid')==='true','Clicking a save issue focuses and marks the affected input');
    await page.screenshot({path:out+'/studio-save-issues.png',animations:'disabled'});
    await defaultJson.fill('');
    await page.getByRole('button',{name:'Lưu project',exact:true}).click();
    await issues.waitFor({state:'detached'});
    await page.getByRole('button',{name:'Thêm template',exact:true}).click();
    const create=page.getByRole('dialog',{name:'Thêm template',exact:true});
    await create.getByRole('textbox',{name:'Mã template',exact:true}).fill('existing');
    await create.getByRole('button',{name:'Thêm',exact:true}).click();
    await create.getByRole('alert').waitFor();
    await create.getByRole('textbox',{name:'Mã template',exact:true}).fill('added.template');
    await create.getByRole('button',{name:'Thêm',exact:true}).click();
    await create.waitFor({state:'detached'});
    await page.getByRole('button',{name:'Đóng Dynamic Template Studio',exact:true}).click();
    const confirmation=page.getByRole('dialog',{name:'Dynamic Template Studio',exact:true});
    await confirmation.waitFor();
    await page.screenshot({path:out+'/studio-confirmation.png',animations:'disabled'});
    await confirmation.getByRole('button',{name:'Hủy',exact:true}).click();
    check(await page.getByRole('tab',{name:'Dynamic Template Studio',exact:true}).isVisible(),
      'Studio uses in-app notices, a validated template form and a cancellable confirmation');
    await page.getByRole('button',{name:'Đóng Dynamic Template Studio',exact:true}).click();
    await confirmation.getByRole('button',{name:'Tiếp tục',exact:true}).click();
    await page.getByRole('tab',{name:'Dynamic Template Studio',exact:true}).waitFor({state:'detached'});
    check(true,'Confirming discards the pending Studio edit and closes the tab');
  } else if(process.argv.includes('--project-target-regression')){
    await page.evaluate(()=>window.__holdProjectTargets=true);
    await page.locator('.project-row').filter({hasText:'Dự án thử UI'}).click();
    await page.getByRole('dialog',{name:'Dự án của bạn'}).waitFor({state:'detached'});
    await page.waitForFunction(()=>window.__releaseProjectTargets);
    await page.getByRole('button',{name:'SQL mới',exact:true}).click();
    await page.getByRole('button',{name:'Chạy SQL',exact:true}).click();
    await page.getByText('Đang chờ kết nối SQL App…',{exact:true}).waitFor();
    check(await page.getByRole('dialog').count()===0 &&
      await page.evaluate(()=>!window.__nativeCalls.some(call=>call.method==='sql.execute')),
      'Project appears before SQL App is ready and Run waits without showing a login dialog');
    await page.screenshot({path:out+'/project-wait-for-app.png',animations:'disabled'});
    await page.evaluate(()=>window.__releaseProjectTargets());
    await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='sql.execute'&&call.payload.targetId==='project-app'));
    check(true,'The queued SQL runs on the App target');
    await page.keyboard.press('Control+o');
    const keptTab=page.getByRole('tab',{name:'fixture.xml',exact:true});
    await keptTab.waitFor();
    await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='project.history.record'));
    await page.getByRole('button',{name:'Lịch sử',exact:true}).click();
    const historyHeader=page.locator('.history-group > summary').filter({hasText:'Dự án thử UI'});
    const recordCount=await page.evaluate(()=>window.__nativeCalls.filter(call=>call.method==='project.history.record').length);
    await historyHeader.click({button:'right'});
    await page.getByRole('menuitem',{name:'Close project',exact:true}).click();
    await historyHeader.waitFor({state:'detached'});
    check(await keptTab.isVisible() && await page.evaluate(()=>window.__nativeCalls.some(call=>
      call.method==='project.history.clear'&&call.payload.id==='fixture')), 'Close project clears only the current project history');
    await page.getByRole('tab',{name:/Untitled-.*\.sql/}).click();
    await keptTab.click();
    await page.waitForFunction(count=>window.__nativeCalls.filter(call=>call.method==='project.history.record').length>count,recordCount);
    await historyHeader.click({button:'right'});
    await page.getByRole('menuitem',{name:'Close all projects',exact:true}).click();
    await historyHeader.waitFor({state:'detached'});
    check(await keptTab.isVisible() && await page.evaluate(()=>window.__nativeCalls.some(call=>
      call.method==='project.history.clear'&&!call.payload.id)), 'Close all projects clears history and keeps editor tabs open');
    await page.getByRole('button',{name:'Đóng cửa sổ',exact:true}).click();
    await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='window.close'));
    check(await page.evaluate(()=>window.__nativeCalls.filter(call=>call.method==='recovery.save').at(-1)?.payload.documents.length===0),
      'Closing the window clears session recovery');
  } else if(process.argv.includes('--table-regression')){
    await testTableRefreshAndNotice();
    check(errors.length===0,'No uncaught browser errors');
  } else {
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
  check(await page.getByRole('status',{name:'Thời gian chạy query'}).textContent()==='Thời gian · 12 ms',
    'Query result toolbar keeps the final execution time');
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

  await testProfiler();

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
  check(await page.evaluate(()=>!window.__nativeCalls.some(c=>c.method==='project.commands'&&c.payload.kind==='mobile')),
    'Mobile menu stays lazy until its tab is selected');
  await page.locator('.menu-kind-tabs').getByRole('tab',{name:'Mobile',exact:true}).click();
  await page.getByRole('button',{name:'Mobile bán hàng',exact:true}).waitFor();
  check(await page.evaluate(()=>window.__nativeCalls.some(c=>c.method==='project.commands'&&c.payload.kind==='mobile')),
    'Mobile tab requests the SYS dvcommand tree on demand');
  await page.locator('.menu-kind-tabs').getByRole('tab',{name:'Web',exact:true}).click();
  check(await page.locator('.function-separator').count()===1,'Dash function renders as horizontal separator');
  const functionRow=page.getByRole('button',{name:'Nhân viên',exact:true});
  check(await page.getByRole('button',{name:'menu-parent',exact:true}).count()===1,
    'Parent Web menu IDs also offer editing');
  await functionRow.click();
  check(await functionRow.getAttribute('aria-current')==='page' &&
    await page.evaluate(before=>window.__nativeCalls.filter(c=>c.method==='document.open').length===before,openedBefore),
    'Single click selects the ERP function');
  await functionRow.dblclick();
  const fileTree=page.locator('.project-file-tree');
  const dirPath='C:\\Fixture\\App_Data\\Controllers\\Dir';
  const gridPath='C:\\Fixture\\App_Data\\Controllers\\Grid';
  const dirFile=fileTree.getByTitle(dirPath+'\\nhanvien.xml',{exact:true});
  const gridFile=fileTree.getByTitle(gridPath+'\\nhanvien.xml',{exact:true});
  await dirFile.waitFor();await gridFile.waitFor();
  check(await page.getByRole('button',{name:'File',exact:true}).getAttribute('aria-selected')==='true' &&
    await page.evaluate(before=>window.__nativeCalls.filter(c=>c.method==='document.open').length===before,openedBefore),
    'Double click opens the ERP controller tree without opening Main');
  const mainFile=fileTree.getByTitle('C:\\Fixture\\Main\\nhanvien.aspx',{exact:true});
  await mainFile.click();
  await page.getByRole('tab',{name:'nhanvien.aspx',exact:true}).waitFor();
  check(await mainFile.getAttribute('aria-current')==='page',
    'Opening Main from File selects the source');
  await page.getByRole('button',{name:'Chức năng',exact:true}).click();
  const opensBeforeBrowse=await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='document.open').length);
  await page.getByRole('button',{name:'Nhân viên',exact:true}).dblclick();
  check(await page.getByRole('button',{name:'File',exact:true}).getAttribute('aria-selected')==='true' &&
    await mainFile.getAttribute('aria-current')===null &&
    await page.evaluate(before=>window.__nativeCalls.filter(c=>c.method==='document.open').length===before,opensBeforeBrowse),
    'Function name switches to File without pointing at Main or opening another document');
  await page.getByRole('button',{name:'Chức năng',exact:true}).click();
  const functionRowAgain=page.getByRole('button',{name:'Nhân viên',exact:true});
  await functionRowAgain.focus();await functionRowAgain.press('F12');
  await page.getByRole('tab',{name:'menu1-menu.sql',exact:true}).waitFor();
  check(await page.evaluate(()=>window.__nativeCalls.some(c=>c.method==='project.menu.insertScript'&&c.payload.wmenuId==='menu1')),
    'F12 generates INSERT scripts for wcommand and command');
  await functionRowAgain.click({button:'right'});
  check(await page.getByRole('button',{name:/Tạo script INSERT menu/}).isVisible(),'ERP menu context menu exposes the INSERT script action');
  await page.keyboard.press('Escape');
  await testMenuEditor(functionRowAgain);
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
  check(await page.evaluate(before=>window.__nativeCalls.filter(c=>c.method==='document.open').length===before+1,openedBefore),
    'Browsing controllers opens only the Main file explicitly selected in File');
  const diagnosticPath=dirPath.replace(/\\Dir$/,'\\Filter')+'\\nhanvien.xml';
  const diagnosticFile=fileTree.getByTitle(diagnosticPath,{exact:true});
  await diagnosticFile.waitFor();
  await page.waitForFunction(path=>[...document.querySelectorAll('button[title]')]
    .some(button=>button.title===path&&button.getAttribute('aria-invalid')==='true'),diagnosticPath);
  check(await diagnosticFile.getAttribute('data-diagnostic')==='error',
    'Controller scan marks only the file with an undeclared entity in red');
  await diagnosticFile.click();
  const diagnosticBanner=page.locator('.editor-diagnostic-banner').filter({hasText:'MissingEntity'});
  await diagnosticBanner.waitFor();
  check((await diagnosticBanner.textContent()).includes('Dòng 2, cột 3')&&
    (await diagnosticBanner.textContent()).includes(diagnosticPath),
    'Opening an invalid controller shows a persistent banner with entity, position, and path');
  await page.locator('.editor-group.focused .monaco-editor').click({position:{x:160,y:48}});
  await page.keyboard.press('Control+Home');
  await page.keyboard.insertText('<!DOCTYPE root [<!ENTITY MissingEntity "đã khai báo">]>\r\n');
  await page.waitForFunction(path=>window.__nativeCalls.some(call=>call.method==='xml.diagnostics'&&
    call.payload.path===path&&call.payload.text.includes('<!ENTITY MissingEntity ')),diagnosticPath);
  await diagnosticBanner.waitFor({state:'detached'});
  await page.waitForFunction(path=>[...document.querySelectorAll('button[title]')]
    .some(button=>button.title===path&&!button.hasAttribute('aria-invalid')),diagnosticPath);
  check(true,'Correct declaration clears the editor banner and red file state after realtime validation');
  check(await fileTree.getByTitle(dirPath+'\\bophan.xml',{exact:true}).count()===0,
    'The controller tree excludes files from other controllers in the same directory');
  check(await fileTree.locator('.category-heading').allTextContents().then(names=>
    ['Main','Dir','Filter','Grid','Report','Templates'].every(name=>names.includes(name))),
    'Real folder tree keeps the colored categories');
  check(await fileTree.locator('.category-heading').first().evaluate(element => getComputedStyle(element).borderTopWidth==='2px'),
    'The category color stripe sits above the folder heading');
  await page.getByRole('button',{name:'Kết quả',exact:true}).click();
  await dirFile.click();
  await page.waitForFunction(()=>document.querySelector('#results-panel')?.classList.contains('collapsed'));
  await page.getByRole('button',{name:'Kết quả',exact:true}).click();
  await page.waitForFunction(()=>!document.querySelector('#results-panel')?.classList.contains('collapsed'));
  await gridFile.click();
  await page.waitForFunction(()=>document.querySelector('#results-panel')?.classList.contains('collapsed'));
  check(true,'Opening or selecting an XML file temporarily collapses the bottom panel');
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
  await page.getByRole('option',{name:'Dir',exact:true}).click();
  await page.getByRole('searchbox',{name:'Nội dung tìm'}).fill('controller');
  await page.locator('#project-sidebar').getByRole('button',{name:'Tìm',exact:true}).click();
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
  const tableInput=tablePanel.getByRole('combobox',{name:'FROM',exact:true});
  check(await tableInput.evaluate(input=>input===document.activeElement),'Opening table inspection focuses the table field');
  await page.getByRole('button',{name:'DB',exact:true}).click();
  const databaseTableRow=page.locator('.db-row').filter({hasText:'dbo.DanhMuc'});
  await databaseTableRow.waitFor();
  await databaseTableRow.click();
  await tablePanel.locator('.data-grid td[title="1"]').waitFor();
  check(await tableInput.inputValue()==='DanhMuc',
    'Clicking a DB table loads the table tab without blanking the WebView');
  for(const name of ['SELECT','WHERE']){
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
  await tablePanel.locator('.table-data-grid td[title="1"]').click();
  await page.keyboard.press('ArrowRight');
  check(await tablePanel.locator('.table-data-grid td[title="x"]').evaluate(cell=>getComputedStyle(cell).outlineStyle==='solid'),
    'A single click selects one cell and arrow keys move the active cell');
  await tablePanel.locator('.table-data-grid thead th').filter({hasText:'label'}).click();
  await page.keyboard.press('Control+c');
  await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='clipboard.write'&&call.payload.text==='x\r\ny'));
  check(true,'Table grid selects and copies an entire column like the SQL result grid');
  check(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.method==='table.catalog').length===1 &&
    !window.__nativeCalls.some(c=>c.method==='table.structure'&&c.payload.table==='danh')),
    'Table search reuses one catalog and opens the selected table immediately');
  await tablePanel.getByRole('button',{name:'Trường lọc',exact:true}).click();
  await page.getByRole('option',{name:'code',exact:true}).click();
  await tablePanel.getByRole('button',{name:'Toán tử',exact:true}).click();
  await page.getByRole('option',{name:'LIKE',exact:true}).click();
  await tablePanel.getByRole('textbox',{name:'Giá trị lọc',exact:true}).fill('ab');
  await tablePanel.getByRole('button',{name:'Lọc',exact:true}).click();
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='table.view'&&c.payload.where==="[code] LIKE N'%ab%'"));
  await tablePanel.getByText('Không tìm thấy dữ liệu phù hợp.',{exact:true}).waitFor();
  check(true,'Table filter controls build a contains WHERE condition and show an empty result state');
  await tablePanel.getByRole('button',{name:'Trường lọc',exact:true}).click();
  await page.getByRole('option',{name:'Chọn trường',exact:true}).click();
  await tablePanel.getByRole('combobox',{name:'WHERE',exact:true}).fill('co');
  await tablePanel.getByRole('option',{name:'code',exact:true}).waitFor();
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Tab');
  check(await tablePanel.getByRole('combobox',{name:'WHERE',exact:true}).inputValue()==='code',
    'Tab inserts a selected ordinary column without brackets');
  const selectInput=tablePanel.getByRole('combobox',{name:'SELECT',exact:true});
  await selectInput.fill('cou');
  await selectInput.press('Escape');
  await selectInput.press('Control+Space');
  await tablePanel.getByRole('option',{name:'COUNT',exact:true}).click();
  await page.waitForFunction(()=>document.querySelector('input[aria-label="SELECT"]')?.selectionStart===6);
  check(await selectInput.inputValue()==='COUNT()','SQL function completion inserts parentheses and places the caret inside');
  await selectInput.fill('COUNT(*) AS total');
  const whereInput=tablePanel.getByRole('combobox',{name:'WHERE',exact:true});
  await whereInput.fill('code IS NOT N');
  await tablePanel.getByRole('option',{name:'IS NOT NULL',exact:true}).click();
  await page.screenshot({path:out+'/table-catalog-sql.png'});
  await tablePanel.getByRole('button',{name:'Chạy',exact:true}).click();
  await page.waitForFunction(()=>window.__nativeCalls.some(c=>c.method==='table.view' &&
    c.payload.select==='COUNT(*) AS total' && c.payload.where==='code IS NOT NULL' && !c.payload.orderBy));
  check(true,'SELECT and WHERE suggestions produce SQL expressions in the query');
  check(await tablePanel.getByRole('button',{name:'Dữ liệu',exact:true}).count()===0 &&
    await tablePanel.getByRole('button',{name:'Cấu trúc',exact:true}).count()===0,
    'Table inspection uses one editable data workspace');
  await selectInput.fill('*');await whereInput.fill('');
  await tablePanel.getByRole('button',{name:'Chạy',exact:true}).click();
  await tablePanel.locator('.data-grid td[title="1"]').waitFor();
  const editableCell=tablePanel.locator('.table-data-grid td[title="x"]');
  await editableCell.dblclick();
  const editableInput=tablePanel.getByRole('textbox',{name:'label dòng 1',exact:true});
  await editableInput.fill('đã sửa');await editableInput.press('Enter');
  check(await page.evaluate(()=>!window.__nativeCalls.some(call=>call.method==='table.writeBatch')), 'Enter stages the edit');
  await page.keyboard.press('Control+s');
  await page.waitForFunction(()=>window.__nativeCalls.some(call=>call.method==='table.writeBatch'&&call.payload.rows[0].mode==='update'));
  await tablePanel.locator('.table-data-grid td[title="1"]').dblclick();
  const keyEditor=tablePanel.locator('.table-data-grid input[data-edit-cell="0:0"]');
  await keyEditor.fill('duplicate-fixture');
  await keyEditor.press('Enter');
  await tablePanel.getByRole('button',{name:'Lưu',exact:true}).click();
  await tablePanel.locator('.table-data-grid td[aria-invalid="true"][title="1"]').waitFor();
  check(true,'A duplicate key restores and marks the original cell');
  check(true,'Editable data grid supports direct cell editing');
  await page.getByRole('button',{name:'Tập lệnh',exact:true}).click();
  check(await page.locator('.table-script-menu:popover-open').evaluate(element=>getComputedStyle(element).position==='fixed'),
    'Table action menu opens in the top layer without toolbar clipping');
  await page.getByRole('menuitem',{name:'Thêm key vào SQL Data'}).click();
  await page.getByRole('tab',{name:'Generate Update',exact:true}).waitFor();
  await firstTableTab.click();
  await tablePanel.locator('.table-data-grid td[title="1"]').first().click({button:'right'});
  const tableScriptContextMenu=page.getByRole('menu',{name:'Loại lệnh SQL'});
  await tableScriptContextMenu.waitFor();
  check(await tableScriptContextMenu.locator('[role="menuitem"], [role="menuitemcheckbox"]').count()===5 &&
    await tableScriptContextMenu.getByRole('menuitemcheckbox',{name:/^Đóng băng đến cột /}).isVisible() &&
    await tableScriptContextMenu.getByRole('menuitem',{name:'Thêm key vào SQL Data'}).isVisible() &&
    await tableScriptContextMenu.getByRole('menuitem',{name:'Tạo lệnh tạo bảng',exact:true}).isVisible() &&
    await tableScriptContextMenu.getByRole('menuitem',{name:'Tạo lệnh INSERT cho kết quả hiện tại'}).isVisible() &&
    await tableScriptContextMenu.getByRole('menuitem',{name:'Tạo lệnh xóa bảng',exact:true}).isVisible(),
    'Right click on table data opens the complete SQL script menu');
  await tableScriptContextMenu.getByRole('menuitem',{name:'Tạo lệnh tạo bảng',exact:true}).click();
  await page.getByRole('tab',{name:'DanhMuc-create.sql'}).waitFor();
  await firstTableTab.click();
  await page.getByRole('button',{name:'Tập lệnh',exact:true}).click();
  await page.getByRole('menuitem',{name:'Tạo lệnh xóa bảng',exact:true}).click();
  await page.getByRole('tab',{name:'DanhMuc-drop.sql'}).waitFor();
  check(true,'Table action menu captures SQL Data keys and opens create/drop scripts');
  await firstTableTab.click();
  const queryActions = tablePanel.locator('.table-query-actions');
  check(await queryActions.getByRole('button',{name:'Chạy',exact:true}).isVisible() &&
    await queryActions.getByRole('button',{name:'Lưu',exact:true}).isVisible(),'Save sits alongside Run');
  await page.getByRole('button',{name:'Tập lệnh',exact:true}).click();
  await page.getByRole('menuitem',{name:'Tạo lệnh INSERT cho kết quả hiện tại'}).click();
  await page.getByRole('tab',{name:'DanhMuc-insert.sql'}).waitFor();
  check(await page.evaluate(()=>window.__nativeCalls.some(call=>call.method==='table.insertScript' &&
    call.payload.scope && call.payload.snapshotId==='fixture-preview')), 'INSERT exports the current scoped snapshot');
  await page.keyboard.press('Control+Shift+t');
  const secondTableTab=page.getByRole('tab',{name:/^Cấu trúc và dữ liệu 2/});
  const secondTablePanel=page.locator('#'+await secondTableTab.getAttribute('aria-controls'));
  const secondTableInput=secondTablePanel.getByRole('combobox',{name:'FROM',exact:true});
  await secondTableInput.fill('danh');
  await secondTablePanel.getByRole('option',{name:'DanhMuc_Archive',exact:true}).click();
  await secondTablePanel.locator('.data-grid td[title="1"]').waitFor();
  const secondTableName=await secondTableInput.inputValue();
  await firstTableTab.click();
  check(await tableInput.inputValue()==='DanhMuc' && secondTableName==='DanhMuc_Archive',
    'Independent inspection tabs retain their selected tables');
  check(await page.evaluate(()=>new Set(window.__nativeCalls.filter(call=>call.method==='table.view').map(call=>call.payload.scope)).size===2),
    'Inspection data previews use independent native scopes');
  check(await page.evaluate(()=>window.__nativeCalls.filter(call=>call.method==='table.catalog').length===1),
    'Inspection tabs reuse the cached catalog for the same connection');
  await page.getByRole('button',{name:'SQL Profiler',exact:true}).click();
  await secondTableTab.click();
  const tableDatabase=page.getByRole('button',{name:'Cơ sở dữ liệu',exact:true});
  const nextDb=(await tableDatabase.textContent()).includes('App')?'Sys':'App';
  await tableDatabase.click({button:'right'});
  await page.waitForFunction(name=>window.__nativeCalls.filter(call=>call.method==='clipboard.write').at(-1)?.payload.text===name,
    nextDb==='Sys'?'Fixture_App':'Fixture_Sys');
  await tableDatabase.click();
  check(await page.getByRole('option',{name:nextDb,exact:true}).isVisible(),'App/Sys database dropdown renders above the workspace');
  await page.keyboard.press('Escape');
  await page.keyboard.press('Control+Alt+d');
  await page.waitForFunction(label=>document.querySelector('button[aria-label="Cơ sở dữ liệu"]')?.textContent?.includes(label),nextDb);
  check(await secondTablePanel.locator('.data-grid').count()===0 &&
    await secondTableInput.inputValue()==='DanhMuc_Archive','Switching App/Sys keeps the table name and clears the old preview');
  await secondTablePanel.getByRole('button',{name:'Chạy',exact:true}).click();
  await page.waitForFunction(db=>window.__nativeCalls.filter(c=>c.method==='table.view').at(-1)?.payload.targetId===db,
    nextDb==='Sys'?'project-sys':'project-app');
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
  const projectHistory=page.locator('.history-group').filter({has:page.locator('summary').getByText('Dự án thử UI',{exact:true})});
  const header=projectHistory.locator(':scope > summary');
  await header.waitFor();
  check(await page.locator('.project-file-tree').getByText('Dự án lịch sử',{exact:true}).count()===0,
    'History only shows the current project');
  const historySearch=page.getByRole('searchbox',{name:'Tìm file hoặc thư mục trong lịch sử'});
  await historySearch.fill('BULK-0.XML');
  const historyFile=projectHistory.getByRole('button',{name:'bulk-0.xml',exact:true});
  await historyFile.waitFor();
  await header.click({button:'right'});
  await page.getByRole('menuitem',{name:'Close project',exact:true}).click();
  await historyFile.waitFor({state:'detached'});
  check(await page.getByRole('tab',{name:'bulk-0.xml',exact:true}).isVisible() &&
    await page.evaluate(()=>window.__nativeCalls.some(call=>call.method==='project.history.clear'&&call.payload.id==='fixture')),
    'Close project clears history and keeps editor tabs open');
  if(errors.length)console.log(JSON.stringify(errors,null,2));check(errors.length===0,'No uncaught browser errors');
  writeFileSync(out+'/interaction-report.json',JSON.stringify({passed:true,checks,transport:'explicit browser fixture; not a .NET/SQL integration test',errors},null,2));
  }
}finally{await browser.close();}
