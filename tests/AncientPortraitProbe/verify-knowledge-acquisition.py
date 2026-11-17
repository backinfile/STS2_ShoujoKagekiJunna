"""Run one real knowledge reward selection and verify its grouped acquisition animation."""
import argparse
import json
import time
import urllib.request
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('--port',type=int,required=True)
parser.add_argument('--case',choices=['native','chain','repeated'],default='native')
parser.add_argument('--label',default='after')
parser.add_argument('--record',action='store_true')
args=parser.parse_args()
root=Path(__file__).resolve().parents[2]
out=root/'out/tmp/knowledge-presentation'
out.mkdir(parents=True,exist_ok=True)
base=f'http://127.0.0.1:{args.port}/api/v1'
def request(path,data=None):
    req=urllib.request.Request(base+path,None if data is None else json.dumps(data).encode(),{'Content-Type':'application/json'})
    with urllib.request.urlopen(req,timeout=15) as response:return json.load(response)
def action(name,**kwargs):
    result=request('/singleplayer',{'action':name,**kwargs})
    assert result.get('status')=='ok',result
    return result
def state():return request('/singleplayer?format=json')
def wait(predicate,timeout=20):
    end=time.monotonic()+timeout
    while time.monotonic()<end:
        s=state()
        if predicate(s):return s
        time.sleep(.15)
    raise TimeoutError(s)
s=state()
for _ in range(30):
    if s.get("menu_screen")!="tutorial":break
    action("menu_select",option="advance")
    time.sleep(.2)
    s=state()
else:raise TimeoutError("Tutorial remained active")
if s.get('menu_screen')=='main':
    action('menu_select',option='continue' if 'continue' in s.get('options',[]) else 'singleplayer')
    s=wait(lambda s:s.get('menu_screen')=='character_select' or 'player' in s)
if s.get('menu_screen')=='character_select':
    action('menu_select',option='JUNNA')
    action('menu_select',option='embark')
    s=wait(lambda s:s.get('state_type') in ('map','event','combat'))
    action('run_command',command='unlock all')
if s.get('state_type')=='map' and not s['map']['visited']:
    action('choose_map_node',index=0)
    wait(lambda s:s.get('battle',{}).get('is_play_phase'))
action('run_command',command=f'junna_knowledge_acquisition_animation prepare {args.case}')
s=wait(lambda s:s.get('state_type') not in ('map','combat','unknown'))
print('reward state:',s['state_type'],flush=True)
if args.record:
    recording=request('/recording/start',{'output_dir':str(out.resolve()),'audio':True,'fps':30,'width':1280,'height':720,'max_duration_seconds':30})
    assert recording.get('audio_source')=='engine_mix',recording
    time.sleep(1)
action('select_card_reward',card_index=0)
time.sleep(.3)
s=state()
for _ in range(30):
    if s.get("menu_screen")!="tutorial":break
    action("menu_select",option="advance")
    time.sleep(.2)
    s=state()
else:raise TimeoutError("Tutorial remained active")
if s.get('state_type') in ('card_select','bundle_select'):
    action('confirm_card_selection' if s['state_type']=='card_select' else 'confirm_bundle_selection')
time.sleep(3)
if args.record:
    request('/recording/stop',{})
    deadline=time.monotonic()+20
    while time.monotonic()<deadline:
        status=request('/recording')
        if status['status'] in ('completed','failed'):break
        time.sleep(.2)
    assert status['status']=='completed',status
    (out/f'{args.label}-{args.case}-recording.json').write_text(json.dumps(status,indent=2),encoding='utf-8')
result=out/'acquisition-results.json'
old=result.stat().st_mtime_ns if result.exists() else 0
action('run_command',command='junna_knowledge_acquisition_animation check')
end=time.monotonic()+10
while time.monotonic()<end:
    if result.exists() and result.stat().st_mtime_ns!=old:break
    time.sleep(.1)
rows=json.loads(result.read_text(encoding='utf-8-sig'))
row=rows[-1]
(out/f'{args.label}-{args.case}.json').write_text(json.dumps(row,indent=2),encoding='utf-8')
print(json.dumps(row,ensure_ascii=False,indent=2))
raise SystemExit(0 if row['pass'] else 1)
