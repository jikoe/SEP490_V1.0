from pathlib import Path
from lxml import etree as E
from zipfile import ZipFile,ZIP_DEFLATED
from collections import Counter
import copy
import json
import shutil

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'output/UC_UML_Both_Videos'
DRAW=ROOT/'output/UC_UML_Drawio_Both_Videos'
full=ROOT/'output/UC_UML_Both_Videos_16_Diagrams.drawio'
tree=E.parse(str(full));ns={'s':'http://www.w3.org/2000/svg'}
only=E.Element('mxfile',host='app.diagrams.net',type='device')
old=E.parse(str(ROOT/'output/UC_UML_Video_Format_16_Diagrams.drawio'))
oldclasses={d.get('name'):E.tostring(d.find('mxGraphModel')) for d in old.getroot() if d.get('name').endswith(' - Class')}
design=json.loads((BASE/'SEQUENCE_DESIGN_CHECKS.json').read_text(encoding='utf-8'))
checks=[]
for d in tree.getroot():
    if d.get('name').endswith(' - Class'):
        assert E.tostring(d.find('mxGraphModel'))==oldclasses[d.get('name')],('class changed',d.get('name'))
        continue
    only.append(copy.deepcopy(d))
    code=d.get('name').split(' - ')[0]
    p=BASE/code
    svg=E.parse(str(p/'sequence.svg'))
    src=(p/'sequence.puml').read_text(encoding='utf-8')
    assert 'mainframe sd ' in src and 'activate ' in src and 'deactivate ' in src
    assert 'boundary ' in src and 'control ' in src and 'entity ' in src
    assert '-->>' in src
    source_messages=svg.xpath('//s:g[@class="message"]',namespaces=ns)
    count=Counter('call' if g.findall('s:polygon',ns) else 'reply' if any('stroke-dasharray' in e.get('style','') for e in g.findall('s:line',ns)) else 'async' for g in source_messages)
    edges=d.xpath('.//mxCell[@edge="1"][@source][@target]')
    assert len(edges)==len(source_messages),(code,len(edges),len(source_messages))
    filled=[e for e in edges if 'endArrow=block;' in e.get('style','')]
    replies=[e for e in edges if 'dashed=1;' in e.get('style','')]
    assert len(filled)==count['call'],(code,'filled arrow count')
    assert len(replies)==count['reply'],(code,'reply count')
    self_edges=[e for e in edges if e.get('source')==e.get('target')]
    assert all(len(e.findall('./mxGeometry/Array/mxPoint'))>=2 for e in self_edges),(code,'self hook lost')
    record=next(r for r in design if r['code']==code)
    numbers=[r['number'] for r in record['messages']]
    assert len(numbers)==len(set(numbers)),(code,'duplicate message number')
    assert not svg.xpath('//*[local-name()="a"]'),(code,'unintended guard link')
    checks.append(dict(code=code,nativeMessageEdges=len(edges),synchronousCalls=len(filled),dashedReplies=len(replies),selfMessages=len(self_edges),balancedActivations=record['balancedActivations'],classViewUnchanged=True))
assert len(only)==8
single=ROOT/'output/UC_Sequence_Diagrams_Video_Format.drawio'
single.write_bytes(E.tostring(only,encoding='UTF-8',xml_declaration=True))
(DRAW/'SEQUENCE_FORMAT_CHECKS.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
for name in ('REVIEW_SEQUENCE_VIDEO.md','REVIEW_VIDEO.md','SEQUENCE_DESIGN_CHECKS.json'):
    shutil.copyfile(BASE/name,DRAW/name)
(DRAW/'README.txt').write_text('Open UC_Sequence_Diagrams_Video_Format.drawio for the 8 revised sequence diagrams.\nOpen UC_UML_Both_Videos_16_Diagrams.drawio for the complete 16-page set.\nThe 8 class diagrams are unchanged from the previous TITV-video revision.\nSequence format: sd frame, Actor/Boundary/Control/Entity icons, blue activations, numbered messages, solid filled-head calls, dashed open-head replies, alt/opt/loop.\nAll shapes, labels and connectors are native editable draw.io objects.\nSee REVIEW_SEQUENCE_VIDEO.md for timestamps, modeling decisions and limitations.\n',encoding='utf-8')
cards=[]
for m in json.loads((BASE/'manifest.json').read_text(encoding='utf-8')):
    code=m['code']
    cards.append(f'<section><h2>{code} — {m["title"]}</h2><p>Report 3: {m["source"]} · {m["section"]}</p><p><a href="{code}/sequence.svg">SVG / phóng to</a> · <a href="{code}/sequence.png">PNG</a> · <a href="../UC_UML_Drawio_Both_Videos/{code}_sequence.drawio">draw.io</a> · <a href="{code}/sequence.puml">PlantUML</a></p><img src="{code}/sequence.svg" alt="{code} sequence diagram"></section>')
html='''<!doctype html><html lang="vi"><meta charset="utf-8"><title>Sequence diagrams theo video</title><style>body{font:16px/1.6 Arial;background:#f2f7fa;color:#222;max-width:1600px;margin:auto;padding:28px}section{background:white;padding:24px;border:1px solid #b9d6e5;margin:24px 0}img{display:block;max-width:100%;height:auto;margin:auto}a{color:#245977}h1{font-size:32px}</style><h1>8 Sequence Diagrams — định dạng video Hoàng Trang Channel</h1><p>Actor → Boundary → Control → Entity · Activation màu xanh · Đồng bộ / reply · Số thứ tự · alt / opt / loop</p><p><a href="../UC_Sequence_Diagrams_Video_Format.drawio">draw.io 8 sequence diagrams</a> · <a href="../UC_UML_Both_Videos_16_Diagrams.drawio">draw.io đủ 16 sơ đồ</a> · <a href="REVIEW_SEQUENCE_VIDEO.md">Từng bước và mốc video</a></p>'''+''.join(cards)+'</html>'
(BASE/'index.html').write_text(html,encoding='utf-8')
archive=ROOT/'output/UC_UML_Both_Videos_Drawio.zip'
with ZipFile(archive,'w',ZIP_DEFLATED) as z:
    for f in (single,full):z.write(f,f.name)
    for f in DRAW.iterdir():z.write(f,'individual/'+f.name)
    for f in BASE.rglob('*'):
        if f.is_file():z.write(f,'reference/'+str(f.relative_to(BASE)).replace('\\','/'))
print('PASS: eight revised sequence diagrams; native filled calls, dashed replies, self-message hooks; all previous class pages byte-identical.')
print(single)
print(archive)
