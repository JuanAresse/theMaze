using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/*
GameObject: GameOverManager (attach to a persistent GameObject or created dynamically)
Descripción: Crea/gestiona la UI de fin de juego, muestra resultado y permite reiniciar o volver al menú.
*/

public class GameOverManager : MonoBehaviour
{
    public GameObject gameOverUI;
    public TMP_Text resultadoText;
    public Button reiniciarButton;
    public Button mainMenuButton;
    public string mainMenuSceneName = "MainMenu";

    private bool gameEnded = false;

    // MostrarResultado: muestra el mensaje de fin de juego y pausa el tiempo.
    // Parámetros: mensaje - texto que se mostrará en la UI.
    public void MostrarResultado(string mensaje)
    {
        if (gameEnded) return;
        gameEnded = true;

        EnsureUI();

        if (gameOverUI != null) gameOverUI.SetActive(true);
        if (resultadoText != null) resultadoText.text = mensaje;

        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // EnsureUI: asegura que exista una UI válida (reutiliza o crea elementos necesarios).
    private void EnsureUI()
    {
        if (gameOverUI != null)
        {
            if (resultadoText == null)
                resultadoText = gameOverUI.GetComponentInChildren<TMP_Text>(true);

            if (reiniciarButton == null)
                reiniciarButton = gameOverUI.GetComponentInChildren<Button>(true);

            if (mainMenuButton == null)
            {
                var allButtons = gameOverUI.GetComponentsInChildren<Button>(true);
                foreach (var b in allButtons)
                {
                    if (b.gameObject.name == "MainMenuButton") { mainMenuButton = b; break; }
                }
            }

            if (reiniciarButton != null)
            {
                reiniciarButton.onClick.RemoveListener(ReiniciarJuego);
                reiniciarButton.onClick.AddListener(ReiniciarJuego);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveListener(IrAlMenuPrincipal);
                mainMenuButton.onClick.AddListener(IrAlMenuPrincipal);
            }

            if (resultadoText != null && reiniciarButton != null && mainMenuButton != null) return;

            Transform panel = gameOverUI.transform.Find("Panel");
            if (panel == null)
            {
                var panelGO = new GameObject("Panel");
                panelGO.transform.SetParent(gameOverUI.transform, false);
                var img = panelGO.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.65f);
                var rt = panelGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.2f, 0.35f);
                rt.anchorMax = new Vector2(0.8f, 0.65f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                panel = panelGO.transform;
            }

            if (resultadoText == null)
            {
                var tGO = new GameObject("ResultadoText");
                tGO.transform.SetParent(panel, false);
                var tmp = tGO.AddComponent<TextMeshProUGUI>();
                tmp.text = "";
                tmp.fontSize = 30;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                var tr = tGO.GetComponent<RectTransform>();
                tr.anchorMin = new Vector2(0.05f, 0.35f);
                tr.anchorMax = new Vector2(0.95f, 0.85f);
                tr.offsetMin = Vector2.zero;
                tr.offsetMax = Vector2.zero;
                resultadoText = tmp;
            }

            if (mainMenuButton == null)
            {
                var bGO = new GameObject("MainMenuButton");
                bGO.transform.SetParent(panel, false);
                var img = bGO.AddComponent<Image>();
                img.color = new Color(0.2f, 0.4f, 0.8f, 1f);
                var btn = bGO.AddComponent<Button>();
                var brt = bGO.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.15f, 0.05f);
                brt.anchorMax = new Vector2(0.4f, 0.25f);
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;

                var btTextGO = new GameObject("Text");
                btTextGO.transform.SetParent(bGO.transform, false);
                var btTxt = btTextGO.AddComponent<TextMeshProUGUI>();
                btTxt.text = "Menú Principal";
                btTxt.fontSize = 20;
                btTxt.alignment = TextAlignmentOptions.Center;
                btTxt.color = Color.white;
                var btTxtRt = btTextGO.GetComponent<RectTransform>();
                btTxtRt.anchorMin = Vector2.zero;
                btTxtRt.anchorMax = Vector2.one;
                btTxtRt.offsetMin = Vector2.zero;
                btTxtRt.offsetMax = Vector2.zero;

                mainMenuButton = btn;
                mainMenuButton.onClick.AddListener(IrAlMenuPrincipal);
            }

            if (reiniciarButton == null)
            {
                var bGO = new GameObject("ReiniciarButton");
                bGO.transform.SetParent(panel, false);
                var img = bGO.AddComponent<Image>();
                img.color = new Color(0.2f, 0.6f, 0.2f, 1f);
                var btn = bGO.AddComponent<Button>();
                var brt = bGO.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.6f, 0.05f);
                brt.anchorMax = new Vector2(0.85f, 0.25f);
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;

                var btTextGO = new GameObject("Text");
                btTextGO.transform.SetParent(bGO.transform, false);
                var btTxt = btTextGO.AddComponent<TextMeshProUGUI>();
                btTxt.text = "Reiniciar";
                btTxt.fontSize = 22;
                btTxt.alignment = TextAlignmentOptions.Center;
                btTxt.color = Color.white;
                var btTxtRt = btTextGO.GetComponent<RectTransform>();
                btTxtRt.anchorMin = Vector2.zero;
                btTxtRt.anchorMax = Vector2.one;
                btTxtRt.offsetMin = Vector2.zero;
                btTxtRt.offsetMax = Vector2.zero;

                reiniciarButton = btn;
                reiniciarButton.onClick.AddListener(ReiniciarJuego);
            }

