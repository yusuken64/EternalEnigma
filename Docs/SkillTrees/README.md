# Skill trees (`.skilltree`)

Class training uses skill points derived from highest level and learned ranks. The `skilltree 1`
header versions this authoring text format; it is not player-save versioning.

One text file per class: its skills laid out in tiers 1-3. The files here are exported from the
`ClassDefinition` assets in `Assets/Resources/Classes/Definitions`.

```
# comment (whole lines only; skill names may contain '#')
skilltree 1
class warrior
name Warrior

tier 1
  mastery   | Warrior Novice Training
  gathering | Mining
  skill     | Double Strike | 5
tier 2
  mastery   | Warrior Adept Training
  single    | Initiative
```

- `skilltree 1` must come first; `class` is required; `name` is optional.
- Entry kinds: `skill` (normal, rank column 1-5 required), `mastery`, `gathering`, `single` (always one rank).
- Fields are separated by `|`, so skill names can hold spaces and punctuation but not `|`.
- Order inside a tier is the order the trainer lists the skills. Skills are matched by `SkillName`.
- A tree needs exactly one mastery per tier and no duplicate skills (`Validate`); the editor shows problems live.

## Edit in the Campaign Explorer

```
dotnet run --project Core/EternalEnigma.Core/EternalEnigma.Campaign.Explorer
```

Press `K` at the start menu (or pass `--skilltree <file|dir>`). Without a terminal, `--skilltree` prints the trees.

| Key | Action |
| --- | --- |
| Up / Down, Left / Right | Select a skill, switch tree |
| `[` `]` | Move the skill down / up a tier |
| `+` `-` | Max rank (normal skills only) |
| `K` | Cycle kind: skill, mastery, gathering, single |
| `A` `N` `Del` | Add, rename, remove |
| `Z` | Undo |
| `S` | Save to the file (nothing is written until then) |
| `E` `I` | Export the tree to another file / import a file (replaces the open tree of the same class) |

Each row shows the rank span, the character levels where ranks unlock and the total skill points to max it.

## Move trees to and from Unity

- `Tools > Eternal Enigma > Skill Trees > Export All To Docs` writes every class to `Docs/SkillTrees`.
- `Tools > Eternal Enigma > Skill Trees > Import From Folder...` shows what would change, then rewrites each
  `ClassDefinition.Skills` list. A file is skipped, with a reason, if it does not parse, names an unknown class,
  fails validation or names a skill that has no `Skill` asset. Importing never creates skills.

The format code is `SkillTreeFormat` and `SkillTreeDocument` in `EternalEnigma.Core/Classes`.
