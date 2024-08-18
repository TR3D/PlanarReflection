# Planar Reflections
![image](https://github.com/TR3D/PlanarReflection/assets/63724445/a9f54ac4-9441-4cfa-b020-77348b77ee86)

Simple planar reflections renderer feature for URP developded with Unity 2022.3. It uses the current viewport camera to render to a Render Texture and blurs the resulting image via dual filtering if desired. 

## Settings
![image](https://github.com/TR3D/PlanarReflection/assets/63724445/10155689-97d2-42e2-b029-f1edb8159f07)

### Active
Should the render texture be updated?

### Plane Y Pos
Y-position of the reflection plane in world space. Use this if the mirror plane is located at another location than 0.

### Render Texture Settings
**Resolution**  
Change the resolution of the reflection texture.
![Unity_o68h9vm7X6](https://github.com/user-attachments/assets/c796f088-8360-4ed2-9ecd-739b38bf5a27)


**Layer Mask**  
Change which layer mask should be rendered for the reflection.

### Blur
**Apply Blur**  
Blur the reflection

**Iterations**  
How many downsample / upsample iterations should be applied?

**Offset**  
Increase the blurriness of the reflection.
![Unity_zta00YnDF5](https://github.com/user-attachments/assets/d4264049-bf6d-46ea-afd7-2e8a1a680d68)

