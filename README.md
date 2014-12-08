CIS565-FInal-Project-OceanWave
==============================
#Tasks
------------------------------
Surface generation

* Fourier transform

* Spectrum generation

Lighting

* Reflection

* Refraction

* BRDF

#Extra Featres

* White Caps
* Specular highlight from the Sun

#Snapshots
------------------------------
Video: http://youtu.be/dvUf_FV1XOE

![cover](/Results/cover.png)
![](/Results/stats.png)

#Performance Analysis
------------------------------
![](/Results/profiler.PNG)
![](/Results/profiler2.PNG)
#Reference Images
------------------------------
![Subsurface scattering](/AlphaPresentation/sss.PNG)
![](/AlphaPresentation/height.PNG)
![](/AlphaPresentation/brdf.PNG)

#Alpha Version Progress
------------------------------
* Mesh grids generation for ocean surface
* Setting up render textures for doing FFT in the fragment shader
* Simple noise spectrum generation for ocean wave simulation
* Wireframe rendering of ocean waves
![Alpha Version](/AlphaPresentation/wireframe.PNG)

#Base Codes
------------------------------
* EncodeFloat.cs, EncodeFloat.shader

passing and reading encoded float to and from rendertexture
* FourierGPU.cs, FourierGPU.shader

FFT functions

#References
------------------------------
“A unified directional spectrum for long and short wind-driven waves” - T. Elfouhaily, B. Chapron, and K. Katsaros 

Real-time Realistic Ocean Lighting using Seamless Transitions from Geometry to BRDF
Eric Bruneton and Fabrice Neyret and Nicolas Holzschuch


