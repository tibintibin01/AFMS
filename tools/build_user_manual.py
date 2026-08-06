from __future__ import annotations

from datetime import date
from pathlib import Path
from typing import Iterable, Sequence

from PIL import Image, ImageDraw, ImageFont
from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.table import WD_ALIGN_VERTICAL, WD_CELL_VERTICAL_ALIGNMENT, WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK, WD_LINE_SPACING
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt, RGBColor


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "output" / "manual"
ASSETS = OUT / "assets"
OUT.mkdir(parents=True, exist_ok=True)
ASSETS.mkdir(parents=True, exist_ok=True)
MANUAL_VERSION = "1.2"
ISSUE_DATE = "20 July 2026"
DOCX_PATH = OUT / f"AFMS_User_Manual_v{MANUAL_VERSION}.docx"

NAVY = "173B5E"
NAVY_DARK = "102D49"
BLUE = "2E74B5"
BLUE_DARK = "1F4D78"
GOLD = "D9A83E"
INK = "17212B"
MUTED = "66778A"
BORDER = "DDE4EC"
PALE_BLUE = "E8EEF5"
PALE_GOLD = "FFF6DF"
PALE_GREEN = "EAF6F1"
PALE_RED = "FFF0EF"
GREEN = "16805C"
RED = "C2413A"


def rgb(hex_value: str) -> RGBColor:
    return RGBColor.from_string(hex_value)


def set_font(run, name="Calibri", size=None, color=INK, bold=None, italic=None):
    run.font.name = name
    run._element.get_or_add_rPr().rFonts.set(qn("w:ascii"), name)
    run._element.get_or_add_rPr().rFonts.set(qn("w:hAnsi"), name)
    if size is not None:
        run.font.size = Pt(size)
    if color:
        run.font.color.rgb = rgb(color)
    if bold is not None:
        run.bold = bold
    if italic is not None:
        run.italic = italic


def set_cell_shading(cell, fill: str):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = tc_pr.find(qn("w:shd"))
    if shd is None:
        shd = OxmlElement("w:shd")
        tc_pr.append(shd)
    shd.set(qn("w:fill"), fill)


def set_cell_margins(cell, top=80, start=120, bottom=80, end=120):
    tc_pr = cell._tc.get_or_add_tcPr()
    tc_mar = tc_pr.first_child_found_in("w:tcMar")
    if tc_mar is None:
        tc_mar = OxmlElement("w:tcMar")
        tc_pr.append(tc_mar)
    for margin, value in (("top", top), ("start", start), ("bottom", bottom), ("end", end)):
        node = tc_mar.find(qn(f"w:{margin}"))
        if node is None:
            node = OxmlElement(f"w:{margin}")
            tc_mar.append(node)
        node.set(qn("w:w"), str(value))
        node.set(qn("w:type"), "dxa")


def set_repeat_table_header(row):
    tr_pr = row._tr.get_or_add_trPr()
    tbl_header = OxmlElement("w:tblHeader")
    tbl_header.set(qn("w:val"), "true")
    tr_pr.append(tbl_header)


def set_table_geometry(table, widths: Sequence[int], indent=120):
    assert sum(widths) == 9360, widths
    table.autofit = False
    table.alignment = WD_TABLE_ALIGNMENT.LEFT
    tbl_pr = table._tbl.tblPr
    tbl_w = tbl_pr.find(qn("w:tblW"))
    if tbl_w is None:
        tbl_w = OxmlElement("w:tblW")
        tbl_pr.append(tbl_w)
    tbl_w.set(qn("w:w"), "9360")
    tbl_w.set(qn("w:type"), "dxa")
    tbl_ind = tbl_pr.find(qn("w:tblInd"))
    if tbl_ind is None:
        tbl_ind = OxmlElement("w:tblInd")
        tbl_pr.append(tbl_ind)
    tbl_ind.set(qn("w:w"), str(indent))
    tbl_ind.set(qn("w:type"), "dxa")
    layout = tbl_pr.find(qn("w:tblLayout"))
    if layout is None:
        layout = OxmlElement("w:tblLayout")
        tbl_pr.append(layout)
    layout.set(qn("w:type"), "fixed")
    grid = table._tbl.tblGrid
    for child in list(grid):
        grid.remove(child)
    for width in widths:
        col = OxmlElement("w:gridCol")
        col.set(qn("w:w"), str(width))
        grid.append(col)
    for row in table.rows:
        for idx, cell in enumerate(row.cells):
            tc_pr = cell._tc.get_or_add_tcPr()
            tc_w = tc_pr.find(qn("w:tcW"))
            if tc_w is None:
                tc_w = OxmlElement("w:tcW")
                tc_pr.append(tc_w)
            tc_w.set(qn("w:w"), str(widths[idx]))
            tc_w.set(qn("w:type"), "dxa")
            set_cell_margins(cell)


def add_custom_numbering(doc: Document, kind: str) -> tuple[int, int]:
    numbering = doc.part.numbering_part.element
    existing_abs = [int(x.get(qn("w:abstractNumId"))) for x in numbering.findall(qn("w:abstractNum"))]
    existing_num = [int(x.get(qn("w:numId"))) for x in numbering.findall(qn("w:num"))]
    abstract_id = max(existing_abs or [0]) + 1
    num_id = max(existing_num or [0]) + 1
    abstract = OxmlElement("w:abstractNum")
    abstract.set(qn("w:abstractNumId"), str(abstract_id))
    multi = OxmlElement("w:multiLevelType")
    multi.set(qn("w:val"), "singleLevel")
    abstract.append(multi)
    lvl = OxmlElement("w:lvl")
    lvl.set(qn("w:ilvl"), "0")
    start = OxmlElement("w:start"); start.set(qn("w:val"), "1"); lvl.append(start)
    num_fmt = OxmlElement("w:numFmt"); num_fmt.set(qn("w:val"), "bullet" if kind == "bullet" else "decimal"); lvl.append(num_fmt)
    lvl_text = OxmlElement("w:lvlText"); lvl_text.set(qn("w:val"), "•" if kind == "bullet" else "%1."); lvl.append(lvl_text)
    lvl_jc = OxmlElement("w:lvlJc"); lvl_jc.set(qn("w:val"), "left"); lvl.append(lvl_jc)
    p_pr = OxmlElement("w:pPr")
    tabs = OxmlElement("w:tabs"); tab = OxmlElement("w:tab"); tab.set(qn("w:val"), "num"); tab.set(qn("w:pos"), "540"); tabs.append(tab); p_pr.append(tabs)
    ind = OxmlElement("w:ind"); ind.set(qn("w:left"), "540"); ind.set(qn("w:hanging"), "270"); p_pr.append(ind)
    lvl.append(p_pr)
    if kind == "bullet":
        r_pr = OxmlElement("w:rPr"); fonts = OxmlElement("w:rFonts"); fonts.set(qn("w:ascii"), "Segoe UI Symbol"); fonts.set(qn("w:hAnsi"), "Segoe UI Symbol"); r_pr.append(fonts); lvl.append(r_pr)
    abstract.append(lvl)
    first_num = numbering.find(qn("w:num"))
    if first_num is None:
        numbering.append(abstract)
    else:
        numbering.insert(numbering.index(first_num), abstract)
    num = OxmlElement("w:num"); num.set(qn("w:numId"), str(num_id)); abs_ref = OxmlElement("w:abstractNumId"); abs_ref.set(qn("w:val"), str(abstract_id)); num.append(abs_ref); numbering.append(num)
    return abstract_id, num_id


def add_numbering_instance(doc: Document, abstract_id: int) -> int:
    numbering = doc.part.numbering_part.element
    existing_num = [int(x.get(qn("w:numId"))) for x in numbering.findall(qn("w:num"))]
    num_id = max(existing_num or [0]) + 1
    num = OxmlElement("w:num"); num.set(qn("w:numId"), str(num_id))
    abs_ref = OxmlElement("w:abstractNumId"); abs_ref.set(qn("w:val"), str(abstract_id)); num.append(abs_ref)
    lvl_override = OxmlElement("w:lvlOverride"); lvl_override.set(qn("w:ilvl"), "0")
    start_override = OxmlElement("w:startOverride"); start_override.set(qn("w:val"), "1")
    lvl_override.append(start_override); num.append(lvl_override)
    numbering.append(num)
    return num_id


def apply_num(paragraph, num_id):
    p_pr = paragraph._p.get_or_add_pPr()
    num_pr = p_pr.find(qn("w:numPr"))
    if num_pr is None:
        num_pr = OxmlElement("w:numPr"); p_pr.append(num_pr)
    ilvl = OxmlElement("w:ilvl"); ilvl.set(qn("w:val"), "0")
    numid = OxmlElement("w:numId"); numid.set(qn("w:val"), str(num_id))
    num_pr.append(ilvl); num_pr.append(numid)


