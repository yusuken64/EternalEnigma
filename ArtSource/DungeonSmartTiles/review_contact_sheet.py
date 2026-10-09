"""Assemble Unity-rendered reference crops; does not alter game assets."""
from pathlib import Path
import csv, math
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[2]
folder = root / 'Docs/Art/Verification/DungeonSmartLayers'
with (folder / 'PickupAdjustments.csv').open(encoding='utf-8-sig', newline='') as f:
    entries = list(csv.DictReader(f))
names = ['Category: Potion','Category: Bread','Category: SpellBook','Bag',
         'Currency','Small key','Category: Ring','Category: Skull',
         'Category: Bow','Category: Sword','Category: Shield','Category: TreasureChest',
         'Arrows','Apprentice Wand','Wooden Spear','Battle Axe',
         'War Hammer','Healing Herb','Iron Ore','Trap Parts']
font_path = Path('C:/Windows/Fonts/segoeui.ttf')
font = ImageFont.truetype(str(font_path), 18) if font_path.exists() else ImageFont.load_default()
title = ImageFont.truetype(str(font_path), 26) if font_path.exists() else font
width, height = 512, 209
sheet = Image.new('RGB', (width * 3, 80 + height * math.ceil(len(names)/3)), '#172025')
draw = ImageDraw.Draw(sheet)
draw.text((20,12),'Dungeon pickups — unchanged hero and gameplay projection',font=title,fill='white')
draw.text((20,48),'Light / dark floors. H = hero screen height; ratios are silhouette diameters.',font=font,fill='#c4ced1')
for i, name in enumerate(names):
    number = next(j for j, entry in enumerate(entries) if entry['name'] == name)
    entry = entries[number]
    image = Image.open(folder / 'Pickups' / f'{number:03}.png').convert('RGB').resize((512,167),Image.Resampling.LANCZOS)
    x, y = i%3*width, 80+i//3*height
    sheet.paste(image, (x,y+34))
    label = name.replace('Category: ','')
    metric = f"{float(entry['projected_diameter_ratio']):.2f} H"
    if entry['size']=='Chest': metric='0.50 world H vertically'
    draw.text((x+10,y+5),f'{label} · {metric}',font=font,fill='white')
sheet.save(folder/'PickupCategories.png')
print(folder/'PickupCategories.png')
