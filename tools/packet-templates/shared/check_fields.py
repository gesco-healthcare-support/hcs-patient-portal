from collections import Counter
from pypdf import PdfReader

r = PdfReader("/work/page1.pdf")
root = r.trailer["/Root"]
print("pages:", len(r.pages))
print("AcroForm present:", "/AcroForm" in root)
fields = r.get_fields() or {}
print("field count:", len(fields))
print("by type:", dict(Counter(str(v.get("/FT")) for v in fields.values())))

print("--- widget rectangles (points; 72pt = 1in) ---")
shown = 0
for page in r.pages:
    for a in (page.get("/Annots") or []):
        o = a.get_object()
        nm = o.get("/T")
        rect = o.get("/Rect")
        ft = o.get("/FT")
        if rect is not None and nm is not None and shown < 12:
            x0, y0, x1, y1 = [float(v) for v in rect]
            print(f"  {str(ft):5} {nm}: w={x1 - x0:6.1f}  h={y1 - y0:5.1f}")
            shown += 1
