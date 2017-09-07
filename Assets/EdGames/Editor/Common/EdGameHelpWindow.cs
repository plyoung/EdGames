using UnityEngine;
using UnityEditor;


namespace EdGames
{
	public class EdGameHelpWindow : EditorWindow
	{
		[System.NonSerialized] private GUIContent label;
		[System.NonSerialized] private GUIContent[] lines;

		public static void ShowWindow(GUIContent label, GUIContent[] lines)
		{
			EdGameHelpWindow win = GetWindow<EdGameHelpWindow>(true, "EdGames Help", true);
			win.label = label;
			win.lines = lines;
		}

		private void OnGUI()
		{
			if (label == null)
			{
				GUIUtility.ExitGUI();
				Close();
				return;
			}

			GUILayout.Label(label, EditorStyles.boldLabel);
			EditorGUILayout.Space();

			foreach (GUIContent l in lines)
			{ 
				if (l == null)
				{
					EditorGUILayout.Space();
					continue;
				}

				GUILayout.Label(l);
			}

		}

		// ------------------------------------------------------------------------------------------------------------------
	}
}