import argparse,json,time,urllib.request,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--port',required=True,type=int);p.add_argument('--output-dir',required=True,type=Path);p.add_argument('--record',action='store_true');p.add_argument('--cases',default='base,upgrade,double,none');p.add_argument('--live-dir',type=Path);a=p.parse_args();root=a.output_dir.resolve();root.mkdir(exist_ok=True,parents=True);base=f'http://127.0.0.1:{a.port}/api/v1';checks=[]
def req(path,body=None):
    r=urllib.request.Request(base+path,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(r,timeout=20) as response:result=json.load(response)
    if result.get('status')=='error' or result.get('error'):raise RuntimeError(result)
    return result
def act(action,**kw):return req('/singleplayer',dict(action=action,**kw))
def state():return req('/singleplayer?format=json')
def live():
    try:return json.loads(((a.live_dir or root)/'live.json').read_text(encoding='utf-8-sig'))
    except (FileNotFoundError,json.JSONDecodeError):return None
def wait(predicate,timeout=50):
    deadline=time.monotonic()+timeout
    while time.monotonic()<deadline:
        s=state();v=live()
        if v and v['expected']==6 and v['previews']==6 and v['addingFlies']==0 and not (root/'six-preview.png').exists():
            with urllib.request.urlopen(base+'/screenshot',timeout=10) as shot:(root/'six-preview.png').write_bytes(shot.read())
        if predicate(s,v):return s,v
        time.sleep(.15)
    (root/'timeout.json').write_text(json.dumps({'state':s,'live':v},ensure_ascii=False,indent=2),encoding='utf-8');raise TimeoutError('See timeout.json')
def check(name,actual,expected):
    checks.append(dict(name=name,actual=actual,expected=expected,passed=actual==expected))
    if actual!=expected:raise AssertionError(checks[-1])
s=state()
if s['state_type']=='menu':act('menu_select',option='continue');wait(lambda s,v:bool(s.get('player')) and s['state_type'] not in ['menu','loading'])
settings=req('/settings');assert settings['silent_test'] and settings['minimized_startup_guard']
for name,count,upgraded in [('base',1,False),('upgrade',1,True),('double',2,False),('six',6,False),('none',0,False)]:
    if name not in a.cases.split(','):continue
    if not state().get('battle',{}).get('is_play_phase'):
        if state()['state_type']=='rewards':act('proceed');wait(lambda s,v:s['state_type']=='map')
        act('run_command',command='fight CULTISTS_NORMAL');wait(lambda s,v:s.get('battle',{}).get('is_play_phase'))
    case_id=uuid.uuid4().hex;act('run_command',command=f'junna_story_deck prepare {count} '+('upgrade' if upgraded else 'base')+' '+case_id)
    s,v=wait(lambda s,v:v and v['caseId']==case_id and len(s.get('player',{}).get('hand',[]))==count+1)
    start=len(checks);check(name+' before victory no new deck card',len(v['added']),0)
    recording=a.record and name=='base'
    if recording:req('/recording/start',dict(output_dir=str(root/'video'),fps=15,width=1280,height=720,audio=True,max_duration_seconds=120));time.sleep(.4)
    try:
        for _ in range(count):
            s=state();n=len(s['player']['hand']);i=next(i for i,c in enumerate(s['player']['hand']) if c['id']=='JUNNA_U_CARD_008')
            if recording:act('hover_card',card_index=i);time.sleep(1.2);act('unhover_card')
            act('play_card',card_index=i);s,v=wait(lambda s,v:len(s['player']['hand'])==n-1 and s.get('battle',{}).get('is_play_phase'));time.sleep(.25)
        v=live();check(name+' installation does not add story',len(v['added']),0)
        s=state();i=next(i for i,c in enumerate(s['player']['hand']) if c['id']=='JUNNA_JK275');act('play_card',card_index=i)
        s,v=wait(lambda s,v:s['state_type']=='rewards' and v and v['caseId']==case_id and v['victorySettlements']==1)
        time.sleep(.6);s=state();v=live()
        check(name+' native victory callback once',v['victorySettlements'],1)
        check(name+' direct permanent deck increase',v['deckCount']-v['beforeDeckCount'],count)
        check(name+' exact story increase',v['storyCount']-v['beforeStoryCount'],count)
        check(name+' only expected story cards',[c['id'] for c in v['added']],['U_CARD_008B']*count)
        check(name+' actual destination',[c['pile'] for c in v['added']],['Deck']*count)
        check(name+' generated story remains base version',[c['upgrade'] for c in v['added']],[0]*count)
        check(name+' full independent shine budget',[(c['shine'],c['maxShine'],c['temporary']) for c in v['added']],[(3,3,False)]*count)
        check(name+' independent physical cards',v['distinctAdded'],count)
        check(name+' original deck preserved',v['originalDeckPreserved'],True)
        check(name+' creates no extra combat reward',v['extraRewards'],[])
        check(name+' actual ordinary card candidates observed',bool(v['rewardCards']),True)
        check(name+' no story card reward',[c['id'] for c in v['rewardCards'] if c['id']=='U_CARD_008B'],[])
        check(name+' reward list contains no story text','故事' in json.dumps(s.get('rewards'),ensure_ascii=False),False)
        check(name+' ordinary rewards remain available',bool(s.get('rewards')),True)
        check(name+' visible story preview count',v['previews'],count)
        check(name+' actual adding-to-deck fly count',v['addingFlies'],count)
        check(name+' fly completed before victory settlement',v['completedFliesAtVictory'],count)
        check(name+' native deck counter completion notifications',v['pileAddFinished'],count)
        (root/(name+'-snapshot.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8');(root/(name+'-rewards.json')).write_text(json.dumps(s,ensure_ascii=False,indent=2),encoding='utf-8')
        if recording:time.sleep(1.5)
        print(json.dumps({'case':name,'expectedStories':count,'added':v['added'],'extraRewards':v['extraRewards'],'assertions':len(checks)-start},ensure_ascii=False),flush=True)
    finally:
        if recording:
            req('/recording/stop',{})
            for _ in range(150):
                status=req('/recording')
                if status['status'] in ['completed','failed']:break
                time.sleep(.2)
            (root/'recording.json').write_text(json.dumps(status,indent=2),encoding='utf-8')
        (root/'results.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'assertions':len(checks),'failures':sum(not c['passed'] for c in checks)}),flush=True)
