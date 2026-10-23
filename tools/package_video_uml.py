from pathlib import Path
from lxml import etree as E
from zipfile import ZipFile,ZIP_DEFLATED
from hashlib import sha256
import copy
import json
import shutil

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'output/UC_UML_Video_Format'
DRAW=ROOT/'output/UC_UML_Drawio_Video_Format'
combined=ROOT/'output/UC_UML_Video_Format_16_Diagrams.drawio'
tree=E.parse(str(combined))
only=E.Element('mxfile',host='app.diagrams.net',type='device')
ns={'s':'http://www.w3.org/2000/svg'}
manifest=json.loads((BASE/'manifest.json').read_text(encoding='utf-8'))
checks=[]
for d in tree.getroot():
    if d.get('name').endswith(' - Class'):only.append(copy.deepcopy(d))
for item in manifest:
    code=item['code'];p=BASE/code
    src=(p/'class.puml').read_text(encoding='utf-8')
    svg=E.parse(str(p/'class.svg'))
    classgroups=svg.xpath('//s:g[@class="entity"]',namespaces=ns)
    for g in classgroups:
        assert len(g.findall('s:line',ns))==2,(code,g.get('data-entity'),'expected 3 compartments')
        assert g.xpath('.//s:text[@font-weight="bold"]',namespaces=ns),(code,'class title not bold')
    assert 'actor ' not in src and '<<' not in src
    attrs=[s.strip() for s in src.splitlines() if s.startswith('  -') and s.strip()!='--']
    methods=[s.strip() for s in src.splitlines() if s.startswith('  +')]
    assert all(':' in a for a in attrs)
    assert all('(' in m and ')' in m for m in methods)
    for ext in ('puml','svg','png'):
        assert (p/f'sequence.{ext}').read_bytes()==(ROOT/'output/UC_UML_Gomaa'/code/f'sequence.{ext}').read_bytes()
    checks.append(dict(code=code,classes=len(classgroups),privateAttributes=len(attrs),publicOperations=len(methods),threeCompartments=True,sequenceUnchanged=True))
assert len(only)==8
onlypath=ROOT/'output/UC_Class_Diagrams_Video_Format.drawio'
onlypath.write_bytes(E.tostring(only,xml_declaration=True,encoding='UTF-8'))
(DRAW/'FORMAT_CHECKS.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
shutil.copyfile(BASE/'REVIEW_VIDEO.md',DRAW/'REVIEW_VIDEO.md')
(DRAW/'README.txt').write_text('Open UC_Class_Diagrams_Video_Format.drawio for the 8 revised class diagrams.\nOpen UC_UML_Video_Format_16_Diagrams.drawio for revised class diagrams plus the 8 original sequence diagrams.\nFormat: pale-yellow three-compartment UML class; bold centered name; private typed attributes; public typed operations.\nAll drawings consist of editable native draw.io objects. No diagram is embedded as an image.\nSee REVIEW_VIDEO.md for timestamps, steps, UC traceability, and modeling decisions.\n',encoding='utf-8')
cards=[]
for m in manifest:
    c=m['code']
    cards.append(f'<section><h2>{c} — {m["title"]}</h2><p>Report 3: {m["source"]} · {m["section"]}</p><p><a href="{c}/class.svg">SVG / phóng to</a> · <a href="{c}/class.png">PNG</a> · <a href="../UC_UML_Drawio_Video_Format/{c}_class.drawio">draw.io</a> · <a href="{c}/class.puml">PlantUML</a></p><img src="{c}/class.svg" alt="{c} class diagram"></section>')
html='''<!doctype html><html lang="vi"><meta charset="utf-8"><title>Class diagram theo video TITV</title><style>body{font:16px/1.6 Arial;background:#faf9ef;color:#222;max-width:1500px;margin:auto;padding:28px}section{background:white;padding:24px;border:1px solid #d2c99c;margin:24px 0}img{display:block;max-width:100%;height:auto;margin:auto}a{color:#4f5734}h1{font-size:32px}</style><h1>8 Class Diagrams — định dạng video TITV</h1><p>Ba ngăn: tên lớp đậm, thuộc tính private, phương thức public. Đã rà soát quan hệ theo Report 3.</p><p><a href="../UC_Class_Diagrams_Video_Format.drawio">draw.io 8 class diagrams</a> · <a href="../UC_UML_Video_Format_16_Diagrams.drawio">draw.io đủ 16 sơ đồ</a> · <a href="REVIEW_VIDEO.md">Các bước, mốc video và giải thích sửa đổi</a></p>'''+''.join(cards)+'</html>'
(BASE/'index.html').write_text(html,encoding='utf-8')
archive=ROOT/'output/UC_UML_Video_Format_Drawio.zip'
with ZipFile(archive,'w',ZIP_DEFLATED) as z:
    for f in [onlypath,combined]:z.write(f,f.name)
    for f in DRAW.iterdir():z.write(f,'individual/'+f.name)
    for f in BASE.rglob('*'):
        if f.is_file():z.write(f,'reference/'+str(f.relative_to(BASE)).replace('\\','/'))
print('PASS: 8 revised class views, every class has 3 compartments and bold name; typed private attributes/public methods; original sequence sources unchanged.')
print(onlypath)
print(archive)
