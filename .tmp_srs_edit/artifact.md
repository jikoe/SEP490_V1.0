# Template fidelity contract

- Reference: `C:\Users\huyng_jgxkbi0\Downloads\Report-3_Software Requirement Specification (2).docx`; SHA-256 `CEEBB2125E6085D783AB64700A60F5993CD643481EE1DBEF3F735ABBFA50555F`.
- Page system: A4 portrait, one section; margins left 1.00 in, right 0.98 in, top/bottom 1.00 in; different first-page header enabled. The bundled renderer could not run because `soffice.exe` is absent, so page count and image inspection are unresolved.
- Content flow: Use Case Specifications uses Heading 2 group sections, Heading 3 UC titles, and a 12-row, 4-column UC detail table. Functional Requirements uses Heading 3/4 screen headings, a short body description, and two-column field-description tables.
- Editable slots: added source-derived Heading 3 titles and cloned tables only. Existing headings, tables, page geometry, headers/footers, styles, numbering, images, and all non-targeted content are preserved.
- Components: UC detail table is cloned from UC-AUTH-01 (table 34). Forgot/reset field-description table is cloned from Change Password (table 46). Paragraphs are cloned from existing Heading 3, Heading 4, and body source elements to preserve formatting.
- Fidelity gate: original reference hash must remain unchanged; final DOCX must reopen using python-docx; added UCs must be present with 12-row UC tables and the forgot/reset field table must retain 13 rows.
