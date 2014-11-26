CIS565-FInal-Project-OceanWave
==============================
#Tasks
------------------------------
Surface generation

* Fourier transform

* Spectrum generation

Lighting

* Normal

* Specular

* BRDF

#Possible Extra Features
------------------------------
* Color blending
* Subsurface scattering
* Interaction with objects

#Reference Images
------------------------------
![Subsurface scattering](/AlphaPresentation/sss.PNG)
![](/AlphaPresentation/height.PNG)
![](/AlphaPresentation/brdf.PNG)

#Alpha Version Progress
------------------------------
* Setting up render textures for doing FFT in the fragment shader
* Simple noise spectrum generation for ocean wave simulation
* wireframe rendering of ocean waves
![Alpha Version](/AlphaPresentation/wireframe.PNG)

#Base Codes
------------------------------
* EncodeFloat.cs

passing and reading encoded float to and from rendertexture
* FourierGPU.cs

FFT functions

#References
------------------------------
“A unified directional spectrum for long and short wind-driven waves” - T. Elfouhaily, B. Chapron, and K. Katsaros 

Real-time Realistic Ocean Lighting using Seamless Transitions from Geometry to BRDF
Eric Bruneton and Fabrice Neyret and Nicolas Holzschuch


