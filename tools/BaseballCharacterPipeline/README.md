# Baseball Character Pipeline

This pipeline converts the user-created Meshy player into reproducible runtime
assets for Dan's RBI Baseball 2026. It requires Blender 5.2 LTS or newer.

The build keeps the generated face and skin details, removes generated uniform
lettering by replacing clothing faces with named materials, and exports running
and walking as animation-only GLBs so the texture is not duplicated.

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' `
  --background `
  --python .\tools\BaseballCharacterPipeline\build_character_asset.py `
  -- `
  --pitch 'D:\Meshy_AI_Animation_baseball_pitching_withSkin.glb' `
  --run '<path-to-running-withSkin.glb>' `
  --walk '<path-to-walking-withSkin.glb>' `
  --output-dir '.\StandaloneBaseball\Assets\Gameplay3D\models' `
  --blend '.\artifacts\BaseballCharacter\DanRBIBaseballPlayer.blend'
```

Runtime material names are `DRBI_Jersey`, `DRBI_Pants`, `DRBI_Cap`,
`DRBI_Accent`, and `DRBI_SkinDetail`. The first four are recolored by the
selected matchup uniform. `DRBI_SkinDetail` retains the generated texture.
