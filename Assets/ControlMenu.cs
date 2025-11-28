using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

/*
GameObject: ControlMenu (attach to UI root in MainMenu or persistent manager)
Descripción: Gestiona navegación de menús, pausa, carga/reinicio de escenas y paneles UI.
*/

public class ControlMenu : MonoBehaviour
{
    [Header("Escenas")]             
    [SerializeField] private string gameplaySceneName = "The maze";
    [SerializeField] private string mainMenuSceneName = "MENU";
    [SerializeField] private string savedScenePlayerPrefKey = "SavedScene";

#if UNITY_EDITOR
    [SerializeField] private SceneAsset gameplaySceneAsset = null;
    [SerializeField] private SceneAsset mainMenuSceneAsset = null;
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

    // Awake: inicializa persistencia opcional y panel activo por defecto.
    private void Awake()
    {
#if UNITY_EDITOR
        if (gameplaySceneAsset != null)
            gameplaySceneName = gameplaySceneAsset.name;
        if (mainMenuSceneAsset != null)
            mainMenuSceneName = mainMenuSceneAsset.name;
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

    // OnEnable: suscribe al evento de carga de escena.
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // OnDisable: desuscribe del evento de carga de escena.
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // OnSceneLoaded: restaura tiempo y estado de pausa al cargar escena.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (pausePanel == null)
            pausePanel = FindPausePanelInScene();

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    // Update: detecta tecla Escape para alternar pausa.
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

    // Desactiva todos los paneles conocidos.
    private void SetAllPanelsInactive()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (instructionsPanel != null) instructionsPanel.SetActive(false);
        if (loadGamePanel != null) loadGamePanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    // Activa un panel y desactiva los demás.
    private void SetActivePanel(GameObject panel)
    {
        SetAllPanelsInactive();
        if (panel != null) panel.SetActive(true);
    }

    // Busca el panel de pausa en la escena por nombre, tag o en canvases.
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

    // Busca recursivamente un hijo por nombre.
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

    // Normaliza un nombre de escena para comparación.
    private static string NormalizeSceneName(string name) =>
        string.IsNullOrEmpty(name) ? string.Empty : name.Replace(" ", "").ToLowerInvariant();

    // Intenta resolver un nombre de escena en Build Settings.
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

    // Inicia la escena de gameplay configurada.
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

    // Carga un juego guardado o muestra el panel de carga.
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

    // Atajos para abrir paneles.
    public void OpenOptions() => SetActivePanel(optionsPanel ?? mainMenuPanel);
    public void OpenInstructions() => SetActivePanel(instructionsPanel ?? mainMenuPanel);
    public void BackToMainMenu() => SetActivePanel(mainMenuPanel);

    // Sale de la aplicación o de Play Mode en el editor.
    public void Exit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Alterna pausa/resume.
    public void TogglePause()
    {
        if (isPaused) Resume();
        else Pause();
    }

    // Activa el panel de pausa y detiene el tiempo.
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

    // Reanuda el juego desde pausa.
    public void Resume()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // Reinicia la escena activa.
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OpenOptionsFromPause() => OpenOptions();
    public void OpenInstructionsFromPause() => OpenInstructions();

    // Vuelve al estado de pausa mostrando el panel correspondiente.
    public void BackToPause()
    {
        SetAllPanelsInactive();
        if (pausePanel != null) pausePanel.SetActive(true);
        isPaused = true;
        Time.timeScale = 0f;
    }

    
    public void BackToMainMenuScene()
    {
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogWarning("ControlMenu: el nombre de la escena del menu principal no está configurado.");
            return;
        }
        if (TryResolveSceneNameInBuild(mainMenuSceneName, out string resolved))
        {
            // Aseguramos que el juego no quede pausado al cargar el menú principal
            Time.timeScale = 1f;
            isPaused = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            SceneManager.LoadScene(resolved);
            return;
        }
        Debug.LogError($"No se encontró la escena '{mainMenuSceneName}' en Build Settings.");
    }
}