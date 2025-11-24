using System;
using System.Collections;
using UnityEngine;
using UI = UnityEngine.UI;
using UnityEngine.EventSystems;

/*
GameObject: TurnBasedManager (attach to a central GameObject, e.g. "GameManager")
Descripción: Gestiona el turno por turno entre dos personajes, crea UI por cámara,
controla temporizadores de edición, historial de radar y notifica estados de fin de juego.
*/

public class TurnBasedManager : MonoBehaviour
{
    private Character _playerA;                 
    private Character _playerB;

    public Character PlayerA => _playerA;
    public Character PlayerB => _playerB;

    [TextArea(3, 10)]
    public string PlayerAScript = "MoveRight();MoveUp();MoveUp();";
    [TextArea(3, 10)]
    public string PlayerBScript = "MoveLeft();MoveDown();";

    public float StepDelay = 0.25f;
    public float ContinuousTurnDelay = 0.15f;

    private bool _isExecuting = false;
    private bool _continuous = false;
    private bool _gameOver = false;

    public GameOverManager gameOverManager;

    private Canvas _canvasA;
    private Canvas _canvasB;
    private UI.InputField _inputA;
    private UI.InputField _inputB;
    private UI.Button _execButtonA;
    private UI.Button _execButtonB;
    private UI.Button _contButtonA;
    private UI.Button _contButtonB;

    private enum Turn { PlayerA, PlayerB }
    private Turn _currentTurn = Turn.PlayerA;

    public float EditTimeLimit = 30f;
    private bool _isEditing = false;
    private Coroutine _turnTimerCoroutine;

    private Vector2Int _lastEndA_pos = Vector2Int.zero;
    private Character.Facing _lastEndA_facing = Character.Facing.North;
    private Vector2Int _prevEndA_pos = Vector2Int.zero;
    private Character.Facing _prevEndA_facing = Character.Facing.North;

    private Vector2Int _lastEndB_pos = Vector2Int.zero;
    private Character.Facing _lastEndB_facing = Character.Facing.North;
    private Vector2Int _prevEndB_pos = Vector2Int.zero;
    private Character.Facing _prevEndB_facing = Character.Facing.North;

    // Inicializa manager con dos personajes y determina cámaras por defecto.
    // Parámetros: a, b - instancias de Character.
    public void Initialize(Character a, Character b)
    {
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

        Initialize(a, b, camA, camB);
    }

    // Inicializa manager con personajes y cámaras específicas.
    // Parámetros: a,b - Characters; camA, camB - cámaras asignadas.
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

        if (_playerA != null && _playerB != null)
        {
            _lastEndA_pos = _playerA.CellPosition;
            _lastEndA_facing = _playerA.CurrentFacing;
            _prevEndA_pos = _lastEndA_pos;
            _prevEndA_facing = _lastEndA_facing;

            _lastEndB_pos = _playerB.CellPosition;
            _lastEndB_facing = _playerB.CurrentFacing;
            _prevEndB_pos = _lastEndB_pos;
            _prevEndB_facing = _lastEndB_facing;
        }

        EnsureEventSystemExists();

        _canvasA = CreateCanvasForCamera(camA, "Player A Controls", out _inputA, out _execButtonA, out _contButtonA);
        _canvasB = CreateCanvasForCamera(camB, "Player B Controls", out _inputB, out _execButtonB, out _contButtonB);

        if (_inputA != null) _inputA.text = PlayerAScript;
        if (_inputB != null) _inputB.text = PlayerBScript;

        if (_inputA != null) _inputA.onValueChanged.AddListener(val => PlayerAScript = val);
        if (_inputB != null) _inputB.onValueChanged.AddListener(val => PlayerBScript = val);

        if (_inputA != null) _inputA.onEndEdit.AddListener(_ => { if (_isEditing && _currentTurn == Turn.PlayerA) EndEditing(); });
        if (_inputB != null) _inputB.onEndEdit.AddListener(_ => { if (_isEditing && _currentTurn == Turn.PlayerB) EndEditing(); });

        if (_execButtonA != null) _execButtonA.onClick.AddListener(() => { if (!_isExecuting && _currentTurn == Turn.PlayerA) { StartCoroutine(ExecuteCurrentPlayer()); } });
        if (_execButtonB != null) _execButtonB.onClick.AddListener(() => { if (!_isExecuting && _currentTurn == Turn.PlayerB) { StartCoroutine(ExecuteCurrentPlayer()); } });

