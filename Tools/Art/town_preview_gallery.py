"""Build a local, dependency-free review gallery from Unity's captured frames."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
folder = root / 'Docs/Art/Previews/TownInteriors'
manifest = json.loads((root / 'ArtSource/TownInteriors/manifest.json').read_text())
actors = [a for a in manifest['assets'] if a['animated']]
parts = ['''<!doctype html><html lang="en"><meta charset="utf-8"><title>Bamao town interiors</title>
<style>body{background:#252b32;color:#fff2cc;font:16px system-ui;margin:32px auto;max-width:1400px;padding:0 24px}h1,h2{color:#f4cfa6}img{max-width:100%;border-radius:8px}section{margin:32px 0}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:20px}figure{margin:0;background:#34333b;padding:12px;border-radius:12px}figcaption{padding:8px 0}button{background:#267c81;color:white;border:0;padding:10px 20px;border-radius:6px;cursor:pointer}.swatch{display:inline-block;width:40px;height:40px}.anim{width:100%}a{color:#9cccea}</style>
<h1>Bamao town interiors</h1><p>Blender-authored characters, spherical birds and furniture. Unity captures use the shared gameplay daylight and biome materials. Character and bird colors stay fixed.</p>
<button id="play">Pause animation</button><section><h2>References and palette</h2><img src="BamaoReferenceBoard.png" alt="Local Bamao shop, character selection, menu and character references"><p>''']
parts += [f'<span class="swatch" style="background:#{c}" title="#{c}"></span>' for c in manifest['palette']]
parts += ['</p></section><section><h2>Characters and birds</h2><div class="grid">']
for a in actors:
    name = a['name']
    controls = '' if name.startswith('Bird') else '<label>Motion <select onchange="this.closest(\'figure\').querySelector(\'.anim\').dataset.motion=this.value"><option value="Animation">Idle</option><option value="Greeting">Greeting</option></select></label>'
    parts += [f'<figure><img class="anim" data-actor="{name}" src="Animation_{name}_0.png" alt="{name} idle animation"><figcaption>{name} · {a["triangles"]:,} triangles · shared palette</figcaption><details><summary>Four views</summary><img src="Turnaround_{name}.png" alt="{name} front, side, back and opposite side"></details></figure>']
parts += ['</div></section><section><h2>Furnished interiors</h2><div class="grid">']
for kind in ['Residential','Shop','Inn','Trainer']:
    parts += [f'<figure><img src="Interior_{kind}.png" alt="{kind} interior"><figcaption>{kind}</figcaption></figure>']
parts += ['</div></section><section><h2>Native TWC rule gallery</h2><img src="TWCRuleGallery.png" alt="Straight, L, U and island counter and carpet arrangements"></section><section><h2>Furniture and decoration kit</h2><img src="PropContactSheet.png" alt="Complete furniture and decoration kit"></section><section><h2>Bird supports</h2><div class="grid">']
for name in ['BirdBlue','BirdRed','BirdYellow']:
    parts += [f'<figure><img src="Perch_{name}.png" alt="Supported {name} perch"><figcaption>{name}</figcaption></figure>']
parts += ['</div></section><section><h2>Eight biome palettes</h2><div class="grid">']
for biome in ['Grassland','Desert','Water','Mountain','Forest','Tundra','Marsh','Volcanic']:
    parts += [f'<figure><img loading="lazy" src="Gameplay_{biome}.png" alt="{biome} town"><figcaption>{biome}</figcaption></figure>']
parts += ['''</div></section><p><a href="UnityVerification.csv">Rendering and cleanup report</a> · <a href="UnityEditMode.json">Unity mask and asset tests</a> · <a href="UnityPlayMode.json">Town gameplay tests</a></p>
<script>let frame=0,playing=true;document.querySelector('#play').onclick=e=>{playing=!playing;e.target.textContent=playing?'Pause animation':'Play animation'};setInterval(()=>{if(!playing)return;frame=(frame+1)%8;for(const img of document.querySelectorAll('.anim'))img.src=`${img.dataset.motion||'Animation'}_${img.dataset.actor}_${frame}.png`},500)</script></html>''']
(folder / 'index.html').write_text(''.join(parts), encoding='utf-8')
print(folder / 'index.html')