def draw_assets():
    regular = "C:/Windows/Fonts/segoeui.ttf"
    semibold = "C:/Windows/Fonts/seguisb.ttf"
    bold = "C:/Windows/Fonts/segoeuib.ttf"
    font = lambda path, size: ImageFont.truetype(path, size)

    cover = Image.new("RGB", (1600, 420), "white")
    d = ImageDraw.Draw(cover)
    d.rounded_rectangle((30, 30, 1570, 390), radius=34, fill="#173B5E")
    d.ellipse((95, 103, 305, 313), fill="#D9A83E")
    d.text((200, 208), "AF", font=font(bold, 74), fill="#102D49", anchor="mm")
    d.text((360, 118), "AFMS", font=font(bold, 80), fill="white")
    d.text((364, 220), "ACCOUNTABLE FORMS MANAGEMENT SYSTEM", font=font(semibold, 34), fill="#D8E5F0")
    d.text((365, 287), "Municipal Treasury Office", font=font(regular, 29), fill="#D9A83E")
    cover.save(ASSETS / "cover_banner.png")

    login = Image.new("RGB", (1600, 900), "#F7F9FB")
    d = ImageDraw.Draw(login)
    d.rounded_rectangle((18, 18, 1582, 882), radius=24, fill="white", outline="#DDE4EC", width=3)
    d.rectangle((18, 18, 730, 882), fill="#0B315A")

    # Scalable document-and-shield identity mark used by the current login screen.
    d.ellipse((150, 70, 600, 450), outline="#17669F", width=4)
    d.ellipse((548, 115, 568, 135), fill="#20BEC4")
    d.rounded_rectangle((240, 105, 485, 395), radius=30, fill="white", outline="#DCEAF4", width=3)
    d.polygon([(400, 105), (485, 190), (425, 190), (400, 165)], fill="#20BEC4")
    for y in (190, 255, 320):
        d.rounded_rectangle((278, y, 325, y + 42), radius=9, fill="#20BEC4")
        d.line((288, y + 20, 300, y + 31, 316, y + 12), fill="white", width=7, joint="curve")
        d.rounded_rectangle((348, y + 6, 438, y + 18), radius=6, fill="#10568B")
        d.rounded_rectangle((348, y + 27, 414, y + 37), radius=5, fill="#10568B")
    shield = [(485, 300), (545, 330), (605, 300), (605, 390), (590, 438), (545, 475), (500, 438), (485, 390)]
    d.polygon(shield, fill="white")
    inner = [(500, 320), (545, 342), (590, 320), (590, 386), (579, 421), (545, 452), (511, 421), (500, 386)]
    d.polygon(inner, fill="#0B497E")
    d.polygon([(545, 342), (590, 320), (590, 386), (579, 421), (545, 452)], fill="#168F9C")
    d.line((520, 385, 540, 405, 574, 366), fill="white", width=11, joint="curve")

    d.text((365, 500), "AFMS", font=font(bold, 64), fill="white", anchor="mm")
    d.line((240, 545, 490, 545), fill="#20BEC4", width=3)
    d.text((365, 582), "Accountable Forms", font=font(regular, 29), fill="#E8F2FA", anchor="mm")
    d.text((365, 620), "Management System", font=font(regular, 29), fill="#E8F2FA", anchor="mm")

    # Draw decorative waves first so the feature row remains fully readable.
    d.pieslice((-250, 735, 650, 1120), 180, 360, fill="#0D568F")
    d.pieslice((180, 780, 1050, 1130), 180, 360, fill="#1476B5")

    features = [(190, "Organize"), (365, "Accountable"), (540, "Efficient")]
    for x, label in features:
        d.ellipse((x - 25, 685, x + 25, 735), outline="#20BEC4", width=3)
        d.text((x, 710), "✓", font=font(bold, 23), fill="white", anchor="mm")
        d.text((x, 762), label, font=font(regular, 20), fill="#EAF5FC", anchor="mm")
    d.text((365, 840), "Municipal Treasury Office • Offline", font=font(regular, 17), fill="#B8D2E8", anchor="mm")

    d.text((955, 235), "Welcome back", font=font(bold, 47), fill="#17212B")
    d.text((958, 305), "Sign in with your authorized AFMS account.", font=font(regular, 22), fill="#66778A")
    d.text((958, 385), "Username", font=font(semibold, 20), fill="#17212B")
    d.rounded_rectangle((955, 425, 1425, 500), radius=10, fill="white", outline="#C9D4E0", width=3)
    d.text((958, 555), "Password", font=font(semibold, 20), fill="#17212B")
    d.rounded_rectangle((955, 595, 1425, 670), radius=10, fill="white", outline="#C9D4E0", width=3)
    d.rounded_rectangle((955, 735, 1425, 808), radius=10, fill="#173B5E")
    d.text((1190, 771), "Sign in", font=font(semibold, 23), fill="white", anchor="mm")
    login.save(ASSETS / "login_screen.png")

    shell = Image.new("RGB", (1600, 780), "#F5F7FA")
    d = ImageDraw.Draw(shell)
    d.rounded_rectangle((30, 30, 1570, 750), radius=28, fill="white", outline="#DDE4EC", width=3)
    d.rounded_rectangle((30, 30, 385, 750), radius=28, fill="#173B5E")
    d.rectangle((357, 30, 385, 750), fill="#173B5E")
    d.ellipse((72, 65, 142, 135), fill="#D9A83E")
    d.text((107, 100), "AF", font=font(bold, 25), fill="#102D49", anchor="mm")
    d.text((165, 77), "AFMS", font=font(bold, 30), fill="white")
    sections = [("OVERVIEW", 175), ("MASTER DATA", 275), ("STOCK & ACCOUNTABILITY", 420), ("ADMINISTRATION", 605)]
    for label, y in sections:
        d.text((70, y), label, font=font(semibold, 15), fill="#8FA8BE")
    for y, label in [(210, "Dashboard"), (310, "Form Types / Barangays / Collectors"), (455, "Receiving / Inventory / Issuance"), (505, "Usage / Transfers / Returns"), (555, "Reports"), (640, "Users / Settings / Audit / Backup")]:
        d.rounded_rectangle((55, y-8, 355, y+38), radius=9, fill="#2A5377" if y == 210 else "#173B5E")
        d.text((82, y+5), label, font=font(regular, 18), fill="white")
    d.rectangle((385, 30, 1570, 115), fill="white")
    d.text((435, 70), "Municipal Treasury Office  /  Current module", font=font(semibold, 21), fill="#173B5E")
    d.rounded_rectangle((1320, 55, 1515, 93), radius=19, fill="#EAF6F1")
    d.text((1417, 74), "OFFLINE READY", font=font(bold, 15), fill="#16805C", anchor="mm")
    d.text((450, 170), "Page heading", font=font(bold, 38), fill="#17212B")
    d.text((450, 222), "Purpose and instructions for the current task", font=font(regular, 21), fill="#66778A")
    d.rounded_rectangle((450, 280, 1505, 405), radius=18, fill="#F7F9FB", outline="#DDE4EC", width=2)
    d.text((485, 315), "Filters and transaction context", font=font(semibold, 24), fill="#173B5E")
    for x in (485, 735, 985, 1235):
        d.rounded_rectangle((x, 352, x+205, 390), radius=7, fill="white", outline="#C9D4E0", width=2)
    d.rounded_rectangle((450, 440, 1505, 700), radius=18, fill="white", outline="#DDE4EC", width=2)
    d.text((485, 475), "Records, details, and contextual actions", font=font(semibold, 24), fill="#173B5E")
    for y in (530, 580, 630):
        d.line((485, y, 1470, y), fill="#DDE4EC", width=2)
    shell.save(ASSETS / "interface_map.png")

    lifecycle = Image.new("RGB", (1800, 520), "white")
    d = ImageDraw.Draw(lifecycle)
    labels = [("1", "RECEIVE", "Create verified stock"), ("2", "INVENTORY", "Generated booklets"), ("3", "ISSUE", "Assign to collector"), ("4", "ACCOUNT", "Use / transfer / return"), ("5", "RESOLVE", "Exceptions / reversal"), ("6", "REPORT", "Reconcile and close")]
    colors = ["#EAF1F7", "#EAF6F1", "#FFF6DF", "#EAF1F7", "#FFF0EF", "#EAF6F1"]
    x_positions = [45, 330, 615, 900, 1185, 1470]
    for idx, ((num, title, sub), x, fill) in enumerate(zip(labels, x_positions, colors)):
        d.rounded_rectangle((x, 135, x+240, 375), radius=24, fill=fill, outline="#DDE4EC", width=3)
        d.ellipse((x+82, 70, x+158, 146), fill="#173B5E")
        d.text((x+120, 108), num, font=font(bold, 30), fill="white", anchor="mm")
        d.text((x+120, 205), title, font=font(bold, 26), fill="#173B5E", anchor="mm")
        lines = sub.split(" / ")
        for j, line in enumerate(lines):
            d.text((x+120, 270+j*34), line, font=font(regular, 19), fill="#4E6175", anchor="mm")
        if idx < len(labels)-1:
            d.line((x+242, 255, x+278, 255), fill="#D9A83E", width=8)
            d.polygon([(x+278, 242), (x+300, 255), (x+278, 268)], fill="#D9A83E")
    lifecycle.save(ASSETS / "lifecycle.png")

    workflow = Image.new("RGB", (1700, 500), "white")
    d = ImageDraw.Draw(workflow)
    boxes = [(70, "DRAFT", "Editable"), (390, "SUBMITTED", "Ready for review"), (710, "APPROVED", "Operator confirmed"), (1030, "FINALIZED", "Stock updated")]
    for x, title, sub in boxes:
        d.rounded_rectangle((x, 95, x+255, 275), radius=20, fill="#EAF1F7", outline="#173B5E", width=3)
        d.text((x+127, 155), title, font=font(bold, 25), fill="#173B5E", anchor="mm")
        d.text((x+127, 215), sub, font=font(regular, 18), fill="#66778A", anchor="mm")
    for x in (325, 645, 965):
        d.line((x, 185, x+55, 185), fill="#D9A83E", width=8); d.polygon([(x+55,172),(x+78,185),(x+55,198)], fill="#D9A83E")
    d.rounded_rectangle((710, 345, 965, 455), radius=18, fill="#FFF0EF", outline="#C2413A", width=3)
    d.text((837, 380), "REJECTED", font=font(bold, 23), fill="#C2413A", anchor="mm")
    d.text((837, 422), "Reason required", font=font(regular, 18), fill="#66778A", anchor="mm")
    d.line((837, 275, 837, 340), fill="#C2413A", width=5)
    d.rounded_rectangle((1350, 95, 1620, 275), radius=20, fill="#FFF6DF", outline="#D9A83E", width=3)
    d.text((1485, 155), "REVERSED", font=font(bold, 25), fill="#8A650E", anchor="mm")
    d.text((1485, 215), "Original preserved", font=font(regular, 18), fill="#66778A", anchor="mm")
    d.line((1285, 185, 1335, 185), fill="#D9A83E", width=8); d.polygon([(1335,172),(1358,185),(1335,198)], fill="#D9A83E")
    workflow.save(ASSETS / "approval_workflow.png")

    backup = Image.new("RGB", (1600, 470), "white")
    d = ImageDraw.Draw(backup)
    items = [(60, "LIVE DATABASE", "Writes paused safely"), (415, "VALIDATE", "SQLite check + hash"), (770, "BACKUP HISTORY", "Retained copy"), (1125, "RESTORE", "Safety backup + restart")]
    for x, title, sub in items:
        d.rounded_rectangle((x, 125, x+300, 340), radius=24, fill="#EAF1F7", outline="#173B5E", width=3)
        d.text((x+150, 195), title, font=font(bold, 24), fill="#173B5E", anchor="mm")
        d.text((x+150, 265), sub, font=font(regular, 18), fill="#66778A", anchor="mm")
        if x < 1125:
            d.line((x+302, 232, x+338, 232), fill="#D9A83E", width=8); d.polygon([(x+338,219),(x+360,232),(x+338,245)], fill="#D9A83E")
    backup.save(ASSETS / "backup_flow.png")


