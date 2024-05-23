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
#### Resolution
Change the resolution of the reflection texture.

#### Layer Mask
Change which layer mask should be rendered for the reflection.

### Blur
#### Apply Blur
Blur the reflection
![image](https://github.com/TR3D/PlanarReflection/assets/63724445/5ccb7f39-ef33-4faa-aa5c-a2b0c0b17698)
![image](https://github.com/TR3D/PlanarReflection/assets/63724445/a1f7a5a6-6542-46b1-adbd-a84d489a51d5)



#### Iterations
How many downsample / upsample iterations should be applied?

#### Offset
Increase the blurriness of the reflection.
![image](https://github.com/TR3D/PlanarReflection/assets/63724445/89e53fce-0bab-4c63-a886-ab5ed6259af7)
![image](https://github.com/TR3D/PlanarReflection/assets/63724445/913ec350-29f4-43c5-83d8-4a383793e092)

