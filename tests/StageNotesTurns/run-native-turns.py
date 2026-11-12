import argparse,json,time,urllib.request,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--port',type=int,required=True);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--case',default='normal');p.add_argument('--upgrade',action='store_true');p.add_argument('--record',action='store_true');a=p.parse_args()
root=a.output_dir.resolve();root.mkdir(parents=True,exist_ok=True);base=f'http://127.0.0.1:{a.port}/api/v1';rows=[];segments=[]
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
        if s.get('player') and s['state_type'] in ['map','combat','event']:break
    else:raise RuntimeError('Run did not initialize')
    cmd('unlock all')
s=state()
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
case_id=uuid.uuid4().hex
cmd('junna_real_cycle prepare '+a.case+(' upgrade' if a.upgrade else '')+' case='+case_id)
s,v=wait(lambda s,v:v and v.get('caseId')==case_id and v['mode']==a.case and v['upgraded']==a.upgrade and not v['picks'] and len(v['hand'])==(10 if a.case=='capacity' else 1),timeout=60)
base_turn=v['baseTurn'];name=a.case+('-upgrade' if a.upgrade else '-base')
if a.record:
    req('/recording/start',dict(output_dir=str(root/name),fps=15,width=1280,height=720,audio=True,max_duration_seconds=180));started=time.monotonic();time.sleep(.4)
try:
    if a.case!='capacity':
        index=next(i for i,c in enumerate(s['player']['hand']) if c['id']=='JUNNA_U_CARD_010')
        before=s['player']['energy']
        if a.record:
            act('hover_card',card_index=index);time.sleep(1.2);act('unhover_card')
        act('play_card',card_index=index)
        s,v=wait(lambda s,v:v and v['powerCount']==1 and not any(c['role']=='power' for c in v['hand']))
        check('installation does not retrieve immediately',len(v['picks']),0)
        check('installation spends printed energy',before-s['player']['energy'],1 if a.upgrade else 2)
    count=6 if a.case in ['normal','missing'] else 4 if a.case=='staggered' else 3
    for offset in range(1,count+1):
        s=state();v=live();before_ids=[c['instance'] for c in v['draw']]
        before_draws=len(v['draws']);before_picks=len(v['picks']);t=time.monotonic()
        act('end_turn')
        expected=[['knowledge','arrow','shine'][(offset-1)%3]]
        if a.case=='staggered' and offset>=2:expected.append(['knowledge','arrow','shine'][(offset-2)%3])
        if a.case=='capacity' and offset==1:expected=[]
        seen=0;deadline=time.monotonic()+45
        while time.monotonic()<deadline:
            s=state();v=live()
            if s['state_type']=='card_select':
                assert seen<len(expected),('unexpected choice',offset,s)
                picker=s['card_select'];cards=picker.get('cards',[])
                if cards:
                    preferred=next((i for i,c in enumerate(cards) if c.get('is_upgraded')),0)
                    if a.record:time.sleep(1.2)
                    act('select_card',index=preferred)
                    if picker.get('can_confirm'):act('confirm_selection')
                    seen+=1;time.sleep(.25)
            elif v and v['turn']==base_turn+offset and s.get('battle',{}).get('is_play_phase'):
                picked=[p for p in v['picks'][before_picks:] if p['turn']==base_turn+offset]
                if len(picked)==len(expected) and all(p['resolved'] and all(c['pile']=='Hand' for c in p['selected']) for p in picked):
                    time.sleep(.3);v=live();break
            time.sleep(.15)
        else:
            (root/'timeout-state.json').write_text(json.dumps({'offset':offset,'expected':expected,'state':s,'live':v},ensure_ascii=False,indent=2),encoding='utf-8')
            raise TimeoutError(f'Turn {offset} did not reach expected selection; see timeout-state.json')
        phase_picks=v['picks'][before_picks:]
        check(f'turn {offset} native turn number',v['turn'],base_turn+offset)
        check(f'turn {offset} retrieval count',len(phase_picks),len(expected))
        selected=[]
        for j,(pick,role) in enumerate(zip(phase_picks,expected)):
            missing=a.case=='missing' and role=='knowledge'
            check(f'turn {offset} instance {j} candidate categories',sorted(set(c['role'] for c in pick['candidates'])),[] if missing else [role])
            check(f'turn {offset} instance {j} selected count',len(pick['selected']),0 if missing else 1)
            check(f'turn {offset} instance identity {j}',pick['instance'],j)
            for card in pick['selected']:
                selected.append(card['instance'])
                check(f'turn {offset} selected actual candidate',card['instance'] in [c['instance'] for c in pick['candidates']],True)
                check(f'turn {offset} retains upgrade',card['upgrade'],card['initialUpgrade'])
                check(f'turn {offset} retains shine',card['shine'],card['initialShine'])
        natural=v['draws'][before_draws:];drawn=[d['card']['instance'] for d in natural]
        check(f'turn {offset} retrieval fires no draw event',bool(set(selected)&set(drawn)),False)
        check(f'turn {offset} other draw order retained',[c['instance'] for c in v['draw']],[i for i in before_ids if i not in selected and i not in drawn])
        check(f'turn {offset} natural draws',[d['card']['role'] for d in natural],[] if a.case=='capacity' and offset==1 else ['secondPower','filler','filler','filler','filler'] if a.case=='staggered' and offset==1 else ['filler']*5)
        (root/(name+f'-turn-{offset}.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
        if a.record:
            for card in [c for p in phase_picks for c in p['selected']]:
                hand_index=next(i for i,c in enumerate(v['hand']) if c['instance']==card['instance'])
                act('hover_card',card_index=hand_index);time.sleep(1);act('unhover_card')
            segments.append({'turn':offset,'role':expected,'start':t-started,'end':time.monotonic()-started})
        print(json.dumps({'case':name,'turn':offset,'expected':expected,'selected':[p['selected'] for p in phase_picks]},ensure_ascii=False),flush=True)
        if a.case=='staggered' and offset==1:
            s=state();before_energy=s['player']['energy'];before_count=len(v['picks']);index=next(i for i,c in enumerate(s['player']['hand']) if c['id']=='JUNNA_U_CARD_010');act('play_card',card_index=index)
            s,v=wait(lambda s,v:v and not any(c['role']=='secondPower' for c in v['hand']))
            check('second installation spends upgraded energy',before_energy-s['player']['energy'],1)
            check('second installation does not retrieve immediately',len(v['picks']),before_count)
        if a.case=='capacity' and offset==1:
            for _ in range(6):
                s=state();hand_count=len(s['player']['hand']);index=next(i for i,c in enumerate(s['player']['hand']) if c['name']=='防御' and str(c['cost'])=='0');act('play_card',card_index=index)
                wait(lambda s,v:len(s['player']['hand'])==hand_count-1 and s.get('battle',{}).get('is_play_phase'));time.sleep(.15)
except Exception as error:
    rows.append(dict(name='native flow failure',pass_=False,error=str(error)))
    raise
finally:
    if a.record:
        req('/recording/stop',{})
        for _ in range(150):
            recording=req('/recording')
            if recording['status'] in ['completed','failed']:break
            time.sleep(.2)
        (root/(name+'-recording.json')).write_text(json.dumps(recording,indent=2),encoding='utf-8')
        (root/(name+'-segments.json')).write_text(json.dumps(segments,ensure_ascii=False,indent=2),encoding='utf-8')
    (root/(name+'-results.json')).write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'case':name,'assertions':len(rows),'failures':sum(not r['pass_'] for r in rows)},ensure_ascii=False),flush=True)
