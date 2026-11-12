import argparse,json,time,urllib.request
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--port',type=int,required=True);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--case',default='normal');p.add_argument('--upgrade',action='store_true');p.add_argument('--record',action='store_true');p.add_argument('--baseline',action='store_true');a=p.parse_args()
root=a.output_dir.resolve();root.mkdir(parents=True,exist_ok=True);base=f'http://127.0.0.1:{a.port}/api/v1';rows=[]
def req(path,body=None):
    r=urllib.request.Request(base+path,data=None if body is None else json.dumps(body,ensure_ascii=False).encode(),headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(r,timeout=20) as response:return json.load(response)
def act(action,**kw):
    r=req('/singleplayer',dict(action=action,**kw))
    if r.get('status')=='error' or r.get('error'):raise RuntimeError(r)
    return r
def cmd(text):return act('run_command',command=text)
def state():return req('/singleplayer?format=json')
def live():
    try:return json.loads((root/'live.json').read_text(encoding='utf-8-sig'))
    except (FileNotFoundError,json.JSONDecodeError):return None
def check(name,actual,expected):
    rows.append(dict(name=name,actual=actual,expected=expected,pass_=actual==expected))
    if actual!=expected:raise AssertionError(rows[-1])
def wait(predicate,timeout=45):
    deadline=time.monotonic()+timeout
    while time.monotonic()<deadline:
        s=state();v=live()
        if predicate(s,v):return s,v
        time.sleep(.15)
    (root/'timeout-state.json').write_text(json.dumps({'state':s,'live':v},ensure_ascii=False,indent=2),encoding='utf-8')
    raise TimeoutError('See timeout-state.json for the actual state')
def dismiss():
    for _ in range(20):
        q=req('/ui/prompt')
        if not q.get('active'):return
        option={'tutorial_prompt':'no','tutorial':'advance','early_access_disclaimer':'proceed'}.get(q.get('menu_screen'))
        if not option:raise RuntimeError(q)
        req('/ui/prompt',{'option':option,'prompt_id':q['prompt_id']});time.sleep(.2)
settings=req('/settings');assert settings['silent_test'] and settings['minimized_startup_guard'] and not settings.get('silent_test_error'),settings
dismiss();s=state()
if s['state_type']=='menu':
    if 'continue' in s.get('options',[]):act('menu_select',option='continue')
    else:
        act('menu_select',option='singleplayer');time.sleep(.3);dismiss();act('menu_select',option='JUNNA');dismiss();act('menu_select',option='confirm')
    for _ in range(150):
        time.sleep(.15);dismiss();s=state()
        if s.get('player') and s['state_type']!='menu':break
    else:raise RuntimeError('Run did not initialize')
    cmd('unlock all')
s,_=wait(lambda s,v:s.get('state_type')=='map' or s.get('battle',{}).get('is_play_phase'))
if s['state_type']=='map':
    s,_=wait(lambda s,v:bool(s.get('map',{}).get('next_options')))
    first=s['map']['next_options'][0];last_choice=0;deadline=time.monotonic()+45
    while time.monotonic()<deadline:
        s=state()
        if s.get('battle',{}).get('is_play_phase'):break
        options=s.get('map',{}).get('next_options',[])
        same=next((n for n in options if n['col']==first['col'] and n['row']==first['row']),None)
        if same and not s.get('map',{}).get('is_traveling') and time.monotonic()-last_choice>=2:
            act('choose_map_node',index=same['index']);last_choice=time.monotonic()
        time.sleep(.2)
wait(lambda s,v:s.get('battle',{}).get('is_play_phase'))
name=a.case+('-upgrade' if a.upgrade else '-base')+('-baseline' if a.baseline else '')
expected=2 if a.case=='stack' else 1
cmd('junna_steady prepare'+(' stack' if expected==2 else '')+(' upgrade' if a.upgrade else ''))
s,v=wait(lambda s,v:v and len(v['events'])==1 and len(s.get('player',{}).get('hand',[]))==expected)
base_turn=v['turn']
recording=None
try:
    if a.record:
        req('/recording/start',dict(output_dir=str(root/name),fps=15,width=1280,height=720,audio=True,max_duration_seconds=90))
        time.sleep(.4)
    for _ in range(expected):
        s=state()
        index=next(i for i,c in enumerate(s['player']['hand']) if c['id']=='JUNNA_JA144')
        act('play_card',card_index=index)
        wait(lambda s,v:len([c for c in s.get('player',{}).get('hand',[]) if c['id']=='JUNNA_JA144'])==expected-_-1)
    check('no generation on installation',len([e for e in live()['events'] if e['kind']=='generate']),0)
    for offset in range(1,3 if a.case=='normal' else 2):
        act('end_turn')
        s,v=wait(lambda s,v:v and v['turn']==base_turn+offset and s.get('battle',{}).get('is_play_phase') and len([e for e in v['events'] if e['turn']==base_turn+offset and e['kind']=='generate'])==expected)
        events=[e for e in v['events'] if e['turn']==base_turn+offset]
        kinds=[e['kind'] for e in events]
        wanted=['draw']*5+['generate']*expected if a.baseline else ['generate']*expected+['draw']*5
        check('native event order turn '+str(offset),kinds,wanted)
        check('generated cards already in hand on first draw turn '+str(offset),next(e for e in events if e['kind']=='draw')['generatedInHand'],(offset-1)*expected if a.baseline else offset*expected)
        check('generated cards retain base version turn '+str(offset),[e['upgrade'] for e in events if e['kind']=='generate'],[0]*expected)
        check('generated hand count turn '+str(offset),len([c for c in s['player']['hand'] if c['id']=='JUNNA_JA119']),offset*expected)
        (root/(name+'-turn-'+str(offset)+'.json')).write_text(json.dumps({'state':s,'observation':v},ensure_ascii=False,indent=2),encoding='utf-8')
        if a.record:time.sleep(.8)
finally:
    if a.record:
        req('/recording/stop',{})
        deadline=time.monotonic()+45
        while time.monotonic()<deadline:
            recording=req('/recording')
            if recording['status'] in ['completed','failed']:break
            time.sleep(.2)
        assert recording['status']=='completed',recording
        (root/(name+'-recording.json')).write_text(json.dumps(recording,ensure_ascii=False,indent=2),encoding='utf-8')
    (root/(name+'-result.json')).write_text(json.dumps({'checks':rows,'recording':recording},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'case':name,'passed':len(rows),'video':recording and recording.get('output_path')},ensure_ascii=False))