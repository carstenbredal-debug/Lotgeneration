import os
import io
import json
from pathlib import Path
from fastapi import FastAPI, Query, Response
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from starlette.responses import FileResponse
import pymssql
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib.colors import HexColor, black, white
from reportlab.platypus import (
    SimpleDocTemplate, Table, TableStyle, Paragraph, Spacer, PageBreak,
    KeepTogether,
)
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_LEFT, TA_CENTER

app = FastAPI()

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


def get_connection():
    conn_str = os.environ.get("SQLCONNSTR_IFTTEST", "")
    parts = {}
    for part in conn_str.split(";"):
        part = part.strip()
        if "=" in part:
            key, val = part.split("=", 1)
            parts[key.strip().lower()] = val.strip()

    server = parts.get("server", "").replace("tcp:", "").split(",")[0]
    port = 1433
    if "," in parts.get("server", ""):
        try:
            port = int(parts["server"].split(",")[1])
        except ValueError:
            port = 1433

    database = parts.get("initial catalog", parts.get("database", ""))
    user = parts.get("user id", parts.get("uid", ""))
    password = parts.get("password", parts.get("pwd", ""))

    return pymssql.connect(
        server=server,
        port=port,
        user=user,
        password=password,
        database=database,
        tds_version="7.3",
    )


@app.get("/api/filters")
async def get_filters():
    try:
        conn = get_connection()
        cursor = conn.cursor()

        filters = {}

        queries = {
            "types": "SELECT DISTINCT SalesType FROM dbo.CatalogLots WHERE SalesType IS NOT NULL ORDER BY SalesType",
            "genders": "SELECT DISTINCT Gender FROM dbo.CatalogLots WHERE Gender IS NOT NULL ORDER BY Gender",
            "groups": "SELECT DISTINCT [Group] FROM dbo.CatalogLots WHERE [Group] IS NOT NULL ORDER BY [Group]",
            "hairLengths": "SELECT DISTINCT HairLength FROM dbo.CatalogLots WHERE HairLength IS NOT NULL ORDER BY HairLength",
            "sizes": "SELECT DISTINCT Size FROM dbo.CatalogLots WHERE Size IS NOT NULL ORDER BY Size",
            "qualities": "SELECT DISTINCT Quality FROM dbo.CatalogLots WHERE Quality IS NOT NULL ORDER BY Quality",
            "colors": "SELECT DISTINCT Color FROM dbo.CatalogLots WHERE Color IS NOT NULL ORDER BY Color",
            "clarities": "SELECT DISTINCT Clarity FROM dbo.CatalogLots WHERE Clarity IS NOT NULL ORDER BY Clarity",
            "damages": "SELECT DISTINCT Damages FROM dbo.CatalogLots WHERE Damages IS NOT NULL ORDER BY Damages",
        }

        for key, sql in queries.items():
            cursor.execute(sql)
            filters[key] = [row[0] for row in cursor.fetchall()]

        conn.close()
        return filters
    except Exception as e:
        return Response(content=str(e), status_code=500)


@app.get("/api/catalog-lots")
async def get_catalog_lots(
    type: str = Query(None),
    gender: str = Query(None),
    group: str = Query(None),
    hairLength: str = Query(None),
    size: str = Query(None),
    quality: str = Query(None),
    color: str = Query(None),
    clarity: str = Query(None),
    damage: str = Query(None),
):
    try:
        conn = get_connection()
        cursor = conn.cursor(as_dict=True)

        sql = """
            SELECT
                LotNumber,
                StringNumber,
                SalesType,
                Gender,
                [Group],
                Color,
                Quality,
                Clarity,
                Size,
                HairLength,
                Damages,
                TotalSkins,
                BoxCount,
                IsShow
            FROM dbo.CatalogLots
            WHERE 1=1"""

        params = []

        if type:
            sql += " AND SalesType = %s"
            params.append(type)
        if gender:
            sql += " AND Gender = %s"
            params.append(gender)
        if group:
            sql += " AND [Group] = %s"
            params.append(group)
        if hairLength:
            sql += " AND HairLength = %s"
            params.append(hairLength)
        if size:
            sql += " AND Size = %s"
            params.append(size)
        if quality:
            sql += " AND Quality = %s"
            params.append(quality)
        if color:
            sql += " AND Color = %s"
            params.append(color)
        if clarity:
            sql += " AND Clarity = %s"
            params.append(clarity)
        if damage:
            sql += " AND Damages = %s"
            params.append(damage)

        sql += " ORDER BY CatalogSortOrder"

        cursor.execute(sql, tuple(params))
        lots = cursor.fetchall()

        # Convert bit/boolean fields
        for lot in lots:
            if "IsShow" in lot:
                lot["IsShow"] = bool(lot["IsShow"])

        conn.close()
        return lots
    except Exception as e:
        return Response(content=str(e), status_code=500)


