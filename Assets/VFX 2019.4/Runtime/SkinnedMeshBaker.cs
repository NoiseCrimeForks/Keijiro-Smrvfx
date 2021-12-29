//#define FORCE_BAKEMESH_2019

using UnityEngine;
using System.Linq;
using Unity.Profiling;

#if !( UNITY_2020_1_OR_NEWER && !FORCE_BAKEMESH_2019 )
using System.Collections.Generic;
#endif

namespace Smrvfx
{
    public sealed class SkinnedMeshBaker : MonoBehaviour
    {
        static ProfilerMarker markerBakeMesh    = new ProfilerMarker("SkinnedMeshBaker.BakeMesh");
        static ProfilerMarker markerTransfer    = new ProfilerMarker("SkinnedMeshBaker.Transfer");

        // Allow toggling between optimal mode and original code.
        // Optimal mode uses less memory and has less overhead than the
        // original code as it re-uses the PositionMap from the previous 
        // frame to calculate the new velocity in the compute Shader. 
        // This avoids having to store and upload the OldPositions buffer.
        [SerializeField, HideInInspector] bool optimalModeState   = false;

        public bool OptimalModeEnabled 
        { 
            get { return optimalModeState; } 
            set { optimalModeState = SetOptimalMode( value ); }  
        }


#region Editable attributes

        [SerializeField] SkinnedMeshRenderer [] _sources = null;
        [SerializeField, HideInInspector] ComputeShader _compute = null;

#endregion

#region Public properties

        public Texture PositionMap => _positionMap;
        public Texture VelocityMap => _velocityMap;
        public Texture NormalMap => _normalMap;
        public int VertexCount { get; private set; }

#endregion

#region Temporary objects


        (Matrix4x4 current, Matrix4x4 previous) _rootMatrix;

        Mesh _mesh;

        ComputeBuffer _positionBuffer1;
        ComputeBuffer _positionBuffer2;
        ComputeBuffer _normalBuffer;

        RenderTexture _positionMap;
        RenderTexture _velocityMap;
        RenderTexture _normalMap;

#endregion

#region MonoBehaviour implementation

        bool SetOptimalMode( bool value )
        {
            if ( value != optimalModeState )
            {
                // Dispose of _positionBuffer2 regardless of new OptimalMode setting.
                if ( null != _positionBuffer2 )             
                    _positionBuffer2.Dispose();
                
                // Allocate _positionBuffer2 if we've disabled OptimalMode
                if ( value == false && null != _positionBuffer1 )                
                    _positionBuffer2 = new ComputeBuffer( _positionBuffer1.count, sizeof( float ) );
            }

            return value;
        }

		void Start()
        {
            if ( _sources == null || _sources.Length == 0 ) return;

            VertexCount = _sources.Select( smr => smr.sharedMesh.vertexCount ).Sum();

            var l2w = _sources[0].transform.localToWorldMatrix;
            _rootMatrix = (l2w, l2w);

            _mesh = new Mesh();

            var vcount_x3 = VertexCount * 3;
            _positionBuffer1 = new ComputeBuffer( vcount_x3, sizeof( float ) );
            _normalBuffer = new ComputeBuffer( vcount_x3, sizeof( float ) );

            if ( !OptimalModeEnabled )
                _positionBuffer2 = new ComputeBuffer( vcount_x3, sizeof( float ) );

            var width = 256;
            var height = (((VertexCount + width - 1) / width + 7) / 8) * 8;
            _positionMap = RenderTextureUtil.AllocateFloat( width, height );
            _velocityMap = RenderTextureUtil.AllocateHalf( width, height );
            _normalMap = RenderTextureUtil.AllocateHalf( width, height );
        }

        void OnDestroy()
        {
            Destroy( _mesh );

            _positionBuffer1.Dispose();           
            _normalBuffer.Dispose();
            
            if (null != _positionBuffer2 )
                _positionBuffer2.Dispose();

            Destroy( _positionMap );
            Destroy( _velocityMap );
            Destroy( _normalMap );
        }

        void LateUpdate()
        {
            if ( VertexCount == 0 ) return;

            // Current transform matrix
            _rootMatrix.current = _sources[ 0 ].transform.localToWorldMatrix;

            using ( markerBakeMesh.Auto() )
            {
                // Bake the sources into the buffers.
                var offset = 0;
                foreach ( var source in _sources )
                    offset += Bake( source, offset );
            }

            using ( markerTransfer.Auto() )
            {
                // ComputeBuffer -> RenderTexture
                TransferData();
            }

            // Position buffer swapping
            if ( !OptimalModeEnabled )
                (_positionBuffer1, _positionBuffer2) = (_positionBuffer2, _positionBuffer1);

            // Transform matrix history
            _rootMatrix.previous = _rootMatrix.current;
        }
        

#endregion

#region Mesh bake function with the new/old Mesh API

#if UNITY_2020_1_OR_NEWER && !FORCE_BAKEMESH_2019

    int Bake(SkinnedMeshRenderer source, int offset)
    {
        source.BakeMesh(_mesh);

        using (var dataArray = Mesh.AcquireReadOnlyMeshData(_mesh))
        {
            var data = dataArray[0];
            var vcount = data.vertexCount;

            using (var pos = MemoryUtil.TempJobArray<Vector3>(vcount))
            using (var nrm = MemoryUtil.TempJobArray<Vector3>(vcount))
            {
                data.GetVertices(pos);
                data.GetNormals(nrm);

                _positionBuffer1.SetData(pos, 0, offset, vcount);
                _normalBuffer.SetData(nrm, 0, offset, vcount);

                return vcount;
            }
        }
    }

#else

        List<Vector3> _positionList = new List<Vector3>();
        List<Vector3> _normalList = new List<Vector3>();

        int Bake( SkinnedMeshRenderer source, int offset )
        {
            source.BakeMesh( _mesh );

            var vcount = _mesh.vertexCount;
            _mesh.GetVertices( _positionList );
            _mesh.GetNormals( _normalList );

            _positionBuffer1.SetData( _positionList, 0, offset, vcount );
            _normalBuffer.SetData( _normalList, 0, offset, vcount );

            return vcount;
        }

#endif

#endregion

#region Buffer operations

        void TransferData()
        {
            var vcount = VertexCount;
            var vcount_x3 = vcount * 3;

            int kernelID = OptimalModeEnabled ? 1 : 0;

            if ( !OptimalModeEnabled )
            {                
                _compute.SetMatrix( "OldTransform", _rootMatrix.previous );
                _compute.SetBuffer( kernelID, "OldPositionBuffer", _positionBuffer2 );
            }

            _compute.SetInt( "VertexCount", vcount );
            _compute.SetFloat( "FrameRate", 1 / Time.deltaTime );
            _compute.SetMatrix( "Transform", _rootMatrix.current );

            _compute.SetBuffer(  kernelID, "PositionBuffer", _positionBuffer1 );
            _compute.SetBuffer(  kernelID, "NormalBuffer", _normalBuffer );

            _compute.SetTexture( kernelID, "PositionMap", _positionMap );
            _compute.SetTexture( kernelID, "VelocityMap", _velocityMap );
            _compute.SetTexture( kernelID, "NormalMap", _normalMap );

            _compute.Dispatch( kernelID, _positionMap.width / 8, _positionMap.height / 8, 1 );
        }

#endregion
    }

} // namespace Smrvfx
