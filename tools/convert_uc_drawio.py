"""Convert the 16 rendered UML views into native, editable mxGraph objects.

SVG is used only as layout geometry. No image payload is embedded in draw.io.
Class boxes and participant/lifeline groups retain connected native edges.
"""
from pathlib import Path
from lxml import etree as E
import base64
import copy
import json
import math
import re
import zlib
from zipfile import ZipFile, ZIP_DEFLATED
import argparse

ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser()
parser.add_argument('--source',default='output/UC_UML_Gomaa')
parser.add_argument('--individual-output',default='output/UC_UML_Drawio')
parser.add_argument('--combined-output',default='output/UC_UML_Gomaa_8UC.drawio')
parser.add_argument('--zip-output',default='output/UC_UML_Drawio_8UC.zip')
args=parser.parse_args()
SRC=ROOT/args.source
OUT=ROOT/args.individual_output
OUT.mkdir(exist_ok=True)
NS={'s':'http://www.w3.org/2000/svg'}
manifest=json.loads((SRC/'manifest.json').read_text(encoding='utf-8'))

def num(el,key,default=0): return float(str(el.get(key,default)).removesuffix('px'))
def tag(el): return E.QName(el).localname if isinstance(el.tag,str) else ''
def style(el):
    d=dict(el.attrib)
    for part in el.get('style','').split(';'):
        if ':' in part:
            k,v=part.split(':',1); d[k]=v
    return d
def fmt(n): return str(round(n,4))

def path_points(data):
    tokens=re.findall(r'[MLCAmlca]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?',data)
    i=0; cmd=None; current=(0.,0.); points=[]
    while i<len(tokens):
        if tokens[i].isalpha(): cmd=tokens[i].upper(); i+=1
        count={'M':2,'L':2,'C':6,'A':7}[cmd]
        vals=list(map(float,tokens[i:i+count])); i+=count
        if cmd in ('M','L'):
            current=tuple(vals); points.append(current)
            if cmd=='M': cmd='L'
        elif cmd=='C':
            p0=current; p1=vals[:2]; p2=vals[2:4]; p3=vals[4:]
            for j in range(1,17):
                t=j/16; u=1-t
                points.append(tuple(u**3*p0[k]+3*u*u*t*p1[k]+3*u*t*t*p2[k]+t**3*p3[k] for k in (0,1)))
            current=tuple(p3)
        elif cmd=='A':
            rx,ry,rot,large,sweep,x2,y2=vals; rx=abs(rx); ry=abs(ry)
            if rx==0 or ry==0 or current==(x2,y2):
                current=(x2,y2); points.append(current); continue
            phi=math.radians(rot); cp=math.cos(phi); sp=math.sin(phi)
            dx=(current[0]-x2)/2; dy=(current[1]-y2)/2
            xp=cp*dx+sp*dy; yp=-sp*dx+cp*dy
            scale=xp*xp/(rx*rx)+yp*yp/(ry*ry)
            if scale>1: rx*=math.sqrt(scale); ry*=math.sqrt(scale)
            coeff=math.sqrt(max(0,(rx*rx*ry*ry-rx*rx*yp*yp-ry*ry*xp*xp)/(rx*rx*yp*yp+ry*ry*xp*xp)))
            if bool(large)==bool(sweep): coeff=-coeff
            cxp=coeff*rx*yp/ry; cyp=-coeff*ry*xp/rx
            cx=cp*cxp-sp*cyp+(current[0]+x2)/2; cy=sp*cxp+cp*cyp+(current[1]+y2)/2
            theta=math.atan2((yp-cyp)/ry,(xp-cxp)/rx)
            end=math.atan2((-yp-cyp)/ry,(-xp-cxp)/rx)
            delta=(end-theta)%(2*math.pi)
            if not sweep: delta-=2*math.pi
            for j in range(1,17):
                a=theta+delta*j/16
                points.append((cx+cp*rx*math.cos(a)-sp*ry*math.sin(a),cy+sp*rx*math.cos(a)+cp*ry*math.sin(a)))
            current=(x2,y2)
    return points

def points(el):
    t=tag(el)
    if t=='path': return path_points(el.get('d',''))
    if t=='polygon':
        vals=list(map(float,re.findall(r'[-+]?\d*\.?\d+',el.get('points',''))))
        return list(zip(vals[::2],vals[1::2]))
    if t=='line': return [(num(el,'x1'),num(el,'y1')),(num(el,'x2'),num(el,'y2'))]
    if t=='ellipse':
        x,y=num(el,'cx'),num(el,'cy'); rx,ry=num(el,'rx'),num(el,'ry')
        return [(x-rx,y-ry),(x+rx,y+ry)]
    if t=='text':
        x,y=num(el,'x'),num(el,'y'); fs=num(el,'font-size',14)
        return [(x,y-fs*.94),(x+num(el,'textLength',len(''.join(el.itertext()))*fs*.55)+2,y+fs*.22)]
    if t=='rect':
        x,y=num(el,'x'),num(el,'y'); return [(x,y),(x+num(el,'width'),y+num(el,'height'))]
    return []

