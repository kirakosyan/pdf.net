import {spawn} from 'node:child_process';
import {mkdtempSync,chmodSync,writeFileSync,rmSync} from 'node:fs';
import path from 'node:path';
if(process.platform!=='linux') throw Error('This trusted original-fixture host has Linux applicability only; it is not a qualified SDK host.');
const [browser,root]=process.argv.slice(2);if(!path.isAbsolute(browser)||!path.isAbsolute(root))throw Error('Explicit absolute browser and temporary parent required');
const profile=mkdtempSync(path.join(root,'html-fixture-'));chmodSync(profile,0o700);
const child=spawn(browser,['--headless','--disable-gpu','--no-startup-window','--no-first-run','--no-default-browser-check','--disable-background-networking','--disable-component-update','--disable-default-apps','--disable-sync','--no-pings','--metrics-recording-only','--host-resolver-rules=MAP * ~NOTFOUND','--proxy-server=http://127.0.0.1:9','--proxy-bypass-list=<-loopback>','--remote-debugging-pipe','--user-data-dir='+profile],{stdio:['ignore','ignore','pipe','pipe','pipe'],detached:true,env:{PATH:'/usr/bin:/bin',LANG:'C.UTF-8',TMPDIR:profile}});
let incoming='',outgoing=Buffer.alloc(0),stderr='',shutting=false;
function kill(){try{process.kill(-child.pid,'SIGKILL')}catch{}}
const deadline=setTimeout(kill,30000);
process.stdin.setEncoding('utf8');process.stdin.on('data',chunk=>{incoming+=chunk;if(incoming.length>67108864)throw Error('Source frame bound');for(let at;(at=incoming.indexOf('\n'))>=0;){const frame=incoming.slice(0,at);incoming=incoming.slice(at+1);if(frame)child.stdio[3].write(frame+'\0');}});
process.stdin.on('end',()=>{shutting=true;try{child.stdio[3].write(JSON.stringify({id:2000000000,method:'Browser.close'})+'\0')}catch{kill()}});
child.stdio[4].on('data',chunk=>{outgoing=Buffer.concat([outgoing,chunk]);if(outgoing.length>67108864)throw Error('Source frame bound');for(let at;(at=outgoing.indexOf(0))>=0;){const frame=outgoing.subarray(0,at);outgoing=outgoing.subarray(at+1);if(frame.length&&!shutting)process.stdout.write(frame.toString('utf8')+'\n');}});
child.stderr.on('data',chunk=>{if(stderr.length<65536)stderr+=chunk.toString('utf8').slice(0,65536-stderr.length)});
child.on('error',error=>{writeFileSync(profile+'/host-error.txt',error.message);kill();process.exitCode=1;});
child.on('exit',(code,signal)=>{clearTimeout(deadline);writeFileSync(profile+'/host-observation.json',JSON.stringify({browser,code,signal,stderr},null,2));process.exitCode=shutting&&code===0?0:1;process.stdin.destroy();});
process.on('SIGTERM',kill);process.on('uncaughtException',error=>{writeFileSync(profile+'/host-error.txt',error.message);kill();process.exitCode=1;});
