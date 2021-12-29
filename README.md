Smrvfx
------

![gif](https://i.imgur.com/HWwnljE.gif)
![gif](https://i.imgur.com/Tk1IlOb.gif)


**Smrvfx** is a Unity sample project that demonstrates how to use either custom skinned mesh sampling code or the
[skinned mesh sampling feature] with VFX Graph to emit particles from animating characters.


The Fork
---------
This is a fork of the [original keijiro's project] to restore the custom skinned mesh sampling code that was removed in Oct 2021. 

The code was restored as its a useful learning resource for supplying poisitonal data via a render texture populated by a compute shader. It maintains use of URP over HDRP so it loses the ground reflection and some other lighting details such as the spot lights. If you want the HDRP look I suggest downloading the [tagged 2019.4 version] from this repository and then merge the updates from the 'VFX 2019.4' folder into the original smrvfx package.


Improvements
------------
I changed the original SkinnedMeshBaker code and compute shader to support using the previously rendered positions texture to calculate the velocity. This avoids having to transfer old positional data each frame reducing memory requirements and general overhead over the original. This functionality can be toggled on or off in the SkinnedMeshBaker inspector.


[original keijiro's project]:
  https://github.com/keijiro/Smrvfx

[skinned mesh sampling feature]:
  https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@12.0/manual/Operator-SampleMesh.html

[tagged 2019.4 version]:
  https://github.com/noisecrime/Smrvfx/releases/tag/2019.4


System requirements
-------------------

- Unity 2021.2
- HDRP/URP 12.0