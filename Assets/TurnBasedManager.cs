using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Reflection;

public class TurnBasedManager : MonoBehaviour
{
    private Character _playerA;
    private Character _playerB;

    // Exponer los jugadores públicamente (requerido por Character.cs)
    public Character PlayerA => _playerA;
    public Character PlayerB => _playerB;

    [TextArea(3, 10)]
    public string PlayerAScript = "MoveRight();MoveUp();MoveUp();";
    [TextArea(3, 10)]
    public string PlayerBScript = "MoveLeft();MoveDown();";

    public float StepDelay = 0.25f;

    private bool _isExecuting = false;
    private bool _runOnce = false;
    private bool _continuous = false;

    // Nuevo: estado de game over
    private bool _gameOver = false;

    // Referencia opcional al GameOverManager (puedes asignarla en el inspector)
    public GameOverManager gameOverManager;

    // UI runtime references
    private Canvas _canvasA;
    private Canvas _canvasB;
    private InputField _inputA;
    private InputField _inputB;
    private Button _execButtonA;
    private Button _execButtonB;
    private Button _contButtonA;
    private Button _contButtonB;

    // Sobrecarga para compatibilidad: Initialize con sólo los dos personajes.
    public void Initialize(Character a, Character b)
    {
        // Intentamos determinar cámaras por defecto.
        Camera camA = Camera.main;
        Camera camB = null;

        var cams = Camera.allCameras;
        if (cams != null && cams.Length > 0)
        {
            if (cams.Length == 1)
            {
                camB = cams[0];
            }
            else
            {
                camB = cams[0] == camA ? cams[1] : cams[0];
            }
        }

        if (camA == null) camA = camB;
        if (camB == null) camB = camA;

        // Llamamos a la implementación principal
        Initialize(a, b, camA, camB);
    }

    public void Initialize(Character a, Character b, Camera camA, Camera camB)
    {
        _playerA = a;
        _playerB = b;

        if (_playerA.GetComponent<PlayerHealth>() == null)
            _playerA.gameObject.AddComponent<PlayerHealth>();
        if (_playerB.GetComponent<PlayerHealth>() == null)
            _playerB.gameObject.AddComponent<PlayerHealth>();

        _playerA.manager = this;
        _playerB.manager = this;

        // LOG: confirmar llamada y cámaras recibidas
        Debug.Log($"TurnBasedManager.Initialize called. camA={(camA!=null?camA.name:"null")}, camB={(camB!=null?camB.name:"null")}");

        EnsureEventSystemExists();

        // Crear UI para cada cámara (si no vienen provistas)
        _canvasA = CreateCanvasForCamera(camA, "Player A Controls", out _inputA, out _execButtonA, out _contButtonA);
        _canvasB = CreateCanvasForCamera(camB, "Player B Controls", out _inputB, out _execButtonB, out _contButtonB);

        // LOG: verificar creación de canvases
        Debug.Log($"Canvas created: A={(_canvasA!=null?_canvasA.gameObject.name:"null")}, B={(_canvasB!=null?_canvasB.gameObject.name:"null")}");

        // Inicializar textos
        if (_inputA != null) _inputA.text = PlayerAScript;
        if (_inputB != null) _inputB.text = PlayerBScript;

        // Listeners
        if (_inputA != null) _inputA.onValueChanged.AddListener(val => PlayerAScript = val);
        if (_inputB != null) _inputB.onValueChanged.AddListener(val => PlayerBScript = val);

        if (_execButtonA != null) _execButtonA.onClick.AddListener(() => { if (!_isExecuting) _runOnce = true; });
        if (_execButtonB != null) _execButtonB.onClick.AddListener(() => { if (!_isExecuting) _runOnce = true; });

        if (_contButtonA != null) _contButtonA.onClick.AddListener(() => ToggleContinuous());
        if (_contButtonB != null) _contButtonB.onClick.AddListener(() => ToggleContinuous());

        UpdateContinuousButtons();
    }

    private void ToggleContinuous()
    {
        _continuous = !_continuous;
        UpdateContinuousButtons();
    }

    private void UpdateContinuousButtons()
    {
        string label = _continuous ? "Stop Continuous" : "Continuous";
        if (_contButtonA != null) _contButtonA.GetComponentInChildren<Text>().text = label;
        if (_contButtonB != null) _contButtonB.GetComponentInChildren<Text>().text = label;
    }

