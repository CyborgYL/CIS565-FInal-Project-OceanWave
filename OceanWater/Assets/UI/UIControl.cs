using UnityEngine;
using System.Collections;

public class UIControl : MonoBehaviour {
	private static UIControl _instance;
	public static UIControl instance{
		get{
			if(_instance == null)
			{
				_instance = GameObject.FindObjectOfType<UIControl>();
			}
			return _instance;
		}
	}
	void Awake() 
	{
		if(_instance == null)
		{
			//If I am the first instance, make me the Singleton
			_instance = this;
		}
		else
		{
			//If a Singleton already exists and you find
			//another reference in scene, destroy it!
			if(this != _instance)
				Destroy(this.gameObject);
		}
	}

	public bool inMenu = false;
	public GameObject canvas;
	public bool holdingMouse = false;

	MouseLook mouseLook;
	FlyCam flyCam;
	// Use this for initialization
	void Start () {
		mouseLook = Camera.main.gameObject.GetComponent<MouseLook> ();
		flyCam = Camera.main.gameObject.GetComponent<FlyCam> ();
		if(canvas == null)
			canvas = GameObject.Find("Canvas");	
	}
	
	// Update is called once per frame
	void Update () {
		if(Input.GetMouseButton(0)){
			holdingMouse = true;
		}
		else
			holdingMouse = false;
		if(Input.GetKeyDown(KeyCode.F)){
			inMenu = !inMenu;
		}
		if(inMenu){
			mouseLook.enabled = false;
			flyCam.enabled = false;
			canvas.SetActive(true);
			Screen.lockCursor = false;
		}
		else{
			mouseLook.enabled = true;
			flyCam.enabled = true;
			canvas.SetActive(false);
			Screen.lockCursor = true;
		}
	}
}
