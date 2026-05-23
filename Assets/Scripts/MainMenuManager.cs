using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public void StartGame()
    {
        SceneManager.LoadScene("GameScene");
    }

    public void Setting()
    {
        Debug.Log("설정 준비중");
    }

    public void Info()
    {
        Debug.Log("게임 설명 준비중");
    }

    public void Credit()
    {
        Debug.Log("제작중...");
    }
}