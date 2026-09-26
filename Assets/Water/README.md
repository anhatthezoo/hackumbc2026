# GodotOceanWaves URP port

This folder contains a Unity URP port of
[GodotOceanWaves](https://github.com/2Retr0/GodotOceanWaves).

## Assets

- `Shaders/GodotOceanWater.shader` — vertex displacement, bicubic normal/foam
  filtering, distance fades, Fresnel/GGX lighting, environment reflections,
  subsurface tinting, fog, and up to eight FFT cascades.
- `Shaders/OceanWaves.compute` — JONSWAP/TMA spectrum generation, temporal
  modulation, two-dimensional Stockham inverse FFT, displacement, gradients,
  and accumulated foam.
- `Scripts/OceanWaveGenerator.cs` — owns the GPU textures and buffers, updates
  cascades in edit mode and play mode, and binds them with a
  `MaterialPropertyBlock`.
- `Materials/GodotOceanWater.mat` — source-inspired water material.
- `Meshes/OceanGrid.asset` — tessellated 400 m water grid used by SampleScene.
- `Textures/OceanSkybox.png` and `Materials/OceanSkybox.mat` — the original
  demo environment, imported as a cubemap for sky and water reflections.

## Usage

Add `OceanWaveGenerator` to a sufficiently tessellated mesh renderer using
`GodotOceanWater.mat`. The default three cascades match the source project's
tile sizes and wave settings. The component creates and binds the displacement
and normal/foam texture arrays automatically.

The material exposes body opacity, crest transparency, transmission color,
and transmission strength. Troughs stay dark while elevated crests transmit
more light; foam and strong Fresnel reflections remain opaque.

No additional static texture is required for the water surface. The
`sea_spray` texture belongs to the original demo's separate billboard particle
effect; it is not sampled by the water shader itself.
