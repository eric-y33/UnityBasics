using UnityEngine;
using TMPro;

public class Resolution : MonoBehaviour {

	[SerializeField]
	TextMeshProUGUI display;

    void Update () {
		int res = GPUGraph.Resolution;
		display.SetText("Resolution \n{0:0}", res);
	}

}