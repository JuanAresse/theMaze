using System;
using System.Collections;
using UnityEngine;
using UI = UnityEngine.UI;
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

    // Retardo entre turnos cuando _continuous == true (evita cambios instantáneos)
    public float ContinuousTurnDelay = 0.15f;

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
    private UI.InputField _inputA;
    private UI.InputField _inputB;
    private UI.Button _execButtonA;
    private UI.Button _execButtonB;
    private UI.Button _contButtonA;
    private UI.Button _contButtonB;

    // Turn system
    private enum Turn { PlayerA, PlayerB }
    private Turn _currentTurn = Turn.PlayerA;

    // Tiempo por turno (segundos)
    public float EditTimeLimit = 30f;
    private bool _isEditing = false;
    private Coroutine _turnTimerCoroutine;

    // Historial por jugador: posición y facing al final del último turno (lastEnd)
    // y al final del turno anterior (prevEnd). Radar debe devolver prevEnd del oponente.
    private Vector2Int _lastEndA_pos = Vector2Int.zero;
    private Character.Facing _lastEndA_facing = Character.Facing.North;
    private Vector2Int _prevEndA_pos = Vector2Int.zero;
    private Character.Facing _prevEndA_facing = Character.Facing.North;

    private Vector2Int _lastEndB_pos = Vector2Int.zero;
    private Character.Facing _lastEndB_facing = Character.Facing.North;
    private Vector2Int _prevEndB_pos = Vector2Int.zero;
    private Character.Facing _prevEndB_facing = Character.Facing.North;

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

        // Inicializar historial de turnos con las posiciones iniciales (ambos slots iguales al inicio)
        if (_playerA != null && _playerB != null)
        {
            // Para A (historial de A)
            _lastEndA_pos = _playerA.CellPosition;
            _lastEndA_facing = _playerA.CurrentFacing;
            _prevEndA_pos = _lastEndA_pos;
            _prevEndA_facing = _lastEndA_facing;

            // Para B (historial de B)
            _lastEndB_pos = _playerB.CellPosition;
            _lastEndB_facing = _playerB.CurrentFacing;
            _prevEndB_pos = _lastEndB_pos;
            _prevEndB_facing = _lastEndB_facing;
        }

        // LOG: confirmar llamada y cámaras recibidas
        Debug.Log($"TurnBasedManager.Initialize called. camA={(camA != null ? camA.name : "null")}, camB={(camB != null ? camB.name : "null")}");

        EnsureEventSystemExists();

        // Crear UI para cada cámara (si no vienen provistas)
        _canvasA = CreateCanvasForCamera(camA, "Player A Controls", out _inputA, out _execButtonA, out _contButtonA);
        _canvasB = CreateCanvasForCamera(camB, "Player B Controls", out _inputB, out _execButtonB, out _contButtonB);

        // LOG: verificar creación de canvases
        Debug.Log($"Canvas created: A={(_canvasA != null ? _canvasA.gameObject.name : "null")}, B={(_canvasB != null ? _canvasB.gameObject.name : "null")}");

        // Inicializar textos
        if (_inputA != null) _inputA.text = PlayerAScript;
        if (_inputB != null) _inputB.text = PlayerBScript;

        // Listeners
        if (_inputA != null) _inputA.onValueChanged.AddListener(val => PlayerAScript = val);
        if (_inputB != null) _inputB.onValueChanged.AddListener(val => PlayerBScript = val);

        // Detectar fin de edición para quitar selección si el jugador acaba la edición manualmente
        if (_inputA != null) _inputA.onEndEdit.AddListener(_ => { if (_isEditing && _currentTurn == Turn.PlayerA) EndEditing(); });
        if (_inputB != null) _inputB.onEndEdit.AddListener(_ => { if (_isEditing && _currentTurn == Turn.PlayerB) EndEditing(); });

        // Ejecutar solo si es el turno del jugador correspondiente
        if (_execButtonA != null) _execButtonA.onClick.AddListener(() => { if (!_isExecuting && _currentTurn == Turn.PlayerA) { _runOnce = false; StartCoroutine(ExecuteCurrentPlayer()); } });
        if (_execButtonB != null) _execButtonB.onClick.AddListener(() => { if (!_isExecuting && _currentTurn == Turn.PlayerB) { _runOnce = false; StartCoroutine(ExecuteCurrentPlayer()); } });

        if (_contButtonA != null) _contButtonA.onClick.AddListener(() => ToggleContinuous());
        if (_contButtonB != null) _contButtonB.onClick.AddListener(() => ToggleContinuous());

        UpdateContinuousButtons();

        // Inicializar turnos: Player A comienza por defecto
        SetTurn(Turn.PlayerA);
    }

    private void ToggleContinuous()
    {
        _continuous = !_continuous;
        UpdateContinuousButtons();
    }

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

    // EnsureEventSystemExists (reemplaza la versión anterior)
    private void EnsureEventSystemExists()
    {
        var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
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

        // Root Canvas - ligado a la cámara para que solo se muestre en su viewport
        GameObject canvasGO = new GameObject($"Canvas_{title}");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        // Diferente orden para cada canvas (Player A = 100, Player B = 101) para evitar solapados
        canvas.sortingOrder = title.Contains("Player B") ? 101 : 100;
        canvas.pixelPerfect = false;

        var cs = canvasGO.AddComponent<UI.CanvasScaler>();
        cs.uiScaleMode = UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1280, 720);
        cs.screenMatchMode = UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<UI.GraphicRaycaster>();

        Debug.Log($"Created camera-bound canvas '{canvasGO.name}' for camera '{cam.name}' (rect={cam.pixelRect}).");

        // Panel (background) - pequeño y anclado a la esquina superior izquierda del canvas
        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelImage = panelGO.AddComponent<UI.Image>();
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

        // InputField background (dentro del panel)
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

        // InputField
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
        var execTxt = execButton.GetComponentInChildren<UI.Text>();
        if (execTxt != null) execTxt.text = "Execute";

        // Continuous toggle button
        continuousButton = CreateButton("Continuous", buttonsGO.transform, new Vector2(0.5f, 0.5f), new Vector2(110, 28));
        var contTxt = continuousButton.GetComponentInChildren<UI.Text>();
        if (contTxt != null) contTxt.text = _continuous ? "Stop" : "Cont.";

        // Position buttons inside container
        RectTransform execRt = execButton.GetComponent<RectTransform>();
        execRt.anchoredPosition = new Vector2(-60f, 0f);
        RectTransform contRt = continuousButton.GetComponent<RectTransform>();
        contRt.anchoredPosition = new Vector2(60f, 0f);

        // Assign out refs
        inputField = input;
        return canvas;
    }

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

    private void Update()
    {
        if (_gameOver) return;

        // Detect selection start to begin editing and enforce mutex between players
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
                    // Deseleccionar si no es su turno
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
            // Ejecutar turno del jugador activo en modo continuo
            StartCoroutine(ExecuteCurrentPlayer());
        }
        else if (_runOnce && !_isExecuting)
        {
            _runOnce = false;
            StartCoroutine(ExecuteCurrentPlayer());
        }
    }

    private IEnumerator ExecuteCurrentPlayer()
    {
        if (_gameOver) yield break;
        if (_isExecuting) yield break;

        _isExecuting = true;

        // Cancelar timer de turno mientras ejecutamos
        if (_turnTimerCoroutine != null)
        {
            StopCoroutine(_turnTimerCoroutine);
            _turnTimerCoroutine = null;
        }
        _isEditing = false;

        // Guardar el turno actual para usar después al actualizar historial
        var executingTurn = _currentTurn;

        if (executingTurn == Turn.PlayerA)
        {
            var movesA = MovementParser.Parse(PlayerAScript, _playerA);
            if (_playerA != null) yield return StartCoroutine(_playerA.ExecuteMoves(movesA, StepDelay));
            if (_gameOver) { _isExecuting = false; SetTurn(_currentTurn); yield break; }
        }
        else // PlayerB
        {
            var movesB = MovementParser.Parse(PlayerBScript, _playerB);
            if (_playerB != null) yield return StartCoroutine(_playerB.ExecuteMoves(movesB, StepDelay));
            if (_gameOver) { _isExecuting = false; SetTurn(_currentTurn); yield break; }
        }

        // Actualizar historial de fin de turno del jugador que acaba de ejecutar:
        if (executingTurn == Turn.PlayerA && _playerA != null)
        {
            _prevEndA_pos = _lastEndA_pos;
            _prevEndA_facing = _lastEndA_facing;
            _lastEndA_pos = _playerA.CellPosition;
            _lastEndA_facing = _playerA.CurrentFacing;
            Debug.Log($"[Radar] Turn end A: prevEndA={_prevEndA_pos}/{_prevEndA_facing} lastEndA={_lastEndA_pos}/{_lastEndA_facing}");
            // Limpiar powerups activos al final del turno
            _playerA.ClearActivePowerups();
        }
        else if (executingTurn == Turn.PlayerB && _playerB != null)
        {
            _prevEndB_pos = _lastEndB_pos;
            _prevEndB_facing = _lastEndB_facing;
            _lastEndB_pos = _playerB.CellPosition;
            _lastEndB_facing = _playerB.CurrentFacing;
            Debug.Log($"[Radar] Turn end B: prevEndB={_prevEndB_pos}/{_prevEndB_facing} lastEndB={_lastEndB_pos}/{_lastEndB_facing}");
            // Limpiar powerups activos al final del turno
            _playerB.ClearActivePowerups();
        }

        // Tras ejecutar, cambiar turno al otro jugador (si no hay game over)
        if (!_gameOver)
        {
            SwitchTurn();
        }

        // Si está en modo continuo, esperar un pequeño delay para evitar cambios instantáneos
        if (_continuous)
            yield return new WaitForSeconds(ContinuousTurnDelay);

        // Liberar ejecución y refrescar UI (para que el botón Execute del nuevo jugador quede activo)
        _isExecuting = false;
        SetTurn(_currentTurn);
    }

    private void SwitchTurn()
    {
        var next = (_currentTurn == Turn.PlayerA) ? Turn.PlayerB : Turn.PlayerA;
        SetTurn(next);
    }

    private void SetTurn(Turn t)
    {
        _currentTurn = t;

        // Cuando comienza el turno de un jugador, promovemos cualquier powerup queued -> active
        if (t == Turn.PlayerA && _playerA != null) _playerA.PromoteQueuedPowerups();
        if (t == Turn.PlayerB && _playerB != null) _playerB.PromoteQueuedPowerups();

        // Asegurar que la UI refleja el turno: solo el jugador activo puede editar/ejecutar/controles
        if (_inputA != null) _inputA.interactable = (t == Turn.PlayerA);
        if (_inputB != null) _inputB.interactable = (t == Turn.PlayerB);

        if (_execButtonA != null) _execButtonA.interactable = (t == Turn.PlayerA) && !_isExecuting;
        if (_execButtonB != null) _execButtonB.interactable = (t == Turn.PlayerB) && !_isExecuting;

        if (_contButtonA != null) _contButtonA.interactable = (t == Turn.PlayerA);
        if (_contButtonB != null) _contButtonB.interactable = (t == Turn.PlayerB);

        // Cancelar edición previa si existiera
        if (_isEditing)
        {
            EndEditing();
        }

        // Deseleccionar cualquier campo UI al cambiar turno
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        // Reiniciar timer de turno (solo si no estamos en plena ejecución)
        if (_turnTimerCoroutine != null)
        {
            StopCoroutine(_turnTimerCoroutine);
            _turnTimerCoroutine = null;
        }

        if (!_isExecuting && !_gameOver)
        {
            _turnTimerCoroutine = StartCoroutine(TurnTimeoutCoroutine(t));
        }

        Debug.Log($"Turn changed to: {t}");
    }

    private void BeginEditing(Turn t)
    {
        _isEditing = true;

        // Asegurar que la UI del contrario está bloqueada
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

        Debug.Log($"Begin editing for {t}. Timer active ({EditTimeLimit}s).");
    }

    private void EndEditing()
    {
        _isEditing = false;

        // No cancelamos el temporizador de turno aquí: el turno sigue corriendo aunque el jugador deje el InputField.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        Debug.Log("End editing (manual).");
    }

    private IEnumerator TurnTimeoutCoroutine(Turn t)
    {
        float remaining = EditTimeLimit;
        while (remaining > 0f)
        {
            yield return null;
            remaining -= Time.unscaledDeltaTime;
            // Posible lugar para exponer remaining a UI si se desea.
        }

        // Tiempo expirado: forzar fin de edición y cambiar turno
        Debug.Log($"Turn time expired for {t} after {EditTimeLimit} seconds. Switching turn.");
        _turnTimerCoroutine = null;
        _isEditing = false;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        // Cambiar turno automático
        SwitchTurn();
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

        // Alineado con PlayerExited: asegurar existencia de GameOverManager (crear si es necesario)
        if (gameOverManager == null)
        {
            try { gameOverManager = UnityEngine.Object.FindFirstObjectByType<GameOverManager>(); } catch { /* fallback */ }
            if (gameOverManager == null) gameOverManager = UnityEngine.Object.FindObjectOfType<GameOverManager>();
            if (gameOverManager == null)
            {
                gameOverManager = gameObject.GetComponent<GameOverManager>();
                if (gameOverManager == null) gameOverManager = gameObject.AddComponent<GameOverManager>();
                Debug.Log("[PlayerLost] Created GameOverManager dynamically for showing result.");
            }
        }

        if (gameOverManager != null) gameOverManager.MostrarResultado(mensaje);
        else Debug.Log(mensaje);

        // Detener ejecución y edición
        _continuous = false;
        _runOnce = false;
        if (_turnTimerCoroutine != null) { StopCoroutine(_turnTimerCoroutine); _turnTimerCoroutine = null; }
        StopAllCoroutines();
        _isExecuting = false;
        _isEditing = false;
    }

    // Llamar cuando un jugador sale del laberinto (exit trigger)
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

        // Asegurar existencia de GameOverManager (crear si es necesario)
        if (gameOverManager == null)
        {
            try { gameOverManager = UnityEngine.Object.FindFirstObjectByType<GameOverManager>(); } catch { /* fallback */ }
            if (gameOverManager == null) gameOverManager = UnityEngine.Object.FindObjectOfType<GameOverManager>();
            if (gameOverManager == null)
            {
                gameOverManager = gameObject.GetComponent<GameOverManager>();
                if (gameOverManager == null) gameOverManager = gameObject.AddComponent<GameOverManager>();
                Debug.Log("[PlayerExited] Created GameOverManager dynamically for showing result.");
            }
        }

        if (gameOverManager != null) gameOverManager.MostrarResultado(mensaje);
        else Debug.Log(mensaje);

        _continuous = false;
        _runOnce = false;
        if (_turnTimerCoroutine != null) { StopCoroutine(_turnTimerCoroutine); _turnTimerCoroutine = null; }
        StopAllCoroutines();
        _isExecuting = false;
        _isEditing = false;
    }

    // Devuelve la última posición y facing conocida por 'requester' sobre su oponente.
    // IMPORTANTE: retorna la posición/facing que el oponente tenía al final de SU turno anterior (prevEnd).
    // Si el requester tiene activeTrueRadar, devolvemos la posición actual del enemigo y consumimos el powerup.
    public (Vector2Int pos, Character.Facing facing) GetLastKnownRadarFor(Character requester)
    {
        if (requester == _playerA)
               {
            // Si A tiene TrueRadar activo, devolver posición actual de B y consumirlo
            if (_playerA != null && _playerA.activeTrueRadar && _playerB != null)
            {
                _playerA.activeTrueRadar = false; // consumo de un solo uso
                Debug.Log($"[Radar] TrueRadar usado por A -> devolviendo posición actual de B: {_playerB.CellPosition}/{_playerB.CurrentFacing} (powerup consumido)");
                return (_playerB.CellPosition, _playerB.CurrentFacing);
            }

            // A solicita lo que conoce de B => devolver prevEnd de B
            Debug.Log($"[Radar] GetLastKnownRadarFor: requester=A -> returning prevEndB={_prevEndB_pos}/{_prevEndB_facing}");
            return (_prevEndB_pos, _prevEndB_facing);
        }
        if (requester == _playerB)
        {
            // Si B tiene TrueRadar activo, devolver posición actual de A y consumirlo
            if (_playerB != null && _playerB.activeTrueRadar && _playerA != null)
            {
                _playerB.activeTrueRadar = false; // consumo de un solo uso
                Debug.Log($"[Radar] TrueRadar usado por B -> devolviendo posición actual de A: {_playerA.CellPosition}/{_playerA.CurrentFacing} (powerup consumido)");
                return (_playerA.CellPosition, _playerA.CurrentFacing);
            }

            // B solicita lo que conoce de A => devolver prevEnd de A
            Debug.Log($"[Radar] GetLastKnownRadarFor: requester=B -> returning prevEndA={_prevEndA_pos}/{_prevEndA_facing}");
            return (_prevEndA_pos, _prevEndA_facing);
        }
        // por defecto (sin requester conocido), devolver (0,North)
        Debug.LogWarning("[Radar] GetLastKnownRadarFor: requester desconocido, devolviendo (0, North)");
        return (Vector2Int.zero, Character.Facing.North);
    }

    // Compatibilidad: ReportPositionMoved (método público de apoyo, no altera la lógica de historial por turnos)
    public void ReportPositionMoved(Character moved, Vector2Int previousPosition, Character.Facing previousFacing)
    {
        // Este método existe por compatibilidad con versiones previas del código que llamaban a manager.ReportPositionMoved(...)
        // La lógica real del radar por turnos se gestiona con prevEnd/lastEnd en ExecuteCurrentPlayer -> actualización al final del turno.
        // Aquí dejamos un log informativo y no alteramos el historial por turnos.
        if (moved == null)
        {
            Debug.LogWarning("[Radar] ReportPositionMoved llamado con 'moved' nulo.");
            return;
        }

        Debug.Log($"[Radar] ReportPositionMoved (compat): moved={moved.name}, previous={previousPosition}, previousFacing={previousFacing} -- no se modifica historial por turnos.");
    }

    // ----------------------------------------------------
    // UI: mostrar notificación temporal cuando un jugador recoge un powerup
    // ----------------------------------------------------
    public void ShowPowerupCollected(Character collector, Powerup.PowerupType type)
    {
        if (collector == null) return;

        string message;
        if (type == Powerup.PowerupType.Phase) message = "Has recogido: Atravesar muro";
        else message = "Has recogido: Radar verdadero";

        // Seleccionar canvas del jugador que recogió
        Canvas targetCanvas = null;
        if (collector == _playerA) targetCanvas = _canvasA;
        else if (collector == _playerB) targetCanvas = _canvasB;

        if (targetCanvas == null)
        {
            Debug.Log($"[Powerup] {collector.name} recogió {type}. Mensaje: {message}");
            return;
        }

        // Intentar parentear el mensaje debajo del panel si existe
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
        // Colocar el mensaje justo debajo del título (si existe), offset -30
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 24f);
        rt.anchoredPosition = new Vector2(0f, -30f);

        // Destruir el mensaje después de 4 segundos
        StartCoroutine(DestroyAfterSeconds(go, 4f));
    }

    private IEnumerator DestroyAfterSeconds(GameObject go, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (go != null) Destroy(go);
    }
}