def bounds(elements):
    pts=[p for el in elements for p in points(el)]
    return (min(p[0] for p in pts),min(p[1] for p in pts),max(p[0] for p in pts),max(p[1] for p in pts))

class Page:
    def __init__(self,svg,name):
        self.svg=svg; self.name=name; self.counter=0; self.parents={}; self.consumed=set(); self.semantic={}
        self.model=E.Element('mxGraphModel',dx='1400',dy='1000',grid='1',gridSize='10',guides='1',tooltips='1',connect='1',arrows='1',fold='1',page='1',pageScale='1',pageWidth=fmt(num(svg,'width')+30),pageHeight=fmt(num(svg,'height')+30),math='0',shadow='0')
        self.root=E.SubElement(self.model,'root'); E.SubElement(self.root,'mxCell',id='0'); E.SubElement(self.root,'mxCell',id='1',parent='0')
    def ident(self): self.counter+=1; return f'c{self.counter}'
    def vertex(self,value,sty,bbox,parent='1',ident=None):
        x,y,x2,y2=bbox
        if parent!='1':
            px,py,_,_=self.parents[parent]; x-=px; y-=py; x2-=px; y2-=py
        ident=ident or self.ident()
        c=E.SubElement(self.root,'mxCell',id=ident,value=value,style=sty,vertex='1',parent=parent)
        E.SubElement(c,'mxGeometry',x=fmt(x),y=fmt(y),width=fmt(max(.01,x2-x)),height=fmt(max(.01,y2-y)),attrib={'as':'geometry'})
        return c
    def group(self,elements,semantic=None):
        b=bounds(elements); ident=self.ident()
        self.vertex('', 'group;connectable=1;',b,ident=ident)
        self.parents[ident]=b
        if semantic: self.semantic[semantic]=ident
        for el in elements:
            self.primitive(el,ident); self.consumed.add(el)
        return ident
    def edge(self,pts,sty,parent='1',source=None,target=None):
        ident=self.ident(); attrs=dict(id=ident,value='',style=sty,edge='1',parent=parent)
        for role,key,p in [('source',source,pts[0]),('target',target,pts[-1])]:
            if key in self.semantic:
                pid=self.semantic[key]; attrs[role]=pid
                x,y,x2,y2=self.parents[pid]
                prefix='exit' if role=='source' else 'entry'
                attrs['style']+=f'{prefix}X={fmt((p[0]-x)/max(.01,x2-x))};{prefix}Y={fmt((p[1]-y)/max(.01,y2-y))};{prefix}Perimeter=0;'
        cell=E.SubElement(self.root,'mxCell',**attrs)
        geo=E.SubElement(cell,'mxGeometry',relative='1',attrib={'as':'geometry'})
        ox,oy=(self.parents[parent][:2] if parent!='1' else (0,0))
        for p,role in [(pts[0],'sourcePoint'),(pts[-1],'targetPoint')]: E.SubElement(geo,'mxPoint',x=fmt(p[0]-ox),y=fmt(p[1]-oy),attrib={'as':role})
        if len(pts)>2:
            arr=E.SubElement(geo,'Array',attrib={'as':'points'})
            for p in pts[1:-1]: E.SubElement(arr,'mxPoint',x=fmt(p[0]-ox),y=fmt(p[1]-oy))
        return cell
    def primitive(self,el,parent='1'):
        t=tag(el); s=style(el)
        if t not in ('rect','ellipse','line','text','polygon','path'): return
        if s.get('fill-opacity')=='0.00000': return
        stroke=s.get('stroke','none'); fill=s.get('fill','none')
        base=f'strokeColor={stroke};fillColor={fill};strokeWidth={s.get("stroke-width","1")};'
        if 'stroke-dasharray' in s: base+='dashed=1;dashPattern='+s['stroke-dasharray'].replace(',',' ')+';'
        if t=='text':
            text=''.join(el.itertext()); fs=num(el,'font-size',14)
            flags=(1 if s.get('font-weight')=='bold' else 0)+(2 if s.get('font-style')=='italic' else 0)
            self.vertex(text,f'text;html=0;align=left;verticalAlign=middle;spacing=0;whiteSpace=nowrap;overflow=visible;resizable=1;fontFamily=Arial;fontSize={fs};fontColor={fill};fontStyle={flags};',bounds([el]),parent)
        elif t in ('rect','ellipse'):
            shape='ellipse' if t=='ellipse' else 'rectangle'
            self.vertex('',f'shape={shape};rounded=0;{base}',bounds([el]),parent)
        elif t in ('line','path') and fill=='none':
            subpaths=re.findall(r'M[^M]*',el.get('d','')) if t=='path' else []
            sets=[path_points(data) for data in subpaths] if subpaths else [points(el)]
            for pts in sets:
                if len(pts)>1:self.edge(pts,'edgeStyle=none;rounded=0;startArrow=none;endArrow=none;'+base,parent)
        else:
            pts=points(el)
            if len(pts)<2:return
            x,y,x2,y2=bounds([el]); w=max(1,x2-x); h=max(1,y2-y)
            shape=E.Element('shape',name='UMLVector',w=fmt(w),h=fmt(h),aspect='variable',strokewidth='inherit')
            fg=E.SubElement(shape,'foreground'); path=E.SubElement(fg,'path')
            for i,(px,py) in enumerate(pts): E.SubElement(path,'move' if i==0 else 'line',x=fmt(px-x),y=fmt(py-y))
            if fill!='none':E.SubElement(path,'close')
            E.SubElement(fg,'fillstroke')
            raw=E.tostring(shape); enc=base64.b64encode(zlib.compress(raw)[2:-4]).decode()
            self.vertex('',f'shape=stencil({enc});{base}',(x,y,x2,y2),parent)
    def convert(self,kind,puml):
        elems=[el for el in self.svg.iter() if tag(el) in ('rect','ellipse','line','text','polygon','path')]
        # Convert frames first, preserving their original SVG stacking order.
        groups=self.svg.xpath('.//s:g',namespaces=NS)
        deferred=set()
        sem_groups=[g for g in groups if (g.get('class')=='entity' if kind=='class' else 'participant-head' in g.get('class',''))]
        for g in sem_groups:
            semantic=g.get('data-entity') if kind=='class' else g.get('data-participant')
            leaves=[e for e in g.iter() if e in elems]
            if kind=='sequence':
                b=bounds(leaves); cx=(b[0]+b[2])/2
                # Locate the matching vertical lifeline by its visible x coordinate.
                candidates=[e for e in elems if tag(e)=='line' and abs(num(e,'x1')-num(e,'x2'))<.1 and abs(num(e,'x1')-cx)<6 and num(e,'y2')-num(e,'y1')>150]
                if candidates: leaves+=candidates[:1]
            deferred.update(leaves)
        linkgroups=[g for g in groups if g.get('class') in ('link','message')]
        for g in linkgroups: deferred.update(e for e in g.iter() if e in elems)
        activation_rects=[]
        overlay_text=[]
        if kind=='sequence':
            activation_rects=[e for e in elems if e not in deferred and tag(e)=='rect' and num(e,'width')<=16 and style(e).get('fill')=='#78C9ED']
            deferred.update(activation_rects)
            overlay_text=[e for e in elems if e not in deferred and tag(e)=='text']
            deferred.update(overlay_text)
        for el in elems:
            if el not in deferred: self.primitive(el);self.consumed.add(el)
        for g in sem_groups:
            leaves=[e for e in g.iter() if e in elems]
            semantic=g.get('data-entity') if kind=='class' else g.get('data-participant')
            if kind=='sequence':
                b=bounds(leaves);cx=(b[0]+b[2])/2
                candidates=[e for e in elems if tag(e)=='line' and abs(num(e,'x1')-num(e,'x2'))<.1 and abs(num(e,'x1')-cx)<6 and num(e,'y2')-num(e,'y1')>150]
                if candidates:leaves+=candidates[:1]
            self.group(leaves,semantic)
        # Activations must cover the dashed lifeline, as in the source drawing.
        for el in activation_rects:self.primitive(el)
        for el in overlay_text:self.primitive(el)
        for g in linkgroups:
            leaves=[e for e in g.iter() if e in elems]
            a=g.get('data-entity-1') or g.get('data-participant-1'); b=g.get('data-entity-2') or g.get('data-participant-2')
            paths=[e for e in leaves if tag(e)=='path' and style(e).get('fill','none')=='none']
            lines=[e for e in leaves if tag(e)=='line']
            main=paths[0] if paths else (max(lines,key=lambda e:math.hypot(num(e,'x2')-num(e,'x1'),num(e,'y2')-num(e,'y1'))) if lines else None)
            if main is None:
                for el in leaves:self.primitive(el)
                continue
            pts=points(main); arrow='endArrow=none;startArrow=none;'
            if kind=='sequence':
                if a==b and not paths and len(lines)>=3:
                    # PlantUML emits a self-call as three separate line segments.
                    # Keep all three as one editable hooked mxGraph connector.
                    pts=[points(lines[0])[0],points(lines[0])[1],points(lines[1])[1],points(lines[2])[0]]
                filled_head=any(tag(el)=='polygon' and style(el).get('fill','none')!='none' for el in leaves)
                arrow=('endArrow=block;endFill=1;' if filled_head else 'endArrow=open;endFill=0;')+'endSize=10;startArrow=none;'
                if a!=b and a in self.semantic and b in self.semantic:
                    ax=self.parents[self.semantic[a]][0];bx=self.parents[self.semantic[b]][0]
                    if (pts[-1][0]>pts[0][0]) != (bx>ax):pts.reverse()
            else:
                rel=next((line for line in puml.splitlines() if re.match(r'^'+re.escape(a)+r'(?: "[^"]*")? [.*\-<>]+(?: "[^"]*")? '+re.escape(b)+r'(?: |$)',line)), '')
                if '..>' in rel:arrow='endArrow=open;endSize=8;startArrow=none;'
                if '*--' in rel:arrow='startArrow=diamond;startFill=1;startSize=9;endArrow=none;'
            s=style(main);base=f'edgeStyle=none;rounded=0;strokeColor={s.get("stroke","#334155")};strokeWidth={s.get("stroke-width","1")};'+arrow
            if kind=='sequence':base+='noEdgeStyle=1;'
            if 'stroke-dasharray' in s:base+='dashed=1;dashPattern='+s['stroke-dasharray'].replace(',',' ')+';'
            self.edge(pts,base,source=a,target=b)
            for el in leaves:
                if tag(el)=='text':self.primitive(el)
        return self.model

