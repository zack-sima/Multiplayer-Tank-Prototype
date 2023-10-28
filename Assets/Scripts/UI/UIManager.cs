using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour {
	public static UIManager instance;

	public Camera mainCamera;
	public Image gunnerSights, driverSights;

	//TODO: temporary
	public TMP_Text tanksText;
	public TMP_Text playerJoinText;

	private void Awake() {
		instance = this;
	}
}
