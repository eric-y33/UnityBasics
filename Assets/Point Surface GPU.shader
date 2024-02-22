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

        // Procedural drawing configuration stuff is in here
        #include "PointGPU.hlsl"

        struct Input {
			float3 worldPos;
		};
    
        float _Smoothness;

        void ConfigureSurface (Input input, inout SurfaceOutputStandard surface) {
            surface.Albedo = saturate(input.worldPos * 0.5 + 0.5);
            surface.Smoothness = _Smoothness;
        }

		ENDCG
    }

    FallBack "Diffuse"

}