def configure_document(doc: Document):
    section = doc.sections[0]
    section.page_width = Inches(8.5); section.page_height = Inches(11)
    section.top_margin = Inches(1); section.bottom_margin = Inches(1); section.left_margin = Inches(1); section.right_margin = Inches(1)
    section.header_distance = Inches(0.492); section.footer_distance = Inches(0.492)
    styles = doc.styles
    normal = styles["Normal"]
    normal.font.name = "Calibri"; normal._element.rPr.rFonts.set(qn("w:ascii"), "Calibri"); normal._element.rPr.rFonts.set(qn("w:hAnsi"), "Calibri"); normal.font.size = Pt(11); normal.font.color.rgb = rgb(INK)
    normal.paragraph_format.space_before = Pt(0); normal.paragraph_format.space_after = Pt(6); normal.paragraph_format.line_spacing = 1.25
    for style_name, size, color, before, after in (("Heading 1",16,BLUE,18,10),("Heading 2",13,BLUE,14,7),("Heading 3",12,BLUE_DARK,10,5)):
        style = styles[style_name]; style.font.name="Calibri"; style._element.rPr.rFonts.set(qn("w:ascii"),"Calibri"); style._element.rPr.rFonts.set(qn("w:hAnsi"),"Calibri"); style.font.size=Pt(size); style.font.bold=True; style.font.color.rgb=rgb(color); style.paragraph_format.space_before=Pt(before); style.paragraph_format.space_after=Pt(after); style.paragraph_format.keep_with_next=True
    header = section.header
    table = header.add_table(rows=1, cols=2, width=Inches(6.5)); set_table_geometry(table, [4680,4680], indent=0)
    table.rows[0].cells[0].paragraphs[0].add_run("AFMS USER MANUAL"); table.rows[0].cells[1].paragraphs[0].add_run("MUNICIPAL TREASURY OFFICE")
    table.rows[0].cells[1].paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.RIGHT
    for cell in table.rows[0].cells:
        cell._tc.get_or_add_tcPr().append(OxmlElement("w:tcBorders"))
        for run in cell.paragraphs[0].runs: set_font(run, size=8.5, color=MUTED, bold=True)
    footer = section.footer
    p = footer.paragraphs[0]; p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = p.add_run(f"AFMS v{MANUAL_VERSION}  |  Dipaculao, Aurora  |  Page "); set_font(r, size=8.5, color=MUTED)
    fld = OxmlElement("w:fldSimple"); fld.set(qn("w:instr"), "PAGE"); p._p.append(fld)
    doc.core_properties.title = "AFMS User Manual"
    doc.core_properties.subject = "Accountable Forms Management System operating guide"
    doc.core_properties.author = "Municipal Treasury Office - Dipaculao"
    doc.core_properties.keywords = "AFMS, accountable forms, municipal treasury, user manual"


class Manual:
    def __init__(self, doc: Document):
        self.doc = doc
        self.bullet_abstract_id, self.bullet_id = add_custom_numbering(doc, "bullet")
        self.number_abstract_id, _ = add_custom_numbering(doc, "decimal")

    def body(self, text: str, bold_lead: str | None = None, italic=False):
        p = self.doc.add_paragraph()
        if bold_lead and text.startswith(bold_lead):
            r = p.add_run(bold_lead); set_font(r, bold=True)
            r = p.add_run(text[len(bold_lead):]); set_font(r, italic=italic)
        else:
            r = p.add_run(text); set_font(r, italic=italic)
        return p

    def bullets(self, items: Iterable[str], compact: bool = False):
        for item in items:
            p = self.doc.add_paragraph(); apply_num(p, self.bullet_id)
            p.paragraph_format.space_after = Pt(1 if compact else 4)
            p.paragraph_format.line_spacing = 1.05 if compact else 1.25
            set_font(p.add_run(item))

    def steps(self, items: Iterable[str], compact: bool = False):
        number_id = add_numbering_instance(self.doc, self.number_abstract_id)
        for item in items:
            p = self.doc.add_paragraph(); apply_num(p, number_id)
            p.paragraph_format.space_after = Pt(2 if compact else 5)
            p.paragraph_format.line_spacing = 1.1 if compact else 1.25
            set_font(p.add_run(item))

    def heading(self, text: str, level=1):
        return self.doc.add_heading(text, level=level)

    def chapter(self, number: str, title: str, subtitle: str | None = None, page_break: bool = True):
        if page_break:
            self.doc.add_page_break()
        p = self.doc.add_paragraph(); p.paragraph_format.space_after=Pt(3)
        set_font(p.add_run(f"CHAPTER {number}"), size=10, color=GOLD, bold=True)
        h = self.doc.add_heading(title, level=1); h.paragraph_format.space_before=Pt(0); h.paragraph_format.space_after=Pt(6)
        if subtitle:
            p = self.doc.add_paragraph(); p.paragraph_format.space_after=Pt(14)
            set_font(p.add_run(subtitle), size=11.5, color=MUTED, italic=True)

    def note(self, title: str, text: str, kind="info"):
        fills = {"info": PALE_BLUE, "warning": PALE_GOLD, "danger": PALE_RED, "success": PALE_GREEN}
        colors = {"info": NAVY, "warning": "8A650E", "danger": RED, "success": GREEN}
        table = self.doc.add_table(rows=1, cols=1); set_table_geometry(table, [9360], indent=120)
        cell = table.cell(0,0); set_cell_shading(cell, fills[kind]); cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
        p = cell.paragraphs[0]; p.paragraph_format.space_after=Pt(2)
        set_font(p.add_run(title.upper()), size=9.5, color=colors[kind], bold=True)
        p = cell.add_paragraph(); p.paragraph_format.space_after=Pt(0); set_font(p.add_run(text), size=10.5)
        self.doc.add_paragraph().paragraph_format.space_after = Pt(1)

    def table(self, headers: Sequence[str], rows: Sequence[Sequence[str]], widths: Sequence[int], font_size=9.5):
        table = self.doc.add_table(rows=1, cols=len(headers)); table.style = "Table Grid"
        for idx, header in enumerate(headers):
            cell = table.rows[0].cells[idx]; set_cell_shading(cell, PALE_BLUE); cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            p = cell.paragraphs[0]; p.paragraph_format.space_after=Pt(0); set_font(p.add_run(header), size=font_size, color=NAVY, bold=True)
        set_repeat_table_header(table.rows[0])
        for row in rows:
            cells = table.add_row().cells
            for idx, value in enumerate(row):
                cells[idx].vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
                p = cells[idx].paragraphs[0]; p.paragraph_format.space_after=Pt(0); p.paragraph_format.line_spacing=1.1; set_font(p.add_run(str(value)), size=font_size)
        set_table_geometry(table, widths)
        self.doc.add_paragraph().paragraph_format.space_after = Pt(1)
        return table

    def picture(self, filename: str, width=6.45, caption: str | None = None):
        p = self.doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.CENTER; p.paragraph_format.keep_with_next = bool(caption)
        shape = p.add_run().add_picture(str(ASSETS / filename), width=Inches(width))
        shape._inline.docPr.set("descr", caption or Path(filename).stem.replace("_", " ").title())
        if caption:
            cp = self.doc.add_paragraph(); cp.alignment = WD_ALIGN_PARAGRAPH.CENTER; cp.paragraph_format.space_after=Pt(8)
            set_font(cp.add_run(caption), size=9, color=MUTED, italic=True)


