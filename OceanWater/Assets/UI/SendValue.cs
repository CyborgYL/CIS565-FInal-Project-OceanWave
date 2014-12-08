using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SendValue : MonoBehaviour {
	public Text text;
	Slider slider;
	// Use this for initialization
	void Start () {
		slider = gameObject.GetComponent<Slider> ();
	}
	
	// Update is called once per frame
	void Update () {
		text.text = slider.value.ToString();
	}
}
