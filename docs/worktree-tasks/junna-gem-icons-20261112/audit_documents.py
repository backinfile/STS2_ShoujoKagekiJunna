"""Review tracked and added documents without printing potentially sensitive values."""
from pathlib import Path
import json,re,subprocess

ROOT=Path.cwd()
TASK=ROOT/'docs/worktree-tasks/junna-gem-icons-20261112'
paths=set(subprocess.check_output(['git','ls-files','-co','--exclude-standard'],text=True).splitlines())
exts={'.md','.txt','.html','.json','.yaml','.yml','.toml','.csv'}
findings=[];secrets=[];count=0
for name in sorted(paths):
    p=ROOT/name
    if p.suffix.lower() not in exts:continue
    count+=1
    for n,line in enumerate(p.read_text(encoding='utf-8-sig').splitlines(),1):
        if re.search(r'(?<![A-Za-z])[A-Za-z]:[\\/](?!/)|/Users/|/home/[a-zA-Z]|/mnt/[a-zA-Z]/',line):
            findings.append({'path':name,'line':n})
        if re.search(r'sk-[A-Za-z0-9_-]{20,}|ghp_[A-Za-z0-9]{20,}|-----BEGIN (?:RSA |OPENSSH )?PRIVATE KEY',line):
            secrets.append({'path':name,'line':n})
report={'documentsReviewed':count,'absolutePathFindings':findings,'credentialFindings':secrets}
(TASK/'document-sanitization.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report))
assert not findings and not secrets
