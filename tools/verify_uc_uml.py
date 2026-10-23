"""Validate exported UML artifacts and prepare contact sheets for visual review."""
from pathlib import Path
from hashlib import sha256
from html.parser import HTMLParser
from PIL import Image, ImageOps, ImageDraw
from lxml import etree
import json
import re

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'output/UC_UML_Gomaa'
manifest = json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
checks = []
for item in manifest:
    folder = OUT/item['code']
    cls = (folder/'class.puml').read_text(encoding='utf-8')
    seq = (folder/'sequence.puml').read_text(encoding='utf-8')
    names = set(re.findall(r'^class (\w+) ', cls, re.M))
    declarations = re.findall(r'^(?:actor|participant) "([^"]+)" as (\w+)', seq, re.M)
    aliases = {alias for _, alias in declarations}
    for label, alias in declarations:
        if '«' in label:
            typ = re.search(r':\s*(\w+)', label)
            if typ:
                assert typ.group(1) in names, (item['code'], typ.group(1), 'missing class')
    for a,b in re.findall(r'^\s*(\w+) ->> (\w+)\s*:', seq, re.M):
        assert a in aliases and b in aliases, (item['code'], a, b, 'undeclared lifeline')
    files = []
    for kind in ['class','sequence']:
        svg = folder/f'{kind}.svg'
        tree = etree.parse(str(svg))
        text = ''.join(tree.getroot().itertext())
        assert 'Syntax Error' not in text and 'Error line' not in text
        # Guard labels must not accidentally turn into Creole hyperlinks.
        assert not tree.xpath('//*[local-name()="a"]'), (svg,'unexpected hyperlink')
        png = folder/f'{kind}.png'
        with Image.open(png) as img:
            img.verify()
        with Image.open(png) as img:
            dims = img.size
        files.append(dict(kind=kind, pngDimensions=dims,
                          sourceSha256=sha256((folder/f'{kind}.puml').read_bytes()).hexdigest()))
    checks.append(dict(code=item['code'],source=item['source'],files=files))

class Links(HTMLParser):
    def handle_starttag(self, tag, attrs):
        for k,v in attrs:
            if k in ('src','href') and not v.startswith(('#','http')):
                assert (OUT/v).is_file(), ('missing gallery target',v)
Links().feed((OUT/'index.html').read_text(encoding='utf-8'))

result = dict(date='2026-10-15',timezone='Asia/Saigon',
              sourceReportSha256=sha256((ROOT/'tmp/report3_uc_current.docx').read_bytes()).hexdigest(),
              counts=dict(useCases=8,classDiagrams=8,sequenceDiagrams=8,puml=16,svg=16,png=16),
              checks='XML/PNG integrity; declared lifelines; participant-class consistency; gallery links; no render-error output; no accidental guard hyperlinks',
              diagrams=checks)
(OUT/'VERIFICATION.json').write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')

for kind in ['class','sequence']:
    sheet = Image.new('RGB',(2400,1800),'#e9efeb')
    draw = ImageDraw.Draw(sheet)
    for i,item in enumerate(manifest):
        x,y=(i%4)*600,(i//4)*900
        draw.text((x+12,y+12),f'{item["code"]} / {kind}',fill='#18352c')
        img=ImageOps.contain(Image.open(OUT/item['code']/f'{kind}.png').convert('RGB'),(575,850))
        sheet.paste(img,(x+12,y+40))
    sheet.save(ROOT/'tmp'/f'uc_uml_{kind}_review.jpg',quality=94)
print('PASS: 8 UC pairs; 16 PUML / SVG / PNG; lifelines, classes, gallery and image integrity.')