combined=E.Element('mxfile',host='app.diagrams.net',agent='Codex',version='26.0.14',type='device')
stats=[]
for i,item in enumerate(manifest):
    for kind in ['class','sequence']:
        svg=E.parse(str(SRC/item['code']/f'{kind}.svg')).getroot()
        puml=(SRC/item['code']/f'{kind}.puml').read_text(encoding='utf-8')
        name=f'{item["code"]} - {kind.title()}'
        page=Page(svg,name); model=page.convert(kind,puml)
        diagram=E.SubElement(combined,'diagram',id=f'{item["code"]}-{kind}',name=name);diagram.append(model)
        single=E.Element('mxfile',host='app.diagrams.net',type='device');single.append(copy.deepcopy(diagram))
        (OUT/f'{item["code"]}_{kind}.drawio').write_bytes(E.tostring(single,encoding='UTF-8',xml_declaration=True))
        cells=model.xpath('.//mxCell');edges=[c for c in cells if c.get('edge')=='1']
        assert len(edges)>0 and model.xpath('.//mxCell[@vertex="1"]')
        assert not any('image=' in c.get('style','') for c in cells)
        assert len(model.xpath('.//mxCell[contains(@style,"text;")]'))==len(svg.xpath('.//s:text',namespaces=NS))
        stats.append(dict(page=name,nativeCells=len(cells),connectors=len(edges),connectedEdges=sum(bool(c.get('source')) and bool(c.get('target')) for c in edges)))
assert len(combined)==16
path=ROOT/args.combined_output;path.write_bytes(E.tostring(combined,encoding='UTF-8',xml_declaration=True))
(OUT/'CONVERSION_CHECKS.json').write_text(json.dumps(stats,indent=2),encoding='utf-8')
(OUT/'README.txt').write_text('16 UML diagrams converted to editable draw.io shapes.\nOpen UC_UML_Gomaa_8UC.drawio for all 16 pages, or open an individual file.\nClass boxes and participant/lifeline objects are grouped; double-click text to edit.\nUngroup (Ctrl+Shift+U) to edit individual compartment lines.\nConnectors are native mxGraph edges; no diagrams are embedded as images.\nContent and layout come from the approved PlantUML/SVG deliverables.\nSee output/UC_UML_Gomaa/README.md for source mapping and modeling assumptions.\n',encoding='utf-8')
archive=ROOT/args.zip_output
with ZipFile(archive,'w',ZIP_DEFLATED) as z:
    z.write(path,path.name)
    for f in OUT.iterdir():z.write(f,'individual/'+f.name)
print(path)
print(archive)
print('PASS: 16 pages, 16 standalone files; all text retained; native shapes and connectors; zero embedded diagram images.')
