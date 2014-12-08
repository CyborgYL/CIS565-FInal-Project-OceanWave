using UnityEngine;
using System.Collections;

public class WaveSpectrum
{

	int m_idx = 0;
	int m_size = 128; //This is the fourier transform size, must pow2 number. Recommend no higher or lower than 64, 128 or 256.
	float m_fsize;
	//Constants from the paper
	const float c_CM = 0.23f;	// Eq 59
	const float c_KM = 370.0f;	// Eq 59

	//These 3 settings can be used to control the look of the waves from rough seas to calm lakes.
	//WARNING - not all combinations of numbers makes sense and the waves will not always look correct.
	float m_windSpeed = 25.0f; //A higher wind speed gives greater swell to the waves
	float m_waveAmp = 2.0f; //Scales the height of the waves
	float m_omega = 20.00f; //A lower number means the waves last longer and will build up larger waves
	
	float m_mipMapLevels;
	Vector4 m_offset;
	Vector4 m_gridSizes = new Vector4(5488, 392, 28, 2);
	Vector4 m_inverseGridSizes;
	Vector2 m_varianceMax;
	Texture3D m_variance;
	Vector4 m_choppyness = new Vector4(2.3f, 2.1f, 1.3f, 0.9f);
	Material m_initSpectrumMat, m_initDisplacementMat, m_initJacobiansMat, m_whiteCapsPrecomputeMat;
	bool m_isInit = false;
	RenderTexture m_spectrum01, m_spectrum23;
	RenderTexture[] m_fourierBuffer0, m_fourierBuffer1, m_fourierBuffer2, m_fourierBuffer3, m_fourierBuffer4, m_fourierBuffer5, m_fourierBuffer6, m_fourierBuffer7;
	RenderTexture m_map0, m_map1, m_map2, m_map3, m_map4, m_map5, m_map6;
	RenderTexture waveTable, m_foam0, m_foam1;
	FourierGPU m_fourier;
	float[] m_spectrum01Data, m_spectrum23Data, m_WTableData;
	ComputeShader m_varianceShader;
	ComputeShader m_writeData;

	public Vector4 GetGridSizes() { return m_gridSizes; }
	public RenderTexture GetMap0() { return m_map0; }
	public RenderTexture GetMap1() { return m_map1; }
	public RenderTexture GetMap2() { return m_map2; }
	public RenderTexture GetMap3() { return m_map3; }
	public RenderTexture GetMap4() { return m_map4; }
	public RenderTexture GetFoam0() { return m_foam0; }
	public RenderTexture GetFoam1() { return m_foam1; }
	public Vector2 GetVarianceMax() { return m_varianceMax; }
	public float GetMipMapLevels() { return m_mipMapLevels; }
	public Texture3D GetVariance() { return m_variance; }
	public Vector4 GetChoppyness() { return m_choppyness; }
	int m_varianceSize = 4;

