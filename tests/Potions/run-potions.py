import argparse,json,time,urllib.request,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--port',type=int,required=True);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--case',choices=['all','knowledge','capacity','dynamic','sports','arrows'],default='all');p.add_argument('--record',action='store_true');a=p.parse_args()
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
    rows.append(dict(name=name,actual=sorted(actual) if isinstance(actual,set) else actual,expected=sorted(expected) if isinstance(expected,set) else expected,pass_=actual==expected))
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

from collections import Counter
def save(name,value): (root/name).write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
def capture(name):
    with urllib.request.urlopen(base+'/screenshot',timeout=20) as response:(root/(name+'.png')).write_bytes(response.read())
def finish_recording(name):
    req('/recording/stop',{})
    for _ in range(180):
        r=req('/recording')
        if r['status'] in ['completed','failed']:break
        time.sleep(.2)
    save(name+'-recording.json',r)
    assert r['status']=='completed',r
    with urllib.request.urlopen(base+'/recording/frames?frame_count=8&columns=4&frame_width=480',timeout=30) as response:
        (root/(name+'-frames.png')).write_bytes(response.read())
for mode in (['knowledge','capacity','dynamic','sports','arrows'] if a.case=='all' else [a.case]):
    case_id=uuid.uuid4().hex
    cmd('junna_potions prepare '+mode+' '+case_id)
    s,v=wait(lambda s,v:v and v.get('caseId')==case_id and len(v['potions'])==3 and all(v['potions']),timeout=60)
    before=v;before_energy=v['energy'];save(mode+'-before.json',v)
    check(mode+' registered class pool',sorted(p['id'] for p in v['pool']),sorted(['JUNNA_KNOWLEDGE_POTION','JUNNA_SPORTS_DRINK','JUNNA_ARROW_RAIN_POTION']))
    check(mode+' class potion rarities',sorted(p['rarity'] for p in v['pool']),['Common','Rare','Uncommon'])
    if a.record:
        req('/recording/start',dict(output_dir=str(root/mode),fps=15,width=1280,height=720,audio=True,max_duration_seconds=120));time.sleep(2)
    try:
        slot={'knowledge':0,'capacity':0,'dynamic':0,'sports':1,'arrows':2}[mode]
        act('use_potion',slot=slot)
        if mode in ['knowledge','capacity','dynamic']:
            s,v=wait(lambda s,v:s['state_type']=='card_select' and v and len(v['bundles'])==3 and v['existingKnowledgeStacks']>=sum(len(b)>1 for b in v['bundles']))
            save(mode+'-offered.json',{'state':s,'live':v});capture(mode+'-offered')
            check(mode+' offer count',len(v['bundles']),3)
            table={c['instance']:c for c in v['cards']}
            signatures=[tuple(sorted(table[i]['id'] for i in bundle)) for bundle in v['bundles']]
            check(mode+' distinct groups',len(set(signatures)),3)
            check(mode+' reuses existing knowledge stacks',v['existingKnowledgeStacks']>=sum(len(b)>1 for b in v['bundles']),True)
            check(mode+' no native bundle screen',v['nativeBundleNodes'],0)
            if mode=='dynamic':
                check('dynamic current relation groups',set(signatures),set(tuple(sorted(family)) for family in v['expectedFamilies']))
            # Select a group with positive printed costs to exercise expiry and a free play.
            index=max(range(3),key=lambda j:sum(max(0,table[i]['printedCost']) for i in v['bundles'][j]))
            preview=v['bundles'][index];clicked_root=v['rootOptions'][index];unchosen=set(v['rootOptions'])-{clicked_root}
            expected=Counter(table[i]['id'] for i in preview)
            act('select_card',index=index)
            s,v=wait(lambda s,v:v and v['used'] and v['selected'] and all(c['removed'] for c in v['cards'] if c['instance'] in unchosen))
            chosen=v['selected']
            save(mode+'-after.json',v);table={c['instance']:c for c in v['cards']}
            check(mode+' one click closes choice',s['state_type'] not in ['bundle_select','card_select'],True)
            check(mode+' deck unchanged',v['deckCount'],before['deckCount'])
            check(mode+' no flight into deck','Deck' in v['flights'],False)
            check(mode+' selected group identity',v['selectedRoot'],clicked_root)
            check(mode+' selected quantity and IDs',dict(Counter(table[i]['id'] for i in v['selected'])),dict(expected))
            check(mode+' all selected free',[table[i]['cost'] for i in chosen],[0]*len(chosen))
            check(mode+' unselected removed',all(table[i]['removed'] for i in unchosen),True)
            expected_pile='Discard' if mode=='capacity' else 'Hand'
            check(mode+' generated destination',[table[i]['pile'] for i in chosen],[expected_pile]*len(chosen))
            check(mode+' original hand preserved',all(c['pile']=='Hand' for c in v['cards'] if c['role']=='original'),True)
            check(mode+' potion spends no energy',v['energy'],before_energy)
            time.sleep(1);capture(mode+'-received')
            if mode=='knowledge':
                target=s['battle']['enemies'][0].get('combat_id') or s['battle']['enemies'][0].get('entity_id')
                playable=[c for c in s['player']['hand'] if c['id'] in expected]
                # Prefer the positive-cost member with no enemy targeting and no extra choice.
                defs=json.loads(Path('data/card-pool.json').read_text(encoding='utf-8-sig'))['cards'];by_model={d['modelId']:d for d in defs}
                candidate=next((i for i,c in enumerate(s['player']['hand']) if c['id'] in expected and by_model[c['id']]['target']=='Self' and by_model[c['id']]['type']!='能力' and not any(word in by_model[c['id']]['text'] for word in ['免费','耗能','费用','选择','打出','抽','能量'])),None)
                if candidate is not None:
                    act('play_card',card_index=candidate)
                    s,v=wait(lambda s,v:v and v['played'] and s.get('battle',{}).get('is_play_phase'))
                    check('knowledge actual play costs zero',v['energy'],before_energy)
                    time.sleep(.8)
                oldturn=v['turn'];act('end_turn')
                s,v=wait(lambda s,v:v and v['turn']==oldturn+1 and s.get('battle',{}).get('is_play_phase'))
                table={c['instance']:c for c in v['cards']}
                check('knowledge free modifier expires',all(table[i]['cost']==max(0,table[i]['printedCost']) for i in chosen),True)
                save(mode+'-next-turn.json',v);capture(mode+'-next-turn');time.sleep(.8)
        elif mode=='sports':
            s,v=wait(lambda s,v:s['state_type']=='hand_select')
            offered=s['hand_select']['cards']
            by_role={c['role']:c for c in before['cards'] if c['role']!='filler'}
            hand_roles=['handAttack','handArrow','handSkill','handStatus']
            check('developer only offers hand cards',Counter(c['id'] for c in offered),
                  Counter(by_role[role]['id'] for role in hand_roles))
            check('developer localized name',next(p['title'] for p in v['pool'] if p['id']=='JUNNA_SPORTS_DRINK'),'显影液')
            capture(mode+'-offered')
            picked=['handAttack','handStatus']
            for role in picked:
                s=state()
                index=next(c['index'] for c in s['hand_select']['cards'] if c['id']==by_role[role]['id'])
                act('combat_select_card',card_index=index)
            act('combat_confirm_selection')
            s,v=wait(lambda s,v:v and 'JUNNA_SPORTS_DRINK' in v['used'])
            cards={c['role']:c for c in v['cards'] if c['role']!='filler'}
            check('developer deploys the selected instances',Counter(v['played']),Counter(by_role[role]['instance'] for role in picked))
            check('developer starts both arrows at three turns',[cards[role]['deployed']['Remaining'] for role in picked],[3,3])
            check('developer leaves the other hand cards', [cards[role]['pile'] for role in ['handArrow','handSkill']],['Hand','Hand'])
            check('developer leaves the existing arrow countdown',cards['deployedArrow']['deployed']['Remaining'],by_role['deployedArrow']['deployed']['Remaining'])
            check('developer spends no energy',v['energy'],before_energy)
            save(mode+'-after.json',v);capture(mode+'-after')
            time.sleep(1)
            for step in range(1,4):
                oldturn=v['turn'];act('end_turn')
                def advanced(s,v):
                    if not v or v['turn']!=oldturn+1 or not s.get('battle',{}).get('is_play_phase'):
                        return False
                    remaining=[(c['deployed'] or {}).get('Remaining') for c in v['cards'] if c['role'] in picked]
                    # Arrow time advances in AfterPlayerTurnStartLate, after the turn number changes.
                    return remaining==[None]*2 if step==3 else len(remaining)==2 and all(n!=4-step for n in remaining)
                s,v=wait(advanced)
                cards={c['role']:c for c in v['cards'] if c['role']!='filler'}
                if step<3:
                    check('developer countdown '+str(step),[(cards[role]['deployed'] or {}).get('Remaining') for role in picked],[3-step]*2)
                else:
                    check('developer restores selected cards after resolution',
                          [(cards[role]['deployed'],cards[role]['arrow'],cards[role]['pile']) for role in picked],
                          [(None,False,'Discard')]*2)
                save(mode+'-turn-'+str(step)+'.json',v)
                time.sleep(.5)
            capture(mode+'-resolved')
        else:
            expected_arrows=[c['instance'] for c in before['cards'] if c['arrow'] and c['role']!='filler']
            s,v=wait(lambda s,v:v and 'JUNNA_ARROW_RAIN_POTION' in v['used'])
            check('arrows plays each pile and deployed once',Counter(v['played']),Counter(expected_arrows))
            check('arrows no energy spent',v['energy'],before_energy)
            check('arrows all deployed',all(c['deployed'] and c['deployed']['Remaining']==2 for c in v['cards'] if c['instance'] in expected_arrows),True)
            check('arrows leaves non-arrow untouched',next(c for c in v['cards'] if c['role']=='nonArrow')['pile'],'Hand')
            old_seq=next(c['deployed']['Sequence'] for c in before['cards'] if c['role']=='deployedArrow')
            new_seq=next(c['deployed']['Sequence'] for c in v['cards'] if c['role']=='deployedArrow')
            check('arrows replays deployed card',new_seq>old_seq,True)
            save(mode+'-after.json',v);capture(mode+'-after');time.sleep(1.5)
        check(mode+' potion consumed',len(v['used']),1)
        print(json.dumps({'case':mode,'passed':True,'checks':len(rows)},ensure_ascii=False),flush=True)
    except Exception as error:
        save(mode+'-failure.json',{'error':str(error),'state':state(),'live':live()})
        raise
    finally:
        if a.record:finish_recording(mode)
        save(mode+'-results.json',rows)
print(json.dumps({'passed':all(r['pass_'] for r in rows),'checks':len(rows)},ensure_ascii=False),flush=True)
