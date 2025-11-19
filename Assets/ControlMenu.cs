using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ControlMenu : MonoBehaviour
{
    [Header("Escenas")]
    [SerializeField] private string gameplaySceneName = "The maze";
    [SerializeField] private string savedScenePlayerPrefKey = "SavedScene";

#if UNITY_EDITOR
    [SerializeField] private SceneAsset gameplaySceneAsset = null;
#endif

    [Header("Paneles UI (asignar en el Inspector)")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject instructionsPanel;
    [SerializeField] private GameObject loadGamePanel;

    [Header("Pausa (Gameplay)")]
    [SerializeField] private GameObject pausePanel;

    [Header("Opciones")]
    [SerializeField] private bool persistBetweenScenes = false;

    private bool isPaused = false;
    private static ControlMenu s_instance;

    private void Awake()
    {
#if UNITY_EDITOR
        if (gameplaySceneAsset != null)
            gameplaySceneName = gameplaySceneAsset.name;
#endif

        if (persistBetweenScenes)
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }

        if (mainMenuPanel != null)
            SetActivePanel(mainMenuPanel);
        else
            SetAllPanelsInactive();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (pausePanel == null)
            pausePanel = FindPausePanelInScene();

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (pausePanel == null)
                pausePanel = FindPausePanelInScene();

            if (pausePanel != null)
                TogglePause();
            else
                Debug.LogWarning("ControlMenu: pausePanel no asignado ni encontrado. Asignalo en el Inspector o crea un GameObject llamado 'PausePanel'.");
        }
    }

    private void SetAllPanelsInactive()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (instructionsPanel != null) instructionsPanel.SetActive(false);
        if (loadGamePanel != null) loadGamePanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    private void SetActivePanel(GameObject panel)
    {
        SetAllPanelsInactive();
        if (panel != null) panel.SetActive(true);
    }

    private GameObject FindPausePanelInScene()
    {
        var g = GameObject.Find("PausePanel");
        if (g != null) return g;

        try
        {
            g = GameObject.FindWithTag("PausePanel");
            if (g != null) return g;
        }
        catch (UnityException) { }

        var canvases = GameObject.FindObjectsOfType<Canvas>();
        foreach (var c in canvases)
        {
            var t = FindChildByNameRecursive(c.transform, "PausePanel");
            if (t != null) return t.gameObject;
        }

        return null;
    }

    private Transform FindChildByNameRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindChildByNameRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static string NormalizeSceneName(string name) =>
        string.IsNullOrEmpty(name) ? string.Empty : name.Replace(" ", "").ToLowerInvariant();

    private bool TryResolveSceneNameInBuild(string requestedName, out string resolvedName)
    {
        resolvedName = string.Empty;
        if (string.IsNullOrEmpty(requestedName)) return false;

        string normRequested = NormalizeSceneName(requestedName);
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.Equals(name, requestedName, StringComparison.OrdinalIgnoreCase))
            {
                resolvedName = name;
                return true;
            }
            if (NormalizeSceneName(name) == normRequested)
            {
                resolvedName = name;
                return true;
            }
        }
        return false;
    }

    public void Play()
    {
        if (string.IsNullOrEmpty(gameplaySceneName))
        {
            Debug.LogWarning("ControlMenu: el nombre de la escena no está configurado.");
            return;
        }
        if (TryResolveSceneNameInBuild(gameplaySceneName, out string resolved))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(resolved);
            return;
        }
        Debug.LogError($"No se encontró la escena '{gameplaySceneName}' en Build Settings.");
    }

    public void LoadGame()
    {
        string savedScene = PlayerPrefs.GetString(savedScenePlayerPrefKey, string.Empty);
        if (!string.IsNullOrEmpty(savedScene) && TryResolveSceneNameInBuild(savedScene, out string resolved))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(resolved);
            return;
        }
        if (loadGamePanel != null)
            SetActivePanel(loadGamePanel);
        else
            Play();
    }

    public void OpenOptions() => SetActivePanel(optionsPanel ?? mainMenuPanel);
    public void OpenInstructions() => SetActivePanel(instructionsPanel ?? mainMenuPanel);
    public void BackToMainMenu() => SetActivePanel(mainMenuPanel);

    public void Exit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void TogglePause()
    {
        if (isPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (pausePanel == null)
        {
            Debug.LogWarning("ControlMenu: pausePanel no asignado.");
            return;
        }
        SetAllPanelsInactive();
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Resume()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OpenOptionsFromPause() => OpenOptions();
    public void OpenInstructionsFromPause() => OpenInstructions();

    public void BackToPause()
    {
        SetAllPanelsInactive();
        if (pausePanel != null) pausePanel.SetActive(true);
        isPaused = true;
        Time.timeScale = 0f;
    }
}