	public WaveSpectrum(int size, float windSpeed, float waveAmp, float omega, int ansio, Vector4 gridSizes)
	{	
		if(size > 256)
		{
			//Fourrier size maximum is 256.
			size = 256;
		}
		
		if(!Mathf.IsPowerOfTwo(size))
		{
			//Fourier buffer must be power of 2.
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
		if(initSpectrumShader == null) Debug.Log("WaveSpectrum - Could not find shader Ocean/InitSpectrum");
		m_initSpectrumMat = new Material(initSpectrumShader);

		Shader initDisplacementShader = Shader.Find("Ocean/InitDisplacement");
		if(initDisplacementShader == null) Debug.Log("WaveSpectrum - Could not find shader Ocean/InitDisplacement");
		m_initDisplacementMat = new Material(initDisplacementShader);
		
		Shader initJacobiansShader = Shader.Find("Ocean/InitJacobians");
		if(initJacobiansShader == null) Debug.Log("WaveSpectrum - Could not find shader Ocean/InitJacobians");
		m_initJacobiansMat = new Material(initJacobiansShader);
		
		Shader whiteCapsPrecomputeShader = Shader.Find("Ocean/WhiteCapsPrecompute");
		if(whiteCapsPrecomputeShader == null) Debug.Log("WaveSpectrum - Could not find shader Ocean/WhiteCapsPrecompute");
		m_whiteCapsPrecomputeMat = new Material(whiteCapsPrecomputeShader);

		m_initSpectrumMat.SetVector("_Offset", m_offset);
		m_initSpectrumMat.SetVector("_InverseGridSizes", m_inverseGridSizes);

		m_initDisplacementMat.SetVector("_InverseGridSizes", m_inverseGridSizes);
		
		m_initJacobiansMat.SetTexture("_Spectrum01", m_spectrum01);
		m_initJacobiansMat.SetTexture("_Spectrum23", m_spectrum23);
		m_initJacobiansMat.SetTexture("_WTable", waveTable);
		m_initJacobiansMat.SetVector("_Offset", m_offset);
		m_initJacobiansMat.SetVector("_InverseGridSizes", m_inverseGridSizes);
	}

	public bool IsCreated()
	{
		//Sometimes Unity will mark render textures as not created when they have been
		//This will check for that. These 3 textures are the important ones.
		if(!m_spectrum01.IsCreated()) return false;
		if(!m_spectrum23.IsCreated()) return false;
		if(!waveTable.IsCreated()) return false;
		
		return true;
	}
	
	public void Init()
	{
		//Creates all the data needed to generate the waves.
		//This is not called in the constructor because greater control
		//over when exactly this data is created is needed.
		GenerateWavesSpectrum();
		CreateWaveTable();
		
		m_initSpectrumMat.SetTexture("_Spectrum01", m_spectrum01);
		m_initSpectrumMat.SetTexture("_Spectrum23", m_spectrum23);
		m_initSpectrumMat.SetTexture("_WTable", waveTable);
		
		m_isInit = true;
		
	}
	public void Release()
	{
		m_map0.Release();
		m_map1.Release();
		m_map2.Release();
		m_map3.Release();
		m_map4.Release();
		m_spectrum01.Release();
		m_spectrum23.Release();
		waveTable.Release();
		m_foam0.Release();
		m_foam1.Release();
		for(int i = 0; i < 2; i++)
		{
			m_fourierBuffer0[i].Release();
			m_fourierBuffer1[i].Release();
			m_fourierBuffer2[i].Release();
			m_fourierBuffer3[i].Release();
			m_fourierBuffer4[i].Release();
			m_fourierBuffer5[i].Release();
			m_fourierBuffer6[i].Release();
			m_fourierBuffer7[i].Release();
		}
	}
	void CreateWaveTable()
	{
		//Some values need for the InitWaveSpectrum function can be precomputed
		Vector2 uv, st;
		float k1, k2, k3, k4, w1, w2, w3, w4;
		
		float[] table = new float[m_size*m_size*4];
		
		for (int x = 0; x < m_size; x++) 
		{
			for (int y = 0; y < m_size; y++) 
			{
				uv = new Vector2(x,y) / m_fsize;
				
				st.x = uv.x > 0.5f ? uv.x - 1.0f : uv.x;
				st.y = uv.y > 0.5f ? uv.y - 1.0f : uv.y;
				
				k1 = (st * m_inverseGridSizes.x).magnitude;
				k2 = (st * m_inverseGridSizes.y).magnitude;
				k3 = (st * m_inverseGridSizes.z).magnitude;
				k4 = (st * m_inverseGridSizes.w).magnitude;
				float fract = c_KM * c_KM;
				w1 = Mathf.Sqrt(9.81f * k1 * (1.0f + k1 * k1 / fract));
				w2 = Mathf.Sqrt(9.81f * k2 * (1.0f + k2 * k2 / fract));
				w3 = Mathf.Sqrt(9.81f * k3 * (1.0f + k3 * k3 / fract));
				w4 = Mathf.Sqrt(9.81f * k4 * (1.0f + k4 * k4 / fract));
				
				table[(x+y*m_size)*4+0] = w1;
				table[(x+y*m_size)*4+1] = w2;
				table[(x+y*m_size)*4+2] = w3;
				table[(x+y*m_size)*4+3] = w4;
				
			}
		}	
		//Write floating point data into render texture
		EncodeFloat.WriteIntoRenderTexture(waveTable, 4, table);
		
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

		m_map3 = new RenderTexture(m_size, m_size, 0, mapFormat);
		m_map3.filterMode = FilterMode.Trilinear;
		m_map3.wrapMode = TextureWrapMode.Repeat;
		m_map3.anisoLevel = ansio;
		m_map3.useMipMap = true;
		m_map3.Create();

		m_map4 = new RenderTexture(m_size, m_size, 0, mapFormat);
		m_map4.filterMode = FilterMode.Trilinear;
		m_map4.wrapMode = TextureWrapMode.Repeat;
		m_map4.anisoLevel = ansio;
		m_map4.useMipMap = true;
		m_map4.Create();

		m_foam0 = new RenderTexture(m_size, m_size, 0, format);
		m_foam0.filterMode = FilterMode.Trilinear;
		m_foam0.wrapMode = TextureWrapMode.Repeat;
		m_foam0.anisoLevel = ansio;
		m_foam0.useMipMap = false;
		m_foam0.Create();

		m_foam1 = new RenderTexture(m_size, m_size, 0, format);
		m_foam1.filterMode = FilterMode.Trilinear;
		m_foam1.wrapMode = TextureWrapMode.Repeat;
		m_foam1.anisoLevel = ansio;
		m_foam1.useMipMap = false;
		m_foam1.Create();

		//These textures are used to perform the fourier transform
		m_fourierBuffer0 = new RenderTexture[2];
		m_fourierBuffer1 = new RenderTexture[2];
		m_fourierBuffer2 = new RenderTexture[2];

		CreateBuffer(ref m_fourierBuffer0, format);//heights
		CreateBuffer(ref m_fourierBuffer1, format);// slopes X
		CreateBuffer(ref m_fourierBuffer2, format);// slopes Y
		CreateBuffer(ref m_fourierBuffer3, format);// displacement X
		CreateBuffer(ref m_fourierBuffer4, format);// displacement Y
		CreateBuffer(ref m_fourierBuffer5, format);// Jacobians XX
		CreateBuffer(ref m_fourierBuffer6, format);// Jacobians YY
		CreateBuffer(ref m_fourierBuffer7, format);// Jacobians XY

		//These textures hold the specturm the fourier transform is performed on
		m_spectrum01 = new RenderTexture(m_size, m_size, 0, format);
		m_spectrum01.enableRandomWrite = true;
		m_spectrum01.wrapMode = TextureWrapMode.Repeat;
		m_spectrum01.filterMode = FilterMode.Point;
		m_spectrum01.Create();
		
		m_spectrum23 = new RenderTexture(m_size, m_size, 0, format);
		m_spectrum23.enableRandomWrite = true;
		m_spectrum23.wrapMode = TextureWrapMode.Repeat;	
		m_spectrum23.filterMode = FilterMode.Point;
		m_spectrum23.Create();

		waveTable = new RenderTexture(m_size, m_size, 0, format);
		waveTable.wrapMode = TextureWrapMode.Clamp;
		waveTable.filterMode = FilterMode.Point;
		waveTable.Create();

		m_variance = new Texture3D(m_varianceSize, m_varianceSize, m_varianceSize, TextureFormat.ARGB32, true);
		m_variance.filterMode = FilterMode.Bilinear;
		m_variance.wrapMode = TextureWrapMode.Clamp;
	}
	
	void CreateBuffer(ref RenderTexture[] tex, RenderTextureFormat format)
	{
		tex = new RenderTexture[2];
		for(int i = 0; i < 2; ++i)
		{
			tex[i] = new RenderTexture(m_size, m_size, 0, format);
			tex[i].wrapMode = TextureWrapMode.Clamp;
			tex[i].filterMode = FilterMode.Point;
			tex[i].Create();
		}
	}
	float sqr(float x){
		return x * x;
	}
	float omega(float k) { // Eq 24
		return Mathf.Sqrt(k * (1.0f + sqr(k / c_KM)) * 9.81f); 
	} 
	float Spectrum(float kx, float ky, bool omnispectrum = false)
	{
//		float k = Mathf.Sqrt(kx * kx + ky * ky);
//		return 1.0f / 255f / (k*k);
		float U10 = m_windSpeed;

		//spectral peak
		float kp = sqr(m_omega / U10) * 9.81f; // after Eq 3
		float cp = omega(kp) / kp;

		//phase speed
		float k = Mathf.Sqrt(kx * kx + ky * ky);
		float c = omega(k) / k;

		float Lpm = Mathf.Exp(- 5.0f / 4.0f * sqr(kp / k)); // after Eq 3
		float sigma = (1.0f + 4.0f / Mathf.Pow(m_omega, 3.0f)) * 0.08f; // after Eq 3
		float gamma = (m_omega < 1.0f) ? 1.7f : 1.7f + 6.0f * Mathf.Log10(m_omega); // after Eq 3
		float Gamma = Mathf.Exp(-1.0f / (2.0f * sqr(sigma)) * sqr(Mathf.Sqrt(k / kp) - 1.0f));
		float Jp = Mathf.Pow(gamma, Gamma); // Eq 3

		float Fp = Lpm * Jp * Mathf.Exp(-m_omega / Mathf.Sqrt(10.0f) * (Mathf.Sqrt(k / kp) - 1.0f)); // Eq 32
		float alphap = Mathf.Sqrt(m_omega) * 0.006f; // Eq 34
		float Bl = 0.5f * alphap * cp / c * Fp; // Eq 31

		//friction velocity
		float z0 = 3.7e-5f * sqr(U10) / 9.81f * Mathf.Pow(U10 / cp, 0.9f); // Eq 66
		float u_star = 0.41f * U10 / Mathf.Log(10.0f / z0); // Eq 60

		float Fm = Mathf.Exp(-0.25f * sqr(k / c_KM - 1.0f)); // Eq 41
		float Alpham = 0.01f * (u_star < c_CM ? 1.0f + Mathf.Log(u_star / c_CM) : 1.0f + 3.0f * Mathf.Log(u_star / c_CM)); // Eq 44
		float Bh = 0.5f * Alpham * c_CM / c * Fm * Lpm; // Eq 40 (fixed)

		float am = 0.13f * u_star / c_CM; // Eq 59
		float a0 = Mathf.Log(2.0f) / 4.0f; 
		float ap = 4.0f; 
		float Delta = (float)System.Math.Tanh(a0 + ap * Mathf.Pow(c / cp, 2.5f) + am * Mathf.Pow(c_CM / c, 2.5f)); // Eq 57
		
		float phi = Mathf.Atan2(ky, kx);
		if (kx < 0.0f) return 0.0f;
		return m_waveAmp * (Bl + Bh) * (1.0f + Delta * Mathf.Cos(2.0f * phi)) / (2.0f * Mathf.PI * sqr(sqr(k))); // Eq 67
	}
	
	Vector2 GetSpectrumSample(float i, float j, float lengthScale, float kMin)
	{
		
		float rnd = Random.value;
		float dk = 2.0f * Mathf.PI / lengthScale;
		float kx = i * dk;
		float ky = j * dk;
		Vector2 result = new Vector2(0.0f,0.0f);
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

	Vector2 ComputeVariance(float slopeVarianceDelta, float[] spectrum01, float[] spectrum23, float idxX, float idxY, float idxZ)
	{
		const float SCALE = 100.0f;
		Vector2 slopeVariances = new Vector2(slopeVarianceDelta, slopeVarianceDelta);
		float A = - 0.5f * Mathf.Pow(idxX / ((float)m_varianceSize - 1.0f), 4.0f) * SCALE;
		float C =  - 0.5f * Mathf.Pow(idxZ / ((float)m_varianceSize - 1.0f), 4.0f) * SCALE;
		float B =  - (2.0f * idxY / ((float)m_varianceSize - 1.0f) - 1.0f) * Mathf.Sqrt(A * C) * 2.0f;

		for (int x = 0; x < m_size; x++) 
		{
			for (int y = 0; y < m_size; y++)
			{
				int i = x >= m_fsize / 2.0f ? x - m_size : x;
				int j = y >= m_fsize / 2.0f ? y - m_size : y;
				
				Vector2 k = new Vector2(i, j) * 2.0f * Mathf.PI;
				
				slopeVariances += GetSlopeVariances(k / m_gridSizes.x, A, B, C, spectrum01[(x+y*m_size)*4+0], spectrum01[(x+y*m_size)*4+1]);
				slopeVariances += GetSlopeVariances(k / m_gridSizes.y, A, B, C, spectrum01[(x+y*m_size)*4+2], spectrum01[(x+y*m_size)*4+3]);
				slopeVariances += GetSlopeVariances(k / m_gridSizes.z, A, B, C, spectrum23[(x+y*m_size)*4+0], spectrum23[(x+y*m_size)*4+1]);
				slopeVariances += GetSlopeVariances(k / m_gridSizes.w, A, B, C, spectrum23[(x+y*m_size)*4+2], spectrum23[(x+y*m_size)*4+3]);
			}
		}
		
		return slopeVariances;
	}

	Vector2 GetSlopeVariances(Vector2 k, float A, float B, float C, float spectrumX, float spectrumY) 
	{
		float w = 1.0f - Mathf.Exp(A * k.x * k.x + B * k.x * k.y + C * k.y * k.y);
		return new Vector2((k.x * k.x) * w, (k.y * k.y) * w) * (spectrumX*spectrumX + spectrumY*spectrumY) * 2.0f;
	}

	void GenerateWavesSpectrum()
	{
		if(!m_isInit)
		{
			// Slope variance due to all waves, by integrating over the full spectrum.
			float theoreticSlopeVariance = 0.0f;
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
			float totalSlopeVariance = 0.0f;
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

			//Compute variance for the BRDF 
			
			float slopeVarianceDelta = 0.5f * (theoreticSlopeVariance - totalSlopeVariance);
			
			m_varianceMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
			
			Vector2[,,] variance32bit = new Vector2[m_varianceSize,m_varianceSize,m_varianceSize];
			Color[] variance8bit = new Color[m_varianceSize*m_varianceSize*m_varianceSize];
			
			for(int x = 0; x < m_varianceSize; x++)
			{
				for(int y = 0; y < m_varianceSize; y++)
				{
					for(int z = 0; z < m_varianceSize; z++)
					{
						variance32bit[x,y,z] = ComputeVariance(slopeVarianceDelta, m_spectrum01Data, m_spectrum23Data, x, y, z);
						
						if(variance32bit[x,y,z].x > m_varianceMax.x) m_varianceMax.x = variance32bit[x,y,z].x;
						if(variance32bit[x,y,z].y > m_varianceMax.y) m_varianceMax.y = variance32bit[x,y,z].y;
					}	
				}
			}
			
			for(int x = 0; x < m_varianceSize; x++)
			{
				for(int y = 0; y < m_varianceSize; y++)
				{
					for(int z = 0; z < m_varianceSize; z++)
					{
						idx = x+y*m_varianceSize+z*m_varianceSize*m_varianceSize;
						
						variance8bit[idx] = new Color( variance32bit[x,y,z].x / m_varianceMax.x, variance32bit[x,y,z].y / m_varianceMax.y, 0.0f, 1.0f);
					}
				}
			}
			
			m_variance.SetPixels(variance8bit);
			m_variance.Apply();
		}
	
		//Write floating point data into render texture
		EncodeFloat.WriteIntoRenderTexture(m_spectrum01, 4, m_spectrum01Data);
		EncodeFloat.WriteIntoRenderTexture(m_spectrum23, 4, m_spectrum23Data);
	

	}

		
	void InitWaveSpectrum(float t)
	{
		RenderTexture[] buffers = new RenderTexture[]{ m_fourierBuffer0[1], m_fourierBuffer1[1], m_fourierBuffer2[1] };
		m_initSpectrumMat.SetFloat("_T", t);
		m_initSpectrumMat.SetVector("_Offset", m_offset);
		m_initSpectrumMat.SetVector("_InverseGridSizes", m_inverseGridSizes);
		m_initSpectrumMat.SetTexture("_Spectrum01", m_spectrum01);
		m_initSpectrumMat.SetTexture("_Spectrum23", m_spectrum23);
		m_initSpectrumMat.SetTexture("_WTable", waveTable);
		RTUtility.MultiTargetBlit(buffers, m_initSpectrumMat);

		// Init displacement (3,4)
		RenderTexture[] buffers34 = new RenderTexture[] { m_fourierBuffer3[1], m_fourierBuffer4[1] };
		
		m_initDisplacementMat.SetTexture("_Buffer1", m_fourierBuffer1[1]);
		m_initDisplacementMat.SetTexture("_Buffer2", m_fourierBuffer2[1]);
		
		RTUtility.MultiTargetBlit(buffers34, m_initDisplacementMat);
		
		// Init jacobians (5,6,7)
		RenderTexture[] buffers567 = new RenderTexture[] { m_fourierBuffer5[1], m_fourierBuffer6[1], m_fourierBuffer7[1] };
		
		m_initJacobiansMat.SetFloat("_T", t/5);
		
		RTUtility.MultiTargetBlit(buffers567, m_initJacobiansMat);
	}
	
	public void SimulateWaves(float t)
	{
		
		InitWaveSpectrum(t);

		m_idx = m_fourier.PeformFFT(m_fourierBuffer0, m_fourierBuffer1,m_fourierBuffer2);
		m_fourier.PeformFFT(m_fourierBuffer3, m_fourierBuffer4);
		m_fourier.PeformFFT(m_fourierBuffer5, m_fourierBuffer6, m_fourierBuffer7);
	
		Graphics.Blit(m_fourierBuffer0[m_idx], m_map0);
		Graphics.Blit(m_fourierBuffer1[m_idx], m_map1);
		Graphics.Blit(m_fourierBuffer2[m_idx], m_map2);
		Graphics.Blit(m_fourierBuffer3[m_idx], m_map3);
		Graphics.Blit(m_fourierBuffer4[m_idx], m_map4);

		
		m_whiteCapsPrecomputeMat.SetTexture("_Map5", m_fourierBuffer5[m_idx]);
		m_whiteCapsPrecomputeMat.SetTexture("_Map6", m_fourierBuffer6[m_idx]);
		m_whiteCapsPrecomputeMat.SetTexture("_Map7", m_fourierBuffer7[m_idx]);
		m_whiteCapsPrecomputeMat.SetVector("_Choppyness", m_choppyness);
		
		RenderTexture[] buffers = new RenderTexture[] { m_foam0, m_foam1 };
		
		RTUtility.MultiTargetBlit(buffers, m_whiteCapsPrecomputeMat);
	}
	
}

