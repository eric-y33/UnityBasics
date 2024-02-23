using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;
using quaternion = Unity.Mathematics.quaternion;
using Random = UnityEngine.Random;

public class Fractal : MonoBehaviour {

    // Explicitly tell Unity to compile our job struct with Burst
    // FloatPrecision.Standard makes sin/cos less precise (faster), FloatMode.Fast makes computer math faster
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    struct UpdateFractalLevelJob : IJobFor {

        public float spinAngleDelta;
		public float scale;

        [ReadOnly]
		public NativeArray<FractalPart> parents;
		public NativeArray<FractalPart> parts;

        [WriteOnly]
		public NativeArray<float3x4> matrices;

        public void Execute (int i) {
            FractalPart parent = parents[i / 5];
            FractalPart part = parts[i];
            part.spinAngle += spinAngleDelta;
            part.worldRotation = mul(parent.worldRotation,
				mul(part.rotation, quaternion.RotateY(part.spinAngle))
			);
            part.worldPosition =
                parent.worldPosition +
                mul(parent.worldRotation, 1.5f * scale * part.direction);
            parts[i] = part;


            float3x3 r = float3x3(part.worldRotation) * scale;
			matrices[i] = float3x4(r.c0, r.c1, r.c2, part.worldPosition);
        }

    }

    struct FractalPart {
		public float3 direction, worldPosition;
		public quaternion rotation, worldRotation;
        public float spinAngle;
	}

    [SerializeField, Range(2, 8)]
	int depth = 4;

    [SerializeField]
	Mesh mesh;

	[SerializeField]
	Material material;

    [SerializeField]
    Gradient gradientA, gradientB;

    static float3[] directions = {
		up(), right(), left(), forward(), back()
	};

	static quaternion[] rotations = {
		quaternion.identity,
		quaternion.RotateZ(-0.5f * PI), quaternion.RotateZ(0.5f * PI),
		quaternion.RotateX(0.5f * PI), quaternion.RotateX(-0.5f * PI)
	};

    FractalPart CreatePart (int childIndex) {
        return new FractalPart {
            direction = directions[childIndex],
			rotation = rotations[childIndex]
        };
	}

    static readonly int 
        colorAId = Shader.PropertyToID("_ColorA"),
		colorBId = Shader.PropertyToID("_ColorB"),
        matricesId = Shader.PropertyToID("_Matrices"),
		sequenceNumbersId = Shader.PropertyToID("_SequenceNumbers");

    NativeArray<FractalPart>[] parts;
	NativeArray<float3x4>[] matrices;
    ComputeBuffer[] matricesBuffers;

    static MaterialPropertyBlock propertyBlock;

    Vector4[] sequenceNumbers;

    void OnEnable () {
        parts = new NativeArray<FractalPart>[depth];
		matrices = new NativeArray<float3x4>[depth];
        matricesBuffers = new ComputeBuffer[depth];
        sequenceNumbers = new Vector4[depth];
		int stride = 12 * 4;
		for (int i = 0, length = 1; i < parts.Length; i++, length *= 5) {
			parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
			matrices[i] = new NativeArray<float3x4>(length, Allocator.Persistent);
            matricesBuffers[i] = new ComputeBuffer(length, stride);
            sequenceNumbers[i] = new Vector4(Random.value, Random.value);
		}

        // float scale = 1f;
		parts[0][0] = CreatePart(0);
		for (int li = 1; li < parts.Length; li++) {
            // scale *= 0.5f;
            NativeArray<FractalPart> levelParts = parts[li];
			for (int fpi = 0; fpi < levelParts.Length; fpi += 5) {
				for (int ci = 0; ci < 5; ci++) {
					levelParts[fpi + ci] = CreatePart(ci);
				}
			}
        }

        if (propertyBlock == null) {
			propertyBlock = new MaterialPropertyBlock();
		}
	}

    void OnDisable () {
		for (int i = 0; i < matricesBuffers.Length; i++) {
			matricesBuffers[i].Release();
            parts[i].Dispose();
			matrices[i].Dispose();
		}
        parts = null;
		matrices = null;
		matricesBuffers = null;
        sequenceNumbers = null;
	}