@app.post("/api/generate")
async def generate_catalog(
    type: str = Query(None), gender: str = Query(None), group: str = Query(None),
    hairLength: str = Query(None), size: str = Query(None), quality: str = Query(None),
    color: str = Query(None), clarity: str = Query(None), damage: str = Query(None),
):
    try:
        conn = get_connection()
        cursor = conn.cursor()

        sql = "SELECT COUNT(*) FROM dbo.CatalogLots WHERE 1=1"
        params = []
        if type: sql += " AND SalesType = %s"; params.append(type)
        if gender: sql += " AND Gender = %s"; params.append(gender)
        if group: sql += " AND [Group] = %s"; params.append(group)
        if hairLength: sql += " AND HairLength = %s"; params.append(hairLength)
        if size: sql += " AND Size = %s"; params.append(size)
        if quality: sql += " AND Quality = %s"; params.append(quality)
        if color: sql += " AND Color = %s"; params.append(color)
        if clarity: sql += " AND Clarity = %s"; params.append(clarity)
        if damage: sql += " AND Damages = %s"; params.append(damage)

        cursor.execute(sql, tuple(params))
        count = cursor.fetchone()[0]
        conn.close()

        return Response(content=f"Found {count} catalog lots matching your filters.", status_code=200)
    except Exception as e:
        return Response(content=str(e), status_code=500)


def _build_where(type, gender, group, hairLength, size, quality, color, clarity, damage):
    sql = " WHERE 1=1"
    params = []
    if type:
        sql += " AND SalesType = %s"; params.append(type)
    if gender:
        sql += " AND Gender = %s"; params.append(gender)
    if group:
        sql += " AND [Group] = %s"; params.append(group)
    if hairLength:
        sql += " AND HairLength = %s"; params.append(hairLength)
    if size:
        sql += " AND Size = %s"; params.append(size)
    if quality:
        sql += " AND Quality = %s"; params.append(quality)
    if color:
        sql += " AND Color = %s"; params.append(color)
    if clarity:
        sql += " AND Clarity = %s"; params.append(clarity)
    if damage:
        sql += " AND Damages = %s"; params.append(damage)
    return sql, params


def _build_description(row):
    parts = [row.get("HairLength", ""), row.get("Size", ""),
             row.get("Quality", ""), row.get("Color", ""),
             row.get("Clarity", "")]
    dmg = row.get("Damages", "")
    if dmg and dmg.lower() != "none":
        parts.append(dmg)
    return " / ".join(p for p in parts if p)


