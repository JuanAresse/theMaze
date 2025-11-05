using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class GameOverManager : MonoBehaviour
{
    public GameObject gameOverUI;
    public TMP_Text resultadoText;
    public Button reiniciarButton;

    private bool gameEnded = false;

    public void MostrarResultado(string mensaje)
    {
        if (gameEnded) return;
        gameEnded = true;

        EnsureUI();

        if (gameOverUI != null) gameOverUI.SetActive(true);
        if (resultadoText != null) resultadoText.text = mensaje;

        // Pausa el juego y muestra cursor para permitir pulsar el botón
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void EnsureUI()
    {
        // Si ya hay un gameOverUI en escena, intentar reutilizar sus componentes
        if (gameOverUI != null)
        {
            if (resultadoText == null)
                resultadoText = gameOverUI.GetComponentInChildren<TMP_Text>(true);

            if (reiniciarButton == null)
                reiniciarButton = gameOverUI.GetComponentInChildren<Button>(true);

            if (reiniciarButton != null)
            {
                reiniciarButton.onClick.RemoveListener(ReiniciarJuego);
                reiniciarButton.onClick.AddListener(ReiniciarJuego);
            }

            // Si falta alguno de los elementos, crearlos bajo un panel dentro del gameOverUI
            if (resultadoText != null && reiniciarButton != null) return;

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

            if (reiniciarButton == null)
            {
                var bGO = new GameObject("ReiniciarButton");
                bGO.transform.SetParent(panel, false);
                var img = bGO.AddComponent<Image>();
                img.color = new Color(0.2f, 0.6f, 0.2f, 1f);
                var btn = bGO.AddComponent<Button>();
                var brt = bGO.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.35f, 0.05f);
                brt.anchorMax = new Vector2(0.65f, 0.25f);
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

        // Si no existe gameOverUI, crear una UI básica por código
        gameOverUI = new GameObject("GameOverUI");
        var canvas = gameOverUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var cs = gameOverUI.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1280, 720);

        gameOverUI.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(gameOverUI);

        // Panel
        var panelGO2 = new GameObject("Panel");
        panelGO2.transform.SetParent(gameOverUI.transform, false);
        var panelImg2 = panelGO2.AddComponent<Image>();
        panelImg2.color = new Color(0f, 0f, 0f, 0.65f);
        var panelRt2 = panelGO2.GetComponent<RectTransform>();
        panelRt2.anchorMin = new Vector2(0.2f, 0.35f);
        panelRt2.anchorMax = new Vector2(0.8f, 0.65f);
        panelRt2.offsetMin = Vector2.zero;
        panelRt2.offsetMax = Vector2.zero;

        // Texto TMP
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

        // Botón Reiniciar
        var btnGO2 = new GameObject("ReiniciarButton");
        btnGO2.transform.SetParent(panelGO2.transform, false);
        var btnImg2 = btnGO2.AddComponent<Image>();
        btnImg2.color = new Color(0.2f, 0.6f, 0.2f, 1f);
        var btn2 = btnGO2.AddComponent<Button>();
        var btnRt2 = btnGO2.GetComponent<RectTransform>();
        btnRt2.anchorMin = new Vector2(0.35f, 0.05f);
        btnRt2.anchorMax = new Vector2(0.65f, 0.25f);
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

    public void ReiniciarJuego()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