    // activates when a change has been made via the inspector or undo/redo
    void OnValidate () {
		if (parts != null && enabled) {
			OnDisable();
			OnEnable();
		}
	}

    void Update () {
        float spinAngleDelta = 0.125f * PI * Time.deltaTime;
        FractalPart rootPart = parts[0][0];
        rootPart.spinAngle += spinAngleDelta;
        rootPart.worldRotation = mul(transform.rotation,
            mul(rootPart.rotation, quaternion.RotateY(rootPart.spinAngle)) // starts with fresh quaternions
        );
        rootPart.worldPosition = transform.position;
        parts[0][0] = rootPart;
        float objectScale = transform.lossyScale.x;
        float3x3 r = float3x3(rootPart.worldRotation) * objectScale;
		matrices[0][0] = float3x4(r.c0, r.c1, r.c2, rootPart.worldPosition);

        float scale = objectScale;
        JobHandle jobHandle = default;
		for (int li = 1; li < parts.Length; li++) {
            scale *= 0.5f;
            jobHandle = new UpdateFractalLevelJob {
				spinAngleDelta = spinAngleDelta,
				scale = scale,
				parents = parts[li - 1],
				parts = parts[li],
				matrices = matrices[li]
			}.ScheduleParallel(parts[li].Length, 5, jobHandle);
            // We don't have to invoke Execute every iteration, we want to schedule it so it does it itself
            // batch count number controls how the iterations get allocated to threads, more work = do less batches
			// for (int fpi = 0; fpi < levelParts.Length; fpi++) {
            //     job.Execute(fpi);
			// } 
		}
        // .Complete() delays further execution of code till job is completed
        // Here, every job is depending on the next through jobHandle so all of them will complete before moving on
        jobHandle.Complete();

        // Send data to the GPU through SetData and draw
        var bounds = new Bounds(rootPart.worldPosition, 3f * objectScale * Vector3.one);
		for (int i = 0; i < matricesBuffers.Length; i++) {
			ComputeBuffer buffer = matricesBuffers[i];
			buffer.SetData(matrices[i]);
            // for one gradient:
            // propertyBlock.SetColor(
			// 	baseColorId, gradient.Evaluate(i / (matricesBuffers.Length - 1f)) 
			// );
            float gradientInterpolator = i / (matricesBuffers.Length - 1f);
			propertyBlock.SetColor(colorAId, gradientA.Evaluate(gradientInterpolator));
			propertyBlock.SetColor(colorBId, gradientB.Evaluate(gradientInterpolator));
			propertyBlock.SetBuffer(matricesId, buffer);
            propertyBlock.SetVector(sequenceNumbersId, sequenceNumbers[i]);
			Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, buffer.count, propertyBlock);
		}
	}






    // UNOPTIMIZED CODE BELOW

	// [SerializeField, Range(1, 8)]
	// int depth = 4;

    // Fractal CreateChild (Vector3 direction, Quaternion rotation) {
	// 	Fractal child = Instantiate(this);
	// 	child.depth = depth - 1;
	// 	child.transform.localPosition = 0.75f * direction;
    //  child.transform.localRotation = rotation;
	// 	child.transform.localScale = 0.5f * Vector3.one;
	// 	return child;
	// }

    // void Start () {
    //     name = "Fractal " + depth;

    //     if (depth <= 1) {
	// 		return;
	// 	}

	// 	Fractal childA = CreateChild(Vector3.up, Quaternion.identity);
	// 	Fractal childB = CreateChild(Vector3.right, Quaternion.Euler(0f, 0f, -90f));
    //  Fractal childC = CreateChild(Vector3.left, Quaternion.Euler(0f, 0f, 90f));
    //  Fractal childD = CreateChild(Vector3.forward, Quaternion.Euler(90f, 0f, 0f));
	// 	Fractal childE = CreateChild(Vector3.back, Quaternion.Euler(-90f, 0f, 0f));

		
	// 	childA.transform.SetParent(transform, false);
	// 	childB.transform.SetParent(transform, false);
    //  childC.transform.SetParent(transform, false);
    //  childD.transform.SetParent(transform, false);
	// 	childE.transform.SetParent(transform, false);
	// }

    // void Update () {
	//      transform.Rotate(0f, 22.5f * Time.deltaTime, 0f);
	// }
    
}