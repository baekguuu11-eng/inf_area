using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    public void RestartGame()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene("GameScene");
    }

    public void GoToMainMenu()
    {
        PrepareMainMenuReturn();
        SceneManager.LoadScene("MainMenu");
    }

    private static void PrepareMainMenuReturn()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        KODBBootSplashController.SkipSplashForMenuReturn();
        MainMenuIntroAnimator.PrepareForReturnFromGameplay();
    }
}