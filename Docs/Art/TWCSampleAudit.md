# TWC sample inspection

This is retained vendor-sample reference material, not a description of production scene
layouts. Production uses the committed EnvironmentKit and detailed Core town adapter.

Read from the installed TileWorldCreator 3 package. Missing local demos were restored from the already-cached package, without replacing existing files. No separately named river demo is present in this package; CliffIsland provides water/sand/cliff transitions.

## Assets/TileWorldCreator/Demo/06_CliffIsland/06_CliffIsland.unity

_TileWorldCreator: Assets/TileWorldCreator/Demo/06_CliffIsland/CliffIslandAsset.asset, 40x40, cell 2, XZ

Blueprint `Base` (fed212de-8327-463f-9324-ee4fd2ebbeaa) active=True
- CellularAutomata: numberOfSteps=2, deathLimit=4, birthLimit=4, guid=0de4a8bc-060a-4e7e-84b4-0c73eef12f59, active=False, foldout=False, resultFailed=False
- Paint: guid=ed90a2ec-2303-45e7-b082-f2ed6dd7692e, active=True, foldout=True, resultFailed=False
- Smooth: smoothCount=3, guid=adb09b74-caa0-48c9-8cc7-096989dfb478, active=False, foldout=True, resultFailed=True

Blueprint `InnerSand` (2214adc3-3843-4cab-9512-f7f6447fd320) active=True
- Add: selectedLayerIndex=0, guidCopyLayer=fed212de-8327-463f-9324-ee4fd2ebbeaa, guid=5f389e80-7723-4e3e-91c1-5520d04a6528, active=True, foldout=True, resultFailed=False
- Shrink: guid=981ca29e-fd11-4e88-b677-efd73268818a, active=True, foldout=False, resultFailed=True

Blueprint `Cactus` (c0357144-b661-4b10-b4e3-d7298048b1a5) active=True
- Add: selectedLayerIndex=0, guidCopyLayer=2214adc3-3843-4cab-9512-f7f6447fd320, guid=23e87b95-3533-4864-82b5-39d0c6296316, active=True, foldout=True, resultFailed=True
- Select: selectType=random, randomSelectionWeight=0.509, guid=5d7620c7-4a65-4ecf-b02d-07c471cbc367, active=True, foldout=True, resultFailed=True

Blueprint `Stones` (f0f7db86-1f7f-49ab-b438-f8c273f70ffd) active=True
- Add: selectedLayerIndex=0, guidCopyLayer=2214adc3-3843-4cab-9512-f7f6447fd320, guid=df3cc4af-7f5c-449e-9409-1d1e2b22b710, active=True, foldout=True, resultFailed=True
- Select: selectType=random, randomSelectionWeight=0.176, guid=2f6d943e-3263-40c7-ab31-a1c8f190333f, active=True, foldout=True, resultFailed=True

Blueprint `BaseWater` (cdd253d5-53b9-4a60-a912-feb6532a7159) active=True
- Add: selectedLayerIndex=0, guidCopyLayer=fed212de-8327-463f-9324-ee4fd2ebbeaa, guid=2b68ef92-0bbf-4683-af32-328a8869c8bb, active=True, foldout=True, resultFailed=False
- Invert: guid=96b3bd93-c434-4483-8102-bb7316ce22ce, active=True, foldout=False, resultFailed=False

Blueprint `Bridges` (56e7051f-81d7-4663-b882-42522c0d0faa) active=True
- LSystem: rootSentence=[F]--F, randomIgnoreRuleModifier=False, chanceToIgnoreRule=0.3, iterations=3, length=4, shortenLength=False, guid=91ffb396-8f05-48a6-b209-f82cfecde6c1, active=True, foldout=False, resultFailed=False
- Subtract: selectedLayerIndex=0, guidCopyLayer=cdd253d5-53b9-4a60-a912-feb6532a7159, guid=cfa75c96-0f52-46a9-9024-4dbf24c8992e, active=True, foldout=True, resultFailed=False
- Subtract: selectedLayerIndex=0, guidCopyLayer=c0357144-b661-4b10-b4e3-d7298048b1a5, guid=0f8b2de7-f064-43f7-be26-a9add095cafd, active=True, foldout=True, resultFailed=False

Blueprint `BridgeBetween` (b775435d-0627-492c-8caa-bd4f08f797bd) active=True
- Paint: guid=d6fb1862-8b15-4da6-895f-ae887913f9a1, active=True, foldout=True, resultFailed=False