@app.get("/api/generate-pdf")
async def generate_pdf(
    type: str = Query(None), gender: str = Query(None), group: str = Query(None),
    hairLength: str = Query(None), size: str = Query(None), quality: str = Query(None),
    color: str = Query(None), clarity: str = Query(None), damage: str = Query(None),
):
    try:
        conn = get_connection()
        cursor = conn.cursor(as_dict=True)

        where, params = _build_where(type, gender, group, hairLength, size, quality, color, clarity, damage)

        sql = f"""
            SELECT
                CatalogSortOrder, StringNumber, LotNumber, IsShow,
                SalesType, Gender, [Group], HairLength, Size,
                Quality, Color, Clarity, Damages,
                IncludedBoxNumbers, BoxCount, TotalSkins,

                COUNT(*) OVER (
                    PARTITION BY StringNumber
                ) AS LotsInString,

                ROW_NUMBER() OVER (
                    PARTITION BY StringNumber
                    ORDER BY CatalogSortOrder
                ) AS LotSequenceInString,

                SUM(TotalSkins) OVER (
                    PARTITION BY StringNumber
                ) AS StringTotalSkins,

                SUM(BoxCount) OVER (
                    PARTITION BY StringNumber
                ) AS StringBoxCount

            FROM dbo.CatalogLots
            {where}
            ORDER BY CatalogSortOrder
        """
        cursor.execute(sql, tuple(params))
        rows = cursor.fetchall()
        conn.close()

        if not rows:
            return Response(content="No lots found matching your filters.", status_code=404)

        # Group rows by section: "SalesType - Gender - Group"
        sections = {}
        for row in rows:
            key = f"{row['SalesType']} - {row['Gender']} - {row['Group']}"
            sections.setdefault(key, []).append(row)

        # Build PDF
        buf = io.BytesIO()
        doc = SimpleDocTemplate(
            buf, pagesize=A4,
            leftMargin=25*mm, rightMargin=25*mm,
            topMargin=20*mm, bottomMargin=20*mm,
        )

        styles = getSampleStyleSheet()
        section_style = ParagraphStyle(
            "SectionTitle", parent=styles["Heading3"],
            fontSize=11, spaceAfter=2, spaceBefore=4,
            textColor=black, fontName="Helvetica-Bold",
        )
        cell_style = ParagraphStyle(
            "CellText", parent=styles["Normal"],
            fontSize=8, leading=10,
        )
        header_style = ParagraphStyle(
            "HeaderText", parent=styles["Normal"],
            fontSize=8, leading=10, fontName="Helvetica-Bold",
        )

        col_widths = [55, 45, None, 50, 70]
        page_w = A4[0] - 50*mm
        fixed = 55 + 45 + 50 + 70
        col_widths[2] = page_w - fixed

        elements = []

        header_text = os.environ.get("CATALOG_HEADER_TEXT", "261 JULY 26")
        elements.append(Paragraph(f"<b>{header_text}</b>", styles["Normal"]))
        elements.append(Spacer(1, 10))

        first_section = True
        for sec_title, sec_rows in sections.items():
            if not first_section:
                elements.append(PageBreak())
            first_section = False

            elements.append(Paragraph(sec_title, section_style))

            # Column header row as its own table
            grey_bg = HexColor("#E8E8E8")
            header_data = [
                [Paragraph("<b>Lots</b>", header_style),
                 Paragraph("<b>Skins</b>", header_style),
                 Paragraph("<b>Description</b>", header_style),
                 Paragraph("<b>Price</b>", header_style),
                 Paragraph("<b>Comments</b>", header_style)]
            ]
            ht = Table(header_data, colWidths=col_widths)
            ht.setStyle(TableStyle([
                ("BACKGROUND", (0, 0), (-1, 0), grey_bg),
                ("TEXTCOLOR", (0, 0), (-1, 0), black),
                ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
                ("FONTSIZE", (0, 0), (-1, -1), 8),
                ("GRID", (0, 0), (-1, -1), 0.5, HexColor("#CCCCCC")),
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                ("LEFTPADDING", (0, 0), (-1, -1), 4),
                ("RIGHTPADDING", (0, 0), (-1, -1), 4),
                ("TOPPADDING", (0, 0), (-1, -1), 3),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
            ]))
            elements.append(ht)

            # Group rows by StringNumber within section
            string_groups = {}
            for r in sec_rows:
                sn = r.get("StringNumber", 0)
                string_groups.setdefault(sn, []).append(r)

            # Each string group as its own table (KeepTogether for multi-lot)
            light_grey = HexColor("#F5F5F5")
            for sn, string_rows in string_groups.items():
                is_multi = len(string_rows) > 1
                sg_data = []
                for idx, r in enumerate(string_rows):
                    seq = idx + 1
                    is_first = (seq == 1)
                    is_last = (seq == len(string_rows))
                    skins = f"{r['TotalSkins']:,}" if r.get("TotalSkins") else ""

                    if not is_multi:
                        desc = _build_description(r)
                    elif is_first:
                        desc = _build_description(r)
                    elif is_last:
                        string_total = r.get("StringTotalSkins", 0)
                        desc = f"{string_total:,} skins"
                    else:
                        desc = str(seq)

                    sg_data.append([
                        Paragraph(str(r.get("LotNumber", "")), cell_style),
                        Paragraph(skins, cell_style),
                        Paragraph(desc, cell_style),
                        Paragraph("", cell_style),
                        Paragraph("", cell_style),
                    ])

                st = Table(sg_data, colWidths=col_widths)
                style_cmds = [
                    ("FONTSIZE", (0, 0), (-1, -1), 8),
                    ("GRID", (0, 0), (-1, -1), 0.5, HexColor("#CCCCCC")),
                    ("ROWBACKGROUNDS", (0, 0), (-1, -1), [white, light_grey]),
                    ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                    ("LEFTPADDING", (0, 0), (-1, -1), 4),
                    ("RIGHTPADDING", (0, 0), (-1, -1), 4),
                    ("TOPPADDING", (0, 0), (-1, -1), 3),
                    ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
                ]

                if is_multi:
                    last_row = len(sg_data) - 1
                    # BOX draws all 4 borders around the range in one command
                    style_cmds.append(("BOX", (0, 0), (3, last_row), 1.25, black))

                st.setStyle(TableStyle(style_cmds))

                if is_multi:
                    elements.append(KeepTogether([st]))
                    elements.append(Spacer(1, 1))
                else:
                    elements.append(st)

        doc.build(elements)

        pdf_bytes = buf.getvalue()
        buf.close()

        return Response(
            content=pdf_bytes,
            media_type="application/pdf",
            headers={
                "Content-Disposition": "attachment; filename=lot-catalog.pdf",
            },
        )
    except Exception as e:
        return Response(content=str(e), status_code=500)


STATIC_DIR = Path(__file__).parent / "static"

if STATIC_DIR.exists():
    @app.get("/{full_path:path}")
    async def serve_spa(full_path: str):
        file_path = STATIC_DIR / full_path
        if file_path.is_file():
            media_type = None
            if full_path.endswith(".html"):
                media_type = "text/html"
            elif full_path.endswith(".css"):
                media_type = "text/css"
            elif full_path.endswith(".js"):
                media_type = "application/javascript"
            elif full_path.endswith(".wasm"):
                media_type = "application/wasm"
            elif full_path.endswith(".json"):
                media_type = "application/json"
            elif full_path.endswith(".dll"):
                media_type = "application/octet-stream"
            elif full_path.endswith(".dat"):
                media_type = "application/octet-stream"
            elif full_path.endswith(".blat"):
                media_type = "application/octet-stream"
            elif full_path.endswith(".png"):
                media_type = "image/png"
            elif full_path.endswith(".ico"):
                media_type = "image/x-icon"
            return FileResponse(file_path, media_type=media_type)
        return FileResponse(STATIC_DIR / "index.html", media_type="text/html")
