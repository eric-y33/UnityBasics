Shader "Graph/Point Surface GPU" {

    Properties {
		_Smoothness ("Smoothness", Range(0,1)) = 0.5
	}
    
    SubShader {
        CGPROGRAM

        #pragma surface ConfigureSurface Standard fullforwardshadows addshadow
        // surface shader needs to invoke ConfigureProcedural per vertex
        #pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural
        // makes this shader compile synchronously so that dummy shader doesn't crash everything
        #pragma editor_sync_compilation
        #pragma target 4.5

        struct Input {
			float3 worldPos;
		};
    
        float _Smoothness;

        float _Step;

        #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
			StructuredBuffer<float3> _Positions;
		#endif

        void ConfigureProcedural () {
            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                float3 position = _Positions[unity_InstanceID];
                // set entire transformation matrix to zero
                unity_ObjectToWorld = 0.0;
                // to construct column vector in transformation matrix
				unity_ObjectToWorld._m03_m13_m23_m33 = float4(position, 1.0);
                // to correctly scale points
                unity_ObjectToWorld._m00_m11_m22 = _Step;
			#endif
        }

        void ConfigureSurface (Input input, inout SurfaceOutputStandard surface) {
            surface.Albedo = saturate(input.worldPos * 0.5 + 0.5);
            surface.Smoothness = _Smoothness;
        }

		ENDCG
    }

    FallBack "Diffuse"

}