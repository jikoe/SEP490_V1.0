import sys
from docx import Document

sys.stdout.reconfigure(encoding="utf-8")

doc = Document("tmp_report3.docx")
terms = [
    "UC-ADM-03", "UC-ADM-05", "UC-CON-02", "UC-AUTH-07",
    "UC-ADM-06", "UC-ADM-07", "UC-ADM-01", "UC-ADM-09",
]
for table_index, table in enumerate(doc.tables):
    text = " | ".join(
        cell.text.replace("\n", " / ")
        for row in table.rows
        for cell in row.cells
    )
    hits = [term for term in terms if term in text]
    if hits:
        print(f"TABLE {table_index} HITS {hits}")
        print(text[:8000])
        print("---")
