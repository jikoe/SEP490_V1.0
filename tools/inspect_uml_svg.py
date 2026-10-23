from pathlib import Path
from lxml import etree
from collections import Counter
ns={'s':'http://www.w3.org/2000/svg'}
ps=list(Path('output/UC_UML_Gomaa').glob('*/*.svg'))
print('Path commands:',Counter(c for p in ps for v in etree.parse(str(p)).xpath('//@d') for c in v if c.isalpha()))
print('Transforms:',[(str(p),etree.parse(str(p)).xpath('//@transform')[:3]) for p in ps])
r=etree.parse('output/UC_UML_Gomaa/UC-ADM-03/sequence.svg')
for g in r.xpath('//s:g',namespaces=ns):
    print(dict(g.attrib),etree.tostring(g).decode()[:350])