            return;
        }

        gameOverUI = new GameObject("GameOverUI");
        var canvas = gameOverUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var cs = gameOverUI.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1280, 720);

        gameOverUI.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(gameOverUI);

        if (FindObjectOfType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(esGO);
        }

        var panelGO2 = new GameObject("Panel");
        panelGO2.transform.SetParent(gameOverUI.transform, false);
        var panelImg2 = panelGO2.AddComponent<Image>();
        panelImg2.color = new Color(0f, 0f, 0f, 0.65f);
        var panelRt2 = panelGO2.GetComponent<RectTransform>();
        panelRt2.anchorMin = new Vector2(0.2f, 0.35f);
        panelRt2.anchorMax = new Vector2(0.8f, 0.65f);
        panelRt2.offsetMin = Vector2.zero;
        panelRt2.offsetMax = Vector2.zero;

        var textGO = new GameObject("ResultadoText");
        textGO.transform.SetParent(panelGO2.transform, false);
        var tmp2 = textGO.AddComponent<TextMeshProUGUI>();
        tmp2.text = "";
        tmp2.fontSize = 30;
        tmp2.alignment = TextAlignmentOptions.Center;
        tmp2.color = Color.white;
        var textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.05f, 0.35f);
        textRt.anchorMax = new Vector2(0.95f, 0.85f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        resultadoText = tmp2;

        var btnMenuGO = new GameObject("MainMenuButton");
        btnMenuGO.transform.SetParent(panelGO2.transform, false);
        var btnMenuImg = btnMenuGO.AddComponent<Image>();
        btnMenuImg.color = new Color(0.2f, 0.4f, 0.8f, 1f);
        var btnMenu = btnMenuGO.AddComponent<Button>();
        var btnMenuRt = btnMenuGO.GetComponent<RectTransform>();
        btnMenuRt.anchorMin = new Vector2(0.15f, 0.05f);
        btnMenuRt.anchorMax = new Vector2(0.4f, 0.25f);
        btnMenuRt.offsetMin = Vector2.zero;
        btnMenuRt.offsetMax = Vector2.zero;

        var btnMenuTextGO = new GameObject("Text");
        btnMenuTextGO.transform.SetParent(btnMenuGO.transform, false);
        var btnMenuTxt = btnMenuTextGO.AddComponent<TextMeshProUGUI>();
        btnMenuTxt.text = "Menú Principal";
        btnMenuTxt.fontSize = 20;
        btnMenuTxt.alignment = TextAlignmentOptions.Center;
        btnMenuTxt.color = Color.white;
        var btnMenuTextRt = btnMenuTextGO.GetComponent<RectTransform>();
        btnMenuTextRt.anchorMin = Vector2.zero;
        btnMenuTextRt.anchorMax = Vector2.one;
        btnMenuTextRt.offsetMin = Vector2.zero;
        btnMenuTextRt.offsetMax = Vector2.zero;

        mainMenuButton = btnMenu;
        mainMenuButton.onClick.AddListener(IrAlMenuPrincipal);

        var btnGO2 = new GameObject("ReiniciarButton");
        btnGO2.transform.SetParent(panelGO2.transform, false);
        var btnImg2 = btnGO2.AddComponent<Image>();
        btnImg2.color = new Color(0.2f, 0.6f, 0.2f, 1f);
        var btn2 = btnGO2.AddComponent<Button>();
        var btnRt2 = btnGO2.GetComponent<RectTransform>();
        btnRt2.anchorMin = new Vector2(0.6f, 0.05f);
        btnRt2.anchorMax = new Vector2(0.85f, 0.25f);
        btnRt2.offsetMin = Vector2.zero;
        btnRt2.offsetMax = Vector2.zero;

        var btnTextGO2 = new GameObject("Text");
        btnTextGO2.transform.SetParent(btnGO2.transform, false);
        var btnTxt2 = btnTextGO2.AddComponent<TextMeshProUGUI>();
        btnTxt2.text = "Reiniciar";
        btnTxt2.fontSize = 22;
        btnTxt2.alignment = TextAlignmentOptions.Center;
        btnTxt2.color = Color.white;
        var btnTextRt2 = btnTextGO2.GetComponent<RectTransform>();
        btnTextRt2.anchorMin = Vector2.zero;
        btnTextRt2.anchorMax = Vector2.one;
        btnTextRt2.offsetMin = Vector2.zero;
        btnTextRt2.offsetMax = Vector2.zero;

        reiniciarButton = btn2;
        reiniciarButton.onClick.AddListener(ReiniciarJuego);

        gameOverUI.SetActive(false);
    }

    // Elimina UI persistente creada por este manager y restaura estado de input/tiempo.
    private void CleanupPersistentUI(bool restoreInputAndTime = true)
    {
        if (gameOverUI != null)
        {
            try { gameOverUI.SetActive(false); } catch { }
            Destroy(gameOverUI);
            gameOverUI = null;
            resultadoText = null;
            reiniciarButton = null;
            mainMenuButton = null;
        }

        if (restoreInputAndTime)
        {
            Time.timeScale = 1f;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            gameEnded = false;
        }
    }

    // Restaura estado de entrada tras cargar escena.
    private void RestoreInputState()
    {
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        gameEnded = false;
    }

    // Reinicia la escena actual.
    public void ReiniciarJuego()
    {
        CleanupPersistentUI(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        RestoreInputState();
    }

    // Carga la escena del menú principal.
    public void IrAlMenuPrincipal()
    {
        CleanupPersistentUI(false);

        if (!string.IsNullOrEmpty(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            SceneManager.LoadScene(0);
        }

        RestoreInputState();
    }
}
