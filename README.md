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

Here are some images from the program
![cover](/Results/cover.png)

![](/Results/components.gif)

Turning on and off several components in ocean shader.

![](/Results/comparison.gif)

Combination of components.


#Performance Analysis
------------------------------
Profiling on CPU and GPU usage
![](/Results/profiler.PNG)
![](/Results/profiler2.PNG)
Our program is mainly calculating in GPU so CPU usage is pretty little. The variance of slope is calculated in CPU per vertex. The grid size is 128 x 128.
The program runs very fast on a desktop computer with 2000 FPS when V-Sync is turned off. (i7-4770K and GTX780)

The following test is made using GT555M
![](/Results/cost_in_shader.PNG)

The final rendering of ocean is in Oceanbrdf shader. Here is the graph about the time cost of different graphics features. The lighting all cost about 2ms. The white caps cost about 6ms.

![](/Results/cost_in_maps.PNG)

To perform the basic wave simulation and BRDF lighting. We need to generate one height map and two slope map, which will cost 7ms through in our test. To add dispalcement in x and z direction we need to generate two displacement map, which will cost about 5.2 ms. For the white caps we need to precompute jaccobian, which will cost about 7.4ms.

#Reference Images
------------------------------
![Subsurface scattering](/AlphaPresentation/sss.PNG)

Ocean from Assasin's Creed III

![](https://github.com/CyborgYL/CIS565-FInal-Project-OceanWave/blob/master/Results/Reference%20IMG/pic1.jpg)

Photo on the Internet

![](/AlphaPresentation/brdf.PNG)

Snapshot in Eric Bruneton's paper

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


