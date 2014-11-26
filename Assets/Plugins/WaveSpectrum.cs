using UnityEngine;
using System.Collections;

public class WaveSpectrum
{

	int m_idx = 0;
	int m_size = 128; //This is the fourier transform size, must pow2 number. Recommend no higher or lower than 64, 128 or 256.
	float m_fsize;
	
	//These 3 settings can be used to control the look of the waves from rough seas to calm lakes.
	//WARNING - not all combinations of numbers makes sense and the waves will not always look correct.
	float m_windSpeed = 8.0f; //A higher wind speed gives greater swell to the waves
	float m_waveAmp = 1.0f; //Scales the height of the waves
	float m_omega = 0.84f; //A lower number means the waves last longer and will build up larger waves
	
	float m_mipMapLevels;
	Vector4 m_offset;
	Vector4 m_gridSizes = new Vector4(5488, 392, 28, 2);
	Vector4 m_inverseGridSizes;
	Vector2 m_varianceMax;
	
	Material m_initSpectrumMat;
	bool m_isInit = false;
	RenderTexture m_spectrum01, m_spectrum23;
	RenderTexture[] m_fourierBuffer0, m_fourierBuffer1, m_fourierBuffer2;
	RenderTexture m_map0, m_map1, m_map2;
	//RenderTexture m_WTable;
	FourierGPU m_fourier;
	float[] m_spectrum01Data, m_spectrum23Data, m_WTableData;

	public Vector4 GetGridSizes() { return m_gridSizes; }
	public RenderTexture GetMap0() { return m_map0; }
	public RenderTexture GetMap1() { return m_map1; }
	public RenderTexture GetMap2() { return m_map2; }

	public float GetMipMapLevels() { return m_mipMapLevels; }

	
	public WaveSpectrum(int size, float windSpeed, float waveAmp, float omega, int ansio, Vector4 gridSizes)
	{	
		if(size > 256)
		{
			Debug.Log("WaveSpectrumGPU::WaveSpectrumGPU	- fourier grid size must not be greater than 256, changing to 256");
			size = 256;
		}
		
		if(!Mathf.IsPowerOfTwo(size))
		{
			Debug.Log("WaveSpectrumGPU::WaveSpectrumGPU	- fourier grid size must be pow2 number, changing to nearest pow2 number");
			size = Mathf.NextPowerOfTwo(size);
		}
		
		m_gridSizes = gridSizes;
		m_size = size;
		m_waveAmp = waveAmp;
		m_windSpeed = windSpeed;
		m_omega = omega;

		m_fsize = (float)m_size;
		m_mipMapLevels = Mathf.Log(m_fsize)/Mathf.Log(2.0f);
		m_offset = new Vector4(1.0f + 0.5f / m_fsize, 1.0f + 0.5f / m_fsize, 0, 0);
		
		float factor = 2.0f * Mathf.PI * m_fsize;
		m_inverseGridSizes = new Vector4(factor/m_gridSizes.x, factor/m_gridSizes.y, factor/m_gridSizes.z, factor/m_gridSizes.w);
		
		m_fourier = new FourierGPU(m_size);
		
		CreateRenderTextures(ansio);
		
		Shader initSpectrumShader = Shader.Find("Ocean/InitSpectrum");
		if(initSpectrumShader == null) Debug.Log("WaveSpectrumGPU::WaveSpectrumGPU - Could not find shader Ocean/InitSpectrum");
		m_initSpectrumMat = new Material(initSpectrumShader);
		
		m_initSpectrumMat.SetVector("_Offset", m_offset);
		m_initSpectrumMat.SetVector("_InverseGridSizes", m_inverseGridSizes);
				
	}
	
	public void Release()
	{
		m_map0.Release();
		m_map1.Release();
		m_map2.Release();
		
		m_spectrum01.Release();
		m_spectrum23.Release();
		
		//m_WTable.Release();

		for(int i = 0; i < 2; i++)
		{
			m_fourierBuffer0[i].Release();
			m_fourierBuffer1[i].Release();
			m_fourierBuffer2[i].Release();
		}
	}
	