    private void EnsureEventSystemExists()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            Debug.Log("EventSystem already exists.");
            return;
        }

        var es = new GameObject("EventSystem");
        es.transform.SetAsFirstSibling();

        // Intentamos añadir el InputSystemUIInputModule (nuevo Input System) si existe,
        // en caso contrario añadimos StandaloneInputModule.
        bool added = false;
        try
        {
            // Tipo usado por el paquete Input System UI
            var inputSystemType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemType != null)
            {
                es.AddComponent(inputSystemType);
                added = true;
                Debug.Log("Added InputSystemUIInputModule to EventSystem (Input System detected).");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to add InputSystemUIInputModule: " + ex.Message);
        }

        if (!added)
        {
            // Fallback al módulo clásico (usa Input.GetMousePosition etc.)
            es.AddComponent<StandaloneInputModule>();
            Debug.Log("Added StandaloneInputModule to EventSystem (legacy input).");
        }

        es.AddComponent<EventSystem>();
        DontDestroyOnLoad(es);

        Debug.Log("EventSystem created at runtime.");
    }

    // Sustituye la función CreateCanvasForCamera por esta (mantén el resto del archivo)
    private Canvas CreateCanvasForCamera(Camera cam, string title, out InputField inputField, out Button execButton, out Button continuousButton)
    {
        inputField = null;
        execButton = null;
        continuousButton = null;

        if (cam == null)
        {
            Debug.LogWarning("TurnBasedManager: cámara nula al crear canvas. Se usará Camera.main.");
            cam = Camera.main;
            if (cam == null) return null;
        }

        // Root Canvas - ligado a la cámara para que solo se muestre en su viewport
        GameObject canvasGO = new GameObject($"Canvas_{title}");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        // Diferente orden para cada canvas (Player A = 100, Player B = 101) para evitar solapados
        canvas.sortingOrder = title.Contains("Player B") ? 101 : 100;
        canvas.pixelPerfect = false;

        var cs = canvasGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1280, 720);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        Debug.Log($"Created camera-bound canvas '{canvasGO.name}' for camera '{cam.name}' (rect={cam.pixelRect}).");

        // Panel (background) - pequeño y anclado a la esquina superior izquierda del canvas
        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform panelRt = panelGO.GetComponent<RectTransform>();
        // Anclar exactamente en la esquina superior izquierda del canvas (viewport de la cámara)
        panelRt.anchorMin = new Vector2(0f, 1f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 1f);
        panelRt.anchoredPosition = new Vector2(10f, -10f);
        // Tamaño reducido
        panelRt.sizeDelta = new Vector2(360f, 140f);

        // Title text
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panelGO.transform, false);
        var titleText = titleGO.AddComponent<Text>();
        titleText.text = title;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.color = Color.white;
        titleText.fontSize = 14;
        RectTransform titleRt = titleGO.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(0f, 20f);
        titleRt.anchoredPosition = new Vector2(0f, -8f);

        // InputField background (dentro del panel)
        GameObject inputBG = new GameObject("InputBG");
        inputBG.transform.SetParent(panelGO.transform, false);
        var inputImage = inputBG.AddComponent<Image>();
        inputImage.color = Color.white;
        RectTransform inputBgRt = inputBG.GetComponent<RectTransform>();
        inputBgRt.anchorMin = new Vector2(0f, 0f);
        inputBgRt.anchorMax = new Vector2(1f, 1f);
        inputBgRt.pivot = new Vector2(0.5f, 0.5f);
        inputBgRt.anchoredPosition = new Vector2(0f, -18f);
        inputBgRt.sizeDelta = new Vector2(-20f, -60f);

        // InputField
        GameObject inputGO = new GameObject("InputField");
        inputGO.transform.SetParent(inputBG.transform, false);
        var inputImg = inputGO.AddComponent<Image>();
        inputImg.color = Color.white;
        var input = inputGO.AddComponent<InputField>();
        input.textComponent = CreateText("InputText", inputGO.transform, "", 12, TextAnchor.UpperLeft);
        input.placeholder = CreateText("Placeholder", inputGO.transform, "Escribe instrucciones; separa por ;", 12, TextAnchor.UpperLeft, Color.gray);
        RectTransform inputRt = inputGO.GetComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0f, 0f);
        inputRt.anchorMax = new Vector2(1f, 1f);
        inputRt.pivot = new Vector2(0.5f, 0.5f);
        inputRt.anchoredPosition = Vector2.zero;
        inputRt.sizeDelta = new Vector2(-10f, -10f);

        // Buttons container (pequeño)
        GameObject buttonsGO = new GameObject("Buttons");
        buttonsGO.transform.SetParent(panelGO.transform, false);
        RectTransform buttonsRt = buttonsGO.AddComponent<RectTransform>();
        buttonsRt.anchorMin = new Vector2(0f, 0f);
        buttonsRt.anchorMax = new Vector2(1f, 0f);
        buttonsRt.pivot = new Vector2(0.5f, 0f);
        buttonsRt.anchoredPosition = new Vector2(0f, 8f);
        buttonsRt.sizeDelta = new Vector2(0f, 32f);

        // Execute button
        execButton = CreateButton("Execute", buttonsGO.transform, new Vector2(0.5f, 0.5f), new Vector2(90, 28));
        execButton.GetComponentInChildren<Text>().text = "Execute";

        // Continuous toggle button
        continuousButton = CreateButton("Continuous", buttonsGO.transform, new Vector2(0.5f, 0.5f), new Vector2(110, 28));
        continuousButton.GetComponentInChildren<Text>().text = _continuous ? "Stop" : "Cont.";

        // Position buttons inside container
        RectTransform execRt = execButton.GetComponent<RectTransform>();
        execRt.anchoredPosition = new Vector2(-60f, 0f);
        RectTransform contRt = continuousButton.GetComponent<RectTransform>();
        contRt.anchoredPosition = new Vector2(60f, 0f);

        // Assign out refs
        inputField = input;
        return canvas;
    }

    private Text CreateText(string name, Transform parent, string text, int fontSize, TextAnchor anchor, Color? color = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = color ?? Color.black;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        return t;
    }

    private Button CreateButton(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        var btn = go.AddComponent<Button>();
        var txt = CreateText("Text", go.transform, name, 14, TextAnchor.MiddleCenter, Color.white);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        return btn;
    }

    private void Update()
    {
        if (_gameOver) return;

        if (_continuous && !_isExecuting)
            StartCoroutine(ExecuteTurn());
        else if (_runOnce && !_isExecuting)
        {
            _runOnce = false;
            StartCoroutine(ExecuteTurn());
        }
    }

    private IEnumerator ExecuteTurn()
    {
        if (_gameOver) yield break;

        _isExecuting = true;

        var movesA = MovementParser.Parse(PlayerAScript, _playerA);
        if (_playerA != null) yield return StartCoroutine(_playerA.ExecuteMoves(movesA, StepDelay));
        if (_gameOver) { _isExecuting = false; yield break; }

        var movesB = MovementParser.Parse(PlayerBScript, _playerB);
        if (_playerB != null) yield return StartCoroutine(_playerB.ExecuteMoves(movesB, StepDelay));
        if (_gameOver) { _isExecuting = false; yield break; }

        _isExecuting = false;
    }

    // Llamar cuando un jugador queda sin vida
    public void PlayerLost(Character loser)
    {
        if (_gameOver) return;
        _gameOver = true;

        Character winner = (loser == _playerA) ? _playerB : _playerA;
        string winnerName = winner != null ? winner.name : "Nadie";
        string loserName = loser != null ? loser.name : "Desconocido";
        string mensaje = $"Ganador: {winnerName}\nPerdedor: {loserName}";

        if (gameOverManager == null) gameOverManager = FindObjectOfType<GameOverManager>();
        if (gameOverManager != null) gameOverManager.MostrarResultado(mensaje);
        else Debug.Log(mensaje);

        // Detener ejecución
        _continuous = false;
        _runOnce = false;
        StopAllCoroutines();
        _isExecuting = false;
    }

    // Llamar cuando un jugador sale del laberinto (exit trigger)
    public void PlayerExited(Character exiter)
    {
        if (_gameOver) return;
        _gameOver = true;

        Character winner = (exiter == _playerA) ? _playerA : _playerB; // quien sale gana (ajustable)
        Character loser = (exiter == _playerA) ? _playerB : _playerA;
        string winnerName = winner != null ? winner.name : "Nadie";
        string loserName = loser != null ? loser.name : "Desconocido";
        string mensaje = $"Jugador salió del laberinto.\nGanador: {winnerName}\nPerdedor: {loserName}";

        if (gameOverManager == null) gameOverManager = FindObjectOfType<GameOverManager>();
        if (gameOverManager != null) gameOverManager.MostrarResultado(mensaje);
        else Debug.Log(mensaje);

        _continuous = false;
        _runOnce = false;
        StopAllCoroutines();
        _isExecuting = false;
    }
}       