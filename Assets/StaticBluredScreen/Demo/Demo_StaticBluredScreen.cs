using UnityEngine;

public class Demo_StaticBluredScreen : MonoBehaviour
{
	[SerializeField] StaticBluredScreen m_StaticBluredScreen;

	public void ToggleBlured(bool flag)
	{
		if (flag)
			m_StaticBluredScreen.UpdateTexture();
		else
			m_StaticBluredScreen.texture = null;
	}

	public void UpdateTex()
	{
		m_StaticBluredScreen.UpdateTexture();
	}

	public void OpenDialog(Animator anim)
	{
		anim.SetTrigger("Show");
	}

	public void CloseDialog(Animator anim)
	{
		anim.SetTrigger("Hide");
	}
}
