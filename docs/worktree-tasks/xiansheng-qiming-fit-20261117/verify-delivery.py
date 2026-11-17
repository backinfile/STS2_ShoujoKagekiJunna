from pathlib import Path
from PIL import Image
import json,sys,hashlib,re,subprocess,unicodedata
repo=Path.cwd();base=repo/'docs/worktree-tasks/xiansheng-qiming-fit-20261117'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def norm(s):return ''.join(c for c in unicodedata.normalize('NFKC',s).lower() if not c.isspace() and unicodedata.category(c)[0] not in 'PS')
titles=read(Path(sys.argv[1])/'ShoujoKagekiAijoKaren/localization/zhs/cards.json')
titles={k:v for k,v in titles.items() if re.fullmatch(r'[^.]+\.title',k) and isinstance(v,str)}
v=read(base/'verification.json');v['workbenchRevision']=5336;v['stylesRead']=36
checks=read(base/'name-check-junna.json');checks['karenCardTitles']=len(titles)
for name,c in checks['candidates'].items():
 c['karenMatches']=[k for k,val in titles.items() if norm(val)==norm(name)]
 assert not c['karenMatches'],name
 if name!='开演':assert not c['junnaMatches'] and not c['fitPoolMatches'],name
assert checks['candidates']['开演']['junnaMatches']==['JA71']
assert checks['candidates']['开演']['fitPoolMatches']==['FIT_CARD_JA71']
v['nameCheck']=checks
v['cloudChanges']={'JA71':{'name':'开演','revision':5333,'unchangedBody':['获得12点格挡。所有飞矢加速到完成。','获得16点格挡。所有飞矢加速到完成。'],'art':'original-tv-ja71-kaien-window-lights-20261117','fitPoolSynchronized':True},'confirmedNameConvention':{'revision':5334,'records':['JA71开演已选定','初生之光谐音否定及旧图重复','JS33重新选名选图']}}
readback=read(base/'final-card-readback.json');v['cardReadback']={c['entity']['id']:{'name':c['entity']['name'],'version':c['version'],'contentMatchesFinalStateRevision':5336} for c in readback['cards']}
assert readback['cards'][0]['entity']['name']=='开演'
assert readback['fit']['entity']['name']=='开演'
assert readback['fit']['entity']['art']==readback['cards'][0]['entity']['art']
v['candidateStatus']={'JS33':'proposal; current card version and effect retained','U_CARD_025':'proposal; current card version and effect retained'}
v['visualReview'].update({'selectedCropDuplicateFound':False,'cardTypes':['skill','skill','skill'],'arrowMaskRequired':['U_CARD_025'],'arrowLayout':'docs/design/workbench/arrow-focus.js','arrowFocus':{'x':.37,'y':.5},'arrowReview':'Both eyes, glasses and smile identifiable; waving hand retained in full card art.'})
v['confirmedOldDuplicates']={'JS33':'JA100星辉：TV9水面星光同一画面','U_CARD_025':'JH24星光结界：TV9同一星空画面'}
images=['sources/xenodude-tv02-21.png','sources/JS33-tv11-planning.jpg','sources/JS33-tv11-book.png','sources/U_CARD_025-tv11-return.png','stage-lights-candidate.png','card-preview.png','JS33-planning-candidate.png','JS33-book-candidate.png','U_CARD_025-stage-candidate.png','JS33-preview.png','U_CARD_025-preview.png','followup-preview.png','U_CARD_025-arrow-preview.png']
v['images']={}
for name in images:
 p=base/name
 with Image.open(p) as im:im.load();size=list(im.size)
 v['images'][name]={'size':size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
tracked=subprocess.check_output(['git','ls-files','-z'],text=True).split('\0')
added=subprocess.check_output(['git','ls-files','--others','--exclude-standard','-z'],text=True).split('\0')
exts={'.md','.json','.txt','.toml','.yaml','.yml','.csv','.tsv','.html','.xml','.config'}
paths=[repo/p for p in sorted(set(tracked+added)) if p and (repo/p).is_file() and (repo/p).suffix.lower() in exts]
absolute=[];credentials=[]
# Do not persist the literal local-path patterns into the JSON report.
for p in paths:
 s=p.read_text(encoding='utf-8-sig',errors='replace')
 if re.search(r'(?i)(?<![A-Za-z])[a-z]:[\\/](?:users|github|godot|steamlibrary|program files)[\\/]|/Users/[^\s/]+/|/home/[^\s/]+/',s):absolute.append(p.relative_to(repo).as_posix())
 if re.search(r'-----BEGIN (?:RSA |OPENSSH |EC )?PRIVATE KEY-----|\bgh[pousr]_[A-Za-z0-9]{30,}|\bsk-[A-Za-z0-9_-]{30,}',s):credentials.append(p.relative_to(repo).as_posix())
v['documents']={'reviewed':len(paths),'absolutePathFindings':absolute,'credentialFindings':credentials}
v['synchronization']={'targetBranch':'main','targetRevision':subprocess.check_output(['git','rev-parse','main'],text=True).strip(),'originalCheckoutStatus':'main clean; original video deletion preserved in parallel main commit'}
v['runtimeTests']='No runtime code changed; source images, naming, cloud readback and relevant previews verified.'
(base/'verification.json').write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'karenTitles':len(titles),'imagesVerified':len(images),'documentsScanned':len(paths),'absolutePathFindings':absolute,'credentialFindings':credentials},ensure_ascii=False))
assert not absolute and not credentials
