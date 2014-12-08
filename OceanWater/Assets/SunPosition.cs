using UnityEngine;
using System.Collections;

public class SunPosition : MonoBehaviour {
	Vector3 centerPos;
	float radius = 2000f;
	Vector3 direction;
	// Use this for initialization
	void Start () {

	}
	
	// Update is called once per frame
	void Update () {
		centerPos = Camera.main.transform.position;
		centerPos.z = 0.0f;
		direction = - transform.forward;
		transform.position = radius * direction;
	}
	public void UpdateAngle(float angle){
		transform.eulerAngles = new Vector3 (angle, 0f, 0f);
	}
}