Build: InstantiateTiles: selectedLayerIndex=0, mergeTiles=False, keepPrefabConnection=False, scaleTileByCellSize=True, scaleTileHeight=False, tileColliderHeightOffset=0, tileColliderHeight=0, tileColliderBorderOffset=0, colliderTypeVariantA=none, colliderTypeVariantB=none, setShadowCastingMode=False, shadowCasting=Off, globalPositionOffset=(0.00, 0.00, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScalingOffset=(0.00, 0.00, 0.00), guid=f188b93a-0688-428d-b8d7-57083e975a7b, assignedGenerationLayerGuid=fed212de-8327-463f-9324-ee4fd2ebbeaa, layerName=Cliffs, foldout=False, active=True
- Preset: Assets/TileWorldCreator/Tiles/Version 2 Tiles/CliffIsland/CliffPresetWater.asset

Build: InstantiateTiles: selectedLayerIndex=0, mergeTiles=False, keepPrefabConnection=False, scaleTileByCellSize=True, scaleTileHeight=False, tileColliderHeightOffset=0, tileColliderHeight=0, tileColliderBorderOffset=0, colliderTypeVariantA=none, colliderTypeVariantB=none, setShadowCastingMode=False, shadowCasting=Off, globalPositionOffset=(0.00, 0.00, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScalingOffset=(0.00, 0.00, 0.00), guid=541726c1-e2a0-495a-9b9d-4a339fa8ccf7, assignedGenerationLayerGuid=2214adc3-3843-4cab-9512-f7f6447fd320, layerName=Sand, foldout=False, active=True
- Preset: Assets/TileWorldCreator/Tiles/Version 2 Tiles/CliffIsland/CliffSandPreset.asset

Build: InstantiateObjects: selectedCopyLayerIndex=0, useTileRotationFrom=00000000-0000-0000-0000-000000000000, mergeObjects=False, keepPrefabConnection=False, addMeshCollider=False, instantiateChilds=True, selectFromPrefabList=False, count=2, radius=1, useSubdividedMap=False, setShadowCastingMode=False, shadowCasting=Off, disableTileRotation=False, globalPositionOffset=(0.00, 0.50, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScaleOffset=(0.00, 0.00, 0.00), localPositionOffset=(0.00, 0.00, 0.00), localRotationOffset=(0.00, 0.00, 0.00), localScaleOffset=(0.00, 0.00, 0.00), rndPosition=False, minRndPosition=(0.00, 0.00, 0.00), maxRndPosition=(0.00, 0.00, 0.00), rndScaling=True, uniformScaling=True, minRndUniformScale=0.8, maxRndUniformScale=1.5, minRndScale=(0.00, 0.00, 0.00), maxRndScale=(0.00, 0.00, 0.00), rndRotation=True, minRndRotation=(0.00, -360.00, 0.00), maxRndRotation=(0.00, 360.00, 0.00), guid=0cdfdfe7-0858-4472-b357-cd691cd1dc6a, assignedGenerationLayerGuid=c0357144-b661-4b10-b4e3-d7298048b1a5, layerName=Cactus, foldout=False, active=True

Build: InstantiateObjects: selectedCopyLayerIndex=0, useTileRotationFrom=00000000-0000-0000-0000-000000000000, mergeObjects=False, keepPrefabConnection=False, addMeshCollider=False, instantiateChilds=True, selectFromPrefabList=False, count=3, radius=2, useSubdividedMap=False, setShadowCastingMode=False, shadowCasting=Off, disableTileRotation=False, globalPositionOffset=(0.00, 0.50, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScaleOffset=(0.00, 0.00, 0.00), localPositionOffset=(0.00, 0.00, 0.00), localRotationOffset=(0.00, 0.00, 0.00), localScaleOffset=(0.00, 0.00, 0.00), rndPosition=False, minRndPosition=(0.00, 0.00, 0.00), maxRndPosition=(0.00, 0.00, 0.00), rndScaling=True, uniformScaling=True, minRndUniformScale=0.5, maxRndUniformScale=1.5, minRndScale=(0.00, 1.00, 0.00), maxRndScale=(0.00, 1.50, 0.00), rndRotation=True, minRndRotation=(0.00, -360.00, 0.00), maxRndRotation=(0.00, 360.00, 0.00), guid=09549124-fcfd-40ae-9031-1011f2e9c5ee, assignedGenerationLayerGuid=f0f7db86-1f7f-49ab-b438-f8c273f70ffd, layerName=Stones, foldout=False, active=True

Build: Instantiate6Tiles: selectedLayerIndex=0, initialOrientationLayerGuid=00000000-0000-0000-0000-000000000000, useOnlyOrientationLayer=False, mergeTiles=False, keepPrefabConnection=False, scaleTileByCellSize=True, tileColliderHeightOffset=0, tileColliderHeight=1, tileColliderBorderOffset=0, colliderTypeVariantA=none, colliderTypeVariantB=none, setShadowCastingMode=False, shadowCasting=Off, globalPositionOffset=(0.00, 1.50, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScalingOffset=(0.00, 0.00, 0.00), guid=733772c4-1118-4505-ba39-6d6877e5cd50, assignedGenerationLayerGuid=56e7051f-81d7-4663-b882-42522c0d0faa, layerName=BridgesPath, foldout=False, active=True

Build: InstantiateTiles: selectedLayerIndex=0, mergeTiles=False, keepPrefabConnection=False, scaleTileByCellSize=True, scaleTileHeight=False, tileColliderHeightOffset=0, tileColliderHeight=1, tileColliderBorderOffset=0, colliderTypeVariantA=none, colliderTypeVariantB=none, setShadowCastingMode=False, shadowCasting=Off, globalPositionOffset=(0.00, -0.50, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScalingOffset=(0.00, 0.00, 0.00), guid=65fa1a66-d8b2-4452-9584-cee0178ccae3, assignedGenerationLayerGuid=cdd253d5-53b9-4a60-a912-feb6532a7159, layerName=Water, foldout=False, active=True
- Preset: Assets/TileWorldCreator/Tiles/Version 2 Tiles/CliffIsland/CliffPresetOnlyWater.asset

Build: InstantiateObjects: selectedCopyLayerIndex=0, useTileRotationFrom=fed212de-8327-463f-9324-ee4fd2ebbeaa, mergeObjects=False, keepPrefabConnection=False, addMeshCollider=False, instantiateChilds=False, selectFromPrefabList=False, count=0, radius=0, useSubdividedMap=False, setShadowCastingMode=False, shadowCasting=On, disableTileRotation=False, globalPositionOffset=(0.00, 0.00, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScaleOffset=(0.00, 0.00, 0.00), localPositionOffset=(0.00, 0.00, 0.00), localRotationOffset=(0.00, 0.00, 0.00), localScaleOffset=(0.00, 0.00, 0.00), rndPosition=False, minRndPosition=(0.00, 0.00, 0.00), maxRndPosition=(0.00, 0.00, 0.00), rndScaling=False, uniformScaling=False, minRndUniformScale=0, maxRndUniformScale=0, minRndScale=(0.00, 0.00, 0.00), maxRndScale=(0.00, 0.00, 0.00), rndRotation=False, minRndRotation=(0.00, 0.00, 0.00), maxRndRotation=(0.00, 0.00, 0.00), guid=8625d8ee-7652-4b4a-bfe9-62d162298d50, assignedGenerationLayerGuid=b775435d-0627-492c-8caa-bd4f08f797bd, layerName=SDASSD, foldout=True, active=True

## Assets/TileWorldCreator/Demo/09_Ramps/09_RampsDemoA.unity

_TileWorldCreator: Assets/TileWorldCreator/Demo/09_Ramps/RampsDemoAssetA.asset, 20x20, cell 2, XZ

Blueprint `Base` (b346ca81-77e1-48a7-8fdb-678f984aaf37) active=True
- Paint: guid=d0e7724c-b3f1-4ef0-b430-ccda319a445a, active=True, foldout=True, resultFailed=False

Blueprint `Ramps` (0dfac369-91de-4c25-bf91-16598b93f26a) active=True
- Paint: guid=b8d62950-93fb-4f57-8a54-59310b144fe7, active=True, foldout=True, resultFailed=False

Blueprint `Combined` (a9b82165-070d-46ac-bc83-cd071a5094ca) active=True
- Add: selectedLayerIndex=0, guidCopyLayer=0dfac369-91de-4c25-bf91-16598b93f26a, guid=1fbaebd8-1757-4a01-b0b6-af81bd84cd3f, active=True, foldout=True, resultFailed=False
- Add: selectedLayerIndex=0, guidCopyLayer=b346ca81-77e1-48a7-8fdb-678f984aaf37, guid=4cb8f078-ff88-4239-8f67-c857466c2a47, active=True, foldout=True, resultFailed=False

Build: InstantiateTiles: selectedLayerIndex=0, mergeTiles=False, keepPrefabConnection=False, scaleTileByCellSize=True, scaleTileHeight=False, tileColliderHeightOffset=0, tileColliderHeight=1, tileColliderBorderOffset=0, colliderTypeVariantA=TileCollider, colliderTypeVariantB=none, setShadowCastingMode=False, shadowCasting=Off, globalPositionOffset=(0.00, 0.00, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScalingOffset=(0.00, 0.00, 0.00), guid=c595e98d-f991-446d-8d1a-6627dd864d5c, assignedGenerationLayerGuid=a9b82165-070d-46ac-bc83-cd071a5094ca, layerName=Base, foldout=True, active=True
- Preset: Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Block/BlockPreset.asset

Build: InstantiateTiles: selectedLayerIndex=0, mergeTiles=False, keepPrefabConnection=False, scaleTileByCellSize=True, scaleTileHeight=False, tileColliderHeightOffset=0, tileColliderHeight=1, tileColliderBorderOffset=0, colliderTypeVariantA=TileCollider, colliderTypeVariantB=none, setShadowCastingMode=False, shadowCasting=Off, globalPositionOffset=(0.00, 0.00, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScalingOffset=(0.00, 0.00, 0.00), guid=f9d2c674-2b7e-4763-8f53-0b049152b9a1, assignedGenerationLayerGuid=a9b82165-070d-46ac-bc83-cd071a5094ca, layerName=Ramps, foldout=False, active=True
- Preset: Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Block/BlockRampPreset2.asset

## Assets/TileWorldCreator/Demo/09_Ramps/09_RampsDemoB.unity

_TileWorldCreator: Assets/TileWorldCreator/Demo/09_Ramps/RampsDemoAssetA.asset, 20x20, cell 2, XZ

Blueprint `Base` (b346ca81-77e1-48a7-8fdb-678f984aaf37) active=True
- Paint: guid=d0e7724c-b3f1-4ef0-b430-ccda319a445a, active=True, foldout=True, resultFailed=False

Blueprint `Ramps` (0dfac369-91de-4c25-bf91-16598b93f26a) active=True
- Paint: guid=b8d62950-93fb-4f57-8a54-59310b144fe7, active=True, foldout=True, resultFailed=False

Blueprint `Combined` (a9b82165-070d-46ac-bc83-cd071a5094ca) active=True
- Add: selectedLayerIndex=0, guidCopyLayer=0dfac369-91de-4c25-bf91-16598b93f26a, guid=1fbaebd8-1757-4a01-b0b6-af81bd84cd3f, active=True, foldout=True, resultFailed=False
- Add: selectedLayerIndex=0, guidCopyLayer=b346ca81-77e1-48a7-8fdb-678f984aaf37, guid=4cb8f078-ff88-4239-8f67-c857466c2a47, active=True, foldout=True, resultFailed=False

Build: InstantiateTiles: selectedLayerIndex=0, mergeTiles=False, keepPrefabConnection=False, scaleTileByCellSize=True, scaleTileHeight=False, tileColliderHeightOffset=0, tileColliderHeight=1, tileColliderBorderOffset=0, colliderTypeVariantA=TileCollider, colliderTypeVariantB=none, setShadowCastingMode=False, shadowCasting=Off, globalPositionOffset=(0.00, 0.00, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScalingOffset=(0.00, 0.00, 0.00), guid=c595e98d-f991-446d-8d1a-6627dd864d5c, assignedGenerationLayerGuid=a9b82165-070d-46ac-bc83-cd071a5094ca, layerName=Base, foldout=True, active=True
- Preset: Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Block/BlockPreset.asset

Build: InstantiateTiles: selectedLayerIndex=0, mergeTiles=False, keepPrefabConnection=False, scaleTileByCellSize=True, scaleTileHeight=False, tileColliderHeightOffset=0, tileColliderHeight=1, tileColliderBorderOffset=0, colliderTypeVariantA=TileCollider, colliderTypeVariantB=none, setShadowCastingMode=False, shadowCasting=Off, globalPositionOffset=(0.00, 0.00, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScalingOffset=(0.00, 0.00, 0.00), guid=f9d2c674-2b7e-4763-8f53-0b049152b9a1, assignedGenerationLayerGuid=a9b82165-070d-46ac-bc83-cd071a5094ca, layerName=Ramps, foldout=False, active=True
- Preset: Assets/TileWorldCreator/Tiles/Version 3 Tiles/4-Tiles/Block/BlockRampPreset2.asset

## Assets/TileWorldCreator/Demo/11_MixTilesets/11_MixTilesets.unity

TileWorldCreator: Assets/TileWorldCreator/Demo/11_MixTilesets/MixTilesetAsset.asset, 20x20, cell 2, XZ

Blueprint `Tileset1` (ecacf3d5-ef00-4ed6-8afe-45b849582085) active=True
- Paint: guid=c5ccfa6f-5b9f-43fb-a69c-8559bf5f4cda, active=True, foldout=True, resultFailed=False

Blueprint `Tileset2` (a0d3e685-17bc-4381-8704-1cf1af3b6401) active=True
- Paint: guid=7600c4b1-4826-4c65-ad85-c08cb275da69, active=True, foldout=True, resultFailed=False

Blueprint `Tileset3` (4c651c65-876e-42be-9705-20cabe678441) active=True
- Paint: guid=728a3ac5-301c-4ccd-a975-114aadc83977, active=True, foldout=True, resultFailed=False

Blueprint `Combined` (011cb2fb-1988-42a5-8bce-a821b5d2a3ec) active=True
- Add: selectedLayerIndex=0, guidCopyLayer=ecacf3d5-ef00-4ed6-8afe-45b849582085, guid=d71de593-94c7-481b-aff4-7fd901f78e5c, active=True, foldout=False, resultFailed=False
- Add: selectedLayerIndex=0, guidCopyLayer=a0d3e685-17bc-4381-8704-1cf1af3b6401, guid=7bc11992-1041-4167-af08-1d15510704b6, active=True, foldout=False, resultFailed=False
- Add: selectedLayerIndex=0, guidCopyLayer=4c651c65-876e-42be-9705-20cabe678441, guid=c5963a81-b65d-492f-ae76-7c76d4f3464c, active=True, foldout=False, resultFailed=False

Build: InstantiateTiles: selectedLayerIndex=0, mergeTiles=True, keepPrefabConnection=False, scaleTileByCellSize=True, scaleTileHeight=False, tileColliderHeightOffset=0, tileColliderHeight=1, tileColliderBorderOffset=0, colliderTypeVariantA=none, colliderTypeVariantB=none, setShadowCastingMode=False, shadowCasting=Off, globalPositionOffset=(0.00, 0.00, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScalingOffset=(0.00, 0.00, 0.00), guid=848e3909-066f-4a00-a8b4-9bff6f06e4e4, assignedGenerationLayerGuid=011cb2fb-1988-42a5-8bce-a821b5d2a3ec, layerName=CliffTiles, foldout=False, active=True
- Preset: Assets/TileWorldCreator/Tiles/Version 2 Tiles/CliffIsland/CliffPreset.asset

Build: InstantiateTiles: selectedLayerIndex=0, mergeTiles=True, keepPrefabConnection=False, scaleTileByCellSize=True, scaleTileHeight=False, tileColliderHeightOffset=0, tileColliderHeight=1, tileColliderBorderOffset=0, colliderTypeVariantA=none, colliderTypeVariantB=none, setShadowCastingMode=False, shadowCasting=Off, globalPositionOffset=(0.00, 0.00, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScalingOffset=(0.00, 0.00, 0.00), guid=a53aad26-de74-4efc-b143-49e82db4c55a, assignedGenerationLayerGuid=011cb2fb-1988-42a5-8bce-a821b5d2a3ec, layerName=DungeonTiles, foldout=False, active=True
- Preset: Assets/TileWorldCreator/Tiles/Version 2 Tiles/Dungeon/DungeonTiles.asset

Build: InstantiateTiles: selectedLayerIndex=0, mergeTiles=True, keepPrefabConnection=False, scaleTileByCellSize=True, scaleTileHeight=False, tileColliderHeightOffset=0, tileColliderHeight=1, tileColliderBorderOffset=0, colliderTypeVariantA=TileCollider, colliderTypeVariantB=none, setShadowCastingMode=False, shadowCasting=Off, globalPositionOffset=(0.00, 0.00, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScalingOffset=(0.00, 0.00, 0.00), guid=be0d49b9-2d61-4906-92ad-c7f3c98245c7, assignedGenerationLayerGuid=011cb2fb-1988-42a5-8bce-a821b5d2a3ec, layerName=PrototypeTiles, foldout=False, active=True
- Preset: Assets/TileWorldCreator/Tiles/Version 2 Tiles/Prototype/PrototypeTiles.asset

Build: InstantiateTiles: selectedLayerIndex=0, mergeTiles=True, keepPrefabConnection=False, scaleTileByCellSize=True, scaleTileHeight=True, tileColliderHeightOffset=0, tileColliderHeight=1, tileColliderBorderOffset=0, colliderTypeVariantA=TileCollider, colliderTypeVariantB=none, setShadowCastingMode=False, shadowCasting=On, globalPositionOffset=(0.00, 0.00, 0.00), globalRotationOffset=(0.00, 0.00, 0.00), globalScalingOffset=(0.00, 0.00, 0.00), guid=01ceca8a-d03e-4ce5-8ee2-3c768c9327fb, assignedGenerationLayerGuid=4c651c65-876e-42be-9705-20cabe678441, layerName=Collision, foldout=False, active=True
