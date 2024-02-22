using UnityEngine;

public class GPUGraph : MonoBehaviour {

	[SerializeField]
	ComputeShader computeShader;

	const int maxResolution = 1000;
    [SerializeField, Range(10, maxResolution)]
	int resolution = 10;

    [SerializeField]
	FunctionLibrary.FunctionName function;

    [SerializeField, Min(0f)]
	float functionDuration = 1f, transitionDuration = 1f;

    public enum TransitionMode { Cycle, Random }
	[SerializeField]
	TransitionMode transitionMode;

	[SerializeField]
	Material material;

	[SerializeField]
	Mesh mesh;

    float duration;
    bool transitioning;
    FunctionLibrary.FunctionName transitionFunction;
	ComputeBuffer positionsBuffer;

	// setting properties of compute shader
	static readonly int positionsId = Shader.PropertyToID("_Positions"),
		resolutionId = Shader.PropertyToID("_Resolution"),
		stepId = Shader.PropertyToID("_Step"),
		timeId = Shader.PropertyToID("_Time");

	void UpdateFunctionOnGPU () {
		float step = 2f / resolution;
		computeShader.SetInt(resolutionId, resolution);
		computeShader.SetFloat(stepId, step);
		computeShader.SetFloat(timeId, Time.time);

		// doesn't set data, just links buffer to kernel (0 = index of kernel function)
		computeShader.SetBuffer(0, positionsId, positionsBuffer);

		// actually runs the kernel with specified amount of groups to run
		int groups = Mathf.CeilToInt(resolution / 8f);
		computeShader.Dispatch(0, groups, groups, 1);

		material.SetBuffer(positionsId, positionsBuffer);
		material.SetFloat(stepId, step);

		// procedural drawing with args for mesh, sub-mesh index, material, bounds, and num of instances
		// this way of drawing DOES NOT USE GAME OBJECTS so unity doesn't know where it is automatically
		var bounds = new Bounds(Vector3.zero, Vector3.one * (2f + 2f / resolution));
		Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, resolution * resolution);
	}

	// gets invoked whenever a component is enabled and survives hot reloads
	void OnEnable () { 
		// allocating space to store positions in GPU
		positionsBuffer = new ComputeBuffer(maxResolution * maxResolution, 3 * 4);
	}

	// gets invoked when a component is disabled like when a graph is destroyed and right before a hot reload
	void OnDisable () { 
		positionsBuffer.Release();
		positionsBuffer = null;
	}

    void Update () {
        duration += Time.deltaTime;
        if (transitioning) {
            if (duration >= transitionDuration) {
				duration -= transitionDuration;
				transitioning = false;
			}
        }
		else if (duration >= functionDuration) {
			duration -= functionDuration;
            transitioning = true;
			transitionFunction = function;
            PickNextFunction();
		}
		UpdateFunctionOnGPU();
	}

    void PickNextFunction () {
		function = transitionMode == TransitionMode.Cycle ?
			FunctionLibrary.GetNextFunctionName(function) :
			FunctionLibrary.GetRandomFunctionNameOtherThan(function);
	}

}