using UnityEditor;
using UnityEngine;

namespace Smrvfx.Editor
{
	[CustomEditor( typeof( SkinnedMeshBaker ) )]
	public class SkinnedMeshBakerEditor : UnityEditor.Editor
	{
		string[] toolbarOptions = new string[]{ "On", "Off" };

		public override void OnInspectorGUI()
		{
			SkinnedMeshBaker smb = ( SkinnedMeshBaker )target;

			base.DrawDefaultInspector();
			
			GUILayout.Space( 20f );

			using ( var horizontalScope = new GUILayout.HorizontalScope( "box" ) )
			{
				GUILayout.Label( "Optimal Mode", EditorStyles.boldLabel );
				smb.OptimalModeEnabled = GUILayout.Toolbar( smb.OptimalModeEnabled ? 0 : 1, toolbarOptions ) == 0 ? true : false;
			}
		}
	}
}