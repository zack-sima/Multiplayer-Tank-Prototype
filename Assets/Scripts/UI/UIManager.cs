using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour {
	public static UIManager instance;

	//TODO: temporary
	public TMP_Text tanksText;

	private void Awake() {
		instance = this;
	}
}
