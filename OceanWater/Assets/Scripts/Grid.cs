using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Grid : MonoBehaviour {
	
	GameObject m_gridWireframe;
	GameObject m_grid;
	public Material m_wireframeMat;
	public Material m_oceanMat;
	WaveSpectrum m_waves;
	public float m_windSpeed = 8.0f; 
	public float m_waveAmp = 1.0f; 
	public float m_inverseWaveAge = 0.5f; 
	public float m_seaLevel = 0.0f;
	public int m_fourierGridSize = 128; 
	public int m_ansio = 2; 
	public float m_lodFadeDist = 2000.0f; 
	public Vector4 m_gridSizes = new Vector4(6000, 360, 30, 2);
	int m_frameCount = 0;
	public float bias = 2.0f;
	public Color m_seaColor = new Color(10.0f / 255.0f, 10.0f / 255.0f, 120.0f / 255.0f, 1.0f);
	public float m_sunIntensity = 100.0f;
	public GameObject sun;

	private float fresnel_on = 0.0f;
	private float refl_on = 1.0f;
	private float refr_on = 1.0f;
	private float sun_on = 1.0f;
	private float white_on = 1.0f;

	public float m_whiteCapStr = 0.6f;

	public Slider s_windSpeed, s_amp, s_freq, s_wc, s_intensity;
	private float o_windSpeed, o_amp, o_freq, o_wc, o_intensity;

	public bool released = false;
	bool needUpdate = false;
	Mesh GenerateGridMesh(int lenX, int lenY)
	{
		
		Vector3[] vertices = new Vector3[lenX*lenY];
		Vector3[] normals = new Vector3[lenX*lenY];
		Vector2[] texcoords = new Vector2[lenX*lenY];

		//vertices pos and normal
		for(int x = 0; x < lenX; x++)
		{
			for(int y = 0; y < lenY; y++)
			{
				float r = Mathf.Pow(x /(float)lenX,bias) * 2.0f;	
				float angle =  Mathf.PI*2.0f * y /(float)(lenY-1);
				vertices[y*lenX+x].x = r * Mathf.Cos(angle);
				vertices[y*lenX+x].y = 0.0f;
				vertices[y*lenX+x].z = r * Mathf.Sin(angle);
				normals[y*lenX+x] = new Vector3(0,1,0);
			}
		}

		//indices
		int[] indices = new int[lenX*lenY*6];
		int index = 0;
		for(int x = 0; x<lenX-1; x++)
		{

			for(int y = 0; y<lenY-1; y++)
			{
				indices[index++] = x + y * lenX;
				indices[index++] = x + (y+1) * lenX;
				indices[index++] = (x+1) + y * lenX;
				
				indices[index++] = x + (y+1) * lenX;
				indices[index++] = (x+1) + (y+1) * lenX;
				indices[index++] = (x+1) + y * lenX;			
			}
		}
		//generate mesh
		Mesh mesh = new Mesh();
		mesh.vertices = vertices;
		mesh.uv = texcoords;
		mesh.normals = normals;
		mesh.triangles = indices;
		return mesh;
	}

	// Use this for initialization
	void Start () {
		o_windSpeed = m_windSpeed;
		o_amp = m_waveAmp;
		o_freq = m_inverseWaveAge;
		o_wc = m_whiteCapStr;
		o_intensity = m_sunIntensity;
		Init ();
	}

	void Init(){
		m_waves = new WaveSpectrum(m_fourierGridSize, m_windSpeed, m_waveAmp, m_inverseWaveAge, m_ansio, m_gridSizes);
		float far = Camera.main.farClipPlane;
		Mesh mesh = GenerateGridMesh (128,128);
		
		/*m_gridWireframe = new GameObject();
		m_gridWireframe.AddComponent<MeshFilter>();
		m_gridWireframe.AddComponent<MeshRenderer>();
		m_gridWireframe.renderer.material = m_wireframeMat;
		m_gridWireframe.GetComponent<MeshFilter>().mesh = mesh;
		m_gridWireframe.transform.localScale = new Vector3(far,1,far);
		m_gridWireframe.layer = 8;*/
		
		m_wireframeMat.SetVector("size", m_waves.GetGridSizes());
		m_wireframeMat.SetFloat("LOD_num", m_waves.GetMipMapLevels());
		
		m_grid = new GameObject("Ocean Grid");
		m_grid.AddComponent<MeshFilter>();
		m_grid.AddComponent<MeshRenderer>();
		m_grid.renderer.material = m_oceanMat;
		m_grid.GetComponent<MeshFilter>().mesh = mesh;
		m_grid.transform.localScale = new Vector3(far,1,far);
		
		
		m_oceanMat.SetTexture("variance_map", m_waves.GetVariance());
		Debug.Log (m_waves.GetGridSizes ());
		m_oceanMat.SetVector("size", m_waves.GetGridSizes());
		m_oceanMat.SetFloat("_MaxLod", m_waves.GetMipMapLevels());

	}
	public void UpdateWave(){
		needUpdate = true;
		m_windSpeed = s_windSpeed.value;
		m_waveAmp = s_amp.value;
		m_inverseWaveAge = s_freq.value;
		m_whiteCapStr = s_wc.value;
		m_sunIntensity = s_intensity.value;
	}
	public void ResetValues(){
		s_windSpeed.value = o_windSpeed;
		s_amp.value = o_amp;
		s_freq.value = o_freq;
		s_wc.value = o_wc;
		s_intensity.value = o_intensity;
		UpdateWave ();
	}
	// Update is called once per frame
	void Update () {
		if(m_frameCount == 1)
			m_waves.Init();
		if(released){
			Init();
			m_waves.Init();
			released = false;
			needUpdate = false;
		}
		else if(needUpdate && !UIControl.instance.holdingMouse){
			m_waves.Release ();
			released = true;
			return;
		}

		m_frameCount++;
		m_waves.SimulateWaves(Time.realtimeSinceStartup);
		m_wireframeMat.SetTexture("map", m_waves.GetMap0());
		m_wireframeMat.SetFloat("far", m_lodFadeDist);

		Vector3 pos = Camera.main.transform.position;
		pos.y = m_seaLevel;
		Vector3 sunDir = - sun.transform.forward;
		sunDir = sunDir.normalized;
		//Update shader values that may change every frame
		m_oceanMat.SetTexture("map", m_waves.GetMap0());
		m_oceanMat.SetTexture("slop_map0", m_waves.GetMap1());
		m_oceanMat.SetTexture("slop_map1", m_waves.GetMap2());
		m_oceanMat.SetFloat("far", m_lodFadeDist);
		m_oceanMat.SetColor("sea_color", m_seaColor);
		m_oceanMat.SetVector("max_var", m_waves.GetVarianceMax());
		m_oceanMat.SetVector("sun_dir", sunDir);
		m_oceanMat.SetFloat("intensity", m_sunIntensity);
		m_grid.transform.localPosition = pos;
		//m_gridWireframe.transform.localPosition = pos;
		m_oceanMat.SetFloat("fresnel_on", fresnel_on);
		m_oceanMat.SetFloat("refl_on", refl_on);
		m_oceanMat.SetFloat("refr_on", refr_on);
		m_oceanMat.SetFloat("sun_on", sun_on);
		m_oceanMat.SetFloat("white_on", white_on);

		m_oceanMat.SetVector("_Choppyness", m_waves.GetChoppyness());
		m_oceanMat.SetTexture("_Map3", m_waves.GetMap3());
		m_oceanMat.SetTexture("_Map4", m_waves.GetMap4());
		m_oceanMat.SetTexture("_Foam0", m_waves.GetFoam0());
		m_oceanMat.SetTexture("_Foam1", m_waves.GetFoam1());
		m_oceanMat.SetFloat("_WhiteCapStr", m_whiteCapStr);
		if(Input.GetKeyDown(KeyCode.Z)){
			fresnel_on=1.0f-fresnel_on;
		}
		if(Input.GetKeyDown(KeyCode.X)){
			refl_on=1.0f-refl_on;
		}
		if(Input.GetKeyDown(KeyCode.C)){
			refr_on=1.0f-refr_on;
		}
		if(Input.GetKeyDown(KeyCode.V)){
			sun_on=1.0f-sun_on;
		}
		if(Input.GetKeyDown(KeyCode.B)){
			white_on=1.0f-white_on;
		}
	}
	public void Toggle_fresnel(bool toggle){
		fresnel_on = toggle ? 1f : 0f;
	}
	public void Toggle_refl(bool toggle){
		refl_on = toggle ? 1f : 0f;
	}
	public void Toggle_refr(bool toggle){
		refr_on = toggle ? 1f : 0f;
	}
	public void Toggle_sun(bool toggle){
		sun_on = toggle ? 1f : 0f;
	}
	public void Toggle_white(bool toggle){
		white_on = toggle ? 1f : 0f;
	}

	void OnDestroy()
	{
		m_waves.Release();	
	}
}
