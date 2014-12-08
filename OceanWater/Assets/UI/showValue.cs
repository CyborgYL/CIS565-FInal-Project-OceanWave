using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class showValue : MonoBehaviour {
	public float value;
	Text text;
	// Use this for initialization
	void Start () {
		text = gameObject.GetComponent<Text> ();
	}
	
	// Update is called once per frame
	void Update () {
		text.text = value.ToString();
	}
}
