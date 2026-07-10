public static class MyEditorMenu
{
	[Menu( "Editor", "YouAreNotAlone / Sample menu" )]
	public static void OpenMyMenu()
	{
		EditorUtility.DisplayDialog("It worked!", "This is being called from your library's editor code!");
	}
}