	public bool IsCreated()
	{
		//Sometimes Unity will mark render textures as not created when they have been
		//This will check for that. These 3 textures are the important ones.
		if(!m_spectrum01.IsCreated()) return false;
		if(!m_spectrum23.IsCreated()) return false;
		//if(!m_WTable.IsCreated()) return false;
		
		return true;
	}
	
	public void Init()
	{
		//Creates all the data needed to generate the waves.
		//This is not called in the constructor because greater control
		//over when exactly this data is created is needed.
		GenerateWavesSpectrum();
		//CreateWTable();
		
		m_initSpectrumMat.SetTexture("_Spectrum01", m_spectrum01);
		m_initSpectrumMat.SetTexture("_Spectrum23", m_spectrum23);
		//m_initSpectrumMat.SetTexture("_WTable", m_WTable);
		
		m_isInit = true;
		
	}
	
	void CreateRenderTextures(int ansio)
	{
		RenderTextureFormat mapFormat = RenderTextureFormat.ARGBFloat;
		RenderTextureFormat format = RenderTextureFormat.ARGBFloat;
		
		//These texture hold the actual data use in the ocean renderer
		m_map0 = new RenderTexture(m_size, m_size, 0, mapFormat);
		m_map0.filterMode = FilterMode.Trilinear;
		m_map0.wrapMode = TextureWrapMode.Repeat;
		m_map0.anisoLevel = ansio;
		m_map0.useMipMap = true;
		m_map0.Create();

		m_map1 = new RenderTexture(m_size, m_size, 0, mapFormat);
		m_map1.filterMode = FilterMode.Trilinear;
		m_map1.wrapMode = TextureWrapMode.Repeat;
		m_map1.anisoLevel = ansio;
		m_map1.useMipMap = true;
		m_map1.Create();
		
		m_map2 = new RenderTexture(m_size, m_size, 0, mapFormat);
		m_map2.filterMode = FilterMode.Trilinear;
		m_map2.wrapMode = TextureWrapMode.Repeat;
		m_map2.anisoLevel = ansio;
		m_map2.useMipMap = true;
		m_map2.Create();
		
		//These textures are used to perform the fourier transform
		m_fourierBuffer0 = new RenderTexture[2];
		m_fourierBuffer1 = new RenderTexture[2];
		m_fourierBuffer2 = new RenderTexture[2];

		CreateBuffer(m_fourierBuffer0, format);
		CreateBuffer(m_fourierBuffer1, format);
		CreateBuffer(m_fourierBuffer2, format);
		
		//These textures hold the specturm the fourier transform is performed on
		m_spectrum01 = new RenderTexture(m_size, m_size, 0, format);
		m_spectrum01.filterMode = FilterMode.Point;
		m_spectrum01.wrapMode = TextureWrapMode.Repeat;
		m_spectrum01.Create();
		
		m_spectrum23 = new RenderTexture(m_size, m_size, 0, format);
		m_spectrum23.filterMode = FilterMode.Point;
		m_spectrum23.wrapMode = TextureWrapMode.Repeat;	
		m_spectrum23.Create();

	}
	
	void CreateBuffer(RenderTexture[] tex, RenderTextureFormat format)
	{
		for(int i = 0; i < 2; i++)
		{
			tex[i] = new RenderTexture(m_size, m_size, 0, format);
			tex[i].filterMode = FilterMode.Point;
			tex[i].wrapMode = TextureWrapMode.Clamp;
			tex[i].Create();
		}
	}

	float Spectrum(float kx, float ky, bool omnispectrum = false)
	{
		float k = Mathf.Sqrt(kx * kx + ky * ky);
		
		return 1.0f / 255f / (k*k);
	}
	
	Vector2 GetSpectrumSample(float i, float j, float lengthScale, float kMin)
	{
		float dk = 2.0f * Mathf.PI / lengthScale;
		float kx = i * dk;
		float ky = j * dk;
		Vector2 result = new Vector2(0.0f,0.0f);
		
		float rnd = Random.value;
		
		if(Mathf.Abs(kx) >= kMin || Mathf.Abs(ky) >= kMin)
		{
			float S = Spectrum(kx, ky);
			float h = Mathf.Sqrt(S / 2.0f) * dk;
						
			float phi = rnd * 2.0f * Mathf.PI;
			result.x = h * Mathf.Cos(phi);
			result.y = h * Mathf.Sin(phi);
		}
		
		return result;
	}
	
