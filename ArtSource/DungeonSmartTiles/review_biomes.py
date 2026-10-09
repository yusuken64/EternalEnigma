"""Index Unity's all-biome captures without changing game assets."""
from pathlib import Path
from html import escape
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[2]
folder = root / 'Docs/Art/Verification/DungeonSmartLayers/Biomes'
biomes = ['Grassland', 'Desert', 'Water', 'Mountain', 'Forest', 'Tundra', 'Marsh', 'Volcanic']
combinations = [('Interior', 'Regular'), ('Interior', 'Throne'), ('Outdoor', 'Regular'), ('Outdoor', 'Throne')]
font_path = Path('C:/Windows/Fonts/segoeui.ttf')
font = ImageFont.truetype(str(font_path), 21) if font_path.exists() else ImageFont.load_default()
title = ImageFont.truetype(str(font_path), 30) if font_path.exists() else font
sheet = Image.new('RGB', (1920, 96 + len(biomes) * 340), '#172025')
draw = ImageDraw.Draw(sheet)
draw.text((20, 12), 'Dungeon smart boundaries - all biomes', font=title, fill='white')
draw.text((20, 54), '4.23-unit walls; existing theme surfaces, hero and gameplay camera projection.', font=font, fill='#c4ced1')
html = ["""<!doctype html><meta charset="utf-8"><title>Dungeon biome review</title>
<style>body{background:#172025;color:#eee;font:16px system-ui;margin:24px}main{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px}img{width:100%}article{background:#242c32;padding:12px}a{color:#adf}h2{font-size:18px}h3{font-size:16px;margin:0 0 8px}</style>
<h1>Smart boundaries in every dungeon biome</h1>
<p>All 32 biome/environment/layout combinations. Seed 12345; 4.234727-unit walls, the unchanged dungeon hero and gameplay camera projection. Each theme retains its floors, accents, wall textures, decorations and lighting.</p>
<p><a href="ContactSheet.png">Contact sheet</a> | <a href="../Verification.md">Verification report</a></p>"""]
for row, biome in enumerate(biomes):
    html.append(f'<h2>{escape(biome)}</h2><main>')
    for col, (environment, layout) in enumerate(combinations):
        name = f'{biome}_{environment}_{layout}'
        source = folder / f'{name}.png'
        assert source.is_file(), source
        with Image.open(source) as image:
            sheet.paste(image.convert('RGB').resize((480, 300), Image.Resampling.LANCZOS), (col * 480, 136 + row * 340))
        draw.text((col * 480 + 10, 102 + row * 340), f'{biome} / {environment} / {layout}', font=font, fill='white')
        runtime = f'Runtime_{name}.png'
        link = f'<p><a href="{runtime}">Gameplay with fog and HUD</a></p>' if (folder / runtime).exists() else ''
        html.append(f'<article><h3>{environment} / {layout}</h3><a href="{name}.png"><img src="{name}.png"></a>{link}</article>')
    html.append('</main>')
sheet.save(folder / 'ContactSheet.png')
(folder / 'Review.html').write_text('\n'.join(html), encoding='utf-8')
print(folder / 'Review.html')
