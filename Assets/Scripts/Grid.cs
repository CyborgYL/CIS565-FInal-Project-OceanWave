using UnityEngine;
using System.Collections;

public class Grid : MonoBehaviour {
	
	GameObject m_gridWireframe;
	public Material m_wireframeMat;
	WaveSpectrum m_waves;
	public float m_windSpeed = 108.0f; //A higher wind speed gives greater swell to the waves
	public float m_waveAmp = 20.0f; //Scales the height of the waves
	public float m_inverseWaveAge = 1.84f; //A lower number means the waves last longer and will build up larger waves
	public float m_seaLevel = 0.0f;
	public int m_fourierGridSize = 128; //Fourier grid size.\
	public int m_ansio = 2; //Ansiotrophic filtering on wave textures
	public float m_lodFadeDist = 2000.0f; 
	public Vector4 m_gridSizes = new Vector4(5488, 392, 28, 2);
	int m_frameCount = 0;
	public float bias = 2.0f;

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
				float r = Mathf.Pow(x /(float)lenX,bias);	
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

		m_waves = new WaveSpectrum(m_fourierGridSize, m_windSpeed, m_waveAmp, m_inverseWaveAge, m_ansio, m_gridSizes);
		float far = Camera.main.farClipPlane;
		Mesh mesh = GenerateGridMesh (128,128);

		m_gridWireframe = new GameObject();
		m_gridWireframe.AddComponent<MeshFilter>();
		m_gridWireframe.AddComponent<MeshRenderer>();
		m_gridWireframe.renderer.material = m_wireframeMat;
		m_gridWireframe.GetComponent<MeshFilter>().mesh = mesh;
		m_gridWireframe.transform.localScale = new Vector3(far,1,far);
		m_gridWireframe.layer = 8;

		m_wireframeMat.SetVector("size", m_waves.GetGridSizes());
		m_wireframeMat.SetFloat("LOD_num", m_waves.GetMipMapLevels());

	}
	
	// Update is called once per frame
	void Update () {
		if(m_frameCount == 1)
			m_waves.Init();

		m_frameCount++;
		m_waves.SimulateWaves(Time.realtimeSinceStartup);
		m_wireframeMat.SetTexture("map", m_waves.GetMap2());
		m_wireframeMat.SetFloat("far", m_lodFadeDist);

		Vector3 pos = Camera.main.transform.position;
		pos.y = m_seaLevel;

		m_gridWireframe.transform.localPosition = pos;
	}

	void OnDestroy()
	{
		m_waves.Release();	
	}
}
