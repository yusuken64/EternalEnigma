"""Generate a local, self-contained index for the retained Before/After evidence."""
from pathlib import Path
import csv, json
from html import escape

root = Path(__file__).resolve().parents[1] / 'Docs/Art/Previews/Diorama'
def stats(folder):
    with (root / folder / 'Stats.csv').open(encoding='utf-8-sig', newline='') as source:
        return {row['view']: row for row in csv.DictReader(source)}
before, after = stats('Before'), stats('After')
views = sorted(set(before) & set(after), key=lambda name: (not name.startswith('Town_'), not name.startswith('Overworld_'), name))
data = []
for name in views:
    modes = [mode for mode in ('Gameplay', 'Detail', 'Overview')
             if all((root / folder / f'{name}_{mode}.png').exists() for folder in ('Before', 'After'))]
    if modes:
        data.append(dict(name=name, modes=modes, before=before[name], after=after[name]))
html = '''<!doctype html>
<html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Diorama restyle review</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#151d19;color:#e9efe9;font:16px system-ui,sans-serif}
header,section{padding:20px 3vw}h1{margin:0 0 8px;font-size:26px}p{color:#bfcfc3;max-width:85ch;line-height:1.5}
a{color:#b4dfa6}nav{display:flex;gap:18px;flex-wrap:wrap}label{display:inline-flex;gap:8px;align-items:center;margin:0 18px 10px 0}
select{padding:8px;border:1px solid #6c8a71;border-radius:5px;background:#26362c;color:inherit;font:inherit;max-width:85vw}
.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0;background:#26362c;border-radius:6px;overflow:hidden}
figcaption{padding:10px 14px;font-weight:600}img{display:block;width:100%;height:auto}table{border-collapse:collapse;margin-top:20px;min-width:360px}
th,td{text-align:right;padding:8px 18px;border-bottom:1px solid #3c4c40}th:first-child,td:first-child{text-align:left}
.note{font-size:14px}footer{padding:20px 3vw;color:#bfcfc3}@media(max-width:850px){.pair{grid-template-columns:1fr}}
</style>
<header><h1>Diorama restyle review</h1><p>Seed 12345. Matching production camera views across all eight biomes, towns and overworld routes. Open either image for its full resolution.</p>
<nav><a href="Items/Review.html">Items and icons</a><a href="Fit/Landmarks.png">Landmark fit</a><a href="After/Overworld_Road_HeroTree.png">Hero and route trees</a><a href="UI/npc-portrait.png">NPC portrait</a><a href="UI/victory.png">Victory</a><a href="UI/defeat.png">Defeat</a><a href="Verification/Determinism.txt">Determinism</a></nav></header>
<section><label>View <select id="view"></select></label><label>Camera <select id="mode"></select></label>
<div class="pair"><figure><figcaption>Before</figcaption><a id="beforeLink"><img id="before" alt="Before restyle"></a></figure><figure><figcaption>After</figcaption><a id="afterLink"><img id="after" alt="After restyle"></a></figure></div>
<table><thead><tr><th>Recorded scene statistic</th><th>Before</th><th>After</th></tr></thead><tbody id="stats"></tbody></table>
<p class="note">These are recorded scene totals. Triangle and material counts include geometry outside the camera; they are not frame-time measurements. Runtime performance and build inclusion reports are retained separately under Verification/.</p></section>
__VALIDATION__
<footer>Evidence and resume status: TODOs/09-diorama-restyle-plan.md. Source and style decisions: Docs/Art/DioramaStyle.md.</footer>
<script>
const data=__DATA__, view=document.querySelector('#view'), mode=document.querySelector('#mode');
data.forEach((entry,index)=>view.add(new Option(entry.name.replaceAll('_',' '),index)));
function render(){const entry=data[Number(view.value)];for(const side of ['before','after']){const file=side[0].toUpperCase()+side.slice(1)+'/'+entry.name+'_'+mode.value+'.png';document.querySelector('#'+side).src=file;document.querySelector('#'+side+'Link').href=file;}
const rows=[['Renderers','renderers'],['Materials','materials'],['Triangles','triangles'],['Texture MiB','textureBytes'],['Batches','batches'],['Set-pass calls','setPassCalls']];
document.querySelector('#stats').replaceChildren(...rows.map(([title,key])=>{const tr=document.createElement('tr');for(const text of [title,...['before','after'].map(side=>key==='textureBytes'?(Number(entry[side][key])/1048576).toFixed(1):Number(entry[side][key]).toLocaleString())]){const td=document.createElement('td');td.textContent=text;tr.append(td);}return tr;}));}
view.onchange=()=>{const old=mode.value;mode.replaceChildren(...data[Number(view.value)].modes.map(value=>new Option(value,value)));if(data[Number(view.value)].modes.includes(old))mode.value=old;render();};mode.onchange=render;view.onchange();
</script></html>'''.replace('__DATA__', json.dumps(data))
validation = ['<section><h2>Final validation</h2>']
for mode in ('EditMode', 'PlayMode'):
    path = root / 'Verification' / f'PostCleanup{mode}.json'
    if path.exists():
        report = json.loads(path.read_text(encoding='utf-8-sig'))
        validation.append(f'<p><a href="Verification/{path.name}">{mode}</a>: '
                          f'{report["passed"]} passed, {report["failed"]} failed, {report["skipped"]} skipped.</p>')
for platform in ('Windows', 'WebGL'):
    path = root / 'Verification' / f'{platform}PlayerValidation.json'
    if not path.exists():
        continue
    report = json.loads(path.read_text(encoding='utf-8-sig'))
    validation.append(f'<h3>{platform}</h3><p>{escape(report["gpu"])} / {escape(report["api"])}</p>'
                      f'<p>{escape(report["protocol"])}</p><p><a href="Verification/{path.name}">Raw measurements</a> · '
                      f'<a href="Verification/{platform}ArtInclusion.csv">Packed art inclusion</a></p>')
    validation.append('<table><tr><th>Scene</th><th>Median ms</th><th>p95 ms</th><th>Error materials</th></tr>')
    for sample in report['samples']:
        scene = escape(sample['scene'])
        validation.append(f'<tr><td>{scene}</td><td>{sample["medianMs"]:.2f}</td>'
                          f'<td>{sample["p95Ms"]:.2f}</td><td>{sample["errorMaterials"]}</td></tr>')
    validation.append('</table><div class="pair">')
    for sample in report['samples']:
        scene = escape(sample['scene'])
        screenshot = f'Verification/{platform}_{scene}.png'
        if (root / screenshot).exists():
            validation.append(f'<figure><figcaption>{platform} — {scene}</figcaption><a href="{screenshot}">'
                              f'<img src="{screenshot}" loading="lazy" alt="{platform} {scene} player capture"></a></figure>')
    validation.append('</div>')
validation.append('</section>')
html = html.replace('__VALIDATION__', '\n'.join(validation))
(root / 'Review.html').write_text(html, encoding='utf-8')
print(f'Review index: {len(data)} matched views, {sum(len(entry["modes"]) for entry in data)} image pairs.')
