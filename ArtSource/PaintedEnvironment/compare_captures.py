"""Compare the complete paired capture CSVs; never modifies Unity assets."""
import csv, json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
folder = root / 'Docs/Art/Previews/PaintedEnvironment'
def read(stage):
    with (folder / stage / 'Stats.csv').open(newline='') as stream:
        rows = list(csv.DictReader(stream))
    assert len(rows) == 54 and len({r['view'] for r in rows}) == 54, stage
    return {r['view']: r for r in rows}

before, after = read('Before'), read('After')
assert before.keys() == after.keys()
metrics = ('seed', 'renderers', 'materials', 'triangles', 'batches', 'setPassCalls')
differences = {name: {m: [before[name][m], after[name][m]] for m in metrics
                      if before[name][m] != after[name][m]} for name in before}
differences = {name: values for name, values in differences.items() if values}
result = {'views': len(before), 'differences': differences,
          'textureBytes': {name: {'before': int(before[name]['textureBytes']),
                                  'after': int(after[name]['textureBytes'])} for name in before}}
(root / 'Docs/Art/Verification/PaintedEnvironmentCaptureComparison.json').write_text(
    json.dumps(result, indent=2) + '\n')
print(json.dumps({'views': len(before), 'differences': differences}, indent=2))
for name in ('Dungeon_Grassland_Interior_Regular', 'Town_Grassland', 'Overworld'):
    print(name, result['textureBytes'][name])
assert not differences, 'Review capture metric differences'