	void GenerateWavesSpectrum()
	{
		
		if(!m_isInit)
		{
			// Slope variance due to all waves, by integrating over the full spectrum.
		
			float k = 5e-3f;
			while (k < 1e3f) 
			{
				float nextK = k * 1.001f;
				k = nextK;
			}
	
			m_spectrum01Data = new float[m_size*m_size*4];
			m_spectrum23Data = new float[m_size*m_size*4];
	
			int idx;
			float i;
			float j;

			Vector2 sample12XY;
			Vector2 sample12ZW;
			Vector2 sample34XY;
			Vector2 sample34ZW;
			
			Random.seed = 0;
			
			for (int x = 0; x < m_size; x++) 
			{
				for (int y = 0; y < m_size; y++) 
				{
					idx = x+y*m_size;
					i = (x >= m_size / 2) ? (float)(x - m_size) : (float)x;
					j = (y >= m_size / 2) ? (float)(y - m_size) : (float)y;
		
					sample12XY = GetSpectrumSample(i, j, m_gridSizes.x, Mathf.PI / m_gridSizes.x);
					sample12ZW = GetSpectrumSample(i, j, m_gridSizes.y, Mathf.PI * m_fsize / m_gridSizes.x);
					sample34XY = GetSpectrumSample(i, j, m_gridSizes.z, Mathf.PI * m_fsize / m_gridSizes.y);
					sample34ZW = GetSpectrumSample(i, j, m_gridSizes.w, Mathf.PI * m_fsize / m_gridSizes.z);
	
					m_spectrum01Data[idx*4+0] = sample12XY.x;
					m_spectrum01Data[idx*4+1] = sample12XY.y;
					m_spectrum01Data[idx*4+2] = sample12ZW.x;
					m_spectrum01Data[idx*4+3] = sample12ZW.y;
					
					m_spectrum23Data[idx*4+0] = sample34XY.x;
					m_spectrum23Data[idx*4+1] = sample34XY.y;
					m_spectrum23Data[idx*4+2] = sample34ZW.x;
					m_spectrum23Data[idx*4+3] = sample34ZW.y;
					
					i *= 2.0f * Mathf.PI;
					j *= 2.0f * Mathf.PI;

				}
			}
			//Write floating point data into render texture
			EncodeFloat.WriteIntoRenderTexture(m_spectrum01, 4, m_spectrum01Data);
			EncodeFloat.WriteIntoRenderTexture(m_spectrum23, 4, m_spectrum23Data);

		}
	
		//Write floating point data into render texture
		EncodeFloat.WriteIntoRenderTexture(m_spectrum01, 4, m_spectrum01Data);
		EncodeFloat.WriteIntoRenderTexture(m_spectrum23, 4, m_spectrum23Data);
	
	}

		
	void InitWaveSpectrum(float t)
	{
		RenderTexture[] buffers = new RenderTexture[]{ m_fourierBuffer0[1], m_fourierBuffer1[1], m_fourierBuffer2[1] };

		m_initSpectrumMat.SetFloat("_T", t);
		
		RTUtility.MultiTargetBlit(buffers, m_initSpectrumMat);
	}
	
	public void SimulateWaves(float t)
	{
		
		InitWaveSpectrum(t);

		m_idx = m_fourier.PeformFFT(m_fourierBuffer0, m_fourierBuffer1,m_fourierBuffer2);
		
		//Copy the contents of the completed fourier transform to the map textures.
		//Graphics Blit is needed as ReadPixels is not able to copy from one render texture to another at time of writting
		//You could just use the buffer textures (m_fourierBuffer0,1,2) to read from for the ocean shader but they need to have mipmaps and unity updates the mipmaps
		//every time the texture is renderer into. This impacts performance during fourier transform stage as mipmaps would be updated every pass
		//and there is no way to disable and then enable mipmaps on render textures at time of writting.
	
		Graphics.Blit(m_fourierBuffer0[m_idx], m_map0);
		Graphics.Blit(m_fourierBuffer1[m_idx], m_map1);
		Graphics.Blit(m_fourierBuffer2[m_idx], m_map2);
			
	}
	
}

