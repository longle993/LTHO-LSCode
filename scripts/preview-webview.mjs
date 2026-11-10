import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
const root=path.resolve('src/LSCode.Web/dist');
const mime={'.html':'text/html; charset=utf-8','.js':'text/javascript','.css':'text/css','.svg':'image/svg+xml','.ttf':'font/ttf'};
http.createServer((req,res)=>{try{const file=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname==='/'?'/index.html':new URL(req.url,'http://localhost').pathname));if(!file.startsWith(root+path.sep)){res.writeHead(403).end();return;}res.setHeader('Content-Type',mime[path.extname(file)]??'application/octet-stream');fs.createReadStream(file).on('error',()=>res.writeHead(404).end()).pipe(res);}catch{res.writeHead(400).end();}}).listen(4173,'127.0.0.1',()=>console.log('LSCode preview: http://127.0.0.1:4173'));
