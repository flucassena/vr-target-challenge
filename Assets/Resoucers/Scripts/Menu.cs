using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    public void StartGame()
    {
        Debug.Log("Start Game");
        SceneManager.LoadScene("Fase");
        // Add logic to start the game
    }

    public void OpenSettings()
    {
        Debug.Log("Open Settings");
        // Add logic to open settings menu
    }

    public void QuitGame()
    {
        Debug.Log("Quit Game");
        // Add logic to quit the game
        Application.Quit();
    }
}
