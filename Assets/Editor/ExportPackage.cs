using UnityEditor;


public static class ExportPackage
{
	const string kPackageName = "StaticBluredScreen.unitypackage";
	static readonly string[] kAssetPathes =
	{
		"Assets/StaticBluredScreen",
	};

	[MenuItem("Export Package/" + kPackageName)]
	[InitializeOnLoadMethod]
	static void Export()
	{
		if (EditorApplication.isPlayingOrWillChangePlaymode)
			return;
			
		AssetDatabase.ExportPackage(kAssetPathes, kPackageName, ExportPackageOptions.Recurse | ExportPackageOptions.Default);
		UnityEngine.Debug.Log("Export successfully : " + kPackageName);
	}
}
