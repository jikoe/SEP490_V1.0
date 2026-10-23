const fs=require('fs');
const path=require('path');
const {chromium}=require('playwright');
(async()=>{
 const root=path.resolve(__dirname,'..');
 const sourceFile=process.argv[2]||'output/UC_UML_Gomaa_8UC.drawio';
 const outputFolder=process.argv[3]||'tmp/drawio-render-check';
 const checkFile=process.argv[4]||'output/UC_UML_Drawio/RENDER_CHECKS.json';
 const browser=await chromium.launch({channel:'msedge',headless:true});
 const page=await browser.newPage({viewport:{width:1400,height:1600},deviceScaleFactor:1});
 const errors=[];
 page.on('pageerror',e=>errors.push(String(e)));
 await page.route('**/*',r=>r.abort());
 await page.setContent('<html><body style="margin:0;background:white"><div id="graph" style="position:absolute;inset:0;overflow:hidden"></div></body></html>');
 await page.addScriptTag({content:'window.mxLoadResources=false;window.mxLoadStylesheets=false;window.urlParams={};'});
 await page.addScriptTag({path:path.join(root,'tmp/drawio-viewer-static.min.js')});
 console.log(await page.evaluate(()=>({Graph:typeof Graph,mxCodec:typeof mxCodec})));
 const xml=fs.readFileSync(path.join(root,sourceFile),'utf8');
 const out=path.join(root,outputFolder);fs.mkdirSync(out,{recursive:true});
 const count=await page.evaluate(xml=>mxUtils.parseXml(xml).getElementsByTagName('diagram').length,xml);
 const rows=[];
 for(let i=0;i<count;i++){
  const result=await page.evaluate(({xml,i})=>{
   if(window.qaGraph)window.qaGraph.destroy();
   const div=document.getElementById('graph');div.innerHTML='';
   const graph=new Graph(div);window.qaGraph=graph;
   graph.setEnabled(false);
   const doc=mxUtils.parseXml(xml);const d=doc.getElementsByTagName('diagram')[i];
   const modelDoc=mxUtils.parseXml(mxUtils.getXml(d.getElementsByTagName('mxGraphModel')[0]));
   new mxCodec(modelDoc).decode(modelDoc.documentElement,graph.getModel());
   graph.view.validate();
   const bounds=graph.getGraphBounds();
   const scale=Math.min(1,1360/bounds.width,1540/bounds.height);
   graph.view.scaleAndTranslate(scale,20/scale-bounds.x,20/scale-bounds.y);
   graph.view.validate();
   let visible=0;let text=0;let edges=0;
   Object.values(graph.getModel().cells).forEach(c=>{if(c.vertex||c.edge){if(graph.view.getState(c))visible++;if(c.value)text++;if(c.edge)edges++;}});
   return {name:d.getAttribute('name'),visible,text,edges,width:bounds.width,height:bounds.height,scale};
  },{xml,i});
  await page.screenshot({path:path.join(out,`${String(i+1).padStart(2,'0')}.png`)});
  if(!result.visible || !result.text || !result.edges) throw new Error('Empty render: '+result.name);
  rows.push(result);
 }
 fs.writeFileSync(path.join(out,'render-check.json'),JSON.stringify({errors,rows},null,2));
 fs.writeFileSync(path.join(root,checkFile),JSON.stringify({errors,rows},null,2));
 console.log(JSON.stringify({pages:rows.length,errors,first:rows[0],last:rows.at(-1)}));
 await browser.close();
 if(errors.length)process.exitCode=1;
})().catch(e=>{console.error(e);process.exit(1)});
