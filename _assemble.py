# -*- coding: utf-8 -*-
import io, re

src = io.open("SalesCom_LLD.md", encoding="utf-8").read()

# --- body from "# 1 Introduction" to end ---
i = src.find("# 1 Introduction")
assert i != -1
body = src[i:]

# strip HTML comments
body = re.sub(r"<!--.*?-->", "", body, flags=re.S)

# replace architecture ASCII fenced block with the image
m = re.search(r"```\n(┌.*?PRESENTATION.*?)\n```", body, flags=re.S)
assert m, "architecture block not found"
body = body[:m.start()] + "![System architecture](architecture.png){width=6.4in}\n" + body[m.end():]

# width on ER image
body = body.replace(
    "![SalesCom Entity Relationship Diagram](ER_Diagram.PNG)",
    "![SalesCom Entity Relationship Diagram](ER_Diagram.PNG){width=6.4in}")

body = re.sub(r"\n{3,}", "\n\n", body).strip() + "\n"

PB = '```{=openxml}\n<w:p><w:r><w:br w:type="page"/></w:r></w:p>\n```\n'

def raw_title(t):
    return ('```{=openxml}\n'
            '<w:p><w:pPr><w:spacing w:before="120" w:after="160"/></w:pPr>'
            '<w:r><w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:b/>'
            '<w:color w:val="1F4E79"/><w:sz w:val="34"/></w:rPr>'
            '<w:t xml:space="preserve">' + t + '</w:t></w:r></w:p>\n```\n')

TOC = ('```{=openxml}\n'
       '<w:p><w:pPr><w:spacing w:before="40" w:after="40"/></w:pPr>'
       '<w:r><w:fldChar w:fldCharType="begin" w:dirty="true"/></w:r>'
       r'<w:r><w:instrText xml:space="preserve"> TOC \o &quot;1-3&quot; \h \z \u </w:instrText></w:r>'
       '<w:r><w:fldChar w:fldCharType="separate"/></w:r>'
       '<w:r><w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial"/><w:color w:val="808080"/></w:rPr>'
       '<w:t xml:space="preserve">Right-click here and choose &quot;Update Field&quot; to build the table of contents.</w:t></w:r>'
       '<w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>\n```\n')

cover = (
    '::: {custom-style="CenterImg"}\n'
    '![banglalink](BL_logo.png){width=2.7in}\n'
    ':::\n\n'
    '```{=openxml}\n<w:p/><w:p/><w:p/>\n```\n\n'
    '::: {custom-style="CoverTitle"}\nLow-Level Design (LLD)\n:::\n\n'
    '::: {custom-style="CoverFor"}\nFor\n:::\n\n'
    '::: {custom-style="CoverSub"}\nSales Commission Automation 2026\n:::\n\n'
    '```{=openxml}\n<w:p/><w:p/>\n```\n\n'
    '::: {custom-style="CoverNote"}\nBanglalink — Sales Commission Automation Platform\n:::\n\n'
)

history = (
    "| SL | Date | Version | Description | Created / Modified by | Reviewed by |\n"
    "|---|----------|-------|------------------|-------------|-----------|\n"
    "| 01 | 17-Jun-2026 | 1.0 | Initial draft | | |\n"
    "| 02 | 22-Jun-2026 | 2.0 | Final — grounded in the real `salescomdbtst` schema; consistency pass folded in | | |\n\n"
)

doc = (cover + PB
       + raw_title("Document History") + history + PB
       + raw_title("Table of Contents") + TOC + PB
       + body)

io.open("_lld_final.md", "w", encoding="utf-8").write(doc)
print("wrote _lld_final.md  chars:", len(doc))