        if (_contButtonA != null) _contButtonA.onClick.AddListener(() => ToggleContinuous());
        if (_contButtonB != null) _contButtonB.onClick.AddListener(() => ToggleContinuous());

        UpdateContinuousButtons();

        SetTurn(Turn.PlayerA);
    }

    // Alterna el modo continuo de ejecución de turnos.
    private void ToggleContinuous()
    {
        _continuous = !_continuous;
        UpdateContinuousButtons();
    }

    // Actualiza las etiquetas de los botones de modo continuo.
    private void UpdateContinuousButtons()
    {
        string label = _continuous ? "Stop Continuous" : "Continuous";
        if (_contButtonA != null)
        {
            var t = _contButtonA.GetComponentInChildren<UI.Text>();
            if (t != null) t.text = label;
        }
        if (_contButtonB != null)
        {
            var t = _contButtonB.GetComponentInChildren<UI.Text>();
            if (t != null) t.text = label;
        }
    }

    // Asegura que existe un EventSystem en la escena; crea uno si es necesario.
    private void EnsureEventSystemExists()
    {
        var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            return;
        }

        var es = new GameObject("EventSystem");
        es.transform.SetAsFirstSibling();

        bool added = false;
        try
        {
            var inputSystemType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemType != null)
            {
                es.AddComponent(inputSystemType);
                added = true;
            }
        }
        catch { }

        if (!added)
        {
            es.AddComponent<StandaloneInputModule>();
        }

        es.AddComponent<EventSystem>();
        DontDestroyOnLoad(es);
    }

    // Crea un Canvas ligado a la cámara y retorna referencias a InputField y botones.
    // Parámetros: cam - cámara; title - título del panel; out inputField, execButton, continuousButton.
    private Canvas CreateCanvasForCamera(Camera cam, string title, out UI.InputField inputField, out UI.Button execButton, out UI.Button continuousButton)
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

        GameObject canvasGO = new GameObject($"Canvas_{title}");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = title.Contains("Player B") ? 101 : 100;
        canvas.pixelPerfect = false;

        var cs = canvasGO.AddComponent<UI.CanvasScaler>();
        cs.uiScaleMode = UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1280, 720);
        cs.screenMatchMode = UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<UI.GraphicRaycaster>();

        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelImage = panelGO.AddComponent<UI.Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform panelRt = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 1f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 1f);
        panelRt.anchoredPosition = new Vector2(10f, -10f);
        panelRt.sizeDelta = new Vector2(360f, 140f);

        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panelGO.transform, false);
        var titleText = titleGO.AddComponent<UI.Text>();
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

        GameObject inputBG = new GameObject("InputBG");
        inputBG.transform.SetParent(panelGO.transform, false);
        var inputImage = inputBG.AddComponent<UI.Image>();
        inputImage.color = Color.white;
        RectTransform inputBgRt = inputBG.GetComponent<RectTransform>();
        inputBgRt.anchorMin = new Vector2(0f, 0f);
        inputBgRt.anchorMax = new Vector2(1f, 1f);
        inputBgRt.pivot = new Vector2(0.5f, 0.5f);
        inputBgRt.anchoredPosition = new Vector2(0f, -18f);
        inputBgRt.sizeDelta = new Vector2(-20f, -60f);

        GameObject inputGO = new GameObject("InputField");
        inputGO.transform.SetParent(inputBG.transform, false);
        var inputImg = inputGO.AddComponent<UI.Image>();
        inputImg.color = Color.white;
        var input = inputGO.AddComponent<UI.InputField>();
        input.textComponent = CreateText("InputText", inputGO.transform, "", 12, TextAnchor.UpperLeft);
        input.placeholder = CreateText("Placeholder", inputGO.transform, "Escribe instrucciones; separa por ;", 12, TextAnchor.UpperLeft, Color.gray);
        RectTransform inputRt = inputGO.GetComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0f, 0f);
        inputRt.anchorMax = new Vector2(1f, 1f);
        inputRt.pivot = new Vector2(0.5f, 0.5f);
        inputRt.anchoredPosition = Vector2.zero;
        inputRt.sizeDelta = new Vector2(-10f, -10f);

        GameObject buttonsGO = new GameObject("Buttons");
        buttonsGO.transform.SetParent(panelGO.transform, false);
        RectTransform buttonsRt = buttonsGO.AddComponent<RectTransform>();
        buttonsRt.anchorMin = new Vector2(0f, 0f);
        buttonsRt.anchorMax = new Vector2(1f, 0f);
        buttonsRt.pivot = new Vector2(0.5f, 0f);
        buttonsRt.anchoredPosition = new Vector2(0f, 8f);
        buttonsRt.sizeDelta = new Vector2(0f, 32f);

        execButton = CreateButton("Execute", buttonsGO.transform, new Vector2(0.5f, 0.5f), new Vector2(90, 28));
        var execTxt = execButton.GetComponentInChildren<UI.Text>();
        if (execTxt != null) execTxt.text = "Execute";

        continuousButton = CreateButton("Continuous", buttonsGO.transform, new Vector2(0.5f, 0.5f), new Vector2(110, 28));
        var contTxt = continuousButton.GetComponentInChildren<UI.Text>();
        if (contTxt != null) contTxt.text = _continuous ? "Stop" : "Cont.";

        RectTransform execRt = execButton.GetComponent<RectTransform>();
        execRt.anchoredPosition = new Vector2(-60f, 0f);
        RectTransform contRt = continuousButton.GetComponent<RectTransform>();
        contRt.anchoredPosition = new Vector2(60f, 0f);

        inputField = input;
        return canvas;
    }

    // Crea un UI.Text hijo y lo devuelve.
    private UI.Text CreateText(string name, Transform parent, string text, int fontSize, TextAnchor anchor, UnityEngine.Color? color = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        UI.Text t = go.AddComponent<UI.Text>();
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

    // Crea un botón simple y lo devuelve.
    private UI.Button CreateButton(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<UI.Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        var btn = go.AddComponent<UI.Button>();
        var txt = CreateText("Text", go.transform, name, 14, TextAnchor.MiddleCenter, Color.white);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        return btn;
    }

    // Actualiza selección de InputFields y lanza ejecución en modo continuo si aplica.
    private void Update()
    {
        if (_gameOver) return;

        if (EventSystem.current != null)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == (_inputA != null ? _inputA.gameObject : null))
            {
                if (_currentTurn == Turn.PlayerA)
                {
                    if (!_isEditing) BeginEditing(Turn.PlayerA);
                }
                else
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
            else if (selected == (_inputB != null ? _inputB.gameObject : null))
            {
                if (_currentTurn == Turn.PlayerB)
                {
                    if (!_isEditing) BeginEditing(Turn.PlayerB);
                }
                else
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }

        if (_continuous && !_isExecuting)
        {
            StartCoroutine(ExecuteCurrentPlayer());
        }
    }

    // Ejecuta las acciones del jugador activo según el script asociado.
    private IEnumerator ExecuteCurrentPlayer()
    {
        if (_gameOver) yield break;
        if (_isExecuting) yield break;

        _isExecuting = true;

        if (_turnTimerCoroutine != null)
        {
            StopCoroutine(_turnTimerCoroutine);
            _turnTimerCoroutine = null;
        }
        _isEditing = false;

        var executingTurn = _currentTurn;

        if (executingTurn == Turn.PlayerA)
        {
            var movesA = MovementParser.Parse(PlayerAScript, _playerA);
            if (_playerA != null) yield return StartCoroutine(_playerA.ExecuteMoves(movesA, StepDelay));
            if (_gameOver) { _isExecuting = false; SetTurn(_currentTurn); yield break; }
        }
        else
        {
            var movesB = MovementParser.Parse(PlayerBScript, _playerB);
            if (_playerB != null) yield return StartCoroutine(_playerB.ExecuteMoves(movesB, StepDelay));
            if (_gameOver) { _isExecuting = false; SetTurn(_currentTurn); yield break; }
        }

        if (executingTurn == Turn.PlayerA && _playerA != null)
        {
            _prevEndA_pos = _lastEndA_pos;
            _prevEndA_facing = _lastEndA_facing;
            _lastEndA_pos = _playerA.CellPosition;
            _lastEndA_facing = _playerA.CurrentFacing;
            _playerA.ClearActivePowerups();
        }
        else if (executingTurn == Turn.PlayerB && _playerB != null)
        {
            _prevEndB_pos = _lastEndB_pos;
            _prevEndB_facing = _lastEndB_facing;
            _lastEndB_pos = _playerB.CellPosition;
            _lastEndB_facing = _playerB.CurrentFacing;
            _playerB.ClearActivePowerups();
        }

        if (!_gameOver)
        {
            SwitchTurn();
        }

        if (_continuous)
            yield return new WaitForSeconds(ContinuousTurnDelay);

        _isExecuting = false;
        SetTurn(_currentTurn);
    }

    // Cambia el turno al jugador opuesto.
    private void SwitchTurn()
    {
        var next = (_currentTurn == Turn.PlayerA) ? Turn.PlayerB : Turn.PlayerA;
        SetTurn(next);
    }

    // Configura el estado y la UI al comenzar un turno.
    private void SetTurn(Turn t)
    {
        _currentTurn = t;

        if (t == Turn.PlayerA && _playerA != null) _playerA.PromoteQueuedPowerups();
        if (t == Turn.PlayerB && _playerB != null) _playerB.PromoteQueuedPowerups();

        if (_inputA != null) _inputA.interactable = (t == Turn.PlayerA);
        if (_inputB != null) _inputB.interactable = (t == Turn.PlayerB);

        if (_execButtonA != null) _execButtonA.interactable = (t == Turn.PlayerA) && !_isExecuting;
        if (_execButtonB != null) _execButtonB.interactable = (t == Turn.PlayerB) && !_isExecuting;

        if (_contButtonA != null) _contButtonA.interactable = (t == Turn.PlayerA);
        if (_contButtonB != null) _contButtonB.interactable = (t == Turn.PlayerB);

        if (_isEditing)
        {
            EndEditing();
        }

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (_turnTimerCoroutine != null)
        {
            StopCoroutine(_turnTimerCoroutine);
            _turnTimerCoroutine = null;
        }

        if (!_isExecuting && !_gameOver)
        {
            _turnTimerCoroutine = StartCoroutine(TurnTimeoutCoroutine(t));
        }
    }

    // Comienza el modo edición para el jugador indicado y bloquea la UI del contrario.
    private void BeginEditing(Turn t)
    {
        _isEditing = true;

        if (t == Turn.PlayerA)
        {
            if (_inputB != null) _inputB.interactable = false;
            if (_execButtonB != null) _execButtonB.interactable = false;
            if (_contButtonB != null) _contButtonB.interactable = false;
        }
        else
        {
            if (_inputA != null) _inputA.interactable = false;
            if (_execButtonA != null) _execButtonA.interactable = false;
            if (_contButtonA != null) _contButtonA.interactable = false;
        }
    }

    // Finaliza la edición y deselecciona cualquier campo UI.
    private void EndEditing()
    {
        _isEditing = false;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    // Temporizador que fuerza el cambio de turno tras expirar EditTimeLimit.
    private IEnumerator TurnTimeoutCoroutine(Turn t)
    {
        float remaining = EditTimeLimit;
        while (remaining > 0f)
        {
            yield return null;
            remaining -= Time.unscaledDeltaTime;
        }

        _turnTimerCoroutine = null;
        _isEditing = false;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        SwitchTurn();
    }

    // Notifica que un jugador ha perdido por quedarse sin vida.
    // Parámetros: loser - Character que perdió.
    public void PlayerLost(Character loser)
    {
        if (_gameOver) return;
        _gameOver = true;

        Character winner = (loser == _playerA) ? _playerB : _playerA;
        string winnerName = winner != null ? winner.name : "Nadie";
        string loserName = loser != null ? loser.name : "Desconocido";
        string mensaje = $"Ganador: {winnerName}\nPerdedor: {loserName}";

        if (gameOverManager == null)
        {
            try { gameOverManager = UnityEngine.Object.FindFirstObjectByType<GameOverManager>(); } catch { }
            if (gameOverManager == null) gameOverManager = UnityEngine.Object.FindObjectOfType<GameOverManager>();
            if (gameOverManager == null)
            {
                gameOverManager = gameObject.GetComponent<GameOverManager>();
                if (gameOverManager == null) gameOverManager = gameObject.AddComponent<GameOverManager>();
            }
        }

        if (gameOverManager != null) gameOverManager.MostrarResultado(mensaje);
        else Debug.Log(mensaje);

        _continuous = false;
        if (_turnTimerCoroutine != null) { StopCoroutine(_turnTimerCoroutine); _turnTimerCoroutine = null; }
        StopAllCoroutines();
        _isExecuting = false;
        _isEditing = false;
    }

    // Notifica que un jugador salió del laberinto (exit).
    // Parámetros: exiter - Character que salió.
    public void PlayerExited(Character exiter)
    {
        if (_gameOver) return;
        _gameOver = true;

        string winnerLabel = (exiter == _playerA) ? "Jugador 1" : "Jugador 2";
        Character winner = (exiter == _playerA) ? _playerA : _playerB;
        Character loser = (exiter == _playerA) ? _playerB : _playerA;
        string winnerName = winner != null ? winner.name : winnerLabel;
        string loserName = loser != null ? loser.name : "Desconocido";
        string mensaje = $"{winnerLabel} ha ganado.\nGanador: {winnerName}\nPerdedor: {loserName}";

        if (gameOverManager == null)
        {
            try { gameOverManager = UnityEngine.Object.FindFirstObjectByType<GameOverManager>(); } catch { }
            if (gameOverManager == null) gameOverManager = UnityEngine.Object.FindObjectOfType<GameOverManager>();
            if (gameOverManager == null)
            {
                gameOverManager = gameObject.GetComponent<GameOverManager>();
                if (gameOverManager == null) gameOverManager = gameObject.AddComponent<GameOverManager>();
            }
        }

        if (gameOverManager != null) gameOverManager.MostrarResultado(mensaje);
        else Debug.Log(mensaje);

        _continuous = false;
        if (_turnTimerCoroutine != null) { StopCoroutine(_turnTimerCoroutine); _turnTimerCoroutine = null; }
        StopAllCoroutines();
        _isExecuting = false;
        _isEditing = false;
    }

    // Devuelve la última posición/facing conocida por el requester sobre su oponente.
    // Parámetros: requester - Character que solicita; Retorno: (pos,facing).
    public (Vector2Int pos, Character.Facing facing) GetLastKnownRadarFor(Character requester)
    {
        if (requester == _playerA)
        {
            if (_playerA != null && _playerA.activeTrueRadar && _playerB != null)
            {
                _playerA.activeTrueRadar = false;
                return (_playerB.CellPosition, _playerB.CurrentFacing);
            }

            return (_prevEndB_pos, _prevEndB_facing);
        }
        if (requester == _playerB)
        {
            if (_playerB != null && _playerB.activeTrueRadar && _playerA != null)
            {
                _playerB.activeTrueRadar = false;
                return (_playerA.CellPosition, _playerA.CurrentFacing);
            }

            return (_prevEndA_pos, _prevEndA_facing);
        }
        return (Vector2Int.zero, Character.Facing.North);
    }

    // Método de compatibilidad que no altera el historial por turnos.
    // Parámetros: moved - Character, previousPosition - Vector2Int, previousFacing - Facing.
    public void ReportPositionMoved(Character moved, Vector2Int previousPosition, Character.Facing previousFacing)
    {
        if (moved == null) return;
    }

    // Muestra una notificación temporal en el canvas del jugador que recogió un powerup.
    // Parámetros: collector - Character que recogió, type - tipo de powerup.
    public void ShowPowerupCollected(Character collector, Powerup.PowerupType type)
    {
        if (collector == null) return;

        string message = (type == Powerup.PowerupType.Phase) ? "Has recogido: Atravesar muro" : "Has recogido: Radar verdadero";

        Canvas targetCanvas = null;
        if (collector == _playerA) targetCanvas = _canvasA;
        else if (collector == _playerB) targetCanvas = _canvasB;

        if (targetCanvas == null)
        {
            Debug.Log($"[Powerup] {collector.name} recogió {type}. Mensaje: {message}");
            return;
        }

        Transform panel = targetCanvas.transform.Find("Panel");
        Transform parent = panel != null ? panel : targetCanvas.transform;

        var go = new GameObject("PowerupMessage");
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<UI.Text>();
        txt.text = message;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 16;
        txt.color = Color.yellow;
        txt.alignment = TextAnchor.UpperLeft;

        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 24f);
        rt.anchoredPosition = new Vector2(0f, -30f);

        StartCoroutine(DestroyAfterSeconds(go, 4f));
    }

    // Destruye un GameObject después de un número de segundos.
    // Parámetros: go - GameObject a destruir; seconds - tiempo en segundos.
    private IEnumerator DestroyAfterSeconds(GameObject go, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (go != null) Destroy(go);
    }
}