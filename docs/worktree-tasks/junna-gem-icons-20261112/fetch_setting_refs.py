"""Index public setting-book preview thumbnails, retaining source page numbers."""
import concurrent.futures, json, re, urllib.request
from pathlib import Path
from PIL import Image, ImageDraw

BASE=Path(__file__).resolve().parent/'research'
BASE.mkdir(exist_ok=True)
for book in (326,327):
    url=f'https://artsee.cc/collection/{book}'
    html=urllib.request.urlopen(url).read().decode()
    data=json.loads(re.search(r'<script[^>]*id="__NUXT_DATA__"[^>]*>(.*?)</script>',html).group(1))
    pages=[]
    for obj in data:
        if isinstance(obj,dict) and 'image_url' in obj and 'order' in obj:
            pages.append({k:data[obj[k]] for k in ('image_url','thumbnail_url','order')})
    pages.sort(key=lambda p:p['order'])
    folder=BASE/str(book);folder.mkdir(exist_ok=True)
    (folder/'index.json').write_text(json.dumps({'source':url,'pages':pages},indent=2),encoding='utf-8')
    def fetch(p):
        file=folder/f"{p['order']+1:03}.webp"
        if not file.exists():
            try:file.write_bytes(urllib.request.urlopen(p['thumbnail_url'] or p['image_url'],timeout=25).read())
            except Exception as error:
                print(f"Missing preview {book}/{p['order']+1}: {error}",flush=True)
                Image.new('RGB',(142,175),'gray').save(file)
        return file
    with concurrent.futures.ThreadPoolExecutor(max_workers=10) as pool:list(pool.map(fetch,pages))
    for start in range(0,len(pages),60):
        sheet=Image.new('RGB',(1500,1200),'#dddddd');draw=ImageDraw.Draw(sheet)
        for n,p in enumerate(pages[start:start+60]):
            im=Image.open(folder/f"{p['order']+1:03}.webp").convert('RGB');im.thumbnail((142,175))
            x=(n%10)*150;y=(n//10)*200
            sheet.paste(im,(x+(150-im.width)//2,y+20))
            draw.text((x+5,y+3),str(p['order']+1),fill='black')
        sheet.save(folder/f'contact-{start+1:03}.jpg')
    print(json.dumps({'book':book,'pages':len(pages),'contactSheets':len(list(folder.glob('contact*')))}),flush=True)