def build_manual():
    draw_assets()
    doc = Document()
    configure_document(doc)
    m = Manual(doc)

    # Cover
    doc.add_paragraph().paragraph_format.space_after = Pt(8)
    m.picture("cover_banner.png", width=6.5)
    p = doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.CENTER; p.paragraph_format.space_before=Pt(18); p.paragraph_format.space_after=Pt(4)
    set_font(p.add_run("COMPREHENSIVE USER MANUAL"), size=25, color=NAVY, bold=True)
    p = doc.add_paragraph(); p.alignment=WD_ALIGN_PARAGRAPH.CENTER; p.paragraph_format.space_after=Pt(24)
    set_font(p.add_run("Receipt accountability from office receipt through final archival"), size=13.5, color=MUTED, italic=True)
    p = doc.add_paragraph(); p.alignment=WD_ALIGN_PARAGRAPH.CENTER; p.paragraph_format.space_after=Pt(4); set_font(p.add_run("Municipality of Dipaculao"), size=14, color=NAVY, bold=True)
    p = doc.add_paragraph(); p.alignment=WD_ALIGN_PARAGRAPH.CENTER; p.paragraph_format.space_after=Pt(2); set_font(p.add_run("Province of Aurora"), size=12, color=MUTED)
    p = doc.add_paragraph(); p.alignment=WD_ALIGN_PARAGRAPH.CENTER; p.paragraph_format.space_after=Pt(30); set_font(p.add_run("Municipal Treasury Office"), size=12, color=MUTED)
    m.table(["Document", "Version", "Issued", "Classification"], [["AFMS User Manual", MANUAL_VERSION, ISSUE_DATE, "Single-operator working copy"]], [3300,1500,1860,2700], font_size=9)
    m.note("Important", f"This manual describes AFMS as implemented through manual version {MANUAL_VERSION}. AFMS is configured for one operator with full access. Transaction statuses and validation safeguards still apply.", "warning")

    # Contents and quick start
    m.chapter("1", "About This Manual", "Purpose, audience, system scope, and how to use this guide.")
    m.heading("1.1 Purpose", 2)
    m.body("AFMS - Accountable Forms Management System is an offline Windows desktop application used by the Municipal Treasury Office to track Official Receipts and other accountable forms. It preserves the chain of accountability from stock receipt through issuance, use, transfer, return, exception handling, reversal, reporting, fiscal-year close, and archival.")
    m.heading("1.2 Intended user", 2)
    m.body("AFMS is configured for one accountable-forms operator who receives stock, records issuance and usage, maintains collector accountability, performs corrections, generates reports, and protects the local database.")
    m.heading("1.3 What AFMS does", 2)
    m.bullets(["Maintains editable master data for form types, barangays, collectors, users, and office settings.", "Generates controlled booklet inventory from validated serial ranges.", "Tracks current holder, barangay, remaining quantity, movements, usage, returns, and exceptions.", "Uses permission-based workflows and records approvals, rejections, finalization, reversal, printing, export, backup, and restore activity.", "Produces printable PDF forms, professional reports, and Excel workbooks entirely offline."])
    m.heading("1.4 What AFMS does not replace", 2)
    m.body("AFMS supports accountability records; it does not replace statutory source documents, physical receipt custody, required signatures, cashbook procedures, or applicable Commission on Audit and treasury regulations. Retain original documents according to office policy.")
    doc.add_page_break()
    m.heading("1.5 Contents at a glance", 2)
    m.table(["Chapter", "Subject"], [["2", "Installation, first launch, login, and security"],["3", "Application interface and navigation"],["4", "Single-operator access and transaction statuses"],["5", "Master data"],["6", "Stock receiving"],["7", "Inventory and movement history"],["8", "Issuance and collector accountability"],["9", "Usage, transfers, returns, exceptions, attachments, and reversals"],["10", "Reports, PDF, Excel, and printing"],["11", "Account, settings, audit log, and session security"],["12", "Backup, restore, and fiscal years"],["13", "Recommended operating workflows and checklists"],["14", "Troubleshooting and frequently asked questions"],["Appendices", "Reference numbers, statuses, initial data, and glossary"]], [1600,7760])
    m.heading("1.6 What's new in manual version 1.2", 2)
    m.bullets(["One full-access operator role replaces the previous multi-role setup.", "The same operator may submit, approve, finalize, reject, or safely reverse eligible transactions.", "Draft stock-receiving records can be deleted before they affect inventory.", "Finalized records remain protected and corrections still use audited reversal.", "Existing users automatically receive the single full-access role while older roles are retained only as inactive history.", "Modern navy-and-teal login branding and responsive grids remain in place."])

    m.chapter("2", "Getting Started and Signing In", "Installation, first operator setup, login, password changes, and logout.")
    m.picture("login_screen.png", caption="Figure 1. Current AFMS sign-in screen. Credentials are entered only in the right-side form.")
    m.heading("2.1 Computer requirements",2)
    m.bullets(["Windows 10 or Windows 11, 64-bit.", "A user account with permission to install the application when using the installer.", "Sufficient local storage for the database, attachments, reports, logs, and backups.", "A configured printer only when paper printing is required. AFMS itself works without internet access."])
    m.heading("2.2 Install AFMS",2)
    m.steps(["Close any running AFMS window.", "Run AFMS-Setup-1.0.0-win-x64.exe.", "Follow the installer prompts and optionally create a desktop shortcut.", "Open AFMS from the Start Menu or desktop shortcut.", "On upgrade, install over the existing application. Operational data is stored separately under the user's local application-data folder."])
    m.note("Data location", "The live database, attachments, and rolling logs are stored under %LOCALAPPDATA%\\AFMS. Uninstalling the program does not silently delete those records. Back them up before computer replacement or operating-system reinstallation.", "info")
    m.heading("2.3 First launch operator setup",2)
    m.steps(["Enter your full display name.", "Enter a unique username.", "Create a password with at least 12 characters containing uppercase and lowercase letters, a number, and a symbol.", "Enter the same password in Confirm password.", "Choose Create my account. AFMS hashes the password with BCrypt; the plain-text password is never stored.", "Store your credentials in a secure place you control."])
    m.note("No default password", "AFMS does not contain a permanent password. Keep your password and a recent validated backup secure; do not edit the database manually to bypass login.", "danger")
    m.heading("2.4 Sign in",2)
    m.steps(["Open AFMS. The navy panel identifies AFMS and confirms offline operation.", "Enter your username and password in the white form on the right.", "Choose Sign in.", "If prompted, create a new password before proceeding.", "Confirm that your name and Full access appear in the lower-left account area."])
    m.body("Failed login messages are intentionally generic. Repeated failed attempts increase the failed-login count and may temporarily lock the account. Inactive accounts cannot sign in.")
    m.heading("2.5 Sign out and automatic timeout",2)
    m.bullets(["Use the sign-out icon beside your account name whenever leaving the workstation.", "AFMS automatically ends an inactive session after the configured timeout.", "Unsaved form entries may be lost when a session expires; save drafts before leaving the workstation.", "Never allow another employee to continue using your signed-in session."])

    m.chapter("3", "Understanding the AFMS Interface", "Navigation, page structure, filters, tables, editor panels, and notifications.", page_break=False)
    m.picture("interface_map.png", caption="Figure 2. AFMS application shell and the main working areas.")
    m.heading("3.1 Left navigation",2)
    m.body("The left navigation is grouped into Overview, Master Data, Stock & Accountability, and Administration. The selected page is highlighted. A page is hidden when the signed-in user lacks the required permission.")
    m.heading("3.2 Top bar",2)
    m.bullets(["The breadcrumb identifies the current module.", "OFFLINE READY confirms that AFMS is designed to operate without an internet connection.", "The window title identifies AFMS and the Municipal Treasury Office."])
    m.heading("3.3 Common page pattern",2)
    m.table(["Area", "How to use it"], [["Page heading", "Explains the purpose of the module."],["Search and filters", "Narrow records before loading or exporting. Clear filters when a record appears missing."],["Data grid", "Select one complete row before using View, Edit, Submit, Approve, Finalize, Print, or Reverse."],["Editor panel", "Enter or review transaction fields. Required fields are marked with an asterisk where applicable."],["Action bar", "Commands apply to the selected record and may be disabled because of status or permission."],["Notification area", "Shows success, validation, or error messages. Read the message before retrying."]], [2050,7310])
    m.heading("3.4 Keyboard and resizing",2)
    m.bullets(["Use Tab and Shift+Tab to move between controls.", "Press Enter on a default Search or Save action where supported.", "Use arrow keys to move through list and table selections.", "Labels remain above date and filter fields so values are readable at common Windows display scales.", "Maximize AFMS for wide inventory and movement grids. Columns retain readable headings and horizontal scrolling remains available when necessary.", "Focused controls display a clear border for keyboard accessibility."])
    m.note("Selection matters", "Most transaction commands operate on the selected row, not merely the visible page. Confirm the reference number and status before approving, finalizing, reversing, printing, or attaching a file.", "warning")

    m.chapter("4", "Single-Operator Access and Workflow Statuses", "Full access with deliberate review steps and protected transaction history.")
    m.heading("4.1 Single full-access role",2)
    m.table(["Access model", "What it means"], [["Full access", "Your signed-in operator account can use all AFMS modules, including approvals, reversals, settings, backups, restore, reports, audit review, and fiscal-year administration."]], [2450,6910])
    m.note("Review steps remain", "You may submit and approve your own transaction, but AFMS keeps the separate statuses so you can pause, recheck the source document and serials, then consciously approve and finalize. Every action is audited.", "info")
    m.picture("approval_workflow.png", caption="Figure 3. Standard approval and reversal workflow.")
    m.heading("4.2 Transaction status meanings",2)
    m.table(["Status", "Meaning", "User action"], [["Draft", "Editable working record.", "Complete fields, save, verify, or delete the draft when supported."],["Submitted", "Ready for a deliberate review.", "Recheck source documents, collector, ranges, and quantities."],["Approved", "You confirmed the transaction is ready.", "Finalize when the physical action and records agree."],["Rejected", "You stopped the submitted request.", "Review the reason and create or correct a valid draft."],["Finalized", "Inventory and accountability effects are committed.", "Do not edit or delete directly."],["Reversed", "A controlled opposite transaction was posted.", "Original remains in history; review movements and ledgers."],["Archived", "Retained for history and no longer operational.", "Read-only reference."]], [1500,3900,3960])
    m.heading("4.3 Booklet status meanings",2)
    m.table(["Status", "Meaning"], [["Available", "In office stock and eligible for issuance."],["Issued", "Assigned to a collector; no recorded usage."],["Partially Used", "Some serials were used; usable balance remains."],["Fully Used", "No usable receipts remain."],["Returned", "Returned from a collector under a recorded return."],["Transferred", "Accountability moved between collectors."],["Spoiled / Cancelled", "Not available for normal use."],["Lost / Missing", "Unavailable and subject to exception handling."],["Archived", "Removed from active operations but preserved historically."]], [2200,7160])

    m.chapter("5", "Master Data", "Form types, barangays, collectors, users, and configuration records used by transactions.")
    m.heading("5.1 Accountable Form Types",2)
    m.body("Use Form Types to define each accountable-form product before receiving stock.")
    m.table(["Field", "Purpose"], [["Form Code", "Unique short code used throughout AFMS."],["Form Name", "Required official or working name."],["Description / Category", "Classification and explanatory details."],["Receipts Per Booklet", "Default quantity; must be greater than zero."],["Serial Prefix / Length", "Optional serial-number formatting information."],["Individual Tracking", "Indicates whether every receipt is tracked."],["Active / Remarks", "Operational availability and notes."]], [2600,6760])
    m.steps(["Open Form Types.", "Use Search and the active/inactive filter to find an existing definition.", "Choose Add form type, enter all required information, then Save.", "To change a record, select it and choose Edit.", "Use Deactivate / Reactivate instead of deleting historical definitions."])
    m.note("Validation", "Form Code must be unique, Form Name is required, and Receipts Per Booklet must be greater than zero. Historical form types remain visible after deactivation.", "warning")
    m.heading("5.2 Barangays",2)
    m.steps(["Open Barangays.", "Search by code or name and filter by active status.", "Choose Add barangay and enter Barangay Code, Barangay Name, Municipality, Province, status, and remarks.", "Select a record and choose Edit to update it.", "Deactivate a barangay that should no longer be selectable; reactivate it when authorized."])
    m.note("Duplicate protection", "Barangay codes and names must be unique. Seeded Dipaculao records remain editable and are imported only once per configuration version.", "info")
    m.heading("5.3 Collectors",2)
    m.table(["Field", "Purpose"], [["Collector Number", "Required unique collector code."],["Full Name", "Collector's official name."],["Employee Number / Position", "Employment reference information."],["Office or Department", "Organizational assignment."],["Assigned Barangay", "Optional current area assignment."],["Contact / Email", "Optional contact details."],["Appointment Reference", "Designation or authority reference."],["Active / Remarks", "Eligibility for new transactions and notes."]], [2700,6660])
    m.steps(["Open Collectors.", "Search by number, name, or employee number; optionally filter by barangay and status.", "Choose Add collector and enter the required collector number and full name.", "Use View details to inspect a record without editing.", "Use Edit to update authorized details.", "Deactivate collectors who should no longer receive forms. Historical transactions continue to show them."])
    m.note("Do not delete history", "Collectors with transaction history must not be permanently deleted. Deactivation prevents new assignments while preserving prior accountability and reports.", "danger")

    m.chapter("6", "Stock Receiving", "Create receiving drafts, validate serial ranges, generate booklets, and finalize stock.")
    m.picture("lifecycle.png", caption="Figure 4. The accountable-form lifecycle managed by AFMS.")
    m.heading("6.1 Before receiving",2)
    m.bullets(["Confirm the accountable form type exists and is active.", "Verify the source document, physical booklet count, receipts per booklet, and complete serial range.", "Confirm the correct series year and storage location.", "Do not receive a range already recorded in AFMS."])
    m.heading("6.2 Create a receiving draft",2)
    m.steps(["Open Receive Stock.", "Choose New receiving draft. AFMS assigns a reference such as RCV-2026-000001.", "Enter Date Received, Series Year, Form Type, Source or Issuing Office, Source Document Number, Storage Location, Received By, and Remarks.", "Choose Add range for each stock-receipt item.", "Enter Number of Booklets, Receipts Per Booklet, Beginning Serial, and Ending Serial. Confirm the calculated total.", "Add additional ranges when one receiving transaction contains more than one valid item.", "Choose Save draft."])
    m.heading("6.3 Quantity and range calculation",2)
    m.body("Total receipt quantity equals Ending Serial minus Beginning Serial plus one. It must also equal Number of Booklets multiplied by Receipts Per Booklet.")
    m.note("Example", "10 booklets x 50 receipts = 500 receipts. Beginning 100001 and ending 100500 also equals 500. AFMS generates 100001-100050, 100051-100100, continuing through 100451-100500.", "success")
    m.heading("6.4 Submit and finalize",2)
    m.steps(["Select the saved draft and use View to recheck every field and range.", "Choose Submit when the transaction is complete.", "Review the source, quantities, and non-overlapping serials yourself.", "Choose Finalize. AFMS validates all items again inside one database transaction.", "Confirm that generated booklets appear in Stock Inventory and Received movements appear in Movement History."])
    m.heading("6.5 Receiving protections",2)
    m.bullets(["Beginning serial cannot exceed ending serial.", "Zero and negative quantities are rejected.", "Booklet count, per-booklet count, range quantity, and generated booklet count must agree.", "Duplicate or overlapping serial ranges are rejected.", "If any item fails, the entire finalization rolls back.", "A Draft can be removed with Delete draft before it affects inventory.", "Finalized receipts cannot be edited or deleted directly.", "Repeated finalization clicks do not create duplicate inventory."])
    m.note("Layered integrity", "AFMS checks ranges before saving and also enforces non-overlap rules inside SQLite. This protects stock, generated booklet ranges, active accountability ranges, and issuance reservations even when operations occur close together.", "info")
    m.heading("6.6 Reversal",2)
    m.body("For a Draft entered by mistake, select it and choose Delete draft. For a Finalized receipt, choose Reverse and enter a complete reason. You may reverse your own receipt, but only while every generated booklet is still untouched and Available. AFMS preserves the original transaction and writes reversal audit and stock history; it blocks reversal when later transactions depend on the stock.")

    m.chapter("7", "Stock Inventory and Movement History", "Find booklets or individual receipts and trace every movement.", page_break=False)
    m.heading("7.1 Search inventory",2)
    m.steps(["Open Stock Inventory.", "Enter free text to search references, form type, collector, or storage location.", "Optionally choose Form Type, Series Year, Status, Collector, Barangay, and received-date range.", "Use Available only for issuable office stock or Outstanding only for collector-held accountability.", "To find an individual receipt, enter its serial number in the serial-number field and choose Search inventory."])
    m.note("Containing-range search", "Searching serial 100025 returns the booklet whose beginning and ending serials contain 100025; an exact beginning-serial match is not required.", "info")
    m.heading("7.2 Read inventory columns",2)
    m.table(["Column group", "Information"], [["Identity", "Form Type, Series Year, Booklet Number, Beginning Serial, Ending Serial."],["Quantity", "Total, Used, and Remaining quantities."],["Accountability", "Current Holder and Barangay."],["Dates", "Date Received, Date Issued, and Last Movement Date."],["Control", "Current Status, Storage Location, and Remarks."]], [2350,7010])
    m.heading("7.3 Inventory detail",2)
    m.steps(["Select a booklet row.", "Choose Load selected details.", "Review the original receiving reference, source, booklet range, current accountability, and status.", "Review the chronological movement history.", "Review usage ranges, exceptions, and remarks before taking corrective action."])
    m.heading("7.4 Stock Movement History",2)
    m.body("Movement History is the central immutable chronology for received, issued, used, transferred, returned, adjusted, cancelled, archived, and reversed movements.")
    m.steps(["Open Movement History.", "Search by reference, form, source, or destination.", "Optionally filter by Form Type, Movement Type, individual receipt serial, and date range.", "Use the results to reconcile inventory, collector accountability, and reports."])
    m.note("Never overwrite history", "Current holder fields describe present accountability. Historical movements preserve prior holders and should be used when investigating how a range reached its current status.", "warning")

    m.chapter("8", "Issuance and Collector Accountability", "Reserve available booklet ranges, obtain approval, finalize issuance, and review collector ledgers.", page_break=False)
    m.heading("8.1 Create an issuance",2)
    m.steps(["Open Issue to Collectors and choose a new issuance draft.", "Enter Date Issued, Collector, Barangay or Collection Area, Purpose, Released By, Approved By, Received By, and Remarks.", "Open the inventory-selection area and filter by form type, series year, storage location, or serial range.", "Select one or more available booklet ranges and add them to the issuance. An available range may be a full original booklet or an exact unused subrange previously returned to stock.", "Review form type, beginning serial, ending serial, quantity, and series year for each item.", "Save the draft. AFMS assigns an ISS reference number."])
    m.heading("8.2 Submit, approve, and finalize",2)
    m.steps(["Select the draft and choose Submit.", "Recheck the collector, purpose, booklets, ranges, and physical release documents.", "Choose Approve or Reject and record comments when required.", "After approval, choose Finalize.", "AFMS marks each booklet Issued, assigns collector and barangay, records Date Issued, writes Issued movements, and updates the collector ledger atomically."])
    m.heading("8.3 Issuance restrictions",2)
    m.bullets(["Inactive collectors cannot receive new issuances.", "Only exact ranges currently marked Available may be issued.", "Already issued, fully used, cancelled, lost, missing, and archived ranges are excluded.", "Overlapping ranges reserved by another unfinalized issuance cannot be selected again.", "A usable partial range returned to available stock can be reissued without changing the history or ownership of other serials from the same original booklet.", "Closed fiscal periods are protected.", "If one item fails, the entire issuance finalization rolls back."])
    m.heading("8.4 Print the issuance receipt",2)
    m.steps(["Select the issuance reference.", "Choose the print-preview or PDF action available on the Issuance screen or select Accountable Forms Issuance Receipt in Reports.", "Confirm municipality, province, office, collector, ranges, quantities, purpose, remarks, and signatory lines.", "Print or save the PDF and obtain required signatures."])
    m.heading("8.5 Collector Accountability Ledger",2)
    m.body("The ledger is calculated from immutable stock movements and shows Quantity In, Quantity Out, and Running Balance for each collector.")
    m.steps(["Open Collector Ledger.", "Choose a Collector and optionally filter by Form Type, Barangay, month, year, or date range.", "Use Outstanding only to focus on current accountability.", "Choose Load ledger.", "Review transaction date, reference, type, serial range, quantities, balance, barangay, status, and remarks."])
    m.note("Balance interpretation", "Issuance and Transfer In increase accountability. Usage, Transfer Out, Return, cancellation/spoilage, valid adjustment, and reversal effects change the balance according to their recorded movement direction.", "info")

    m.chapter("9", "Usage, Transfers, Returns, Exceptions, and Reversals", "Receipt-level accountability changes after issuance.", page_break=False)
    m.heading("9.1 Select the accountable range",2)
    m.steps(["Open Usage, Transfers & Returns.", "Select the Current Collector.", "Choose Load owned ranges.", "Select the Owned unused range.", "Confirm Beginning, Ending, Transaction Date, and the required reason or source reference.", "Choose the correct Usage, Transfers, Returns, or Exceptions tab."])
    m.heading("9.2 Record used or sold receipts",2)
    m.steps(["On Usage, enter Amount Collected when applicable and Remarks.", "Enter a beginning and ending serial within the selected collector-held range.", "Choose Add serial range. Repeat for additional continuous ranges in the same report.", "Review the pending ranges and choose Save usage draft.", "Select the saved usage report and choose Finalize selected after verification."])
    m.bullets(["AFMS rejects ranges outside the collector's accountability, under the wrong collector, duplicated, overlapping, negative, or greater than the usable balance.", "A sequential gap requires an explanation when sequential use is required.", "Partial use sets the booklet to Partially Used; no remaining usable receipts sets it to Fully Used."])
    m.heading("9.3 Transfer unused receipts",2)
    m.steps(["Select the original collector and unused range.", "On Transfers, choose an active Receiving Collector.", "Enter Released or Approved By and Remarks.", "Save the transfer draft.", "Select the record and follow Submit, Approve, and Finalize.", "Use Add attachment for supporting documents when needed."])
    m.body("Only unused receipts currently held by the original collector can be transferred. Partial-range transfer is supported. Finalization writes Transfer Out and Transfer In movements and ledger entries while preserving the original issuance history.")
    m.heading("9.4 Return unused forms",2)
    m.steps(["Select the collector-held unused range.", "On Returns, choose the correct Disposition.", "Enter Received By and Remarks, then save the return draft.", "Submit, review, approve, and finalize.", "Attach supporting documents when required."])
    m.table(["Disposition", "Inventory result"], [["Returned to Available Stock", "Usable returned range becomes available for future issuance."],["Returned but Damaged", "Not returned to issuable stock."],["For Cancellation", "Held for controlled cancellation."],["Archived", "Removed from active operations and retained historically."],["Subject to Investigation", "Not available until investigation and resolution."]], [3100,6260])
    m.heading("9.5 Exception management",2)
    m.body("Use Exceptions for Spoiled, Cancelled, Lost, Missing, Damaged, Numbering Error, Invalidated, or Other incidents.")
    m.steps(["Select the affected collector and booklet/range.", "Choose the Incident Type.", "Enter a detailed reason or explanation, action taken, and remarks.", "Save the exception draft and add allowed supporting attachments.", "Submit the incident.", "Review the details and choose Approve or Reject.", "Finalize only after approval."])
    m.note("Permanent record", "Exception transactions are never permanently deleted. Lost, missing, damaged, cancelled, spoiled, and invalidated receipts do not become normally issuable stock.", "danger")
    m.heading("9.6 Attachments",2)
    m.bullets(["Attachments are supported for transfers, returns, and exceptions.", "Select a saved transaction before choosing Add attachment.", "AFMS stores the file outside the database in an organized application-data folder and records file metadata.", "Dangerous executable file types are rejected.", "Use only authorized supporting documents and follow privacy requirements."])
    m.heading("9.7 Rejecting and reversing",2)
    m.body("Reject applies to a submitted record and requires a meaningful reason. Reverse applies to an eligible finalized record and requires a complete reason. Your full-access account can perform both actions.")
    m.steps(["Select the exact reference and confirm its status.", "Enter the reason in the reason/source field.", "Choose Reject or Reverse as authorized.", "Read the confirmation carefully and proceed once.", "Verify the opposite stock movement, collector balance, transaction status, and audit entry."])
    m.note("Reversal safety", "A reversal never deletes the original transaction. It may be blocked when later activity makes restoration unsafe. Repeated reversal is prohibited.", "warning")

    m.chapter("10", "Reports, PDF, Excel, and Printing", "Generate official reports from shared finalized-transaction calculations.")
    m.heading("10.1 Report Center workflow",2)
    m.steps(["Open Reports.", "Choose a category and report type.", "Enter applicable filters: date range, fiscal year, form type, collector, barangay, status, reference number, outstanding only, or include drafts when specifically required.", "Choose Generate or Preview.", "Review totals and metadata before printing or exporting.", "Choose Save as PDF, Export to Excel, or Print.", "Open the generated file when prompted and file it according to office policy."])
    m.note("Finalized data by default", "Official reports use finalized transactions unless the user explicitly selects drafts. Reversals, returned-to-stock forms, and exceptions are handled by shared report query services to avoid duplicate business calculations.", "info")
    report_rows = [
        ("Inventory", "Accountable Forms Stock Inventory; Available Forms; Inventory by Form Type; Beginning, Ending, and Year-End Inventory; Stock Movement; Inventory by Storage Location"),
        ("Collector", "Issuance per Collector; Accountability Ledger; Outstanding Forms; Used or Sold Receipts; Unused Forms; Transaction History; Accountability by Barangay; Year-End Accountability"),
        ("Transaction", "Stock Receiving, Issuance, Usage, Transfer, Return, and Reversal Registers"),
        ("Exception", "Spoiled; Cancelled; Lost or Missing; Damaged; Serial Number Gap; Inventory Variance; Unresolved Exceptions"),
        ("Consolidated", "Annual Consolidated; Monthly Issuance and Usage; Inventory by Barangay; Issued vs Used vs Remaining; Balance Roll-Forward; Summary by Form Type or Collector"),
        ("Audit", "Audit Activity Report"),
    ]
    m.heading("10.2 Available report families",2)
    m.table(["Category", "Reports"], report_rows, [1900,7460], font_size=9)
    m.heading("10.3 Printable transaction forms",2)
    m.bullets(["Stock Receiving Form", "Accountable Forms Issuance Receipt", "Usage Report Form", "Accountable Forms Transfer Form", "Accountable Forms Return Form", "Accountable Forms Exception Report", "Transaction Reversal Record"])
    m.heading("10.4 Official report layout",2)
    m.body("Official reports use System Settings for the Republic of the Philippines heading, Province, Municipality, Municipal Treasury Office, office address, report title, period, generated date/time, report reference, Prepared By, Checked By, Certified Correct By, and page numbering.")
    m.heading("10.5 Excel output",2)
    m.bullets(["Clear headers and report metadata", "True numeric quantities and appropriate date formats", "Auto-sized columns", "Frozen header row", "Filters where appropriate", "Configured output folder and friendly file-access errors"])
    m.heading("10.6 Printing checklist",2)
    m.steps(["Confirm the report title, filters, reporting period, and reference number.", "Compare a sample total with Inventory, Movement History, or Collector Ledger.", "Confirm signatory names from System Settings.", "Use Print Preview before paper printing.", "Select the correct printer and paper size.", "File the signed output and retain the electronic copy if required."])

    m.chapter("11", "Account, Settings, and Audit", "Single-operator account maintenance, system settings, audit-log review, and security operations.")
    m.heading("11.1 Operator account",2)
    m.body("AFMS uses one Full access role. The Users page remains available for account recovery or a future handover, but every active account receives the same full-access role; older multi-role options are inactive and no longer selectable.")
    m.steps(["Open Users.", "Select your account to view status, last login, failed-login count, and account activity.", "Use Reset password only when needed and choose a password that meets the policy.", "Keep at least one active account so AFMS remains accessible."])
    m.heading("11.2 Edit, reset, activate, or deactivate",2)
    m.bullets(["Edit updates the display name, optional collector link, and active status; Full access is fixed.", "Reset password creates a temporary password and can force change at next login.", "Password reset or account deactivation invalidates older sessions for that account.", "Deactivate blocks future login while preserving activity and transaction history.", "AFMS will not deactivate or delete the final active full-access account.", "View activity shows login, account, and security events.", "Password hashes are never displayed."])
    m.heading("11.3 System Settings",2)
    m.table(["Group", "Settings"], [["Municipality and office", "Municipality, Province, Office Name, Office Address, Municipal Treasurer, Default Fiscal Year."],["Report signatories", "Prepared By, Checked By, Certified Correct By."],["Files and references", "Reference prefixes, backup folder, report output folder."],["Backup policy", "Daily backup enabled/time, retention days, recent-backup warning hours."],["Session security", "Automatic logout after inactivity in minutes."]], [2500,6860])
    m.note("Configuration source", "Do not hard-code signatories or office information into reports. Update System Settings and regenerate the report. Prepared By, Checked By, Certified Correct By, and Default Fiscal Year are intentionally flexible.", "info")
    m.heading("11.4 Audit Log",2)
    m.steps(["Open Audit Log.", "Search by user, description, entity, reference, or computer name.", "Filter by date range and activity type.", "Use Previous and Next to move through paginated results.", "Select a row to review original values, new values, reason, and integrity hash."])
    m.body("Audit records include login, logout, failed login, create, edit, submit, approve, reject, finalize, reverse, print, export, backup, restore, permission changes, and fiscal-year actions. Ordinary users cannot edit or delete the audit log. Records are append-only and integrity-chained. When an older database is upgraded, AFMS retains its legacy audit history, upgrades eligible entries into the protected chain, verifies the result, and records the upgrade event.")

    m.chapter("12", "Backup, Restore, and Fiscal Years", "Protect the database and control year-end operations.", page_break=False)
    m.picture("backup_flow.png", caption="Figure 5. Validated backup and restart-safe restoration flow.")
    m.heading("12.1 Backup responsibilities",2)
    m.bullets(["AFMS can create manual, scheduled, application-closing, and pre-restore backups.", "The backup service validates SQLite integrity, records file size and hash, and keeps backup history.", "Retention settings remove old backups according to policy.", "A visible warning appears when no recent valid backup exists.", "Use a separate drive or approved network location; do not keep every backup beside the live database."])
    m.heading("12.2 Create and validate a manual backup",2)
    m.steps(["Open Backup & Fiscal Years and select Backups.", "Confirm the configured backup folder and any location warning.", "Enter a reason when required.", "Choose Create backup.", "Select the new history row and choose Validate backup.", "Confirm Valid is checked and record the created date, file, size, and creator."])
    m.heading("12.3 Restore a backup",2)
    m.note("Sensitive operation", "Restoration replaces the working database after restart. Ensure all AFMS users are out, no writes are active, and the selected file is the correct validated backup.", "danger")
    m.steps(["Create and validate a current manual backup first.", "Choose the backup file to restore or select an eligible history record.", "Enter the required restore reason.", "Choose Restore and confirm the warning.", "AFMS validates the file and creates an automatic pre-restore safety backup.", "AFMS schedules the replacement and restarts safely.", "After restart, confirm users, settings, recent transactions, inventory totals, and audit entries."], compact=True)
    m.heading("12.4 Fiscal-year creation and close",2)
    m.steps(["Open Backup & Fiscal Years and select Fiscal Years.", "Enter Year, Start Date, and End Date; choose Create fiscal year.", "Before closing, select the year and choose Pre-close check.", "Resolve draft transactions, unresolved exceptions, inventory discrepancies, invalid balances, and unapproved transactions.", "Enter the reason and choose Request close.", "Review the check, then choose Approve close using your signed-in account.", "AFMS creates a year-end snapshot, carries forward outstanding forms, and protects the closed period from edits."], compact=True)
    m.heading("12.5 Documented exceptions",2)
    m.body("When authorized by office policy, Allow documented exceptions permits closure despite listed issues only when the reason fully documents the exception. This does not remove the underlying audit trail or reconciliation responsibility.")
    m.heading("12.6 Reopen a fiscal year",2)
    m.steps(["Select the closed year, enter a detailed reason, and choose Request reopen.", "Review the reason and choose Approve reopen.", "AFMS records both actions, timestamps, and the reason.", "After corrections, rerun the pre-close check and close the year again."], compact=True)

    m.chapter("13", "Recommended Operating Procedures", "Practical daily, receiving, issuing, monthly, and year-end checklists.")
    m.heading("13.1 Daily opening checklist",2)
    m.bullets(["Sign in using your own account.", "Review Dashboard counts and pending approvals.", "Check the backup warning and last validated backup.", "Confirm the correct fiscal year is open.", "Review unresolved exceptions or rejected transactions requiring attention."])
    m.heading("13.2 Receiving-day checklist",2)
    m.bullets(["Count physical booklets and verify the source document.", "Check beginning and ending serials on the first and last booklet.", "Confirm per-booklet quantity and series year.", "Save as draft, independently review, then submit/finalize.", "Search the first and last serial in Inventory.", "File the receiving document and generated receiving form."])
    m.heading("13.3 Issuance-day checklist",2)
    m.bullets(["Confirm collector is active and authorized.", "Select only Available inventory.", "Match physical booklet serials to the issuance items.", "Submit, pause, and recheck the transaction before approving it yourself.", "Finalize only after physical release controls are complete.", "Print, sign, and file the issuance receipt.", "Verify the collector ledger increased correctly."])
    m.heading("13.4 Usage reporting checklist",2)
    m.bullets(["Use the collector who actually holds the range.", "Record continuous ranges accurately and explain legitimate gaps.", "Compare amount collected with the source report where applicable.", "Finalize and verify Used and Remaining quantities.", "Resolve spoilage, cancellation, loss, or numbering errors through Exceptions rather than forcing usage."])
    m.heading("13.5 Monthly reconciliation",2)
    m.bullets(["Generate Stock Inventory and Stock Movement reports.", "Review Outstanding Forms per Collector and collector ledgers.", "Compare Issued versus Used versus Remaining totals.", "Review unresolved exceptions and serial gaps.", "Confirm all submitted transactions were approved, rejected, or finalized.", "Create and validate an off-device backup."])
    m.heading("13.6 Year-end checklist",2)
    m.bullets(["Complete all draft and pending transactions.", "Investigate inventory variances and invalid balances.", "Resolve or formally document exceptions.", "Generate beginning, ending, year-end, and consolidated reports.", "Run the fiscal-year pre-close check.", "Obtain authorized close approval and inspect the snapshot.", "Create, validate, and securely retain a year-end backup."])
    m.heading("13.7 Complete lifecycle example",2)
    m.steps(["Receive 10 booklets of 50 receipts, serials 100001-100500, and finalize.", "Verify 10 Available booklets and 10 Received movements.", "Issue booklet 100001-100050 to an active collector and finalize.", "Record usage 100001-100020; verify Partially Used and remaining 30.", "Transfer unused 100021-100030 to another active collector and finalize.", "Return 100031-100040 to available stock when physically usable.", "Record an approved cancellation exception for 100041 if required.", "Verify both collector ledgers, movement history, inventory detail, and consolidated reports."], compact=True)

    m.chapter("14", "Troubleshooting and Frequently Asked Questions", "Safe responses to common operating problems.", page_break=False)
    faq = [
        ("I cannot see a menu item.", "Sign out and sign in again so AFMS refreshes the Full access session. If it remains missing, review the application log or contact technical support."),
        ("Save or Finalize is disabled.", "Confirm a row is selected, required fields are complete, and the transaction has the correct status. Wait for an active save to complete before clicking again."),
        ("AFMS says the range overlaps.", "Search the beginning, ending, and a middle serial in Inventory. Review the original receiving reference. Correct the draft; never alter finalized database records manually."),
        ("An individual serial cannot be found.", "Clear restrictive filters, search only the serial-number field, then review whether the relevant receiving transaction was finalized or reversed."),
        ("A collector is missing from issuance.", "The collector may be inactive. Review the Collectors module and reactivate only with authorization."),
        ("Can I approve a transaction I submitted?", "Yes. In the single-operator version, submit the record, recheck it, then approve and finalize it using the same account."),
        ("A reversal is blocked.", "Later activity may depend on the transaction, or it may already be reversed. Review inventory detail and movements, then follow the authorized correction procedure."),
        ("A report is empty.", "Check date range, fiscal year, status, reference, collector, and form filters. Official reports usually require finalized transactions."),
        ("PDF or Excel export fails.", "Confirm the output folder exists and is writable, the destination file is not open, and sufficient disk space is available. Technical details are written to AFMS logs."),
        ("Backup validation fails.", "Do not restore the file. Select another validated backup or create a new one. Preserve the failed file for technical investigation."),
        ("I forgot my password.", "AFMS cannot display the old password because only a secure hash is stored. Use a verified recovery or handover account if one exists; otherwise contact technical support and keep a current validated backup available."),
        ("The application closes after inactivity.", "This is the configured session timeout. Sign in again and save drafts before leaving the workstation."),
    ]
    m.table(["Problem", "Safe response"], faq, [2850,6510], font_size=9)
    m.doc.add_page_break()
    m.heading("14.1 When to contact technical support",2)
    m.bullets(["AFMS cannot start after an upgrade or restore.", "The same error continues after correcting visible validation issues.", "Database validation or backup validation fails.", "Inventory, movement, and ledger totals disagree after filters are aligned.", "PDF, Excel, printer, attachment, or folder failures persist despite verified access.", "The log contains repeated Error or Fatal entries."])
    m.note("Do not expose sensitive data", "When reporting a technical problem, provide the time, signed-in username, module, reference number, and error message. Do not send passwords, password hashes, or unrestricted database copies through unapproved channels.", "danger")

    m.chapter("A", "Appendix A - Reference Number and Status Guide")
    m.table(["Prefix", "Transaction", "Example"], [["RCV", "Stock Receiving", "RCV-2026-000001"],["ISS", "Issuance", "ISS-2026-000001"],["USE", "Usage Report", "USE-2026-000001"],["TRF", "Transfer", "TRF-2026-000001"],["RTN", "Return", "RTN-2026-000001"],["EXC", "Exception", "EXC-2026-000001"]], [1400,3500,4460])
    m.body("Reference sequences are generated atomically. AFMS does not obtain the next number by merely counting rows, which protects against duplicate references during repeated or concurrent operations.")
    m.heading("Stock movement types",2)
    m.bullets(["Received", "Issued", "Used", "Transferred", "Returned", "Adjusted", "Cancelled", "Archived", "Reversed"], compact=True)
    m.heading("Exception types",2)
    m.bullets(["Spoiled", "Cancelled", "Lost", "Missing", "Damaged", "Numbering Error", "Invalidated", "Other"], compact=True)

    m.chapter("B", "Appendix B - Initial Dipaculao Configuration")
    m.note("Editable seed data", "These records are starter configuration only. Authorized users may edit or deactivate them in AFMS. The initializer imports each configuration version once and does not overwrite later office edits.", "info")
    barangays = ["001 North Poblacion","002 South Poblacion","003 Bayabas","004 Borlongan","005 Buenavista","006 Calaocan","007 Diamanen","008 Dianed","009 Diarabasin","010 Dibutunan","011 Dimabuno","012 Dinadiawan","013 Ditale","014 Gupa","015 Ipil","016 Laboy","017 Lipit","018 Lobbot","019 Maligaya","020 Mijares","021 Mucdol","022 Puangi","023 Salay","024 Sapangkawayan","025 Toytoyan"]
    m.heading("Barangays",2)
    m.table(["Code and name", "Code and name"], [[barangays[i], barangays[i+13] if i+13 < len(barangays) else ""] for i in range(13)], [4680,4680], font_size=9.5)
    m.heading("Accountable form types",2)
    form_types = [["BIR-0016","CEDULA","50"],["AF51","LOCAL","50"],["AF56","RPT","50"],["AF57","SLAUGHTE","50"],["AF53","OWNERSH","50"],["AF54","MARRIAGE","50"],["AF58","BURIAL","50"],["AF51-TRUST","TRUST","50"],["AF52","TRANSFER","50"],["CASH-TICKET","CASH TICKET","50"],["AF51-BLD","LOCAL (BLD)","50"],["BIR-0017","CORPORATE CEDULA","50"]]
    m.table(["Code", "Form name", "Per booklet"], form_types, [2500,5160,1700])
    m.heading("Collectors",2)
    collectors = [["SMV","Shella May I. Velasco"],["AU","Marigold L. Dela Cruz"],["LCM","Leonardo C. Marzan Jr."],["NPDR","Nobleza P. Delos Reyes"],["ADRA","Angelita Dr. Agua"],["KFJ","Kristofferson F. Javar"],["CJC","Cherrie Joy L. Cacanindin"],["RMGC","Rhonica May G. Cagurangan"],["CLQ","Cecille Lyn C. Quirino"],["MEPC","Maria Elena P. Chavez"]]
    m.table(["Collector no.", "Full name"], collectors, [2500,6860])

    m.chapter("C", "Appendix C - Glossary")
    glossary = [["Accountability", "The forms or receipt ranges currently assigned to a holder and not yet discharged by valid usage, transfer, return, exception, adjustment, or reversal."],["Booklet", "A generated accountable-form inventory unit with a controlled beginning and ending serial."],["Collector", "A person authorized to receive accountable forms and report their disposition."],["Exception", "A controlled incident such as spoiled, cancelled, lost, missing, damaged, numbering error, or invalidation."],["Finalization", "The atomic step that commits inventory, movements, ledgers, and audit effects."],["Fiscal year snapshot", "The preserved year-end balance by form type produced during fiscal-year close."],["Outstanding", "Accountable forms currently held by collectors or otherwise not discharged."],["Range", "An inclusive beginning and ending serial; quantity equals ending minus beginning plus one."],["Reversal", "A controlled opposite entry that preserves the original transaction and audit trail."],["Stock movement", "An immutable chronological record of inventory or accountability change."],["Transaction status", "The workflow stage controlling what actions may occur."],["Validated backup", "A database copy that passed file, schema, SQLite integrity, and hash checks."]]
    m.table(["Term", "Definition"], glossary, [2200,7160], font_size=9.5)

    m.chapter("D", "Appendix D - Document Control and Training Sign-Off")
    m.heading("Revision history",2)
    m.table(["Version", "Date", "Summary", "Approved by"], [["1.0","16 July 2026","Initial comprehensive AFMS user manual.",""],["1.1",ISSUE_DATE,"Login redesign; range-aware issuance; integrity, session, audit, and responsive-layout updates.",""],["1.2",ISSUE_DATE,"Single-operator full access, same-user approval and reversal, and receiving-draft deletion.",""]], [1300,1700,4560,1800])
    m.heading("User training acknowledgment",2)
    m.body("I acknowledge that I received orientation on AFMS, understand that I must use only my assigned account, and agree to follow Municipal Treasury Office procedures for accountable forms, approvals, reports, backups, and confidential information.")
    m.table(["Name and position", "Signature", "Date"], [["","",""] for _ in range(5)], [4300,2860,2200])
    m.heading("Office notes",2)
    for _ in range(6):
        p = doc.add_paragraph("________________________________________________________________________________")
        p.paragraph_format.space_after = Pt(8); set_font(p.runs[0], color=BORDER)

    # Keep tables from splitting individual rows where possible.
    for table in doc.tables:
        for row in table.rows:
            tr_pr = row._tr.get_or_add_trPr()
            cant_split = OxmlElement("w:cantSplit"); tr_pr.append(cant_split)

    doc.save(DOCX_PATH)
    print(DOCX_PATH)


if __name__ == "__main__":
    build_manual()
